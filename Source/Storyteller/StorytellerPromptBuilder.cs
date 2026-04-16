using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimMind.Core;
using RimMind.Core.Prompt;
using RimMind.Storyteller.Memory;
using RimMind.Storyteller.Settings;
using RimWorld;
using Verse;

namespace RimMind.Storyteller
{
    public static class StorytellerPromptBuilder
    {
        public static string BuildSystemPrompt(StorytellerMemory memory)
        {
            var builder = StructuredPromptBuilder.FromKeyPrefix("RimMind.Storyteller.Prompt.System");

            string difficultyGuidance = BuildDifficultyGuidance();
            if (!string.IsNullOrEmpty(difficultyGuidance))
                builder.Constraint(difficultyGuidance);

            builder.WithCustom(memory.CustomSystemPrompt?.Trim(),
                "RimMind.Storyteller.Prompt.CustomStyleHeader");

            return builder.Build();
        }

        public static string BuildDialogueSystemPrompt()
        {
            var builder = StructuredPromptBuilder.FromKeyPrefix("RimMind.Storyteller.Dialogue.System");

            var diffDef = Find.Storyteller?.difficultyDef;
            if (diffDef != null)
            {
                string difficultyKey = GetDialogueDifficultyKey(diffDef);
                builder.Constraint(difficultyKey.Translate());
            }

            return builder.Build();
        }

        public static string BuildUserPrompt(Map map, StorytellerMemory memory, int maxCandidates)
        {
            var sections = new List<PromptSection>();

            sections.Add(new PromptSection("candidates",
                "RimMind.Storyteller.Prompt.CandidateEvents".Translate() + "\n" + RimMindIncidentSelector.BuildIncidentList(map, memory, maxCandidates),
                PromptSection.PriorityCurrentInput));

            AddMapContextSection(sections, map);
            AddTensionSection(sections, memory);
            AddSnapshotSection(sections, map, memory);
            AddChainsSection(sections, memory);
            AddNarrativeMemorySection(sections);
            AddHistorySection(sections, memory);
            AddDialogueMemorySection(sections, memory);

            var budget = new PromptBudget(6000, 800);
            return budget.ComposeToString(sections);
        }

        public static string BuildDialogueUserPrompt(Map map, StorytellerMemory memory, string userMsg, List<(string role, string content)> dialogueMessages)
        {
            var sections = new List<PromptSection>();

            sections.Add(new PromptSection("player_message",
                "RimMind.Storyteller.Dialogue.Prompt.PlayerMessage".Translate(userMsg),
                PromptSection.PriorityCurrentInput));

            AddMapContextSection(sections, map);
            AddTensionSection(sections, memory);
            AddSnapshotSection(sections, map, memory);
            AddChainsSection(sections, memory);

            string recentSummary = memory.GetRecentSummary(5);
            if (!recentSummary.NullOrEmpty())
                sections.Add(new PromptSection("recent_events",
                    "RimMind.Storyteller.Dialogue.Prompt.RecentEvents".Translate() + "\n" + recentSummary,
                    PromptSection.PriorityMemory));

            AddDialogueMemorySection(sections, memory);
            AddNarrativeMemorySection(sections);

            AddConfidentialSection(sections, map);

            if (dialogueMessages != null && dialogueMessages.Count > 0)
            {
                string historyText = string.Join("\n", dialogueMessages.Select(msg =>
                {
                    string prefix = msg.role == "user"
                        ? "RimMind.Storyteller.Dialogue.PlayerPrefix".Translate()
                        : "RimMind.Storyteller.Dialogue.StorytellerPrefix".Translate();
                    return $"{prefix}{msg.content}";
                }));
                string compressed = ContextComposer.CompressHistory(historyText, 6,
                    "RimMind.Storyteller.Dialogue.Prompt.HistoryCompressed".Translate());
                sections.Add(new PromptSection("dialogue_history",
                    "RimMind.Storyteller.Dialogue.Prompt.DialogueHistory".Translate() + "\n" + compressed,
                    PromptSection.PriorityMemory));
            }

            var budget = new PromptBudget(6000, 800);
            return budget.ComposeToString(sections);
        }

        // ── 共享段落构建 ──────────────────────────────────────────────────────

        private static void AddMapContextSection(List<PromptSection> sections, Map map)
        {
            var section = new PromptSection("situation",
                "RimMind.Storyteller.Prompt.CurrentSituation".Translate() + "\n" + RimMindAPI.BuildMapContext(map, brief: false),
                PromptSection.PriorityKeyState);
            section.Compress = _ =>
                "RimMind.Storyteller.Prompt.CurrentSituation".Translate() + "\n" + RimMindAPI.BuildMapContext(map, brief: true);
            sections.Add(section);
        }

        private static void AddTensionSection(List<PromptSection> sections, StorytellerMemory memory)
        {
            string tensionLabel = GetTensionLabel(memory.TensionLevel);
            sections.Add(new PromptSection("tension",
                "RimMind.Storyteller.Prompt.TensionLevel".Translate(tensionLabel, $"{memory.TensionLevel:F2}"),
                PromptSection.PriorityKeyState));
        }

        private static void AddSnapshotSection(List<PromptSection> sections, Map map, StorytellerMemory memory)
        {
            string snapshotDiff = memory.GetSnapshotDiff(map);
            if (!string.IsNullOrEmpty(snapshotDiff))
                sections.Add(new PromptSection("consequences",
                    "RimMind.Storyteller.Prompt.EventConsequences".Translate() + "\n" + snapshotDiff,
                    PromptSection.PriorityAuxiliary));
        }

        private static void AddChainsSection(List<PromptSection> sections, StorytellerMemory memory)
        {
            string chainsSummary = memory.GetActiveChainsSummary();
            if (!string.IsNullOrEmpty(chainsSummary))
                sections.Add(new PromptSection("chains",
                    "RimMind.Storyteller.Prompt.ActiveChains".Translate() + "\n" + chainsSummary,
                    PromptSection.PriorityAuxiliary));
        }

        private static void AddNarrativeMemorySection(List<PromptSection> sections)
        {
            string narratorContext = RimMindAPI.BuildStaticContext();
            if (!string.IsNullOrEmpty(narratorContext))
                sections.Add(new PromptSection("narrative_memory",
                    "RimMind.Storyteller.Prompt.NarrativeMemory".Translate() + "\n" + narratorContext,
                    PromptSection.PriorityMemory));
        }

        private static void AddHistorySection(List<PromptSection> sections, StorytellerMemory memory)
        {
            string history = memory.GetRecentSummary(10);
            sections.Add(new PromptSection("history",
                "RimMind.Storyteller.Prompt.HistoryMemory".Translate() + "\n" +
                (history ?? "RimMind.Storyteller.Prompt.NoHistory".Translate()),
                PromptSection.PriorityMemory));
        }

        private static void AddDialogueMemorySection(List<PromptSection> sections, StorytellerMemory memory)
        {
            string dialogueSummary = memory.GetRecentDialogueSummary(5);
            if (!string.IsNullOrEmpty(dialogueSummary))
                sections.Add(new PromptSection("dialogue_memory",
                    "RimMind.Storyteller.Prompt.DialogueMemory".Translate() + "\n" + dialogueSummary,
                    PromptSection.PriorityAuxiliary));
        }

        private static void AddConfidentialSection(List<PromptSection> sections, Map map)
        {
            var sb = new StringBuilder();
            sb.AppendLine("RimMind.Storyteller.Dialogue.Prompt.ConfidentialHeader".Translate());

            var director = Find.Storyteller?.storytellerComps?
                .OfType<StorytellerComp_RimMindDirector>()
                .FirstOrDefault();
            if (director != null)
            {
                int ticksLeft = director.GetEstimatedTicksUntilNextEvent();
                float hoursLeft = ticksLeft / 2500f;
                float daysLeft = ticksLeft / 60000f;
                string timeStr = daysLeft >= 1f
                    ? "RimMind.Storyteller.Dialogue.Prompt.DaysLater".Translate($"{daysLeft:F1}")
                    : "RimMind.Storyteller.Dialogue.Prompt.HoursLater".Translate($"{hoursLeft:F1}");
                sb.AppendLine("RimMind.Storyteller.Dialogue.Prompt.NextEventEstimate".Translate(timeStr));
            }

            var diffDef = Find.Storyteller?.difficultyDef;
            if (diffDef != null)
            {
                sb.AppendLine("RimMind.Storyteller.Dialogue.Prompt.CurrentDifficulty".Translate(diffDef.LabelCap, $"{diffDef.threatScale:F2}"));
                if (!diffDef.allowBigThreats)
                    sb.AppendLine("RimMind.Storyteller.Dialogue.Prompt.BigThreatsDisabled".Translate());
            }

            sb.AppendLine("RimMind.Storyteller.Dialogue.Prompt.ConfidentialFooter".Translate());

            sections.Add(new PromptSection("confidential",
                sb.ToString().TrimEnd(),
                PromptSection.PriorityAuxiliary));
        }

        // ── 辅助 ──────────────────────────────────────────────────────────────

        public static string BuildDifficultyGuidance()
        {
            var diffDef = Find.Storyteller?.difficultyDef;
            if (diffDef == null) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("RimMind.Storyteller.Prompt.DifficultyContext".Translate());
            sb.AppendLine("RimMind.Storyteller.Prompt.DifficultyDetail".Translate(
                diffDef.LabelCap, $"{diffDef.threatScale:F2}"));

            if (!diffDef.allowBigThreats)
                sb.AppendLine("RimMind.Storyteller.Prompt.NoBigThreats".Translate());
            if (!diffDef.allowIntroThreats)
                sb.AppendLine("RimMind.Storyteller.Prompt.NoIntroThreats".Translate());
            if (!diffDef.allowViolentQuests)
                sb.AppendLine("RimMind.Storyteller.Prompt.NoViolentQuests".Translate());
            if (diffDef.colonistMoodOffset != 0)
                sb.AppendLine("RimMind.Storyteller.Prompt.MoodOffset".Translate($"{diffDef.colonistMoodOffset:+#;-#;0}"));

            sb.AppendLine("RimMind.Storyteller.Prompt.DifficultyGuidance".Translate(
                GetDifficultyBehaviorLabel(diffDef.threatScale, diffDef.allowBigThreats)));

            return sb.ToString().TrimEnd();
        }

        public static string GetDifficultyBehaviorLabel(float threatScale, bool allowBigThreats)
        {
            if (!allowBigThreats || threatScale <= 0.15f)
                return "RimMind.Storyteller.Prompt.DifficultyPeaceful".Translate();
            if (threatScale <= 0.35f)
                return "RimMind.Storyteller.Prompt.DifficultyEasy".Translate();
            if (threatScale <= 0.65f)
                return "RimMind.Storyteller.Prompt.DifficultyMedium".Translate();
            if (threatScale <= 1.1f)
                return "RimMind.Storyteller.Prompt.DifficultyRough".Translate();
            if (threatScale <= 1.6f)
                return "RimMind.Storyteller.Prompt.DifficultyHard".Translate();
            return "RimMind.Storyteller.Prompt.DifficultyExtreme".Translate();
        }

        private static string GetDialogueDifficultyKey(DifficultyDef diffDef)
        {
            if (!diffDef.allowBigThreats || diffDef.threatScale <= 0.15f)
                return "RimMind.Storyteller.Dialogue.DifficultyPeaceful";
            if (diffDef.threatScale <= 0.35f)
                return "RimMind.Storyteller.Dialogue.DifficultyEasy";
            if (diffDef.threatScale <= 0.65f)
                return "RimMind.Storyteller.Dialogue.DifficultyMedium";
            if (diffDef.threatScale <= 1.1f)
                return "RimMind.Storyteller.Dialogue.DifficultyRough";
            if (diffDef.threatScale <= 1.6f)
                return "RimMind.Storyteller.Dialogue.DifficultyHard";
            return "RimMind.Storyteller.Dialogue.DifficultyExtreme";
        }

        public static string GetTensionLabel(float tension)
        {
            if (tension >= 0.8f) return "RimMind.Storyteller.Prompt.TensionVeryHigh".Translate();
            if (tension >= 0.6f) return "RimMind.Storyteller.Prompt.TensionHigh".Translate();
            if (tension >= 0.4f) return "RimMind.Storyteller.Prompt.TensionMedium".Translate();
            if (tension >= 0.2f) return "RimMind.Storyteller.Prompt.TensionLow".Translate();
            return "RimMind.Storyteller.Prompt.TensionVeryLow".Translate();
        }
    }
}
