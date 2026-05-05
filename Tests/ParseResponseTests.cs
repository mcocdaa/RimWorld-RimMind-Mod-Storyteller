using Newtonsoft.Json;
using RimMind.Storyteller;
using Xunit;

namespace RimMind.Storyteller.Tests
{
    public class ParseResponseTests
    {
        [Fact]
        public void ParseResponse_ValidJson_DeserializesCorrectly()
        {
            string json = @"{""defName"":""RaidEnemy"",""reason"":""tension rising""}";
            var result = JsonConvert.DeserializeObject<IncidentResponse>(json);

            Assert.NotNull(result);
            Assert.Equal("RaidEnemy", result!.defName);
            Assert.Equal("tension rising", result.reason);
        }

        [Fact]
        public void ParseResponse_TruncatedJson_RepairsAndDeserialize()
        {
            string truncated = "{\"defName\":\"RaidEnemy\",\"reason\":\"tension\"";
            string? repaired = RimMind.Core.Client.JsonRepairHelper.TryRepairTruncatedJson(truncated);

            Assert.NotNull(repaired);
            var result = JsonConvert.DeserializeObject<IncidentResponse>(repaired!);
            Assert.NotNull(result);
            Assert.Equal("RaidEnemy", result!.defName);
        }

        [Fact]
        public void ParseResponse_EmptyString_ReturnsNull()
        {
            string? repaired = RimMind.Core.Client.JsonRepairHelper.TryRepairTruncatedJson("");
            Assert.Null(repaired);
        }

        [Fact]
        public void ParseResponse_AlreadyValidJson_ReturnsNull()
        {
            string valid = @"{""defName"":""Eclipse""}";
            string? repaired = RimMind.Core.Client.JsonRepairHelper.TryRepairTruncatedJson(valid);
            Assert.Null(repaired);
        }

        [Fact]
        public void ParseResponse_TruncatedWithNestedBraces()
        {
            string truncated = @"{""defName"":""RaidEnemy"",""params"":{""points"":1.5";
            string? repaired = RimMind.Core.Client.JsonRepairHelper.TryRepairTruncatedJson(truncated);

            Assert.NotNull(repaired);
            Assert.True(repaired!.EndsWith("}}"));
        }

        [Fact]
        public void ParseResponse_FullFields_DeserializesAll()
        {
            string json = @"{""defName"":""RaidEnemy"",""reason"":""test"",""announce"":""Attack!"",""params"":{""points_multiplier"":1.5},""chain"":{""chain_id"":""c1"",""chain_step"":1,""chain_total"":3,""next_hint"":""Infestation""}}";
            var result = JsonConvert.DeserializeObject<IncidentResponse>(json);

            Assert.NotNull(result);
            Assert.Equal("RaidEnemy", result!.defName);
            Assert.Equal("Attack!", result.announce);
            Assert.NotNull(result.@params);
            Assert.Equal(1.5f, result.@params!.points_multiplier);
            Assert.NotNull(result.chain);
            Assert.Equal("c1", result.chain!.chain_id);
            Assert.Equal(1, result.chain.chain_step);
            Assert.Equal(3, result.chain.chain_total);
            Assert.Equal("Infestation", result.chain.next_hint);
        }
    }
}
