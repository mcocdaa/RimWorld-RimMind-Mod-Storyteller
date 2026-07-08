using System;
using System.IO;
using Xunit;

namespace RimMind.Storyteller.Tests
{
    /// <summary>
    /// 回归测试：张力双重衰减 Bug 修复。
    ///
    /// Bug 背景：原 StorytellerMemory 存在两条衰减路径
    ///   - 路径 A：WorldComponentTick 每 60000 tick 调用 DecayTensionDaily()
    ///   - 路径 B：MakeIntervalIncidents 调用 ApplyDecayAndCleanup() -> DecayTension(ticksElapsed)
    /// 路径 A 重置 _lastTensionDecayTick，导致路径 B 在同日稍后 tick 看到的 elapsed 接近 0，
    /// 但当 MakeIntervalIncidents 在更晚的 tick 触发时，路径 B 会再次衰减，
    /// 实际衰减速率约为设定值的 1.5~2 倍。
    ///
    /// 修复：移除 DecayTensionDaily()，WorldComponentTick 直接调用 ApplyDecayAndCleanup()
    /// 作为唯一衰减路径。ApplyDecayAndCleanup 内部已通过 (now - _lastTensionDecayTick)
    /// 正确计算经过 tick 数。
    ///
    /// 本测试采用 ArchTest 模式（读取源文件文本断言），因为 StorytellerMemory 依赖
    /// RimWorld/Verse，无法在 net10.0 纯逻辑测试项目中实例化。TensionMath 的数学行为
    /// 已由 TensionDecayTests / TensionMathExtendedTests 覆盖。
    /// </summary>
    public sealed class StorytellerMemoryDecayTests
    {
        private static readonly string ProjectRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

        private static readonly string SourceDir = Path.Combine(ProjectRoot, "Source");

        private static string ReadSource(string relativePath)
            => File.ReadAllText(Path.Combine(SourceDir, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        /// <summary>
        /// DecayTensionDaily() 公开方法必须被整体移除，禁止残留定义或调用。
        /// 这是双衰减 Bug 的根因入口。
        /// </summary>
        [Fact]
        public void StorytellerMemory_DecayTensionDaily_Method_Removed()
        {
            string content = ReadSource("Memory/StorytellerMemory.cs");

            Assert.DoesNotContain("DecayTensionDaily", content);
        }

        /// <summary>
        /// WorldComponentTick 必须以 ApplyDecayAndCleanup() 作为唯一日级衰减入口，
        /// 不再调用已移除的 DecayTensionDaily()。
        /// </summary>
        [Fact]
        public void WorldComponentTick_Calls_ApplyDecayAndCleanup_AsSoleDecayPath()
        {
            string content = ReadSource("Memory/StorytellerMemory.cs");

            // 日级衰减仍以 60000 tick 为周期触发
            Assert.Contains("TicksGame % 60000 == 0", content);

            // 唯一衰减入口为 ApplyDecayAndCleanup
            Assert.Contains("ApplyDecayAndCleanup()", content);

            // 禁止在 WorldComponentTick 内出现 ComputeDailyDecay 直调
            int tickStart = content.IndexOf("public override void WorldComponentTick()", StringComparison.Ordinal);
            Assert.True(tickStart >= 0, "WorldComponentTick override not found");
            int tickEnd = content.IndexOf("\n        }", tickStart, StringComparison.Ordinal);
            Assert.True(tickEnd > tickStart, "WorldComponentTick body end not found");
            string tickBody = content.Substring(tickStart, tickEnd - tickStart);
            Assert.DoesNotContain("ComputeDailyDecay", tickBody);
            Assert.DoesNotContain("DecayTensionDaily", tickBody);
        }

        /// <summary>
        /// ApplyDecayAndCleanup 必须保留并正确通过 (now - _lastTensionDecayTick) 计算经过 tick，
        /// 调用 DecayTension(elapsed) 作为唯一衰减执行点。
        /// </summary>
        [Fact]
        public void ApplyDecayAndCleanup_Computes_ElapsedTicks_AndCalls_DecayTension()
        {
            string content = ReadSource("Memory/StorytellerMemory.cs");

            Assert.Contains("public void ApplyDecayAndCleanup()", content);
            Assert.Contains("DecayTension(now - _lastTensionDecayTick)", content);
            Assert.Contains("_lastTensionDecayTick = now", content);
        }

        /// <summary>
        /// 私有 DecayTension(int ticksElapsed) 必须调用 TensionMath.ComputeDecay（基于 tick 的衰减），
        /// 而非 TensionMath.ComputeDailyDecay（固定日衰减，会与 tick 路径叠加）。
        /// </summary>
        [Fact]
        public void DecayTension_Uses_TickBased_ComputeDecay_Not_DailyDecay()
        {
            string content = ReadSource("Memory/StorytellerMemory.cs");

            int decayStart = content.IndexOf("private void DecayTension(int ticksElapsed)", StringComparison.Ordinal);
            Assert.True(decayStart >= 0, "DecayTension(int) method not found");
            int decayEnd = content.IndexOf("\n        }", decayStart, StringComparison.Ordinal);
            Assert.True(decayEnd > decayStart, "DecayTension body end not found");
            string decayBody = content.Substring(decayStart, decayEnd - decayStart);

            Assert.Contains("TensionMath.ComputeDecay(", decayBody);
            Assert.DoesNotContain("ComputeDailyDecay", decayBody);
        }
    }
}
