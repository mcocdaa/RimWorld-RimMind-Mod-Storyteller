using System.Collections.Generic;
using RimMind.Core.UI;
using RimMind.Storyteller.Memory;
using UnityEngine;
using Verse;

namespace RimMind.Storyteller.Settings
{
    public static class StorytellerSettingsTab
    {
        private static Vector2 _scrollPos = Vector2.zero;
        private static string _promptText = string.Empty;
        private static bool _promptLoaded;

        public static void Draw(Rect inRect)
        {
            LoadPromptOnce();

            Rect contentArea = SettingsUIHelper.SplitContentArea(inRect);
            Rect bottomBar = SettingsUIHelper.SplitBottomBar(inRect);

            float contentH = EstimateHeight();
            Rect viewRect = new Rect(0f, 0f, contentArea.width - 16f, contentH);
            Widgets.BeginScrollView(contentArea, ref _scrollPos, viewRect);

            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            var settings = RimMindStorytellerMod.Settings;

            SettingsUIHelper.DrawSectionHeader(listing, "RimMind.Storyteller.UI.TriggerSources".Translate());
            listing.CheckboxLabeled("RimMind.Storyteller.UI.EnableIntervalTrigger".Translate(), ref settings.enableIntervalTrigger,
                "RimMind.Storyteller.UI.EnableIntervalTrigger.Desc".Translate());
            listing.CheckboxLabeled("RimMind.Storyteller.UI.EnableEventNotification".Translate(), ref settings.enableEventNotification,
                "RimMind.Storyteller.UI.EnableEventNotification.Desc".Translate());

            SettingsUIHelper.DrawSectionHeader(listing, "RimMind.Storyteller.UI.Section.Fallback".Translate());
            listing.Label("RimMind.Storyteller.UI.FallbackModeLabel".Translate(settings.fallbackMode.ToString()));
            GUI.color = Color.gray;
            listing.Label("  " + "RimMind.Storyteller.UI.FallbackModeDesc".Translate());
            GUI.color = Color.white;
            if (Widgets.ButtonText(listing.GetRect(30f), "RimMind.Storyteller.UI.SwitchFallback".Translate()))
            {
                var modes = new List<FallbackMode>
                {
                    FallbackMode.Cassandra,
                    FallbackMode.Randy,
                    FallbackMode.Phoebe,
                    FallbackMode.None,
                };
                int idx = modes.IndexOf(settings.fallbackMode);
                settings.fallbackMode = modes[(idx + 1) % modes.Count];
            }

            SettingsUIHelper.DrawCustomPromptSection(listing,
                "RimMind.Storyteller.UI.StylePromptLabel".Translate(),
                ref _promptText);
            SavePrompt();

            SettingsUIHelper.DrawSectionHeader(listing, "RimMind.Storyteller.UI.Section.Request".Translate());
            listing.Label("RimMind.Storyteller.UI.MTBDays".Translate($"{settings.mtbDays:F1}"));
            GUI.color = Color.gray;
            listing.Label("  " + "RimMind.Storyteller.UI.MTBDays.Desc".Translate());
            GUI.color = Color.white;
            settings.mtbDays = listing.Slider(settings.mtbDays, 0.5f, 10f);
            settings.mtbDays = (float)System.Math.Round(settings.mtbDays, 1);

            listing.Label("RimMind.Storyteller.UI.RequestExpire".Translate($"{settings.requestExpireTicks / 60000f:F2}"));
            GUI.color = Color.gray;
            listing.Label("  " + "RimMind.Storyteller.UI.RequestExpire.Desc".Translate());
            GUI.color = Color.white;
            settings.requestExpireTicks = (int)listing.Slider(settings.requestExpireTicks, 3600f, 120000f);
            settings.requestExpireTicks = (settings.requestExpireTicks / 1500) * 1500;

            SettingsUIHelper.DrawSectionHeader(listing, "RimMind.Storyteller.UI.Section.Debug".Translate());
            listing.CheckboxLabeled("RimMind.Storyteller.UI.DebugLogging".Translate(), ref settings.debugLogging,
                "RimMind.Storyteller.UI.DebugLogging.Desc".Translate());

            SettingsUIHelper.DrawSectionHeader(listing, "RimMind.Storyteller.UI.Section.Memory".Translate());
            listing.Label("RimMind.Storyteller.UI.MaxEventRecords".Translate(settings.maxEventRecords));
            GUI.color = Color.gray;
            listing.Label("  " + "RimMind.Storyteller.UI.MaxEventRecords.Desc".Translate());
            GUI.color = Color.white;
            settings.maxEventRecords = (int)listing.Slider(settings.maxEventRecords, 10f, 100f);
            listing.Label("RimMind.Storyteller.UI.MaxDialogueRecords".Translate(settings.maxDialogueRecords));
            GUI.color = Color.gray;
            listing.Label("  " + "RimMind.Storyteller.UI.MaxDialogueRecords.Desc".Translate());
            GUI.color = Color.white;
            settings.maxDialogueRecords = (int)listing.Slider(settings.maxDialogueRecords, 5f, 60f);

            listing.Label("RimMind.Storyteller.UI.MaxPlayerReactions".Translate(settings.maxPlayerReactions));
            GUI.color = Color.gray;
            listing.Label("  " + "RimMind.Storyteller.UI.MaxPlayerReactions.Desc".Translate());
            GUI.color = Color.white;
            settings.maxPlayerReactions = (int)listing.Slider(settings.maxPlayerReactions, 5f, 50f);

            listing.Label("RimMind.Storyteller.UI.ChainExpireDays".Translate($"{settings.chainExpireDays:F1}"));
            GUI.color = Color.gray;
            listing.Label("  " + "RimMind.Storyteller.UI.ChainExpireDays.Desc".Translate());
            GUI.color = Color.white;
            settings.chainExpireDays = listing.Slider(settings.chainExpireDays, 3f, 30f);
            settings.chainExpireDays = (float)System.Math.Round(settings.chainExpireDays * 2f) / 2f;

            listing.Label("RimMind.Storyteller.UI.TensionDecayPerDay".Translate($"{settings.tensionDecayPerDay:F3}"));
            GUI.color = Color.gray;
            listing.Label("  " + "RimMind.Storyteller.UI.TensionDecayPerDay.Desc".Translate());
            GUI.color = Color.white;
            settings.tensionDecayPerDay = listing.Slider(settings.tensionDecayPerDay, 0.01f, 0.10f);
            settings.tensionDecayPerDay = (float)System.Math.Round(settings.tensionDecayPerDay * 200f) / 200f;

            SettingsUIHelper.DrawSectionHeader(listing, "RimMind.Storyteller.UI.MemoryTitle".Translate());
            var memory = StorytellerMemory.Instance;
            if (memory == null)
            {
                listing.Label("RimMind.Storyteller.UI.MemoryNeedLoad".Translate());
            }
            else
            {
                listing.Label("RimMind.Storyteller.UI.MemoryRecordCount".Translate(memory.Records.Count, settings.maxEventRecords));
                listing.Label("RimMind.Storyteller.UI.DialogueRecordCount".Translate(memory.DialogueRecords.Count));
                int now = Find.TickManager.TicksGame;
                for (int i = memory.Records.Count - 1; i >= 0 && i >= memory.Records.Count - 10; i--)
                {
                    var r = memory.Records[i];
                    int day = r.TriggeredTick / 60000 + 1;
                    listing.Label("RimMind.Storyteller.UI.MemoryDayEntry".Translate(day, r.Label, r.MapName));
                }
                if (Widgets.ButtonText(listing.GetRect(30f), "RimMind.Storyteller.UI.ClearMemory".Translate()))
                {
                    memory.ClearRecords();
                    memory.ClearDialogueRecords();
                }
            }

            listing.End();
            Widgets.EndScrollView();

            SettingsUIHelper.DrawBottomBar(bottomBar, () =>
            {
                settings.enableIntervalTrigger = true;
                settings.fallbackMode = FallbackMode.Cassandra;
                settings.mtbDays = 1.5f;
                settings.debugLogging = false;
                settings.requestExpireTicks = 30000;
                settings.maxEventRecords = 50;
                settings.maxDialogueRecords = 30;
                settings.enableEventNotification = true;
                settings.maxPlayerReactions = 20;
                settings.chainExpireDays = 10.0f;
                settings.tensionDecayPerDay = 0.03f;
                _promptText = string.Empty;
                SavePrompt();
            });

            settings.Write();
        }

        private static void LoadPromptOnce()
        {
            if (_promptLoaded) return;
            _promptLoaded = true;
            var memory = StorytellerMemory.Instance;
            if (memory != null)
                _promptText = memory.CustomSystemPrompt ?? string.Empty;
        }

        private static void SavePrompt()
        {
            var memory = StorytellerMemory.Instance;
            if (memory != null)
                memory.CustomSystemPrompt = _promptText;
        }

        private static float EstimateHeight()
        {
            float h = 30f;
            h += 24f + 24f;
            if (RimMindStorytellerMod.Settings.enableIntervalTrigger)
                h += 24f;
            h += 24f + 24f + 32f;
            h += 24f + 24f + 30f;
            h += 24f + 80f;
            h += 24f + 24f + 32f + 24f + 32f;
            h += 24f + 24f;
            h += 24f;

            h += 24f + 24f + 32f + 24f + 32f;
            h += 24f + 24f + 32f + 24f + 32f + 24f + 32f;

            var memory = StorytellerMemory.Instance;
            if (memory != null)
            {
                int shown = System.Math.Min(memory.Records.Count, 10);
                h += 24f + shown * 24f + 30f;
            }
            else
            {
                h += 24f;
            }

            return h + 40f;
        }
    }
}
