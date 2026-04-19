using Verse;

namespace RimMind.Storyteller.Settings
{
    public enum FallbackMode
    {
        Cassandra,
        Randy,
        Phoebe,
        None,
    }

    public class RimMindStorytellerSettings : ModSettings
    {
        public bool enableIntervalTrigger = true;
        public FallbackMode fallbackMode = FallbackMode.Cassandra;
        public float mtbDays = 1.5f;
        public int maxCandidates = 15;
        public bool debugLogging = false;
        public int requestExpireTicks = 30000;
        public int maxEventRecords = 50;
        public int maxDialogueRecords = 30;
        public bool enableEventNotification = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enableIntervalTrigger, "enableIntervalTrigger", true);
            Scribe_Values.Look(ref fallbackMode, "fallbackMode", FallbackMode.Cassandra);
            Scribe_Values.Look(ref mtbDays, "mtbDays", 1.5f);
            Scribe_Values.Look(ref maxCandidates, "maxCandidates", 15);
            Scribe_Values.Look(ref debugLogging, "debugLogging", false);
            Scribe_Values.Look(ref requestExpireTicks, "requestExpireTicks", 30000);
            Scribe_Values.Look(ref maxEventRecords, "maxEventRecords", 50);
            Scribe_Values.Look(ref maxDialogueRecords, "maxDialogueRecords", 30);
            Scribe_Values.Look(ref enableEventNotification, "enableEventNotification", true);
        }
    }
}
