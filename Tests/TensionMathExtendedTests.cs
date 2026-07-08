using RimMind.Storyteller.Memory;
using Xunit;

namespace RimMind.Storyteller.Tests
{
    // TensionMath 补充测试：边界值和极端场景
    public class TensionMathExtendedTests
    {
        [Fact]
        public void Clamp01_ExactZero_ReturnsZero()
        {
            Assert.Equal(0f, TensionMath.Clamp01(0f));
        }

        [Fact]
        public void Clamp01_ExactOne_ReturnsOne()
        {
            Assert.Equal(1f, TensionMath.Clamp01(1f));
        }

        [Fact]
        public void Clamp01_SlightlyBelowZero_ReturnsZero()
        {
            Assert.Equal(0f, TensionMath.Clamp01(-0.001f));
        }

        [Fact]
        public void Clamp01_SlightlyAboveOne_ReturnsOne()
        {
            Assert.Equal(1f, TensionMath.Clamp01(1.001f));
        }

        [Fact]
        public void ComputeDecay_ZeroRate_NoChange()
        {
            float result = TensionMath.ComputeDecay(0.8f, 0f, TensionMath.TicksPerDay);
            Assert.Equal(0.8f, result, 3);
        }

        [Fact]
        public void ComputeDecay_LargeTicks_ClampsToZero()
        {
            // 极长时间衰减，应降为 0
            float result = TensionMath.ComputeDecay(0.5f, 0.03f, TensionMath.TicksPerDay * 100);
            Assert.Equal(0f, result, 3);
        }

        [Fact]
        public void ComputeDecay_HalfDay_DecaysProportionally()
        {
            float result = TensionMath.ComputeDecay(0.5f, 0.03f, TensionMath.TicksPerDay / 2);
            Assert.Equal(0.485f, result, 3);
        }

        [Fact]
        public void ApplyDelta_ZeroDelta_NoChange()
        {
            float result = TensionMath.ApplyDelta(0.5f, 0f);
            Assert.Equal(0.5f, result, 3);
        }

        [Fact]
        public void ApplyDelta_LargePositive_ClampsToOne()
        {
            float result = TensionMath.ApplyDelta(0.5f, 1.0f);
            Assert.Equal(1f, result, 3);
        }

        [Fact]
        public void ApplyDelta_LargeNegative_ClampsToZero()
        {
            float result = TensionMath.ApplyDelta(0.5f, -1.0f);
            Assert.Equal(0f, result, 3);
        }

        [Fact]
        public void ComputeDecay_VerySmallTickIncrement()
        {
            // 1 tick 的衰减应非常微小
            float result = TensionMath.ComputeDecay(1.0f, 0.03f, 1);
            Assert.True(result > 0.999f);
            Assert.True(result <= 1.0f);
        }
    }
}
