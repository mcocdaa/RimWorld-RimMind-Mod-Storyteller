using Newtonsoft.Json;
using RimMind.Storyteller;
using Xunit;

namespace RimMind.Storyteller.Tests
{
    public class IncidentResponseTests
    {
        [Fact]
        public void Deserialize_MinimalResponse()
        {
            string json = "{\"defName\":\"RaidEnemy\",\"reason\":\"tension\"}";
            var result = JsonConvert.DeserializeObject<IncidentResponse>(json);

            Assert.NotNull(result);
            Assert.Equal("RaidEnemy", result!.defName);
            Assert.Equal("tension", result.reason);
            Assert.Null(result.announce);
            Assert.Null(result.@params);
            Assert.Null(result.chain);
        }

        [Fact]
        public void Deserialize_WithParams()
        {
            string json = @"{
                ""defName"": ""RaidEnemy"",
                ""reason"": ""hostile"",
                ""params"": {
                    ""points_multiplier"": 1.5,
                    ""faction_hint"": ""OutlanderCivil"",
                    ""raid_strategy_hint"": ""ImmediateAttack""
                }
            }";
            var result = JsonConvert.DeserializeObject<IncidentResponse>(json);

            Assert.NotNull(result);
            Assert.NotNull(result!.@params);
            Assert.Equal(1.5f, result.@params!.points_multiplier);
            Assert.Equal("OutlanderCivil", result.@params.faction_hint);
            Assert.Equal("ImmediateAttack", result.@params.raid_strategy_hint);
        }

        [Fact]
        public void Deserialize_WithChain()
        {
            string json = @"{
                ""defName"": ""RaidEnemy"",
                ""reason"": ""chain"",
                ""chain"": {
                    ""chain_id"": ""raid_siege_01"",
                    ""chain_step"": 2,
                    ""chain_total"": 4,
                    ""next_hint"": ""Infestation""
                }
            }";
            var result = JsonConvert.DeserializeObject<IncidentResponse>(json);

            Assert.NotNull(result);
            Assert.NotNull(result!.chain);
            Assert.Equal("raid_siege_01", result.chain!.chain_id);
            Assert.Equal(2, result.chain.chain_step);
            Assert.Equal(4, result.chain.chain_total);
            Assert.Equal("Infestation", result.chain.next_hint);
        }

        [Fact]
        public void Deserialize_WithAnnounce()
        {
            string json = @"{""defName"": ""Eclipse"", ""reason"": ""dramatic"", ""announce"": ""An eclipse has begun!""}";
            var result = JsonConvert.DeserializeObject<IncidentResponse>(json);

            Assert.NotNull(result);
            Assert.Equal("An eclipse has begun!", result!.announce);
        }

        [Fact]
        public void Deserialize_EmptyDefName()
        {
            string json = @"{""defName"":"""",""reason"":""test""}";
            var result = JsonConvert.DeserializeObject<IncidentResponse>(json);

            Assert.NotNull(result);
            Assert.Equal("", result!.defName);
        }

        [Fact]
        public void Deserialize_InvalidJson_ReturnsNull()
        {
            string json = "not json at all";
            Assert.Throws<JsonReaderException>(() => JsonConvert.DeserializeObject<IncidentResponse>(json));
        }

        [Fact]
        public void Deserialize_ParamsPartialFields()
        {
            string json = @"{""defName"":""ToxicFallout"",""params"":{""points_multiplier"":0.8}}";
            var result = JsonConvert.DeserializeObject<IncidentResponse>(json);

            Assert.NotNull(result);
            Assert.NotNull(result!.@params);
            Assert.Equal(0.8f, result.@params!.points_multiplier);
            Assert.Null(result.@params.faction_hint);
            Assert.Null(result.@params.raid_strategy_hint);
        }
    }
}
