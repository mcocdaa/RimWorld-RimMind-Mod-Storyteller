using System.Collections.Generic;
using System.Linq;
using RimMind.Application.Common.Interfaces.Client;
using RimMind.Application.Common.Models.Client;
using RimMind.Domain.ValueObjects;
using RimMind.Storyteller.Memory;
using RimWorld;
using Verse;

namespace RimMind.Storyteller
{
    public static class RimMindIncidentSelector
    {
        public static (FiringIncident? incident, IncidentResponse? response) ParseResponse(string aiContent, IIncidentTarget target, RimWorld.StorytellerComp source)
        {
            // Delegate JSON parse + repair to the pure-logic parser (single repair path).
            // StorytellerResponseParserPure handles null/empty input, repair fallback, and
            // defName validation, returning null in all those failure cases.
            var result = StorytellerResponseParserPure.ParseResponse(aiContent);
            if (result == null) return (null, null);

            var incidentDef = DefDatabase<IncidentDef>.GetNamedSilentFail(result.defName);
            if (incidentDef == null)
            {
                RimMindErrors.Warn($"[RimMind-Storyteller] AI returned unknown defName: {result.defName}");
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
                RimMindErrors.Warn($"[RimMind-Storyteller] AI selected event cannot fire now: {result.defName}");
                return (null, result);
            }

            if (!string.IsNullOrEmpty(result.reason))
                Log.Message($"[RimMind-Storyteller] AI selection reason: {result.reason}");

            return (new FiringIncident(incidentDef, source, parms), result);
        }

    }
}
