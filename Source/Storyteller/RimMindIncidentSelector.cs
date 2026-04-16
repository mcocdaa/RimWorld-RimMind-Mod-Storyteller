using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using RimMind.Core;
using RimMind.Core.Prompt;
using RimMind.Storyteller.Memory;
using RimMind.Storyteller.Settings;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Storyteller
{
    public static class RimMindIncidentSelector
    {
        private static readonly HashSet<string> ExcludedIncidents = new HashSet<string>
        {
            "DeepDrillInfestation",
        };

        private static readonly IncidentCategoryDef FactionArrivalCat =
            DefDatabase<IncidentCategoryDef>.GetNamedSilentFail("FactionArrival");

        private static readonly HashSet<IncidentCategoryDef> AllowedCategories = new HashSet<IncidentCategoryDef>
        {
            IncidentCategoryDefOf.ThreatBig,
            IncidentCategoryDefOf.ThreatSmall,
            IncidentCategoryDefOf.Misc,
        };

        public static string BuildSystemPrompt(StorytellerMemory memory)
        {
            return StorytellerPromptBuilder.BuildSystemPrompt(memory);
        }

        public static string BuildUserPrompt(Map map, StorytellerMemory memory, int maxCandidates)
        {
            return StorytellerPromptBuilder.BuildUserPrompt(map, memory, maxCandidates);
        }

        public static string BuildIncidentList(Map map, StorytellerMemory memory, int maxCandidates)
        {
            var sb = new StringBuilder();
            var target = (IIncidentTarget)map;
            var candidates = new List<(string text, float score)>();

            var fallbackMode = RimMindStorytellerMod.Settings?.fallbackMode ?? FallbackMode.Cassandra;

            foreach (var def in DefDatabase<IncidentDef>.AllDefsListForReading)
            {
                if (!AllowedCategories.Contains(def.category) && def.category != FactionArrivalCat) continue;
                if (ExcludedIncidents.Contains(def.defName)) continue;

                if (def.targetTags == null || !def.targetTags.Contains(IncidentTargetTagDefOf.Map_PlayerHome))
                    continue;

                var parms = StorytellerUtility.DefaultParmsNow(def.category, target);
                if (!def.Worker.CanFireNow(parms)) continue;

                if (memory.IsOnCooldown(def)) continue;

                string threatLevel = GetThreatLevel(def.category);
                string label = def.LabelCap.RawText.NullOrEmpty() ? def.defName : def.LabelCap.RawText;
                string text = "RimMind.Storyteller.Prompt.CandidateFormat".Translate(
                    def.defName, def.category.defName, threatLevel, $"{def.baseChance:F1}", label);

                float score = GetFallbackCategoryScore(def.category, fallbackMode) + def.baseChance * 0.01f;
                candidates.Add((text, score));
            }

            foreach (var c in candidates.OrderByDescending(c => c.score).Take(maxCandidates))
                sb.AppendLine(c.text);

            if (candidates.Count == 0)
                sb.AppendLine("RimMind.Storyteller.Prompt.NoCandidates".Translate());

            return sb.ToString().TrimEnd();
        }

        private static float GetFallbackCategoryScore(IncidentCategoryDef category, FallbackMode mode)
        {
            return mode switch
            {
                FallbackMode.Cassandra => category == IncidentCategoryDefOf.ThreatBig ? 3f
                    : category == IncidentCategoryDefOf.ThreatSmall ? 2f
                    : category == IncidentCategoryDefOf.Misc ? 1f
                    : 0.5f,
                FallbackMode.Randy => category == IncidentCategoryDefOf.ThreatBig ? 2f
                    : category == IncidentCategoryDefOf.ThreatSmall ? 2f
                    : category == IncidentCategoryDefOf.Misc ? 2f
                    : 1.5f,
                FallbackMode.Phoebe => category == FactionArrivalCat ? 3f
                    : category == IncidentCategoryDefOf.ThreatSmall ? 2f
                    : category == IncidentCategoryDefOf.Misc ? 1.5f
                    : 0.5f,
                _ => 1f,
            };
        }

        private static string GetThreatLevel(IncidentCategoryDef category)
        {
            if (category == IncidentCategoryDefOf.ThreatBig) return "RimMind.Storyteller.Prompt.ThreatHigh".Translate();
            if (category == IncidentCategoryDefOf.ThreatSmall) return "RimMind.Storyteller.Prompt.ThreatMedium".Translate();
            if (category == IncidentCategoryDefOf.Misc) return "RimMind.Storyteller.Prompt.ThreatLow".Translate();
            if (category == FactionArrivalCat) return "RimMind.Storyteller.Prompt.ThreatNone".Translate();
            return "RimMind.Storyteller.Prompt.ThreatNone".Translate();
        }

        public static (FiringIncident? incident, IncidentResponse? response) ParseResponse(string aiContent, IIncidentTarget target, RimWorld.StorytellerComp source)
        {
            if (string.IsNullOrEmpty(aiContent)) return (null, null);

            IncidentResponse? result;
            try
            {
                result = JsonConvert.DeserializeObject<IncidentResponse>(aiContent);
            }
            catch
            {
                return (null, null);
            }

            if (result == null || string.IsNullOrEmpty(result.defName)) return (null, null);

            var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(result.defName);
            if (incidentDef == null)
            {
                Log.Warning($"[RimMind-Storyteller] AI returned unknown defName: {result.defName}");
                return (null, result);
            }

            var parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, target);

            if (result.@params != null)
            {
                if (result.@params.points_multiplier.HasValue)
                {
                    float mult = UnityEngine.Mathf.Clamp(result.@params.points_multiplier.Value, 0.3f, 2.0f);
                    parms.points *= mult;
                }

                if (!string.IsNullOrEmpty(result.@params.faction_hint))
                {
                    var faction = Find.FactionManager.AllFactions
                        .FirstOrDefault(f => f.def.defName == result.@params.faction_hint
                                          && f.HostileTo(Faction.OfPlayer));
                    if (faction != null) parms.faction = faction;
                }

                if (!string.IsNullOrEmpty(result.@params.raid_strategy_hint))
                {
                    var strategy = DefDatabase<RaidStrategyDef>.GetNamedSilentFail(result.@params.raid_strategy_hint);
                    if (strategy != null) parms.raidStrategy = strategy;
                }
            }

            if (!incidentDef.Worker.CanFireNow(parms))
            {
                Log.Warning($"[RimMind-Storyteller] AI selected event cannot fire now: {result.defName}");
                return (null, result);
            }

            if (!string.IsNullOrEmpty(result.reason))
                Log.Message($"[RimMind-Storyteller] AI selection reason: {result.reason}");

            return (new FiringIncident(incidentDef, source, parms), result);
        }
    }

    public class IncidentResponse
    {
        public string defName = string.Empty;
        public string reason = string.Empty;
        public IncidentParams? @params;
        public ChainInfo? chain;
    }

    public class IncidentParams
    {
        public float? points_multiplier;
        public string? faction_hint;
        public string? raid_strategy_hint;
    }

    public class ChainInfo
    {
        public string chain_id = string.Empty;
        public int chain_step;
        public int chain_total;
        public string? next_hint;
    }
}
