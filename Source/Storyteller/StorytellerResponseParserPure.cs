using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RimMind.Storyteller
{
    /// <summary>
    /// 纯逻辑版本的叙事者响应解析器，不依赖 RimMindAPI 或 RimWorld 运行时。
    /// 仅负责 JSON 修复与反序列化，不涉及 DefDatabase/FiringIncident 等游戏逻辑。
    /// 供测试项目直接编译使用。
    /// </summary>
    public static class StorytellerResponseParserPure
    {
        private static readonly Regex TrailingCommaRegex = new Regex(
            @",\s*([}\]])",
            RegexOptions.Compiled);

        /// <summary>
        /// 解析AI响应内容，尝试反序列化为 IncidentResponse。
        /// 失败时尝试修复截断JSON后重试。若 defName 为空则返回 null。
        /// </summary>
        public static IncidentResponse? ParseResponse(string aiContent)
        {
            if (string.IsNullOrEmpty(aiContent)) return null;

            IncidentResponse? result;
            try
            {
                result = JsonConvert.DeserializeObject<IncidentResponse>(aiContent);
            }
            catch
            {
                string? repaired = TryRepairTruncatedJson(aiContent);
                if (repaired != null)
                {
                    try { result = JsonConvert.DeserializeObject<IncidentResponse>(repaired); }
                    catch { result = null; }
                }
                else
                {
                    result = null;
                }
            }

            if (result == null || string.IsNullOrEmpty(result.defName)) return null;
            return result;
        }

        /// <summary>
        /// 尝试修复截断的JSON。若输入已是合法JSON则返回null（无需修复），
        /// 若输入为空白或不可修复则返回null。
        /// </summary>
        public static string? TryRepairTruncatedJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            try
            {
                JToken.Parse(input);
                return null; // 已是合法JSON，无需修复
            }
            catch
            {
                return Repair(input);
            }
        }

        private static string Repair(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "{}";

            var result = new StringBuilder(input);
            result = new StringBuilder(TrailingCommaRegex.Replace(result.ToString(), "$1"));
            var balanced = BalanceBrackets(result.ToString());
            return balanced;
        }

        private static string BalanceBrackets(string input)
        {
            int openCurly = 0, openSquare = 0;
            foreach (char c in input)
            {
                if (c == '{') openCurly++;
                else if (c == '}') openCurly--;
                else if (c == '[') openSquare++;
                else if (c == ']') openSquare--;
            }

            var sb = new StringBuilder(input);
            while (openSquare > 0) { sb.Append(']'); openSquare--; }
            while (openCurly > 0) { sb.Append('}'); openCurly--; }
            return sb.ToString();
        }
    }
}
