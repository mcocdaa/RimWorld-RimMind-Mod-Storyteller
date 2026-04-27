using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using RimMind.Core;
using RimMind.Core.Context;
using RimMind.Core.Prompt;
using RimMind.Storyteller.Memory;
using RimMind.Storyteller.Settings;
using UnityEngine;
using Verse;

namespace RimMind.Storyteller
{
    public class RimMindStorytellerMod : Mod
    {
        public static RimMindStorytellerSettings Settings = null!;
        private const string ModId = "RimMind.Storyteller";

        public RimMindStorytellerMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RimMindStorytellerSettings>();
            new Harmony("mcocdaa.RimMindStoryteller").PatchAll();

            RimMindAPI.RegisterSettingsTab("storyteller", () => "RimMind.Storyteller.UI.TabLabel".Translate(), StorytellerSettingsTab.Draw);
            RimMindAPI.RegisterModCooldown("Storyteller", () => (int)(Settings.mtbDays * 60000f));

            RegisterProviders();

            Log.Message("[RimMind-Storyteller] Initialized.");
        }

        private void RegisterProviders()
        {
            RimMindAPI.RegisterPawnContextProvider("storyteller_state", pawn =>
            {
                var mem = StorytellerMemory.Instance;
                if (mem == null) return null;

                var sb = new StringBuilder("RimMind.Storyteller.Prompt.StorytellerStateHeader".Translate());
                sb.AppendLine($" Tension: {mem.TensionLevel:F2}");
                string summary = mem.GetRecentSummary(5);
                if (!string.IsNullOrEmpty(summary))
                    sb.AppendLine(summary);
                string chains = mem.GetActiveChainsSummary();
                if (!string.IsNullOrEmpty(chains))
                    sb.AppendLine(chains);
                return sb.ToString().TrimEnd();
            }, PromptSection.PriorityAuxiliary, ModId);

            RimMindAPI.RegisterStaticProvider("storyteller_dialogue", () =>
            {
                var mem = StorytellerMemory.Instance;
                if (mem == null) return (string?)null;
                string dialogue = mem.GetRecentDialogueSummary(5);
                return string.IsNullOrEmpty(dialogue) ? null : $"{"RimMind.Storyteller.Dialogue.StorytellerDialogueHeader".Translate()}\n{dialogue}";
            }, PromptSection.PriorityAuxiliary, ModId);

            ContextKeyRegistry.Register("storyteller_task", ContextLayer.L0_Static, 0.95f,
                pawn =>
                {
                    if (ContextKeyRegistry.CurrentScenario != ScenarioIds.Storyteller) return new List<ContextEntry>();
                    string taskInstruction = TaskInstructionBuilder.Build(
                        "RimMind.Storyteller.Prompt.TaskInstruction",
                        "Role", "Goal", "Process", "Constraint", "Example", "Output", "Fallback",
                        "SystemJsonFormat", "SystemTensionGuidance", "SystemChainGuidance",
                        "SystemParamsGuidance", "SystemRequirements");
                    return new List<ContextEntry> { new ContextEntry(taskInstruction) };
                }, "RimMind.Storyteller");

            ContextKeyRegistry.Register("storyteller_context", ContextLayer.L1_Baseline, 0.85f,
                pawn =>
                {
                    if (ContextKeyRegistry.CurrentScenario != ScenarioIds.Storyteller) return new List<ContextEntry>();
                    var mem = StorytellerMemory.Instance;
                    if (mem == null) return new List<ContextEntry>();
                    var sb = new StringBuilder();
                    sb.AppendLine("RimMind.Storyteller.Prompt.StorytellerStateHeader".Translate());
                    sb.AppendLine("RimMind.Storyteller.Prompt.TensionLevel".Translate(
                        $"{(int)(mem.TensionLevel * 100)}%", $"{mem.TensionLevel:F2}"));
                    string summary = mem.GetRecentSummary(5);
                    if (!string.IsNullOrEmpty(summary))
                        sb.AppendLine(summary);
                    string chains = mem.GetActiveChainsSummary();
                    if (!string.IsNullOrEmpty(chains))
                        sb.AppendLine(chains);
                    return new List<ContextEntry> { new ContextEntry(sb.ToString().TrimEnd()) };
                }, "RimMind.Storyteller");

            ContextKeyRegistry.Register("storyteller_reactions", ContextLayer.L1_Baseline, 0.8f,
                pawn =>
                {
                    if (ContextKeyRegistry.CurrentScenario != ScenarioIds.Storyteller) return new List<ContextEntry>();
                    var mem = StorytellerMemory.Instance;
                    if (mem == null) return new List<ContextEntry>();
                    var reactions = mem.PlayerReactions;
                    if (reactions.Count == 0) return new List<ContextEntry>();
                    var sb = new StringBuilder();
                    foreach (var r in reactions)
                    {
                        int day = r.tick / 60000 + 1;
                        sb.AppendLine("RimMind.Storyteller.Prompt.ReactionRecordLine".Translate(
                            day.ToString(), r.incidentLabel, r.reactionLabel));
                    }
                    return new List<ContextEntry> { new ContextEntry(sb.ToString().TrimEnd()) };
                }, "RimMind.Storyteller");
        }

        public override string SettingsCategory() => "RimMind - Storyteller";

        public override void DoSettingsWindowContents(Rect rect)
        {
            StorytellerSettingsTab.Draw(rect);
        }
    }
}
