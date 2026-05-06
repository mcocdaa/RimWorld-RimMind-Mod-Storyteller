using UnityEngine;
using RimMind.Contracts.Extension;
using RimMind.Storyteller.Settings;

namespace RimMind.Storyteller
{
    internal sealed class StorytellerSettingsTabAdapter : ISettingsTab
    {
        public string Id => "storyteller";
        public string Label => "RimMind.Storyteller.UI.TabLabel".Translate();
        public void Draw(Rect rect) => Settings.StorytellerSettingsTab.Draw(rect);
    }
}
