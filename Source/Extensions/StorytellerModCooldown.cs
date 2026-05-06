using RimMind.Contracts.Extension;

namespace RimMind.Storyteller
{
    internal sealed class StorytellerModCooldown : IModCooldown
    {
        private readonly RimMindStorytellerSettings _settings;
        public StorytellerModCooldown(RimMindStorytellerSettings settings) { _settings = settings; }
        public string Id => "Storyteller";
        public int CooldownTicks => (int)(_settings.mtbDays * 60000f);
    }
}
