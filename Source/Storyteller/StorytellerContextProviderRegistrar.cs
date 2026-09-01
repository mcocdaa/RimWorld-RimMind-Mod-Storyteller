using System.Text;
using RimMind.Application.Common.Interfaces.Context;
using RimMind.Domain.ValueObjects;
using RimMind.Presentation.Api;
using RimMind.Storyteller.Extensions;
using RimMind.Storyteller.Memory;
using Verse;

namespace RimMind.Storyteller
{
    internal static class StorytellerContextProviderRegistrar
    {
        private const string ModId = "RimMind.Storyteller";

        internal static void RegisterAll()
        {
            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "storyteller_dialogue", ContextLayer.L3_State, 0.5f,
                async (ctx, ct) =>
                {
                    if (!StorytellerContextPolicy.IsApplicable(
                            ctx.Scenario,
                            RimMindAPI.Context.ScenarioStoryteller,
                            ctx.PawnId))
                    {
                        return null;
                    }
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
                    if (!StorytellerContextPolicy.IsApplicable(
                            ctx.Scenario,
                            RimMindAPI.Context.ScenarioStoryteller,
                            ctx.PawnId))
                    {
                        return null;
                    }
                    string taskInstruction = RimMindAPI.Prompt.BuildTaskInstruction(
                        "RimMind.Storyteller.Prompt.TaskInstruction",
                        null,
                        "Role", "Goal", "Process", "Constraint", "Example", "Output", "Fallback",
                        "SystemJsonFormat", "SystemTensionGuidance", "SystemChainGuidance",
                        "SystemParamsGuidance", "SystemRequirements");

                    // 将 UI 中存储的 CustomSystemPrompt 前置注入到任务指令中，
                    // 使玩家自定义的系统层提示词作为最高优先级上下文生效。
                    return StorytellerContextPolicy.ComposeTaskInstruction(
                        StorytellerMemory.Instance?.CustomSystemPrompt,
                        taskInstruction);
                }, ownerMod: ModId, stalenessTicks: 0, invalidationTriggers: new[] { "StorytellerEvent" },
                cacheScope: CacheScope.Static));

            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "storyteller_context", ContextLayer.L1_Baseline, 0.85f,
                async (ctx, ct) =>
                {
                    if (!StorytellerContextPolicy.IsApplicable(
                            ctx.Scenario,
                            RimMindAPI.Context.ScenarioStoryteller,
                            ctx.PawnId))
                    {
                        return null;
                    }
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
                    if (!StorytellerContextPolicy.IsApplicable(
                            ctx.Scenario,
                            RimMindAPI.Context.ScenarioStoryteller,
                            ctx.PawnId))
                    {
                        return null;
                    }
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
                    if (!StorytellerContextPolicy.IsApplicable(
                            ctx.Scenario,
                            RimMindAPI.Context.ScenarioStoryteller,
                            ctx.PawnId))
                    {
                        return null;
                    }
                    var pawn = PawnLookup.FindPawnById(ctx.PawnId);
                    if (pawn == null) return null;
                    var narrations = RimMindAPI.Memory.GetRecentNarrations(5);
                    if (narrations.Count == 0) return null;

                    var narrationText = new StringBuilder();
                    narrationText.AppendLine("RimMind.Storyteller.Prompt.RecentIncidents".Translate());
                    foreach (var narration in narrations)
                    {
                        int day = narration.Tick / 60000 + 1;
                        narrationText.AppendLine($"[Day {day}] {narration.Content}");
                    }
                    return narrationText.ToString().TrimEnd();
                }, ownerMod: ModId, stalenessTicks: 3000, invalidationTriggers: new[] { "StorytellerEvent" },
                cacheScope: CacheScope.Scenario));
        }

    }
}
