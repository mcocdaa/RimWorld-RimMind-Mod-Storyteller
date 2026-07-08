using System;
using System.IO;
using Xunit;

namespace RimMind.Storyteller.Tests
{
    /// <summary>
    /// ArchTest for StorytellerContextBuilder extraction (Task 7).
    ///
    /// StorytellerContextBuilder 的部分方法（AppendDifficultyContext / AppendThreatLevel /
    /// AppendTensionLabel）依赖 Verse.Find 和 TaggedString.Translate（RimWorld 运行时 API），
    /// 无法在 net10.0 纯逻辑测试项目中实例化。采用 ArchTest 模式：读取源文件文本并断言内容，
    /// 确保 builder 存在、方法签名正确、且被 RimMindStorytellerMod 委托调用。
    /// </summary>
    public sealed class StorytellerContextBuilderTests
    {
        private static readonly string ProjectRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

        private static readonly string SourceDir = Path.Combine(ProjectRoot, "Source");

        private static string ReadSource(string relativePath)
            => File.ReadAllText(Path.Combine(SourceDir, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        /// <summary>
        /// StorytellerContextBuilder 必须作为静态类存在于 Source/Extensions/StorytellerContextBuilder.cs。
        /// </summary>
        [Fact]
        public void StorytellerContextBuilder_FileExists_AndIsStaticClass()
        {
            string content = ReadSource("Extensions/StorytellerContextBuilder.cs");

            Assert.Contains("public static class StorytellerContextBuilder", content);
        }

        /// <summary>
        /// Builder 必须暴露全部 6 个方法：3 个纯逻辑解析器 + 3 个 UI/运行时附加器。
        /// </summary>
        [Fact]
        public void StorytellerContextBuilder_HasAllSixMethods()
        {
            string content = ReadSource("Extensions/StorytellerContextBuilder.cs");

            Assert.Contains("ResolveDifficultyTier", content);
            Assert.Contains("ResolveDifficultyName", content);
            Assert.Contains("ResolveDifficultyGuidanceKey", content);
            Assert.Contains("AppendDifficultyContext", content);
            Assert.Contains("AppendThreatLevel", content);
            Assert.Contains("AppendTensionLabel", content);
        }

        /// <summary>
        /// ResolveDifficultyTier 必须为 public static，接受 float threatScale，返回 int。
        /// 6 档分级（0-5）阈值需与原 Mod 行为一致。
        /// </summary>
        [Fact]
        public void ResolveDifficultyTier_IsPublic_Static_ReturnsInt_WithSixTiers()
        {
            string content = ReadSource("Extensions/StorytellerContextBuilder.cs");

            Assert.Contains("public static int ResolveDifficultyTier(float threatScale)", content);
            // 6 档阈值
            Assert.Contains("< 0.1f => 0", content);
            Assert.Contains("< 0.5f => 1", content);
            Assert.Contains("< 0.8f => 2", content);
            Assert.Contains("< 1.2f => 3", content);
            Assert.Contains("< 1.8f => 4", content);
            Assert.Contains("_ => 5", content);
        }

        /// <summary>
        /// ResolveDifficultyGuidanceKey 必须为 public static，接受 int，返回 string?（可空）。
        /// 6 档映射到对应的翻译 key，无效值返回 null。
        /// </summary>
        [Fact]
        public void ResolveDifficultyGuidanceKey_IsPublic_Static_ReturnsNullableString()
        {
            string content = ReadSource("Extensions/StorytellerContextBuilder.cs");

            Assert.Contains("public static string? ResolveDifficultyGuidanceKey(int difficultyLevel)", content);
            Assert.Contains("RimMind.Storyteller.Prompt.DifficultyPeaceful", content);
            Assert.Contains("RimMind.Storyteller.Prompt.DifficultyEasy", content);
            Assert.Contains("RimMind.Storyteller.Prompt.DifficultyMedium", content);
            Assert.Contains("RimMind.Storyteller.Prompt.DifficultyRough", content);
            Assert.Contains("RimMind.Storyteller.Prompt.DifficultyHard", content);
            Assert.Contains("RimMind.Storyteller.Prompt.DifficultyExtreme", content);
            Assert.Contains("_ => null", content);
        }

        /// <summary>
        /// ResolveDifficultyName 必须为 public static，接受 int，返回 string（非空）。
        /// 6 档难度名与 RimWorld 原生难度名一致。
        /// </summary>
        [Fact]
        public void ResolveDifficultyName_IsPublic_Static_ReturnsString()
        {
            string content = ReadSource("Extensions/StorytellerContextBuilder.cs");

            Assert.Contains("public static string ResolveDifficultyName(int difficultyLevel)", content);
            Assert.Contains("Peaceful", content);
            Assert.Contains("Community Builder", content);
            Assert.Contains("Adventure Story", content);
            Assert.Contains("Strive to Survive", content);
            Assert.Contains("Blood and Dust", content);
            Assert.Contains("Losing is Fun", content);
        }

        /// <summary>
        /// AppendDifficultyContext / AppendThreatLevel / AppendTensionLabel 必须为 public static，
        /// 接受 StringBuilder（AppendTensionLabel 额外接受 float tension）。
        /// </summary>
        [Fact]
        public void AppendMethods_ArePublic_Static_WithCorrectSignatures()
        {
            string content = ReadSource("Extensions/StorytellerContextBuilder.cs");

            Assert.Contains("public static void AppendDifficultyContext(StringBuilder sb)", content);
            Assert.Contains("public static void AppendThreatLevel(StringBuilder sb)", content);
            Assert.Contains("public static void AppendTensionLabel(StringBuilder sb, float tension)", content);
        }

        /// <summary>
        /// AppendThreatLevel 必须使用 4 档阈值（None/Low/Medium/High），
        /// 与原 Mod 行为一致（不要误改成 5 档）。
        /// </summary>
        [Fact]
        public void AppendThreatLevel_UsesFourTiers_MatchingOriginalBehavior()
        {
            string content = ReadSource("Extensions/StorytellerContextBuilder.cs");

            Assert.Contains("RimMind.Storyteller.Prompt.ThreatNone", content);
            Assert.Contains("RimMind.Storyteller.Prompt.ThreatLow", content);
            Assert.Contains("RimMind.Storyteller.Prompt.ThreatMedium", content);
            Assert.Contains("RimMind.Storyteller.Prompt.ThreatHigh", content);
            // 阈值顺序必须保留
            Assert.Contains("< 0.1f =>", content);
            Assert.Contains("< 0.5f =>", content);
            Assert.Contains("< 0.8f =>", content);
        }

        /// <summary>
        /// AppendTensionLabel 必须使用 5 档张力标签。
        /// </summary>
        [Fact]
        public void AppendTensionLabel_UsesFiveTiers_MatchingOriginalBehavior()
        {
            string content = ReadSource("Extensions/StorytellerContextBuilder.cs");

            Assert.Contains("RimMind.Storyteller.Prompt.TensionVeryLow", content);
            Assert.Contains("RimMind.Storyteller.Prompt.TensionLow", content);
            Assert.Contains("RimMind.Storyteller.Prompt.TensionMedium", content);
            Assert.Contains("RimMind.Storyteller.Prompt.TensionHigh", content);
            Assert.Contains("RimMind.Storyteller.Prompt.TensionVeryHigh", content);
        }

        /// <summary>
        /// RimMindStorytellerMod 必须通过 StorytellerContextBuilder.X 委托调用 3 个 Append 方法，
        /// 而非保留内联 private static 方法。
        /// </summary>
        [Fact]
        public void RimMindStorytellerMod_DelegatesToBuilder_NotInlineMethods()
        {
            string modContent = ReadSource("RimMindStorytellerMod.cs");

            Assert.Contains("StorytellerContextBuilder.AppendDifficultyContext", modContent);
            Assert.Contains("StorytellerContextBuilder.AppendThreatLevel", modContent);
            Assert.Contains("StorytellerContextBuilder.AppendTensionLabel", modContent);
            // 原 private static 方法必须全部移除
            Assert.DoesNotContain("private static void AppendDifficultyContext", modContent);
            Assert.DoesNotContain("private static void AppendThreatLevel", modContent);
            Assert.DoesNotContain("private static void AppendTensionLabel", modContent);
            // 纯逻辑解析器也必须从 Mod 中移除（已迁移到 builder）
            Assert.DoesNotContain("private static int ResolveDifficultyTier", modContent);
            Assert.DoesNotContain("private static string? ResolveDifficultyGuidanceKey", modContent);
            Assert.DoesNotContain("private static string ResolveDifficultyName", modContent);
        }

        /// <summary>
        /// Mod 文件必须已 using RimMind.Storyteller.Extensions 命名空间（Task 3/6 引入）。
        /// </summary>
        [Fact]
        public void RimMindStorytellerMod_ImportsExtensionsNamespace()
        {
            string modContent = ReadSource("RimMindStorytellerMod.cs");

            Assert.Contains("using RimMind.Storyteller.Extensions;", modContent);
        }
    }
}
