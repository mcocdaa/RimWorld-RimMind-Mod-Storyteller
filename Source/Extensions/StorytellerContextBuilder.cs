using System.Text;
using RimWorld;
using Verse;

namespace RimMind.Storyteller.Extensions
{
    /// <summary>
    /// Builds storyteller context text sections (difficulty, threat, tension).
    /// Extracted from RimMindStorytellerMod for reuse and testing.
    ///
    /// 3 个纯逻辑解析器（ResolveDifficultyTier / ResolveDifficultyName / ResolveDifficultyGuidanceKey）
    /// 可在 net10.0 测试项目中直接单元测试；3 个 Append 方法依赖 RimWorld 运行时 API
    /// （Find.Storyteller / TaggedString.Translate），通过 ArchTest 模式验证。
    /// </summary>
    public static class StorytellerContextBuilder
    {
        /// <summary>
        /// 根据 threatScale 解析为 6 档难度 tier（0-5）。
        /// </summary>
        public static int ResolveDifficultyTier(float threatScale)
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

        /// <summary>
        /// 根据 difficultyLevel 返回对应的难度指导翻译 key；无效值返回 null。
        /// </summary>
        public static string? ResolveDifficultyGuidanceKey(int difficultyLevel)
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

        /// <summary>
        /// 根据 difficultyLevel 返回 RimWorld 原生难度名；未知值返回 Custom 格式。
        /// </summary>
        public static string ResolveDifficultyName(int difficultyLevel)
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

        /// <summary>
        /// 向 StringBuilder 追加当前难度上下文（难度名、threatScale、限制开关、难度指导）。
        /// 依赖 Find.Storyteller 运行时 API。
        /// </summary>
        public static void AppendDifficultyContext(StringBuilder sb)
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

        /// <summary>
        /// 向 StringBuilder 追加威胁等级标签（4 档：None/Low/Medium/High）。
        /// 依赖 Find.Storyteller 运行时 API。
        /// </summary>
        public static void AppendThreatLevel(StringBuilder sb)
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

        /// <summary>
        /// 向 StringBuilder 追加张力分类标签（5 档：VeryLow/Low/Medium/High/VeryHigh）。
        /// </summary>
        public static void AppendTensionLabel(StringBuilder sb, float tension)
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
    }
}
