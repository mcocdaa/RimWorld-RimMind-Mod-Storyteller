namespace RimMind.Storyteller.Memory
{
    public static class TensionMath
    {
        public const int TicksPerDay = 60000;

        public static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        public static float ComputeDecay(float currentTension, float decayPerDay, int ticksElapsed)
        {
            if (ticksElapsed <= 0) return currentTension;
            float daysElapsed = ticksElapsed / (float)TicksPerDay;
            return Clamp01(currentTension - decayPerDay * daysElapsed);
        }

        public static float ComputeDailyDecay(float currentTension, float decayPerDay)
        {
            return Clamp01(currentTension - decayPerDay);
        }

        public static float ApplyDelta(float currentTension, float delta)
        {
            return Clamp01(currentTension + delta);
        }
    }
}
