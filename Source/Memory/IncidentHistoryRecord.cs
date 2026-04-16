using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimMind.Storyteller.Memory
{
    public class IncidentHistoryRecord : IExposable
    {
        public string IncidentDefName = string.Empty;
        public string Label = string.Empty;
        public int TriggeredTick;
        public string MapName = string.Empty;
        public string CategoryDefName = string.Empty;

        public IncidentHistoryRecord() { }

        public static IncidentHistoryRecord Create(IncidentDef def, IIncidentTarget target, int tick)
        {
            string mapName = "RimMind.Storyteller.UI.UnknownMap".Translate();
            if (target is Map map)
                mapName = map.Parent?.Label ?? "RimMind.Storyteller.UI.MainBase".Translate();

            return new IncidentHistoryRecord
            {
                IncidentDefName = def.defName,
                Label = def.LabelCap.RawText.NullOrEmpty() ? def.defName : def.LabelCap.RawText,
                TriggeredTick = tick,
                MapName = mapName,
                CategoryDefName = def.category?.defName ?? string.Empty,
            };
        }

        public void ExposeData()
        {
#pragma warning disable CS8601
            Scribe_Values.Look(ref IncidentDefName, "incidentDefName", string.Empty);
            Scribe_Values.Look(ref Label, "label", string.Empty);
            Scribe_Values.Look(ref TriggeredTick, "triggeredTick");
            Scribe_Values.Look(ref MapName, "mapName", string.Empty);
            Scribe_Values.Look(ref CategoryDefName, "categoryDefName", string.Empty);
#pragma warning restore CS8601
        }
    }
}
