using System.Linq;
using System.Text;
using LudeonTK;
using RimMind.Core;
using RimMind.Storyteller.Memory;
using RimMind.Storyteller.Settings;
using RimWorld;
using Verse;

namespace RimMind.Storyteller.Debug
{
    [StaticConstructorOnStartup]
    public static class StorytellerDebugActions
    {
        [DebugAction("RimMind Storyteller", "Force AI Incident Selection", actionType = DebugActionType.Action)]
        private static void ForceAIIncidentSelection()
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                Log.Warning("[RimMind-Storyteller] No current map.");
                return;
            }

            if (!RimMindAPI.IsConfigured())
            {
                Log.Warning("[RimMind-Storyteller] API not configured.");
                return;
            }

            var director = Find.Storyteller?.storytellerComps?
                .OfType<StorytellerComp_RimMindDirector>()
                .FirstOrDefault();

            if (director == null)
            {
                Log.Warning("[RimMind-Storyteller] Current storyteller is not RimMind Director.");
                return;
            }

            bool ok = director.ForceRequest(map);
            Log.Message(ok
                ? "[RimMind-Storyteller] Forced AI incident selection request sent."
                : "[RimMind-Storyteller] Forced AI incident selection request FAILED.");
        }

        [DebugAction("RimMind Storyteller", "Show Memory", actionType = DebugActionType.Action)]
        private static void ShowMemory()
        {
            var memory = StorytellerMemory.Instance;
            if (memory == null)
            {
                Log.Message("[RimMind-Storyteller] Memory not initialized (load a game first).");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[RimMind-Storyteller] History Memory ({memory.Records.Count}/50):");
            string summary = memory.GetRecentSummary(10);
            if (string.IsNullOrEmpty(summary))
                sb.AppendLine("(No records)");
            else
                sb.AppendLine(summary);

            sb.AppendLine($"Custom Prompt: {(string.IsNullOrEmpty(memory.CustomSystemPrompt) ? "(Default)" : memory.CustomSystemPrompt)}");
            sb.AppendLine($"Tension Level: {memory.TensionLevel:F2}");
            if (memory.ActiveChainsCount > 0)
            {
                sb.AppendLine($"Active Chains: {memory.ActiveChainsCount}");
                sb.AppendLine(memory.GetActiveChainsSummary());
            }
            Log.Message(sb.ToString());
        }

        [DebugAction("RimMind Storyteller", "Fire Incident (manual)", actionType = DebugActionType.Action)]
        private static void FireIncidentManual()
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                Log.Warning("[RimMind-Storyteller] No current map.");
                return;
            }

            var incidents = DefDatabase<IncidentDef>.AllDefsListForReading
                .Where(d => d.targetTags != null && d.targetTags.Contains(IncidentTargetTagDefOf.Map_PlayerHome))
                .Where(d => d.Worker.CanFireNow(StorytellerUtility.DefaultParmsNow(d.category, map)))
                .OrderBy(d => d.defName)
                .ToList();

            var options = incidents.Select(d => d.defName).ToList();

            Find.WindowStack.Add(new Dialog_DebugOptionListLister(
                options.Select(opt => new DebugMenuOption(opt, DebugMenuOptionMode.Action, () =>
                {
                    var def = DefDatabase<IncidentDef>.GetNamedSilentFail(opt);
                    if (def != null)
                    {
                        var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
                        def.Worker.TryExecute(parms);
                        Log.Message($"[RimMind-Storyteller] Manually fired incident: {opt}");
                    }
                })).ToList(),
                "Select Event"));
        }

        [DebugAction("RimMind Storyteller", "Test Fallback Mode", actionType = DebugActionType.Action)]
        private static void TestFallbackMode()
        {
            var settings = RimMindStorytellerMod.Settings;
            var modes = new[] { FallbackMode.Cassandra, FallbackMode.Randy, FallbackMode.Phoebe, FallbackMode.None };
            int idx = System.Array.IndexOf(modes, settings.fallbackMode);
            settings.fallbackMode = modes[(idx + 1) % modes.Length];
            Log.Message($"[RimMind-Storyteller] Fallback mode switched to: {settings.fallbackMode}");
        }

        [DebugAction("RimMind Storyteller", "Show Director State", actionType = DebugActionType.Action)]
        private static void ShowDirectorState()
        {
            var director = Find.Storyteller?.storytellerComps?
                .OfType<StorytellerComp_RimMindDirector>()
                .FirstOrDefault();

            if (director == null)
            {
                Log.Message("[RimMind-Storyteller] Director comp not found.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[RimMind-Storyteller] Director State:");
            sb.AppendLine($"  IsActive: {director.IsActive}");
            sb.AppendLine($"  Estimated ticks until next event: {director.GetEstimatedTicksUntilNextEvent()}");
            float hours = director.GetEstimatedTicksUntilNextEvent() / 2500f;
            float days = director.GetEstimatedTicksUntilNextEvent() / 60000f;
            sb.AppendLine($"  Estimated time: {days:F1} days / {hours:F1} hours");
            sb.AppendLine($"  Difficulty: {Find.Storyteller?.difficultyDef?.LabelCap ?? "unknown"} (threatScale={Find.Storyteller?.difficultyDef?.threatScale:F2})");
            sb.AppendLine($"  Interval trigger: {RimMindStorytellerMod.Settings?.enableIntervalTrigger}");
            sb.AppendLine($"  MTB days: {RimMindStorytellerMod.Settings?.mtbDays:F1}");

            var memory = StorytellerMemory.Instance;
            if (memory != null)
            {
                sb.AppendLine($"  Dialogue records: {memory.DialogueRecords.Count}");
                sb.AppendLine($"  Event records: {memory.Records.Count}");
                string dialogueSummary = memory.GetRecentDialogueSummary(3);
                if (!string.IsNullOrEmpty(dialogueSummary))
                {
                    sb.AppendLine("  Recent dialogues:");
                    sb.AppendLine(dialogueSummary);
                }
            }

            Log.Message(sb.ToString());
        }

        [DebugAction("RimMind Storyteller", "Show Tension History", actionType = DebugActionType.Action)]
        private static void ShowTensionHistory()
        {
            var memory = StorytellerMemory.Instance;
            if (memory == null)
            {
                Log.Message("[RimMind-Storyteller] Memory not initialized (load a game first).");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[RimMind-Storyteller] Tension History:");
            sb.AppendLine($"  Current TensionLevel: {memory.TensionLevel:F2}");

            var reactions = memory.PlayerReactions;
            if (reactions.Count == 0)
            {
                sb.AppendLine("  No player reactions recorded.");
            }
            else
            {
                sb.AppendLine($"  Recent PlayerReactions ({reactions.Count}):");
                int showCount = System.Math.Min(reactions.Count, 10);
                var recent = reactions.Skip(System.Math.Max(0, reactions.Count - showCount)).ToList();
                foreach (var r in recent)
                {
                    int day = r.tick / 60000 + 1;
                    sb.AppendLine($"    Day {day}: [{r.incidentDefName}] {r.incidentLabel} -> {r.reaction} ({r.reactionLabel})");
                }
            }

            Log.Message(sb.ToString());
        }

        [DebugAction("RimMind Storyteller", "Clear Storyteller Memory", actionType = DebugActionType.Action)]
        private static void ClearStorytellerMemory()
        {
            var memory = StorytellerMemory.Instance;
            if (memory == null)
            {
                Log.Warning("[RimMind-Storyteller] Memory not initialized (load a game first).");
                return;
            }

            int recordsBefore = memory.Records.Count;
            int dialoguesBefore = memory.DialogueRecords.Count;
            memory.ClearRecords();
            memory.ClearDialogueRecords();
            Log.Message($"[RimMind-Storyteller] Memory cleared. Records: {recordsBefore} -> 0, DialogueRecords: {dialoguesBefore} -> 0.");
        }

        [DebugAction("RimMind Storyteller", "Reset Tension Level", actionType = DebugActionType.Action)]
        private static void ResetTensionLevel()
        {
            var memory = StorytellerMemory.Instance;
            if (memory == null)
            {
                Log.Warning("[RimMind-Storyteller] Memory not initialized (load a game first).");
                return;
            }

            float before = memory.TensionLevel;
            float delta = 0.5f - before;
            memory.ApplyTensionDelta(delta);
            Log.Message($"[RimMind-Storyteller] TensionLevel reset: {before:F2} -> {memory.TensionLevel:F2} (delta={delta:F2}).");
        }

        [DebugAction("RimMind Storyteller", "Show Event Chains Detail", actionType = DebugActionType.Action)]
        private static void ShowEventChainsDetail()
        {
            var memory = StorytellerMemory.Instance;
            if (memory == null)
            {
                Log.Message("[RimMind-Storyteller] Memory not initialized (load a game first).");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[RimMind-Storyteller] Event Chains Detail:");
            sb.AppendLine($"  ActiveChainsCount: {memory.ActiveChainsCount}");

            if (memory.ActiveChainsCount > 0)
            {
                string summary = memory.GetActiveChainsSummary();
                if (!string.IsNullOrEmpty(summary))
                    sb.AppendLine(summary);
            }
            else
            {
                sb.AppendLine("  No active chains.");
            }

            Log.Message(sb.ToString());
        }
    }
}
