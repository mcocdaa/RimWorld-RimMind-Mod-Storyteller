using HarmonyLib;
using RimMind.Core;
using RimMind.Storyteller.Settings;
using UnityEngine;
using Verse;

namespace RimMind.Storyteller
{
    public class RimMindStorytellerMod : Mod
    {
        public static RimMindStorytellerSettings Settings = null!;

        public RimMindStorytellerMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RimMindStorytellerSettings>();
            new Harmony("mcocdaa.RimMindStoryteller").PatchAll();

            RimMindAPI.RegisterSettingsTab("storyteller", () => "RimMind.Storyteller.UI.TabLabel".Translate(), StorytellerSettingsTab.Draw);
            RimMindAPI.RegisterModCooldown("Storyteller", () => (int)(Settings.mtbDays * 60000f));
            Log.Message("[RimMind-Storyteller] Initialized.");
        }

        public override string SettingsCategory() => "RimMind - Storyteller";

        public override void DoSettingsWindowContents(Rect rect)
        {
            StorytellerSettingsTab.Draw(rect);
        }
    }
}
