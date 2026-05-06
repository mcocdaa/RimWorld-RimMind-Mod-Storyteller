using RimMind.Contracts.Extension;

namespace RimMind.Storyteller
{
    internal sealed class StorytellerIncidentExecutedListener : IIncidentExecutedListener
    {
        public string Id => "storyteller.incident_executed";
        public void OnIncidentExecuted() { }
    }
}
