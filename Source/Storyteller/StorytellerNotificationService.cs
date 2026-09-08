using RimMind.Application.Common.Models.UI;
using RimMind.Presentation.Api;
using RimMind.Storyteller.Memory;
using RimWorld;
using Verse;

namespace RimMind.Storyteller
{
    internal sealed class StorytellerNotificationService
    {
        public void Register(
            FiringIncident incident,
            IncidentResponse incidentResponse,
            StorytellerMemory memory)
        {
            bool isBigThreat = incident.def.category == IncidentCategoryDefOf.ThreatBig;
            string titleKey = isBigThreat
                ? "RimMind.Storyteller.UI.DeclareTitle"
                : "RimMind.Storyteller.UI.WhisperTitle";
            string title = titleKey.Translate(incident.def.LabelCap);
            string description = BuildDescription(incident, incidentResponse);

            string shock = "RimMind.Storyteller.UI.Shock".Translate();
            string excited = "RimMind.Storyteller.UI.Excited".Translate();
            string accept = "RimMind.Storyteller.UI.Accept".Translate();
            string tooltip = "RimMind.Storyteller.UI.NoInterfere".Translate();

            string defName = incident.def.defName;
            string label = incident.def.LabelCap.ToString();
            var entry = new RequestEntry
            {
                source = "storyteller",
                title = title,
                description = description,
                options = new[] { shock, excited, accept },
                optionTooltips = new[] { tooltip, tooltip, tooltip },
                expireTicks = RimMindStorytellerMod.Settings?.requestExpireTicks ?? 30000,
                callback = choice => RecordReaction(
                    choice,
                    shock,
                    excited,
                    accept,
                    defName,
                    label,
                    memory)
            };

            RimMindAPI.RegisterPendingRequest(entry);
        }

        private static string BuildDescription(
            FiringIncident incident,
            IncidentResponse response)
        {
            if (!string.IsNullOrEmpty(response.announce))
                return response.announce!;

            if (!string.IsNullOrEmpty(response.reason))
            {
                return response.reason.Length > 20
                    ? response.reason.Substring(0, 20) + "..."
                    : response.reason;
            }

            return "RimMind.Storyteller.UI.DefaultDesc".Translate(
                incident.def.LabelCap);
        }

        private static void RecordReaction(
            string choice,
            string shock,
            string excited,
            string accept,
            string incidentDefName,
            string incidentLabel,
            StorytellerMemory memory)
        {
            string reaction;
            string reactionLabel;
            float tensionDelta;

            if (choice == shock)
            {
                reaction = "shock";
                reactionLabel = shock;
                tensionDelta = 0.05f;
            }
            else if (choice == excited)
            {
                reaction = "excited";
                reactionLabel = excited;
                tensionDelta = -0.05f;
            }
            else
            {
                reaction = "accept";
                reactionLabel = accept;
                tensionDelta = 0f;
            }

            memory.RecordPlayerReaction(
                incidentDefName,
                incidentLabel,
                reaction,
                reactionLabel,
                Find.TickManager.TicksGame);

            if (tensionDelta != 0f)
                memory.ApplyTensionDelta(tensionDelta);
        }
    }
}
