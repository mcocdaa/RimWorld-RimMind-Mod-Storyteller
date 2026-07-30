using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.Storyteller.Extensions;
using RimMind.Storyteller.Memory;
using RimMind.Testing;
using Xunit;

namespace RimMind.Storyteller.Tests.Contracts
{
    public sealed class StorytellerIncidentPolicyContracts
    {
        [Fact]
        public void Stable_incident_policy_boundaries()
        {
            ContractCaseRunner.Run(
                ("valid incident responses retain parameters and chain metadata", ValidResponsesRetainMetadata),
                ("truncated and trailing comma responses are repaired", RecoverableResponsesAreRepaired),
                ("empty malformed and missing def names become no incident", InvalidResponsesBecomeNoIncident),
                ("unknown and unfireable incidents remain no ops", UnknownAndUnfireableIncidentsRemainNoOps),
                ("pawn lookup rejects invalid ids before world then map lookup", PawnLookupOrderRemainsStable),
                ("only threat categories notify the player", NotificationPolicyRemainsNarrow));
        }

        private static void ValidResponsesRetainMetadata()
        {
            const string json =
                "{\"defName\":\"RaidEnemy\",\"reason\":\"pressure\"," +
                "\"params\":{\"points_multiplier\":1.25,\"faction_hint\":\"Pirate\",\"raid_strategy_hint\":\"ImmediateAttack\"}," +
                "\"chain\":{\"chain_id\":\"arc-7\",\"chain_step\":2,\"chain_total\":4,\"next_hint\":\"reprisal\"}}";

            IncidentResponse? response = StorytellerResponseParserPure.ParseResponse(json);

            Assert.NotNull(response);
            Assert.Equal("RaidEnemy", response!.defName);
            Assert.Equal(1.25f, response.@params!.points_multiplier);
            Assert.Equal("Pirate", response.@params.faction_hint);
            Assert.Equal("ImmediateAttack", response.@params.raid_strategy_hint);
            Assert.Equal("arc-7", response.chain!.chain_id);
            Assert.Equal(2, response.chain.chain_step);
            Assert.Equal(4, response.chain.chain_total);
            Assert.Equal("reprisal", response.chain.next_hint);
        }

        private static void RecoverableResponsesAreRepaired()
        {
            IncidentResponse? truncated = StorytellerResponseParserPure.ParseResponse(
                "{\"defName\":\"WandererJoin\",\"reason\":\"mercy\"");
            IncidentResponse? trailingComma = StorytellerResponseParserPure.ParseResponse(
                "{\"defName\":\"CargoPodCrash\",\"reason\":\"gift\",}");

            Assert.Equal("WandererJoin", truncated?.defName);
            Assert.Equal("CargoPodCrash", trailingComma?.defName);
        }

        private static void InvalidResponsesBecomeNoIncident()
        {
            Assert.Null(StorytellerResponseParserPure.ParseResponse(string.Empty));
            Assert.Null(StorytellerResponseParserPure.ParseResponse("not-json"));
            Assert.Null(StorytellerResponseParserPure.ParseResponse("{\"reason\":\"missing def\"}"));
            Assert.Null(StorytellerResponseParserPure.ParseResponse("{\"defName\":\"\"}"));
        }

        private static void UnknownAndUnfireableIncidentsRemainNoOps()
        {
            Assert.Equal(
                IncidentSelectionDisposition.InvalidResponse,
                IncidentSelectionPolicy.Evaluate(false, false, false));
            Assert.Equal(
                IncidentSelectionDisposition.UnknownDefinition,
                IncidentSelectionPolicy.Evaluate(true, false, false));
            Assert.Equal(
                IncidentSelectionDisposition.CannotFire,
                IncidentSelectionPolicy.Evaluate(true, true, false));
            Assert.Equal(
                IncidentSelectionDisposition.Selected,
                IncidentSelectionPolicy.Evaluate(true, true, true));
            Assert.Equal(0.3f, IncidentSelectionPolicy.ClampPointsMultiplier(-1f));
            Assert.Equal(2f, IncidentSelectionPolicy.ClampPointsMultiplier(8f));
        }

        private static void PawnLookupOrderRemainsStable()
        {
            int enumerations = 0;
            IEnumerable<TestPawn> Track(params TestPawn[] pawns)
            {
                enumerations++;
                foreach (TestPawn pawn in pawns)
                    yield return pawn;
            }

            Assert.Null(PawnLookupCore.FindById(
                0,
                () => Track(new TestPawn(1, "world")),
                () => Track(new TestPawn(1, "map")),
                pawn => pawn.Id));
            Assert.Equal(0, enumerations);

            var world = new[] { new TestPawn(7, "world") };
            var map = new[] { new TestPawn(7, "map"), new TestPawn(8, "fallback") };
            int mapFactoryCalls = 0;
            Assert.Equal(
                "world",
                PawnLookupCore.FindById(
                    7,
                    () => world,
                    () =>
                    {
                        mapFactoryCalls++;
                        return map;
                    },
                    pawn => pawn.Id)!.Source);
            Assert.Equal(0, mapFactoryCalls);
            Assert.Equal(
                "fallback",
                PawnLookupCore.FindById(
                    8,
                    () => world,
                    () =>
                    {
                        mapFactoryCalls++;
                        return map;
                    },
                    pawn => pawn.Id)!.Source);
            Assert.Equal(1, mapFactoryCalls);
        }

        private static void NotificationPolicyRemainsNarrow()
        {
            Assert.True(IncidentSelectionPolicy.ShouldNotify(
                true, StorytellerIncidentKind.ThreatBig));
            Assert.True(IncidentSelectionPolicy.ShouldNotify(
                true, StorytellerIncidentKind.ThreatSmall));
            Assert.False(IncidentSelectionPolicy.ShouldNotify(
                true, StorytellerIncidentKind.Other));
            Assert.False(IncidentSelectionPolicy.ShouldNotify(
                false, StorytellerIncidentKind.ThreatBig));
        }

        private sealed class TestPawn
        {
            public TestPawn(int id, string source)
            {
                Id = id;
                Source = source;
            }

            public int Id { get; }
            public string Source { get; }
        }
    }
}
