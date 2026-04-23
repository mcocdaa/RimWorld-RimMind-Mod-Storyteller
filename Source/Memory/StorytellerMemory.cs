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

    public class PlayerReactionRecord : IExposable
    {
        public string incidentDefName = string.Empty;
        public string incidentLabel = string.Empty;
        public string reaction = string.Empty;
        public string reactionLabel = string.Empty;
        public int tick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref incidentDefName, "incidentDefName", string.Empty);
            Scribe_Values.Look(ref incidentLabel, "incidentLabel", string.Empty);
            Scribe_Values.Look(ref reaction, "reaction", string.Empty);
            Scribe_Values.Look(ref reactionLabel, "reactionLabel", string.Empty);
            Scribe_Values.Look(ref tick, "tick");
        }
    }

    public class StorytellerMemory : WorldComponent
    {
        private List<IncidentHistoryRecord> _records = new List<IncidentHistoryRecord>();
        private int _maxRecords = 50;

        private List<DialogueRecord> _dialogueRecords = new List<DialogueRecord>();
        private int _maxDialogueRecords = 30;

        private List<PlayerReactionRecord> _playerReactions = new List<PlayerReactionRecord>();
        private int _maxPlayerReactions = 20;

        public string CustomSystemPrompt = string.Empty;

        private static StorytellerMemory? _instance;
        public static StorytellerMemory? Instance => _instance;

        private float _tensionLevel = 0.5f;
        public float TensionLevel => _tensionLevel;
        private int _lastTensionDecayTick;

        private List<EventChain> _activeChains = new List<EventChain>();

        public StorytellerMemory(World world) : base(world)
        {
            _instance = this;
        }

        public IReadOnlyList<IncidentHistoryRecord> Records => _records;
        public IReadOnlyList<DialogueRecord> DialogueRecords => _dialogueRecords;
        private IReadOnlyList<PlayerReactionRecord> PlayerReactions => _playerReactions;
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

        public void RecordPlayerReaction(string incidentDefName, string incidentLabel, string reaction, string reactionLabel, int tick)
        {
            var settings = RimMind.Storyteller.RimMindStorytellerMod.Settings;
            if (settings != null)
                _maxPlayerReactions = settings.maxPlayerReactions;
            _playerReactions.Add(new PlayerReactionRecord
            {
                incidentDefName = incidentDefName,
                incidentLabel = incidentLabel,
                reaction = reaction,
                reactionLabel = reactionLabel,
                tick = tick,
            });
            while (_playerReactions.Count > _maxPlayerReactions)
                _playerReactions.RemoveAt(0);
        }

        public void UpdateTension(IncidentCategoryDef category)
        {
            var factionArrival = DefDatabase<IncidentCategoryDef>.GetNamedSilentFail("FactionArrival");
            float delta = category switch
            {
                var c when c == IncidentCategoryDefOf.ThreatBig => 0.25f,
                var c when c == IncidentCategoryDefOf.ThreatSmall => 0.12f,
                var c when c == IncidentCategoryDefOf.Misc => -0.05f,
                var c when c == factionArrival => -0.08f,
                _ => 0f
            };
            _tensionLevel = Mathf.Clamp01(_tensionLevel + delta);
        }

        public void ApplyDecayAndCleanup()
        {
            int now = Find.TickManager.TicksGame;
            if (_lastTensionDecayTick <= 0)
                _lastTensionDecayTick = now;
            DecayTension(now - _lastTensionDecayTick);
            _lastTensionDecayTick = now;
            CleanupExpiredChains();
        }

        public void DecayTension(int ticksElapsed)
        {
            if (ticksElapsed <= 0) return;
            float decayPerDay = RimMind.Storyteller.RimMindStorytellerMod.Settings?.tensionDecayPerDay ?? 0.03f;
            float daysElapsed = ticksElapsed / 60000f;
            _tensionLevel = Mathf.Clamp01(_tensionLevel - decayPerDay * daysElapsed);
        }

        public void ApplyTensionDelta(float delta)
        {
            _tensionLevel = Mathf.Clamp01(_tensionLevel + delta);
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
            float expireDays = RimMind.Storyteller.RimMindStorytellerMod.Settings?.chainExpireDays ?? 10.0f;
            int expireTicks = (int)(expireDays * 60000f);
            _activeChains.RemoveAll(c => now - c.lastAdvancedTick > expireTicks);
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

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref _records, "records", LookMode.Deep);
            _records ??= new List<IncidentHistoryRecord>();
            Scribe_Collections.Look(ref _dialogueRecords, "dialogueRecords", LookMode.Deep);
            _dialogueRecords ??= new List<DialogueRecord>();
            Scribe_Collections.Look(ref _playerReactions, "playerReactions", LookMode.Deep);
            _playerReactions ??= new List<PlayerReactionRecord>();
#pragma warning disable CS8601
            Scribe_Values.Look(ref CustomSystemPrompt, "customSystemPrompt", string.Empty);
#pragma warning restore CS8601
            Scribe_Values.Look(ref _tensionLevel, "tensionLevel", 0.5f);
            Scribe_Values.Look(ref _lastTensionDecayTick, "lastTensionDecayTick", -1);
            Scribe_Collections.Look(ref _activeChains, "activeChains", LookMode.Deep);
            _activeChains ??= new List<EventChain>();

        }
    }
}
