using System;
using System.IO;
using Xunit;

namespace RimMind.Storyteller.Tests
{
    /// <summary>
    /// ArchTest for PawnLookup extraction (Task 6).
    ///
    /// PawnLookup 依赖 Verse.Find（RimWorld 运行时 API），无法在 net10.0 纯逻辑测试项目中
    /// 实例化。采用 ArchTest 模式：读取源文件文本并断言内容，确保 helper 存在且被使用。
    /// </summary>
    public sealed class PawnLookupTests
    {
        private static readonly string ProjectRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

        private static readonly string SourceDir = Path.Combine(ProjectRoot, "Source");

        private static string ReadSource(string relativePath)
            => File.ReadAllText(Path.Combine(SourceDir, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        /// <summary>
        /// PawnLookup 必须作为静态类存在于 Source/Extensions/PawnLookup.cs，
        /// 且暴露 FindPawnById(int) 方法作为唯一对外 API。
        /// </summary>
        [Fact]
        public void PawnLookup_FileExists_AndIsStaticClass_WithFindPawnById()
        {
            string content = ReadSource("Extensions/PawnLookup.cs");

            Assert.Contains("public static class PawnLookup", content);
            Assert.Contains("FindPawnById", content);
        }

        /// <summary>
        /// FindPawnById 必须在内部处理 pawnId <= 0 的无效输入，返回 null。
        /// 调用方无需在调用前重复该 guard。
        /// </summary>
        [Fact]
        public void PawnLookup_HandlesInvalidIds_ReturnsNull()
        {
            string content = ReadSource("Extensions/PawnLookup.cs");

            Assert.Contains("if (pawnId <= 0) return null", content);
        }

        /// <summary>
        /// PawnLookup 必须先搜索 WorldPawns.AllPawnsAlive，再回退到 CurrentMap 的 FreeColonists，
        /// 与原内联 lookup 行为保持一致。
        /// </summary>
        [Fact]
        public void PawnLookup_SearchesWorldPawnsThenCurrentMap()
        {
            string content = ReadSource("Extensions/PawnLookup.cs");

            Assert.Contains("Find.WorldPawns.AllPawnsAlive.FirstOrDefault", content);
            Assert.Contains("Find.CurrentMap?.mapPawns?.FreeColonists.FirstOrDefault", content);
        }

        /// <summary>
        /// RimMindStorytellerMod 必须通过 PawnLookup.FindPawnById 调用 helper，
        /// 而非保留内联 lookup 模式。
        /// </summary>
        [Fact]
        public void RimMindStorytellerMod_UsesPawnLookup_NotInlineLookup()
        {
            string modContent = ReadSource("RimMindStorytellerMod.cs");

            Assert.Contains("PawnLookup.FindPawnById", modContent);
            // 原内联 lookup 模式必须被完全移除
            Assert.DoesNotContain("Find.WorldPawns.AllPawnsAlive.FirstOrDefault", modContent);
        }
    }
}
