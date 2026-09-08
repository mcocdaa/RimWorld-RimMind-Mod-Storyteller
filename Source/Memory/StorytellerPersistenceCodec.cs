using System.Collections.Generic;
using Verse;

namespace RimMind.Storyteller.Memory
{
    public static class StorytellerPersistenceCodec
    {
        public static void LookMemory<TRecord, TDialogue, TReaction, TChain>(
            ref List<TRecord> records,
            ref List<TDialogue> dialogueRecords,
            ref List<TReaction> playerReactions,
            ref string customSystemPrompt,
            ref float tensionLevel,
            ref int lastTensionDecayTick,
            ref List<TChain> activeChains)
        {
            Scribe_Collections.Look(ref records, "records", LookMode.Deep);
            records ??= new List<TRecord>();
            Scribe_Collections.Look(ref dialogueRecords, "dialogueRecords", LookMode.Deep);
            dialogueRecords ??= new List<TDialogue>();
            Scribe_Collections.Look(ref playerReactions, "playerReactions", LookMode.Deep);
            playerReactions ??= new List<TReaction>();
#pragma warning disable CS8601
            Scribe_Values.Look(ref customSystemPrompt, "customSystemPrompt", string.Empty);
#pragma warning restore CS8601
            Scribe_Values.Look(ref tensionLevel, "tensionLevel", 0.5f);
            Scribe_Values.Look(ref lastTensionDecayTick, "lastTensionDecayTick", -1);
            Scribe_Collections.Look(ref activeChains, "activeChains", LookMode.Deep);
            activeChains ??= new List<TChain>();
        }

        public static void LookEventChain<TStep>(
            ref string chainId,
            ref List<TStep> steps,
            ref int currentStep,
            ref int totalSteps,
            ref string nextHint,
            ref int lastAdvancedTick,
            ref string lastFactionDefName,
            ref float lastPoints)
        {
#pragma warning disable CS8601
            Scribe_Values.Look(ref chainId, "chainId", string.Empty);
#pragma warning restore CS8601
            Scribe_Collections.Look(ref steps, "steps", LookMode.Deep);
            steps ??= new List<TStep>();
            Scribe_Values.Look(ref currentStep, "currentStep");
            Scribe_Values.Look(ref totalSteps, "totalSteps");
#pragma warning disable CS8601
            Scribe_Values.Look(ref nextHint, "nextHint", string.Empty);
#pragma warning restore CS8601
            Scribe_Values.Look(ref lastAdvancedTick, "lastAdvancedTick");
#pragma warning disable CS8601
            Scribe_Values.Look(ref lastFactionDefName, "lastFactionDefName", string.Empty);
#pragma warning restore CS8601
            Scribe_Values.Look(ref lastPoints, "lastPoints");
        }
    }
}
