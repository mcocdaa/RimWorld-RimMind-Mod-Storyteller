using HarmonyLib;
using RimMind.Application.Common.Interfaces.Extension;
using RimMind.Presentation.Api;
using RimMind.Presentation.Settings;
using RimMind.Storyteller.Extensions;
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

            RimMindAPI.Extensions<ISettingsTab>().Register(new StorytellerSettingsTabAdapter());
            RimMindAPI.Extensions<IModCooldown>().Register(new StorytellerModCooldown(Settings));
            RimMindAPI.Extensions<ISkipCheck>().Register(new StorytellerIncidentSkipCheck(Settings));

            StorytellerContextProviderRegistrar.RegisterAll();

            Log.Message("[RimMind-Storyteller] Initialized.");
        }

        public override string SettingsCategory() => "RimMind - Storyteller";

        public override void DoSettingsWindowContents(Rect rect)
        {
            StorytellerSettingsTab.Draw(rect);
        }
    }
}
