using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimMind.Application.Common.Interfaces.Context;
using RimMind.Application.Common.Interfaces.Extension;
using RimMind.Domain.ValueObjects;
using RimMind.Presentation;
using RimMind.Presentation.Settings;
using RimMind.Application.Common.Models.UI;
using RimMind.Storyteller.Memory;
using RimMind.Storyteller.Settings;
using RimWorld;
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

            RimMindAPI.Extensions<ISettingsTab>().Register(new StorytellerSettingsTabAdapter());
            RimMindAPI.Extensions<IModCooldown>().Register(new StorytellerModCooldown(Settings));
            RimMindAPI.Extensions<ISkipCheck>().Register(new StorytellerIncidentSkipCheck(Settings));
            RimMindAPI.Extensions<IIncidentExecutedListener>().Register(new StorytellerIncidentExecutedListener());

            RegisterProviders();

            Log.Message("[RimMind-Storyteller] Initialized.");
        }

        private void RegisterProviders()
        {
            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "storyteller_dialogue", ContextLayer.L3_State, 0.5f,
                async (ctx, ct) =>
                {
                    if (ctx.PawnId <= 0) return null;
                    if (ctx.Scenario != RimMindAPI.Context.ScenarioStoryteller) return null;
                    var pawn = Find.WorldPawns.AllPawnsAlive.FirstOrDefault(p => p.thingIDNumber == ctx.PawnId)
                        ?? Find.CurrentMap?.mapPawns?.FreeColonists.FirstOrDefault(p => p.thingIDNumber == ctx.PawnId);
                    if (pawn == null) return null;
                    var mem = StorytellerMemory.Instance;
                    if (mem == null) return null;
                    string dialogue = mem.GetRecentDialogueSummary(5);
                    return string.IsNullOrEmpty(dialogue)
                        ? null
                        : $"{"RimMind.Storyteller.Dialogue.StorytellerDialogueHeader".Translate()}\n{dialogue}";
                }, "RimMind.Storyteller", stalenessTicks: 750, invalidationTriggers: new[] { "StorytellerEvent" }));

            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "storyteller_task", ContextLayer.L0_Static, 0.95f,
                async (ctx, ct) =>
                {
                    if (ctx.PawnId <= 0) return null;
                    if (ctx.Scenario != RimMindAPI.Context.ScenarioStoryteller) return null;
                    string taskInstruction = RimMindAPI.Prompt.BuildTaskInstruction(
                        "RimMind.Storyteller.Prompt.TaskInstruction",
                        null,
                        "Role", "Goal", "Process", "Constraint", "Example", "Output", "Fallback",
                        "SystemJsonFormat", "SystemTensionGuidance", "SystemChainGuidance",
                        "SystemParamsGuidance", "SystemRequirements");
                    return taskInstruction;
                }, "RimMind.Storyteller", stalenessTicks: 0, invalidationTriggers: new[] { "StorytellerEvent" }));

            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "storyteller_context", ContextLayer.L1_Baseline, 0.85f,
                async (ctx, ct) =>
                {
                    if (ctx.PawnId <= 0) return null;
                    if (ctx.Scenario != RimMindAPI.Context.ScenarioStoryteller) return null;
                    var pawn = Find.WorldPawns.AllPawnsAlive.FirstOrDefault(p => p.thingIDNumber == ctx.PawnId)
                        ?? Find.CurrentMap?.mapPawns?.FreeColonists.FirstOrDefault(p => p.thingIDNumber == ctx.PawnId);
                    if (pawn == null) return null;
                    var mem = StorytellerMemory.Instance;
                    if (mem == null) return null;
                    var sb = new StringBuilder();
                    sb.AppendLine("RimMind.Storyteller.Prompt.StorytellerStateHeader".Translate());
                    AppendDifficultyContext(sb);
                    AppendThreatLevel(sb);
                    AppendTensionLabel(sb, mem.TensionLevel);
                    sb.AppendLine("RimMind.Storyteller.Prompt.TensionLevel".Translate(
                        $"{(int)(mem.TensionLevel * 100)}%", $"{mem.TensionLevel:F2}"));
                    string summary = mem.GetRecentSummary(5);
                    if (!string.IsNullOrEmpty(summary))
                        sb.AppendLine(summary);
                    string chains = mem.GetActiveChainsSummary();
                    if (!string.IsNullOrEmpty(chains))
                        sb.AppendLine(chains);
                    return sb.ToString().TrimEnd();
                }, "RimMind.Storyteller", stalenessTicks: 3000, invalidationTriggers: new[] { "StorytellerEvent" }));

            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "storyteller_reactions", ContextLayer.L1_Baseline, 0.8f,
                async (ctx, ct) =>
                {
                    if (ctx.PawnId <= 0) return null;
                    if (ctx.Scenario != RimMindAPI.Context.ScenarioStoryteller) return null;
                    var pawn = Find.WorldPawns.AllPawnsAlive.FirstOrDefault(p => p.thingIDNumber == ctx.PawnId)
                        ?? Find.CurrentMap?.mapPawns?.FreeColonists.FirstOrDefault(p => p.thingIDNumber == ctx.PawnId);
                    if (pawn == null) return null;
                    var mem = StorytellerMemory.Instance;
                    if (mem == null) return null;
                    string? text = mem.ConsumedReactionsText;
                    return !string.IsNullOrEmpty(text) ? text : null;
                }, "RimMind.Storyteller", stalenessTicks: 3000, invalidationTriggers: new[] { "StorytellerEvent" }));

            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "storyteller_recent_incidents", ContextLayer.L4_History, 0.7f,
                async (ctx, ct) =>
                {
                    if (ctx.PawnId <= 0) return null;
                    if (ctx.Scenario != RimMindAPI.Context.ScenarioStoryteller) return null;
                    var pawn = Find.WorldPawns.AllPawnsAlive.FirstOrDefault(p => p.thingIDNumber == ctx.PawnId)
                        ?? Find.CurrentMap?.mapPawns?.FreeColonists.FirstOrDefault(p => p.thingIDNumber == ctx.PawnId);
                    if (pawn == null) return null;
                    string narrations = GetRecentNarrationsFromMemory(5);
                    return string.IsNullOrEmpty(narrations) ? null : narrations;
                }, "RimMind.Storyteller", stalenessTicks: 3000, invalidationTriggers: new[] { "StorytellerEvent" }));
        }

        private static string GetRecentNarrationsFromMemory(int count)
        {
            try
            {
                var memoryMod = LoadedModManager.RunningMods
                    .FirstOrDefault(m => m.Name == "RimMind Memory" || m.Name.Contains("RimMind.Memory"));
                if (memoryMod == null) return string.Empty;

                var asm = memoryMod.assemblies?.loadedAssemblies
                    ?.FirstOrDefault(a => a.GetName().Name == "RimMindMemory");
                if (asm == null) return string.Empty;

                var storeType = asm.GetType("RimMind.Memory.Data.NarratorMemoryStore");
                if (storeType == null) return string.Empty;

                var instanceProp = storeType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                if (instanceProp == null) return string.Empty;

                var instance = instanceProp.GetValue(null);
                if (instance == null) return string.Empty;

                var getRecentMethod = storeType.GetMethod("GetRecentNarrations",
                    BindingFlags.Public | BindingFlags.Instance);
                if (getRecentMethod == null) return string.Empty;

                var narrations = getRecentMethod.Invoke(instance, new object[] { count }) as System.Collections.IList;
                if (narrations == null || narrations.Count == 0) return string.Empty;

                var sb = new StringBuilder();
                sb.AppendLine("RimMind.Storyteller.Prompt.RecentIncidents".Translate());
                var narType = narrations[0].GetType();
                var contentProp = narType.GetProperty("Content");
                var tickProp = narType.GetProperty("Tick");
                var tickField = narType.GetField("Tick");
                for (int i = 0; i < narrations.Count; i++)
                {
                    var n = narrations[i];
                    string content = (contentProp?.GetValue(n) as string) ?? string.Empty;
                    int tick = (tickProp?.GetValue(n) as int?) ?? (tickField?.GetValue(n) as int?) ?? 0;
                    int day = tick / 60000 + 1;
                    sb.AppendLine($"[Day {day}] {content}");
                }
                return sb.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                RimMindErrors.Warn($"[RimMind-Storyteller] Failed to read NarratorStore via reflection: {ex.Message}");
                return string.Empty;
            }
        }

        private static void AppendDifficultyContext(StringBuilder sb)
        {
            var diff = Find.Storyteller?.difficulty;
            if (diff == null) return;
            int tier = ResolveDifficultyTier(diff.threatScale);
            string difficultyName = ResolveDifficultyName(tier);
            sb.AppendLine("RimMind.Storyteller.Prompt.DifficultyContext".Translate());
            sb.AppendLine("RimMind.Storyteller.Prompt.DifficultyDetail".Translate(
                difficultyName, $"{diff.threatScale:F2}"));
            if (!diff.allowBigThreats)
                sb.AppendLine("RimMind.Storyteller.Prompt.NoBigThreats".Translate());
            if (!diff.allowIntroThreats)
                sb.AppendLine("RimMind.Storyteller.Prompt.NoIntroThreats".Translate());
            if (!diff.allowViolentQuests)
                sb.AppendLine("RimMind.Storyteller.Prompt.NoViolentQuests".Translate());
            string? guidanceKey = ResolveDifficultyGuidanceKey(tier);
            if (guidanceKey != null)
                sb.AppendLine("RimMind.Storyteller.Prompt.DifficultyGuidance".Translate(guidanceKey.Translate()));
        }

        private static void AppendThreatLevel(StringBuilder sb)
        {
            var diff = Find.Storyteller?.difficulty;
            if (diff == null) return;
            string threatLabel = diff.threatScale switch
            {
                < 0.1f => "RimMind.Storyteller.Prompt.ThreatNone".Translate(),
                < 0.5f => "RimMind.Storyteller.Prompt.ThreatLow".Translate(),
                < 0.8f => "RimMind.Storyteller.Prompt.ThreatMedium".Translate(),
                _ => "RimMind.Storyteller.Prompt.ThreatHigh".Translate()
            };
            sb.AppendLine($"[Threat Level] {threatLabel}");
        }

        private static void AppendTensionLabel(StringBuilder sb, float tension)
        {
            string tensionLabel = tension switch
            {
                < 0.2f => "RimMind.Storyteller.Prompt.TensionVeryLow".Translate(),
                < 0.4f => "RimMind.Storyteller.Prompt.TensionLow".Translate(),
                < 0.6f => "RimMind.Storyteller.Prompt.TensionMedium".Translate(),
                < 0.8f => "RimMind.Storyteller.Prompt.TensionHigh".Translate(),
                _ => "RimMind.Storyteller.Prompt.TensionVeryHigh".Translate()
            };
            sb.AppendLine($"[Tension Category] {tensionLabel}");
        }

        private static int ResolveDifficultyTier(float threatScale)
        {
            return threatScale switch
            {
                < 0.1f => 0,
                < 0.5f => 1,
                < 0.8f => 2,
                < 1.2f => 3,
                < 1.8f => 4,
                _ => 5
            };
        }

        private static string? ResolveDifficultyGuidanceKey(int difficultyLevel)
        {
            return difficultyLevel switch
            {
                0 => "RimMind.Storyteller.Prompt.DifficultyPeaceful",
                1 => "RimMind.Storyteller.Prompt.DifficultyEasy",
                2 => "RimMind.Storyteller.Prompt.DifficultyMedium",
                3 => "RimMind.Storyteller.Prompt.DifficultyRough",
                4 => "RimMind.Storyteller.Prompt.DifficultyHard",
                5 => "RimMind.Storyteller.Prompt.DifficultyExtreme",
                _ => null
            };
        }

        private static string ResolveDifficultyName(int difficultyLevel)
        {
            return difficultyLevel switch
            {
                0 => "Peaceful",
                1 => "Community Builder",
                2 => "Adventure Story",
                3 => "Strive to Survive",
                4 => "Blood and Dust",
                5 => "Losing is Fun",
                _ => $"Custom ({difficultyLevel})"
            };
        }

        public override string SettingsCategory() => "RimMind - Storyteller";

        public override void DoSettingsWindowContents(Rect rect)
        {
            StorytellerSettingsTab.Draw(rect);
        }
    }
}
