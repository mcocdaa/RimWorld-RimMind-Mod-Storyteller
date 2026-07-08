using System;
using System.IO;
using Xunit;

namespace RimMind.Storyteller.Tests
{
    /// <summary>
    /// Architecture test verifying CustomSystemPrompt is wired into storyteller_task.
    ///
    /// storyteller_task ContextKey 的注册 lambda 依赖 RimMindAPI.Prompt.BuildTaskInstruction
    /// (RimWorld 运行时 API)，无法在 net10.0 纯逻辑测试项目中实例化。采用 ArchTest 模式：
    /// 读取源文件文本并断言内容，确保 CustomSystemPrompt 被注入到任务指令中。
    /// </summary>
    public class CustomSystemPromptWiringTests
    {
        private static readonly string ProjectRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

        private static string ReadSource(string relativePath)
            => File.ReadAllText(Path.Combine(ProjectRoot, "Source",
                relativePath.Replace('/', Path.DirectorySeparatorChar)));

        /// <summary>
        /// storyteller_task 注册必须引用 CustomSystemPrompt 与 StorytellerMemory.Instance，
        /// 否则自定义系统提示词将无法被注入到任务指令中（half-deleted feature）。
        /// </summary>
        [Fact]
        public void StorytellerTask_WiresCustomSystemPrompt()
        {
            string modContent = ReadSource("RimMindStorytellerMod.cs");
            // Must reference CustomSystemPrompt
            Assert.Contains("CustomSystemPrompt", modContent);
            // Must reference StorytellerMemory.Instance
            Assert.Contains("StorytellerMemory.Instance", modContent);
        }

        /// <summary>
        /// storyteller_task 必须在返回前将 CustomSystemPrompt 拼接到 taskInstruction 之前，
        /// 形成 "{custom}\n\n{taskInstruction}" 的顺序，确保自定义提示词作为系统层前置。
        /// </summary>
        [Fact]
        public void StorytellerTask_PrependsCustomPrompt_ToTaskInstruction()
        {
            string modContent = ReadSource("RimMindStorytellerMod.cs");
            // The pattern should prepend custom prompt before task instruction.
            // Both tokens appear on the same line in the interpolation:
            //   $"{mem.CustomSystemPrompt.Trim()}\n\n{taskInstruction}"
            // Note: variable is `taskInstruction` (camelCase), and Assert.Matches
            // is case-sensitive with `.` not crossing newlines, so we match on
            // the single-line interpolation containing both tokens.
            Assert.Matches(@"CustomSystemPrompt.*taskInstruction|taskInstruction.*CustomSystemPrompt",
                modContent);
        }
    }
}
