using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimMind.Application.Common.Interfaces.Context;
using RimMind.Application.Common.Interfaces.Extension;
using RimMind.Domain.ValueObjects;
using RimMind.Presentation.Api;
using RimMind.Presentation.Settings;
using RimMind.Application.Common.Models.UI;
using RimMind.Storyteller.Extensions;
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

            RegisterProviders();

            WireMemoryBridge();

            Log.Message("[RimMind-Storyteller] Initialized.");
        }

        /// <summary>
        /// 注入 StorytellerMemoryBridge 所需的 RimWorld 运行时依赖：
        /// 程序集解析 (LoadedModManager.RunningMods)、警告日志 (RimMindErrors.Warn)、
        /// 翻译查找 (key.Translate())。桥接本身为纯反射逻辑，不直接依赖这些运行时。
        /// </summary>
        private static void WireMemoryBridge()
        {
            StorytellerMemoryBridge.AssemblyResolver = () =>
            {
                var memoryMod = LoadedModManager.RunningMods
                    .FirstOrDefault(m => m.Name == "RimMind Memory" || m.Name.Contains("RimMind.Memory"));
                return memoryMod?.assemblies?.loadedAssemblies
                    ?.FirstOrDefault(a => a.GetName().Name == "RimMindMemory");
            };
            StorytellerMemoryBridge.Warn = msg => RimMindErrors.Warn(msg);
            StorytellerMemoryBridge.Translate = key => key.Translate();
        }

        private void RegisterProviders()
        {
            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "storyteller_dialogue", ContextLayer.L3_State, 0.5f,
                async (ctx, ct) =>
                {
                    if (ctx.Scenario != RimMindAPI.Context.ScenarioStoryteller) return null;
                    var pawn = PawnLookup.FindPawnById(ctx.PawnId);
                    if (pawn == null) return null;
                    var mem = StorytellerMemory.Instance;
                    if (mem == null) return null;
                    string dialogue = mem.GetRecentDialogueSummary(5);
                    return string.IsNullOrEmpty(dialogue)
                        ? null
                        : $"{"RimMind.Storyteller.Dialogue.StorytellerDialogueHeader".Translate()}\n{dialogue}";
                }, ownerMod: ModId, stalenessTicks: 750, invalidationTriggers: new[] { "StorytellerEvent" },
                cacheScope: CacheScope.Scenario));

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

                    // 将 UI 中存储的 CustomSystemPrompt 前置注入到任务指令中，
                    // 使玩家自定义的系统层提示词作为最高优先级上下文生效。
                    var mem = StorytellerMemory.Instance;
                    if (mem != null && !string.IsNullOrWhiteSpace(mem.CustomSystemPrompt))
                        return $"{mem.CustomSystemPrompt.Trim()}\n\n{taskInstruction}";

                    return taskInstruction;
                }, ownerMod: ModId, stalenessTicks: 0, invalidationTriggers: new[] { "StorytellerEvent" },
                cacheScope: CacheScope.Static));

            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "storyteller_context", ContextLayer.L1_Baseline, 0.85f,
                async (ctx, ct) =>
                {
                    if (ctx.Scenario != RimMindAPI.Context.ScenarioStoryteller) return null;
                    var pawn = PawnLookup.FindPawnById(ctx.PawnId);
                    if (pawn == null) return null;
                    var mem = StorytellerMemory.Instance;
                    if (mem == null) return null;
                    var sb = new StringBuilder();
                    sb.AppendLine("RimMind.Storyteller.Prompt.StorytellerStateHeader".Translate());
                    StorytellerContextBuilder.AppendDifficultyContext(sb);
                    StorytellerContextBuilder.AppendThreatLevel(sb);
                    StorytellerContextBuilder.AppendTensionLabel(sb, mem.TensionLevel);
                    sb.AppendLine("RimMind.Storyteller.Prompt.TensionLevel".Translate(
                        $"{(int)(mem.TensionLevel * 100)}%", $"{mem.TensionLevel:F2}"));
                    string summary = mem.GetRecentSummary(5);
                    if (!string.IsNullOrEmpty(summary))
                        sb.AppendLine(summary);
                    string chains = mem.GetActiveChainsSummary();
                    if (!string.IsNullOrEmpty(chains))
                        sb.AppendLine(chains);
                    return sb.ToString().TrimEnd();
                }, ownerMod: ModId, stalenessTicks: 3000, invalidationTriggers: new[] { "StorytellerEvent" },
                cacheScope: CacheScope.Scenario));

            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "storyteller_reactions", ContextLayer.L1_Baseline, 0.8f,
                async (ctx, ct) =>
                {
                    if (ctx.Scenario != RimMindAPI.Context.ScenarioStoryteller) return null;
                    var pawn = PawnLookup.FindPawnById(ctx.PawnId);
                    if (pawn == null) return null;
                    var mem = StorytellerMemory.Instance;
                    if (mem == null) return null;
                    string? text = mem.ConsumedReactionsText;
                    return !string.IsNullOrEmpty(text) ? text : null;
                }, ownerMod: ModId, stalenessTicks: 3000, invalidationTriggers: new[] { "StorytellerEvent" },
                cacheScope: CacheScope.Scenario));

            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "storyteller_recent_incidents", ContextLayer.L4_History, 0.7f,
                async (ctx, ct) =>
                {
                    if (ctx.Scenario != RimMindAPI.Context.ScenarioStoryteller) return null;
                    var pawn = PawnLookup.FindPawnById(ctx.PawnId);
                    if (pawn == null) return null;
                    string narrations = GetRecentNarrationsFromMemory(5);
                    return string.IsNullOrEmpty(narrations) ? null : narrations;
                }, ownerMod: ModId, stalenessTicks: 3000, invalidationTriggers: new[] { "StorytellerEvent" },
                cacheScope: CacheScope.Scenario));
        }

        private static string GetRecentNarrationsFromMemory(int count)
        {
            return StorytellerMemoryBridge.GetRecentNarrations(count);
        }

        public override string SettingsCategory() => "RimMind - Storyteller";

        public override void DoSettingsWindowContents(Rect rect)
        {
            StorytellerSettingsTab.Draw(rect);
        }
    }
}
