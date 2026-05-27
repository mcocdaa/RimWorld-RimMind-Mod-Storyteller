using RimMind.Storyteller;
using Xunit;

namespace RimMind.Storyteller.Tests
{
    // StorytellerResponseParserPure 补充测试：JSON 修复和边界场景
    public class StorytellerResponseParserExtendedTests
    {
        [Fact]
        public void ParseResponse_NullInput_ReturnsNull()
        {
            var result = StorytellerResponseParserPure.ParseResponse(null!);
            Assert.Null(result);
        }

        [Fact]
        public void ParseResponse_EmptyDefName_ReturnsNull()
        {
            string json = @"{""defName"":"""",""reason"":""test""}";
            var result = StorytellerResponseParserPure.ParseResponse(json);
            Assert.Null(result);
        }

        [Fact]
        public void ParseResponse_MissingDefName_ReturnsNull()
        {
            string json = @"{""reason"":""no defName""}";
            var result = StorytellerResponseParserPure.ParseResponse(json);
            Assert.Null(result);
        }

        [Fact]
        public void ParseResponse_CompletelyInvalidJson_ReturnsNull()
        {
            var result = StorytellerResponseParserPure.ParseResponse("not json at all");
            Assert.Null(result);
        }

        [Fact]
        public void ParseResponse_TrailingCommaInObject_Repaired()
        {
            string json = @"{""defName"":""Eclipse"",""reason"":""dramatic"",}";
            var result = StorytellerResponseParserPure.ParseResponse(json);
            Assert.NotNull(result);
            Assert.Equal("Eclipse", result!.defName);
        }

        [Fact]
        public void ParseResponse_TrailingCommaInArray_Repaired()
        {
            // params 中的数组（如果有的话）末尾多余逗号
            string json = @"{""defName"":""RaidEnemy"",""reason"":""test"",""params"":{""points_multiplier"":1.0,}}";
            var result = StorytellerResponseParserPure.ParseResponse(json);
            Assert.NotNull(result);
            Assert.Equal("RaidEnemy", result!.defName);
        }

        [Fact]
        public void TryRepairTruncatedJson_WhitespaceOnly_ReturnsNull()
        {
            string? result = StorytellerResponseParserPure.TryRepairTruncatedJson("   \n\t  ");
            Assert.Null(result);
        }

        [Fact]
        public void TryRepairTruncatedJson_NullInput_ReturnsNull()
        {
            string? result = StorytellerResponseParserPure.TryRepairTruncatedJson(null!);
            Assert.Null(result);
        }

        [Fact]
        public void ParseResponse_WithChainAndParams_FullParse()
        {
            string json = @"{""defName"":""Infestation"",""reason"":""underground threat"",""announce"":""Bugs!"",""params"":{""points_multiplier"":1.2,""faction_hint"":""Insect""},""chain"":{""chain_id"":""bug_01"",""chain_step"":1,""chain_total"":3,""next_hint"":""RaidEnemy""}}";
            var result = StorytellerResponseParserPure.ParseResponse(json);

            Assert.NotNull(result);
            Assert.Equal("Infestation", result!.defName);
            Assert.Equal("Bugs!", result.announce);
            Assert.NotNull(result.@params);
            Assert.Equal(1.2f, result.@params!.points_multiplier);
            Assert.Equal("Insect", result.@params.faction_hint);
            Assert.NotNull(result.chain);
            Assert.Equal("bug_01", result.chain!.chain_id);
            Assert.Equal(1, result.chain.chain_step);
            Assert.Equal(3, result.chain.chain_total);
            Assert.Equal("RaidEnemy", result.chain.next_hint);
        }

        [Fact]
        public void ParseResponse_OnlyDefName_MinimalValid()
        {
            string json = @"{""defName"":""Eclipse""}";
            var result = StorytellerResponseParserPure.ParseResponse(json);
            Assert.NotNull(result);
            Assert.Equal("Eclipse", result!.defName);
            Assert.Equal(string.Empty, result.reason);
        }

        [Fact]
        public void ParseResponse_TruncatedWithParams_RepairsBrackets()
        {
            // 缺少 } } 两个闭合括号
            string truncated = @"{""defName"":""RaidEnemy"",""params"":{""points_multiplier"":1.5";
            var result = StorytellerResponseParserPure.ParseResponse(truncated);
            Assert.NotNull(result);
            Assert.Equal("RaidEnemy", result!.defName);
        }

        [Fact]
        public void ParseResponse_TruncatedString_Repairs()
        {
            // 字符串值被截断
            string truncated = @"{""defName"":""RaidEn";
            var result = StorytellerResponseParserPure.ParseResponse(truncated);
            // 截断的字符串值无法修复，应返回 null
            Assert.Null(result);
        }
    }
}
