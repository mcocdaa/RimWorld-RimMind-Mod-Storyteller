using System;

namespace RimMind.Storyteller.Extensions
{
    public static class StorytellerContextPolicy
    {
        public static bool IsApplicable(
            string? actualScenario,
            string storytellerScenario,
            int pawnId,
            bool requiresPawn = true)
            => string.Equals(actualScenario, storytellerScenario, StringComparison.Ordinal)
               && (!requiresPawn || pawnId > 0);

        public static string ComposeTaskInstruction(
            string? customSystemPrompt,
            string generatedTaskInstruction)
        {
            if (string.IsNullOrWhiteSpace(customSystemPrompt))
                return generatedTaskInstruction;

            return $"{customSystemPrompt!.Trim()}\n\n{generatedTaskInstruction}";
        }
    }
}
