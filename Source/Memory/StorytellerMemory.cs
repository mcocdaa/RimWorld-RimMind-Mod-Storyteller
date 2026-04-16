using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimMind.Storyteller.Memory
{
    public class ChainStep : IExposable
    {
        public string incidentDefName = string.Empty;
        public int triggeredTick;
        public bool completed;

        public void ExposeData()
        {
            Scribe_Values.Look(ref incidentDefName, "incidentDefName", string.Empty);
            Scribe_Values.Look(ref triggeredTick, "triggeredTick");
            Scribe_Values.Look(ref completed, "completed");
        }
    }

    public class EventChain : IExposable
    {
        public string chainId = string.Empty;
        public List<ChainStep> steps = new List<ChainStep>();
        public int currentStep;
        public string nextHint = string.Empty;
        public int lastAdvancedTick;
        public string lastFactionDefName = string.Empty;
        public float lastPoints;

        public void ExposeData()
        {
            Scribe_Values.Look(ref chainId, "chainId", string.Empty);
            Scribe_Collections.Look(ref steps, "steps", LookMode.Deep);
            Scribe_Values.Look(ref currentStep, "currentStep");
            Scribe_Values.Look(ref nextHint, "nextHint", string.Empty);
            Scribe_Values.Look(ref lastAdvancedTick, "lastAdvancedTick");
            Scribe_Values.Look(ref lastFactionDefName, "lastFactionDefName", string.Empty);
            Scribe_Values.Look(ref lastPoints, "lastPoints");
            steps ??= new List<ChainStep>();
        }
    }

    public class DialogueRecord : IExposable
    {
        public string role = string.Empty;
        public string content = string.Empty;
        public int tick;

        public DialogueRecord() { }

        public static DialogueRecord Create(string role, string content, int tick)
        {
            return new DialogueRecord { role = role, content = content, tick = tick };
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref role, "role", string.Empty);
            Scribe_Values.Look(ref content, "content", string.Empty);
            Scribe_Values.Look(ref tick, "tick");
        }
    }

    public class StorytellerMemory : WorldComponent
    {
        private List<IncidentHistoryRecord> _records = new List<IncidentHistoryRecord>();
        private int _maxRecords = 50;

        private List<DialogueRecord> _dialogueRecords = new List<DialogueRecord>();
        private int _maxDialogueRecords = 30;

        public string CustomSystemPrompt = string.Empty;

        private static StorytellerMemory? _instance;
        public static StorytellerMemory? Instance => _instance;

        private float _tensionLevel = 0.5f;
        public float TensionLevel => _tensionLevel;
        private int _lastTensionDecayTick;

        private List<EventChain> _activeChains = new List<EventChain>();

        private ColonySnapshot? _lastSnapshot;

        private static readonly IncidentCategoryDef FactionArrivalCat =
            DefDatabase<IncidentCategoryDef>.GetNamedSilentFail("FactionArrival");

        public StorytellerMemory(World world) : base(world)
        {
            _instance = this;
        }

        public IReadOnlyList<IncidentHistoryRecord> Records => _records;
        public IReadOnlyList<DialogueRecord> DialogueRecords => _dialogueRecords;
        public int ActiveChainsCount => _activeChains.Count;

        public int MaxRecords
        {
            get => _maxRecords;
            set => _maxRecords = value;
        }

        public int MaxDialogueRecords
        {
            get => _maxDialogueRecords;
            set => _maxDialogueRecords = value;
        }

        public void RecordIncident(IncidentDef def, IIncidentTarget target, int tick)
        {
            var record = IncidentHistoryRecord.Create(def, target, tick);
            _records.Add(record);
            if (_records.Count > _maxRecords)
                _records.RemoveAt(0);
        }

        public string GetRecentSummary(int count)
        {
            if (_records.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            var recent = _records.Skip(System.Math.Max(0, _records.Count - count)).ToList();
            foreach (var r in recent)
            {
                int day = r.TriggeredTick / 60000 + 1;
                sb.AppendLine("RimMind.Storyteller.Prompt.DaySummary".Translate(day, r.Label, r.MapName));
            }
            return sb.ToString().TrimEnd();
        }

        public bool IsOnCooldown(IncidentDef def)
        {
            if (def.minRefireDays <= 0f) return false;

            int now = Find.TickManager.TicksGame;
            float minRefireTicks = def.minRefireDays * 60000f;

            for (int i = _records.Count - 1; i >= 0; i--)
            {
                if (_records[i].IncidentDefName == def.defName)
                {
                    float elapsed = now - _records[i].TriggeredTick;
                    if (elapsed < minRefireTicks)
                        return true;
                    break;
                }
            }
            return false;
        }

        public void ClearRecords() => _records.Clear();

        public void RecordDialogue(string role, string content, int tick)
        {
            _dialogueRecords.Add(DialogueRecord.Create(role, content, tick));
            while (_dialogueRecords.Count > _maxDialogueRecords)
                _dialogueRecords.RemoveAt(0);
        }

        public string GetRecentDialogueSummary(int count)
        {
            if (_dialogueRecords.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            var recent = _dialogueRecords.Skip(System.Math.Max(0, _dialogueRecords.Count - count)).ToList();
            foreach (var r in recent)
            {
                int day = r.tick / 60000 + 1;
                string prefix = r.role == "user"
                    ? "RimMind.Storyteller.Prompt.RolePlayer".Translate()
                    : "RimMind.Storyteller.Prompt.RoleNarrator".Translate();
                sb.AppendLine("RimMind.Storyteller.Prompt.DialogueRecordLine".Translate($"{day}", prefix, r.content));
            }
            return sb.ToString().TrimEnd();
        }

        public void ClearDialogueRecords() => _dialogueRecords.Clear();

        public void UpdateTension(IncidentCategoryDef category)
        {
            float delta = category switch
            {
                var c when c == IncidentCategoryDefOf.ThreatBig => 0.25f,
                var c when c == IncidentCategoryDefOf.ThreatSmall => 0.12f,
                var c when c == IncidentCategoryDefOf.Misc => -0.05f,
                var c when c == FactionArrivalCat => -0.08f,
                _ => 0f
            };
            _tensionLevel = Mathf.Clamp01(_tensionLevel + delta);
        }

        public void ApplyDecayAndCleanup()
        {
            int now = Find.TickManager.TicksGame;
            DecayTension(now - _lastTensionDecayTick);
            _lastTensionDecayTick = now;
            CleanupExpiredChains();
        }

        public void DecayTension(int ticksElapsed)
        {
            if (ticksElapsed <= 0) return;
            float decayPerDay = 0.03f;
            float daysElapsed = ticksElapsed / 60000f;
            _tensionLevel = Mathf.Clamp01(_tensionLevel - decayPerDay * daysElapsed);
        }

        public void RecordChainStep(string chainId, int chainStep, int chainTotal, string nextHint, string incidentDefName, int tick, float points, string factionDefName)
        {
            EventChain chain = _activeChains.FirstOrDefault(c => c.chainId == chainId);
            if (chain != null)
            {
                chain.steps.Add(new ChainStep { incidentDefName = incidentDefName, triggeredTick = tick, completed = true });
                chain.currentStep = chainStep;
                chain.nextHint = nextHint;
                chain.lastAdvancedTick = tick;
                chain.lastFactionDefName = factionDefName;
                chain.lastPoints = points;
            }
            else
            {
                chain = new EventChain
                {
                    chainId = chainId,
                    currentStep = chainStep,
                    nextHint = nextHint,
                    lastAdvancedTick = tick,
                    lastFactionDefName = factionDefName,
                    lastPoints = points
                };
                chain.steps.Add(new ChainStep { incidentDefName = incidentDefName, triggeredTick = tick, completed = true });
                _activeChains.Add(chain);
            }
        }

        public void CleanupExpiredChains()
        {
            int now = Find.TickManager.TicksGame;
            _activeChains.RemoveAll(c => now - c.lastAdvancedTick > 600000);
        }

        public string GetActiveChainsSummary()
        {
            if (_activeChains.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (var chain in _activeChains)
            {
                var triggeredNames = string.Join(", ", chain.steps.Select(s => s.incidentDefName));
                sb.AppendLine("RimMind.Storyteller.Prompt.ChainProgress".Translate(
                    chain.chainId, $"{chain.currentStep}", $"{chain.steps.Count}", triggeredNames));
                sb.AppendLine("RimMind.Storyteller.Prompt.ChainHint".Translate(chain.nextHint ?? ""));
            }
            return sb.ToString().TrimEnd();
        }

        public ColonySnapshot TakeSnapshot(Map map)
        {
            return new ColonySnapshot
            {
                colonistCount = map.mapPawns.FreeColonistsSpawnedCount,
                wealth = map.wealthWatcher.WealthTotal,
                tick = Find.TickManager.TicksGame
            };
        }

        public string GetSnapshotDiff(Map map)
        {
            var current = TakeSnapshot(map);
            if (_lastSnapshot == null)
            {
                _lastSnapshot = current;
                return string.Empty;
            }

            var sb = new StringBuilder();
            int colonistDelta = current.colonistCount - _lastSnapshot.colonistCount;
            float wealthDelta = current.wealth - _lastSnapshot.wealth;

            if (colonistDelta != 0)
                sb.AppendLine("RimMind.Storyteller.Prompt.ColonistDelta".Translate(colonistDelta));
            if (System.Math.Abs(wealthDelta) > 100)
                sb.AppendLine("RimMind.Storyteller.Prompt.WealthDelta".Translate(wealthDelta));

            _lastSnapshot = current;
            return sb.ToString().TrimEnd();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref _records, "records", LookMode.Deep);
            _records ??= new List<IncidentHistoryRecord>();
            Scribe_Collections.Look(ref _dialogueRecords, "dialogueRecords", LookMode.Deep);
            _dialogueRecords ??= new List<DialogueRecord>();
#pragma warning disable CS8601
            Scribe_Values.Look(ref CustomSystemPrompt, "customSystemPrompt", string.Empty);
#pragma warning restore CS8601
            Scribe_Values.Look(ref _tensionLevel, "tensionLevel", 0.5f);
            Scribe_Values.Look(ref _lastTensionDecayTick, "lastTensionDecayTick");
            Scribe_Collections.Look(ref _activeChains, "activeChains", LookMode.Deep);
            _activeChains ??= new List<EventChain>();

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (_lastSnapshot != null)
                {
                    var snapColonist = _lastSnapshot.colonistCount;
                    var snapWealth = _lastSnapshot.wealth;
                    var snapTick = _lastSnapshot.tick;
                    Scribe_Values.Look(ref snapColonist, "snapshotColonistCount");
                    Scribe_Values.Look(ref snapWealth, "snapshotWealth");
                    Scribe_Values.Look(ref snapTick, "snapshotTick");
                }
            }
            else
            {
                int snapColonist = 0;
                float snapWealth = 0f;
                int snapTick = 0;
                Scribe_Values.Look(ref snapColonist, "snapshotColonistCount");
                Scribe_Values.Look(ref snapWealth, "snapshotWealth");
                Scribe_Values.Look(ref snapTick, "snapshotTick");
                if (snapTick > 0)
                {
                    _lastSnapshot = new ColonySnapshot { colonistCount = snapColonist, wealth = snapWealth, tick = snapTick };
                }
            }
        }
    }

    public class ColonySnapshot : IExposable
    {
        public int colonistCount;
        public float wealth;
        public int tick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref colonistCount, "colonistCount");
            Scribe_Values.Look(ref wealth, "wealth");
            Scribe_Values.Look(ref tick, "tick");
        }
    }
}
