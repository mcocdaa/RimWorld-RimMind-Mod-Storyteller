using Newtonsoft.Json;
using RimMind.Storyteller;
using Xunit;

namespace RimMind.Storyteller.Tests
{
    // IncidentResponse / IncidentParams / ChainInfo 补充测试
    public class IncidentResponseExtendedTests
    {
        [Fact]
        public void Deserialize_ChainWithNullNextHint()
        {
            string json = @"{""defName"":""RaidEnemy"",""chain"":{""chain_id"":""c1"",""chain_step"":1,""chain_total"":2}}";
            var result = JsonConvert.DeserializeObject<IncidentResponse>(json);

            Assert.NotNull(result);
            Assert.NotNull(result!.chain);
            Assert.Null(result.chain!.next_hint);
        }

        [Fact]
        public void Deserialize_ParamsAllNull()
        {
            string json = @"{""defName"":""Eclipse"",""params"":{}}";
            var result = JsonConvert.DeserializeObject<IncidentResponse>(json);

            Assert.NotNull(result);
            Assert.NotNull(result!.@params);
            Assert.Null(result.@params!.points_multiplier);
            Assert.Null(result.@params.faction_hint);
            Assert.Null(result.@params.raid_strategy_hint);
        }

        [Fact]
        public void Deserialize_MultipleFieldsTogether()
        {
            string json = @"{""defName"":""ToxicFallout"",""reason"":""environment"",""announce"":""Toxic fallout!"",""params"":{""points_multiplier"":0.5},""chain"":{""chain_id"":""tox_01"",""chain_step"":3,""chain_total"":5,""next_hint"":""PsychicDrone""}}";
            var result = JsonConvert.DeserializeObject<IncidentResponse>(json);

            Assert.NotNull(result);
            Assert.Equal("ToxicFallout", result!.defName);
            Assert.Equal("environment", result.reason);
            Assert.Equal("Toxic fallout!", result.announce);
            Assert.Equal(0.5f, result.@params!.points_multiplier);
            Assert.Equal("tox_01", result.chain!.chain_id);
            Assert.Equal(3, result.chain.chain_step);
            Assert.Equal(5, result.chain.chain_total);
            Assert.Equal("PsychicDrone", result.chain.next_hint);
        }

        [Fact]
        public void IncidentParams_DefaultValues()
        {
            var parms = new IncidentParams();
            Assert.Null(parms.points_multiplier);
            Assert.Null(parms.faction_hint);
            Assert.Null(parms.raid_strategy_hint);
        }

        [Fact]
        public void ChainInfo_DefaultValues()
        {
            var chain = new ChainInfo();
            Assert.Equal(string.Empty, chain.chain_id);
            Assert.Equal(0, chain.chain_step);
            Assert.Equal(0, chain.chain_total);
            Assert.Null(chain.next_hint);
        }

        [Fact]
        public void Deserialize_ExtraFields_Ignored()
        {
            // JSON 中包含未知字段，应忽略
            string json = @"{""defName"":""Eclipse"",""unknown_field"":42}";
            var result = JsonConvert.DeserializeObject<IncidentResponse>(json);

            Assert.NotNull(result);
            Assert.Equal("Eclipse", result!.defName);
        }

        [Fact]
        public void Deserialize_NegativePointsMultiplier()
        {
            string json = @"{""defName"":""RaidEnemy"",""params"":{""points_multiplier"":-0.5}}";
            var result = JsonConvert.DeserializeObject<IncidentResponse>(json);

            Assert.NotNull(result);
            Assert.Equal(-0.5f, result!.@params!.points_multiplier);
        }

        [Fact]
        public void Deserialize_ZeroChainStep()
        {
            string json = @"{""defName"":""RaidEnemy"",""chain"":{""chain_id"":""c1"",""chain_step"":0,""chain_total"":1}}";
            var result = JsonConvert.DeserializeObject<IncidentResponse>(json);

            Assert.NotNull(result);
            Assert.Equal(0, result!.chain!.chain_step);
        }
    }
}
