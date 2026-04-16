using System.Collections.Generic;
using System.Linq;
using RimMind.Core;
using RimMind.Storyteller.Settings;
using RimWorld;
using Verse;

namespace RimMind.Storyteller
{
    public class StorytellerComp_RimMindFallback : RimWorld.StorytellerComp
    {
        private static readonly IncidentCategoryDef FactionArrivalCat =
            DefDatabase<IncidentCategoryDef>.GetNamedSilentFail("FactionArrival");

        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            var settings = RimMindStorytellerMod.Settings;
            if (settings == null) yield break;

            var mode = settings.fallbackMode;
            if (mode == FallbackMode.None) yield break;

            var aiComp = Find.Storyteller.storytellerComps
                .OfType<StorytellerComp_RimMindDirector>()
                .FirstOrDefault();

            if (aiComp != null && aiComp.IsActive)
                yield break;

            if (RimMindAPI.IsConfigured() && settings.enableIntervalTrigger && aiComp != null)
            {
                int now = Find.TickManager.TicksGame;
                float directorMtb = settings.mtbDays;
                int healthyThreshold = (int)(directorMtb * 60000f * 2f);

                bool directorHealthy = now - aiComp.LastSuccessTick < healthyThreshold;
                bool directorFailedRecently = now - aiComp.LastFailTick < healthyThreshold;

                if (directorHealthy && !directorFailedRecently)
                    yield break;
            }

            if (!Rand.MTBEventOccurs(GetMTBDays(mode), 60000f, 1000f))
                yield break;

            IncidentCategoryDef category = ChooseCategory(mode);
            IncidentParms parms = GenerateParms(category, target);

            var usable = UsableIncidentsInCategory(category, parms);
            if (!usable.Any()) yield break;

            if (TrySelectRandomIncident(usable, out IncidentDef def, target))
                yield return new FiringIncident(def, this, parms);
        }

        private float GetMTBDays(FallbackMode mode)
        {
            return mode switch
            {
                FallbackMode.Cassandra => 4.6f,
                FallbackMode.Randy => 1.35f,
                FallbackMode.Phoebe => 8.0f,
                _ => 999f,
            };
        }

        private IncidentCategoryDef ChooseCategory(FallbackMode mode)
        {
            return mode switch
            {
                FallbackMode.Phoebe => Rand.Value < 0.4f && FactionArrivalCat != null
                    ? FactionArrivalCat
                    : IncidentCategoryDefOf.ThreatSmall,
                FallbackMode.Randy => Rand.Value < 0.3f
                    ? IncidentCategoryDefOf.ThreatBig
                    : Rand.Value < 0.6f
                        ? IncidentCategoryDefOf.ThreatSmall
                        : IncidentCategoryDefOf.Misc,
                _ => IncidentCategoryDefOf.ThreatBig,
            };
        }
    }
}
