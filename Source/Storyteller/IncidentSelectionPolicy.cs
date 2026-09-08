using System;

namespace RimMind.Storyteller
{
    public enum IncidentSelectionDisposition
    {
        InvalidResponse,
        UnknownDefinition,
        CannotFire,
        Selected,
    }

    public enum StorytellerIncidentKind
    {
        Other,
        ThreatSmall,
        ThreatBig,
    }

    public static class IncidentSelectionPolicy
    {
        public static IncidentSelectionDisposition Evaluate(
            bool hasParsedResponse,
            bool definitionExists,
            bool canFireNow)
        {
            if (!hasParsedResponse) return IncidentSelectionDisposition.InvalidResponse;
            if (!definitionExists) return IncidentSelectionDisposition.UnknownDefinition;
            return canFireNow
                ? IncidentSelectionDisposition.Selected
                : IncidentSelectionDisposition.CannotFire;
        }

        public static float ClampPointsMultiplier(float multiplier)
            => Math.Clamp(multiplier, 0.3f, 2.0f);

        public static bool ShouldNotify(
            bool notificationsEnabled,
            StorytellerIncidentKind kind)
            => notificationsEnabled
               && (kind == StorytellerIncidentKind.ThreatBig
                   || kind == StorytellerIncidentKind.ThreatSmall);
    }
}
