using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using RimMind.Core;
using RimMind.Core.Context;
using RimMind.Core.Prompt;
using RimMind.Storyteller.Memory;
using RimMind.Storyteller.Settings;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Storyteller
{
    public class RimMindStorytellerMod : Mod
    {
        public static RimMindStorytellerSettings Settings = null!;
        private const string ModId = "RimMind.Storyteller";

        public RimMindStorytellerMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RimMindStorytellerSettings>();
            new Harmony("mcocdaa.RimMindStoryteller").PatchAll();

            RimMindAPI.RegisterSettingsTab("storyteller", () => "RimMind.Storyteller.UI.TabLabel".Translate(), StorytellerSettingsTab.Draw);
            RimMindAPI.RegisterModCooldown("Storyteller", () => (int)(Settings.mtbDays * 60000f));

            RegisterProviders();

            Log.Message("[RimMind-Storyteller] Initialized.");
        }

        private void RegisterProviders()
        {
            ContextKeyRegistry.Register("storyteller_dialogue", ContextLayer.L3_State, 0.5f,
                pawn =>
                {
                    if (ContextKeyRegistry.CurrentScenario != ScenarioIds.Storyteller) return new List<ContextEntry>();
                    var mem = StorytellerMemory.Instance;
                    if (mem == null) return new List<ContextEntry>();
                    string dialogue = mem.GetRecentDialogueSummary(5);
                    return string.IsNullOrEmpty(dialogue)
                        ? new List<ContextEntry>()
                        : new List<ContextEntry> { new ContextEntry($"{"RimMind.Storyteller.Dialogue.StorytellerDialogueHeader".Translate()}\n{dialogue}") };
                }, "RimMind.Storyteller");

            ContextKeyRegistry.Register("storyteller_task", ContextLayer.L0_Static, 0.95f,
                pawn =>
                {
                    if (ContextKeyRegistry.CurrentScenario != ScenarioIds.Storyteller) return new List<ContextEntry>();
                    string taskInstruction = TaskInstructionBuilder.Build(
                        "RimMind.Storyteller.Prompt.TaskInstruction",
                        "Role", "Goal", "Process", "Constraint", "Example", "Output", "Fallback",
                        "SystemJsonFormat", "SystemTensionGuidance", "SystemChainGuidance",
                        "SystemParamsGuidance", "SystemRequirements");
                    return new List<ContextEntry> { new ContextEntry(taskInstruction) };
                }, "RimMind.Storyteller");

            ContextKeyRegistry.Register("storyteller_context", ContextLayer.L1_Baseline, 0.85f,
                pawn =>
                {
                    if (ContextKeyRegistry.CurrentScenario != ScenarioIds.Storyteller) return new List<ContextEntry>();
                    var mem = StorytellerMemory.Instance;
                    if (mem == null) return new List<ContextEntry>();
                    var sb = new StringBuilder();
                    sb.AppendLine("RimMind.Storyteller.Prompt.StorytellerStateHeader".Translate());
                    AppendDifficultyContext(sb);
                    AppendThreatLevel(sb);
                    AppendTensionLabel(sb, mem.TensionLevel);
                    sb.AppendLine("RimMind.Storyteller.Prompt.TensionLevel".Translate(
                        $"{(int)(mem.TensionLevel * 100)}%", $"{mem.TensionLevel:F2}"));
                    string summary = mem.GetRecentSummary(5);
                    if (!string.IsNullOrEmpty(summary))
                        sb.AppendLine(summary);
                    string chains = mem.GetActiveChainsSummary();
                    if (!string.IsNullOrEmpty(chains))
                        sb.AppendLine(chains);
                    return new List<ContextEntry> { new ContextEntry(sb.ToString().TrimEnd()) };
                }, "RimMind.Storyteller");

            ContextKeyRegistry.Register("storyteller_reactions", ContextLayer.L1_Baseline, 0.8f,
                pawn =>
                {
                    if (ContextKeyRegistry.CurrentScenario != ScenarioIds.Storyteller) return new List<ContextEntry>();
                    var mem = StorytellerMemory.Instance;
                    if (mem == null) return new List<ContextEntry>();
                    var reactions = mem.PlayerReactions;
                    if (reactions.Count == 0) return new List<ContextEntry>();
                    var sb = new StringBuilder();
                    foreach (var r in reactions)
                    {
                        int day = r.tick / 60000 + 1;
                        sb.AppendLine("RimMind.Storyteller.Prompt.ReactionRecordLine".Translate(
                            day.ToString(), r.incidentLabel, r.reactionLabel));
                    }
                    return new List<ContextEntry> { new ContextEntry(sb.ToString().TrimEnd()) };
                }, "RimMind.Storyteller");
        }

        private static void AppendDifficultyContext(StringBuilder sb)
        {
            var diff = Find.Storyteller?.difficulty;
            if (diff == null) return;
            int tier = ResolveDifficultyTier(diff.threatScale);
            string difficultyName = ResolveDifficultyName(tier);
            sb.AppendLine("RimMind.Storyteller.Prompt.DifficultyContext".Translate());
            sb.AppendLine("RimMind.Storyteller.Prompt.DifficultyDetail".Translate(
                difficultyName, $"{diff.threatScale:F2}"));
            if (!diff.allowBigThreats)
                sb.AppendLine("RimMind.Storyteller.Prompt.NoBigThreats".Translate());
            if (!diff.allowIntroThreats)
                sb.AppendLine("RimMind.Storyteller.Prompt.NoIntroThreats".Translate());
            if (!diff.allowViolentQuests)
                sb.AppendLine("RimMind.Storyteller.Prompt.NoViolentQuests".Translate());
            string? guidanceKey = ResolveDifficultyGuidanceKey(tier);
            if (guidanceKey != null)
                sb.AppendLine("RimMind.Storyteller.Prompt.DifficultyGuidance".Translate(guidanceKey.Translate()));
        }

        private static void AppendThreatLevel(StringBuilder sb)
        {
            var diff = Find.Storyteller?.difficulty;
            if (diff == null) return;
            string threatLabel = diff.threatScale switch
            {
                < 0.1f => "RimMind.Storyteller.Prompt.ThreatNone".Translate(),
                < 0.5f => "RimMind.Storyteller.Prompt.ThreatLow".Translate(),
                < 0.8f => "RimMind.Storyteller.Prompt.ThreatMedium".Translate(),
                _ => "RimMind.Storyteller.Prompt.ThreatHigh".Translate()
            };
            sb.AppendLine($"[Threat Level] {threatLabel}");
        }

        private static void AppendTensionLabel(StringBuilder sb, float tension)
        {
            string tensionLabel = tension switch
            {
                < 0.2f => "RimMind.Storyteller.Prompt.TensionVeryLow".Translate(),
                < 0.4f => "RimMind.Storyteller.Prompt.TensionLow".Translate(),
                < 0.6f => "RimMind.Storyteller.Prompt.TensionMedium".Translate(),
                < 0.8f => "RimMind.Storyteller.Prompt.TensionHigh".Translate(),
                _ => "RimMind.Storyteller.Prompt.TensionVeryHigh".Translate()
            };
            sb.AppendLine($"[Tension Category] {tensionLabel}");
        }

        private static int ResolveDifficultyTier(float threatScale)
        {
            return threatScale switch
            {
                < 0.1f => 0,
                < 0.5f => 1,
                < 0.8f => 2,
                < 1.2f => 3,
                < 1.8f => 4,
                _ => 5
            };
        }

        private static string? ResolveDifficultyGuidanceKey(int difficultyLevel)
        {
            return difficultyLevel switch
            {
                0 => "RimMind.Storyteller.Prompt.DifficultyPeaceful",
                1 => "RimMind.Storyteller.Prompt.DifficultyEasy",
                2 => "RimMind.Storyteller.Prompt.DifficultyMedium",
                3 => "RimMind.Storyteller.Prompt.DifficultyRough",
                4 => "RimMind.Storyteller.Prompt.DifficultyHard",
                5 => "RimMind.Storyteller.Prompt.DifficultyExtreme",
                _ => null
            };
        }

        private static string ResolveDifficultyName(int difficultyLevel)
        {
            return difficultyLevel switch
            {
                0 => "Peaceful",
                1 => "Community Builder",
                2 => "Adventure Story",
                3 => "Strive to Survive",
                4 => "Blood and Dust",
                5 => "Losing is Fun",
                _ => $"Custom ({difficultyLevel})"
            };
        }

        public override string SettingsCategory() => "RimMind - Storyteller";

        public override void DoSettingsWindowContents(Rect rect)
        {
            StorytellerSettingsTab.Draw(rect);
        }
    }
}
