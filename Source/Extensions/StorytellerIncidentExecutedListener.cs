using RimMind.Application.Common.Interfaces.Extension;

namespace RimMind.Storyteller
{
    internal sealed class StorytellerIncidentExecutedListener : IIncidentExecutedListener
    {
        public string Id => "storyteller.incident_executed";
        public string OwnerModId => "RimMind.Storyteller";
        public void OnIncidentExecuted() { }
    }
}
