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
                    return new List<ContextEntry> { new ContextEntry("RimMind.Storyteller.Prompt.TaskInstruction".Translate()) };
                }, "RimMind.Storyteller");
        }

        public override string SettingsCategory() => "RimMind - Storyteller";

        public override void DoSettingsWindowContents(Rect rect)
        {
            StorytellerSettingsTab.Draw(rect);
        }
    }
}
