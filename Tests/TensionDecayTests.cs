using RimMind.Storyteller.Memory;
using Xunit;

namespace RimMind.Storyteller.Tests
{
    public class TensionDecayTests
    {
        [Fact]
        public void Decay_ZeroTicks_NoChange()
        {
            Assert.Equal(0.5f, TensionMath.ComputeDecay(0.5f, 0.03f, 0));
        }

        [Fact]
        public void Decay_NegativeTicks_NoChange()
        {
            Assert.Equal(0.5f, TensionMath.ComputeDecay(0.5f, 0.03f, -100));
        }

        [Fact]
        public void Decay_OneDay_DecreasesByRate()
        {
            float result = TensionMath.ComputeDecay(0.5f, 0.03f, TensionMath.TicksPerDay);
            Assert.Equal(0.47f, result, 3);
        }

        [Fact]
        public void Decay_MultipleDays_DecreasesProportionally()
        {
            float result = TensionMath.ComputeDecay(0.5f, 0.03f, TensionMath.TicksPerDay * 5);
            Assert.Equal(0.35f, result, 3);
        }

        [Fact]
        public void Decay_CannotGoBelowZero()
        {
            float result = TensionMath.ComputeDecay(0.1f, 0.03f, TensionMath.TicksPerDay * 10);
            Assert.Equal(0f, result, 3);
        }

        [Fact]
        public void Decay_CannotExceedOne()
        {
            float result = TensionMath.ComputeDecay(1.5f, 0.03f, TensionMath.TicksPerDay);
            Assert.Equal(1f, result, 3);
        }

        [Fact]
        public void DailyDecay_DecreasesByRate()
        {
            float result = TensionMath.ComputeDailyDecay(0.5f, 0.03f);
            Assert.Equal(0.47f, result, 3);
        }

        [Fact]
        public void DailyDecay_CannotGoBelowZero()
        {
            float result = TensionMath.ComputeDailyDecay(0.02f, 0.03f);
            Assert.Equal(0f, result, 3);
        }

        [Fact]
        public void DoubleDecayBug_DailyDecayPlusTickDecay()
        {
            float tension = 0.5f;
            float rate = 0.03f;

            float afterDailyDecay = TensionMath.ComputeDailyDecay(tension, rate);
            float afterTickDecay = TensionMath.ComputeDecay(afterDailyDecay, rate, TensionMath.TicksPerDay);

            Assert.Equal(0.47f, afterDailyDecay, 3);
            Assert.Equal(0.44f, afterTickDecay, 3);

            float expectedSingleDecay = TensionMath.ComputeDailyDecay(tension, rate);
            Assert.NotEqual(expectedSingleDecay, afterTickDecay);
        }

        [Fact]
        public void ApplyDelta_IncreasesTension()
        {
            float result = TensionMath.ApplyDelta(0.5f, 0.25f);
            Assert.Equal(0.75f, result, 3);
        }

        [Fact]
        public void ApplyDelta_ClampsToOne()
        {
            float result = TensionMath.ApplyDelta(0.9f, 0.25f);
            Assert.Equal(1f, result, 3);
        }

        [Fact]
        public void ApplyDelta_Negative_DecreasesTension()
        {
            float result = TensionMath.ApplyDelta(0.5f, -0.1f);
            Assert.Equal(0.4f, result, 3);
        }

        [Fact]
        public void Clamp01_BelowZero_ReturnsZero()
        {
            Assert.Equal(0f, TensionMath.Clamp01(-0.5f));
        }

        [Fact]
        public void Clamp01_AboveOne_ReturnsOne()
        {
            Assert.Equal(1f, TensionMath.Clamp01(1.5f));
        }

        [Fact]
        public void Clamp01_InRange_ReturnsValue()
        {
            Assert.Equal(0.5f, TensionMath.Clamp01(0.5f));
        }

        [Fact]
        public void TicksPerDay_Is60000()
        {
            Assert.Equal(60000, TensionMath.TicksPerDay);
        }
    }
}
