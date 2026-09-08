using System.Collections.Generic;
using System.Linq;
using RimMind.Presentation.Api;
using RimMind.Storyteller.Settings;
using RimWorld;
using Verse;

namespace RimMind.Storyteller
{
    public class StorytellerComp_RimMindDirector : RimWorld.StorytellerComp
    {
        private StorytellerRequestCoordinator? _requestCoordinator;

        private StorytellerRequestCoordinator RequestCoordinator =>
            _requestCoordinator ??= new StorytellerRequestCoordinator(this);

        private StorytellerCompProperties_RimMindDirector Props =>
            (StorytellerCompProperties_RimMindDirector)props;

        public bool IsActive => RequestCoordinator.IsActive;
        public int LastSuccessTick => RequestCoordinator.LastSuccessTick;
        public int LastFailTick => RequestCoordinator.LastFailTick;

        public int GetEstimatedTicksUntilNextEvent()
        {
            float mtb = RimMindStorytellerMod.Settings?.mtbDays ?? Props.mtbDays;
            return (int)(mtb * 60000f);
        }

        public override IEnumerable<FiringIncident> MakeIntervalIncidents(
            IIncidentTarget target)
        {
            if (!target.IncidentTargetTags().Contains(
                    IncidentTargetTagDefOf.Map_PlayerHome))
            {
                yield break;
            }

            if (target is not Map)
                yield break;

            if (!RequestCoordinator.ApplyWorldMaintenance())
                yield break;

            if (RequestCoordinator.TryTakePendingIncident(
                    out FiringIncident? incident))
            {
                if (RimMindStorytellerMod.Settings?.debugLogging == true)
                {
                    Log.Message(
                        $"[RimMind-Storyteller] AI incident firing: {incident!.def.defName}");
                }

                yield return incident!;
                yield break;
            }

            if (RequestCoordinator.HasPendingRequest)
                yield break;

            if (!RimMindAPI.IsConfigured())
                yield break;

            if (!(RimMindStorytellerMod.Settings?.enableIntervalTrigger ?? false))
                yield break;

            if (RimMindAPI.ShouldSkipStorytellerIncident())
                yield break;

            float mtb = RimMindStorytellerMod.Settings?.mtbDays ?? Props.mtbDays;
            if (!Rand.MTBEventOccurs(mtb, 60000f, 1000f))
                yield break;

            RequestCoordinator.TryDispatch(target);
        }

        public bool ForceRequest(IIncidentTarget target)
            => RequestCoordinator.ForceRequest(target);

    }
}
