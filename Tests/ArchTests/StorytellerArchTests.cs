using System;
using System.IO;
using System.Linq;
using Xunit;

namespace RimMind.Storyteller.Tests.ArchTests
{
    /// <summary>
    /// Architecture guard tests enforcing the refactors from Tasks 1-8.
    /// If someone re-introduces a removed anti-pattern, these tests fail.
    ///
    /// 覆盖范围说明（避免与其他测试文件重复）：
    /// - Task 1（DecayTensionDaily 移除）：由 StorytellerMemoryDecayTests.cs 覆盖
    /// - Task 3（Memory 桥运行时行为）：由 StorytellerMemoryBridgeTests.cs 覆盖
    /// - Task 6（PawnLookup 抽取）：由 PawnLookupTests.cs 覆盖
    /// - Task 7（ContextBuilder 抽取）：由 StorytellerContextBuilderTests.cs 覆盖
    /// - Task 8（CustomSystemPrompt 接线）：由 CustomSystemPromptWiringTests.cs 覆盖
    ///
    /// 本文件仅覆盖上述文件未测试的架构不变式（GAPS）。
    /// </summary>
    public sealed class StorytellerArchTests
    {
        private static readonly string ProjectRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

        private static readonly string SourceDir = Path.Combine(ProjectRoot, "Source");

        private static string ReadSource(string relativePath)
            => File.ReadAllText(Path.Combine(SourceDir, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        // --- Task 2: JSON 修复路径统一 ---

        /// <summary>
        /// RimMindIncidentSelector 必须将 JSON 解析+修复委托给 StorytellerResponseParserPure.ParseResponse，
        /// 作为唯一的修复路径。禁止再引入 RimMindAPI.Json.TryRepairTruncatedJson 直调，
        /// 否则会出现两条修复路径导致行为不一致。
        /// </summary>
        [Fact]
        public void RimMindIncidentSelector_DelegatesTo_StorytellerResponseParserPure()
        {
            string content = ReadSource("Storyteller/RimMindIncidentSelector.cs");

            Assert.Contains("StorytellerResponseParserPure.ParseResponse", content);
            Assert.DoesNotContain("RimMindAPI.Json.TryRepairTruncatedJson", content);
        }

        // --- Task 3: Memory 反射统一（源码级委托断言） ---

        /// <summary>
        /// Window_StorytellerDialogue 必须通过 StorytellerMemoryBridge.TryPushNarratorEntry 委托记忆推送，
        /// 禁止再使用 AppDomain.CurrentDomain.GetAssemblies 的内联反射路径。
        /// 桥的运行时行为已由 StorytellerMemoryBridgeTests.cs 覆盖，此处仅断言源码级委托关系。
        /// </summary>
        [Fact]
        public void Window_StorytellerDialogue_DelegatesToBridge_NoAppDomainReflection()
        {
            string content = ReadSource("UI/Window_StorytellerDialogue.cs");

            Assert.Contains("StorytellerMemoryBridge.TryPushNarratorEntry", content);
            Assert.DoesNotContain("AppDomain.CurrentDomain.GetAssemblies", content);
        }

        // --- Task 4: 死代码监听器移除 ---

        /// <summary>
        /// StorytellerIncidentExecutedListener.cs 文件必须已被删除（Task 4 移除的死代码）。
        /// 该监听器从未被注册，且其逻辑已被其他路径取代。
        /// </summary>
        [Fact]
        public void StorytellerIncidentExecutedListener_FileDeleted()
        {
            string path = Path.Combine(SourceDir, "Extensions", "StorytellerIncidentExecutedListener.cs");

            Assert.False(File.Exists(path),
                "Dead code file StorytellerIncidentExecutedListener.cs should have been deleted in Task 4.");
        }

        /// <summary>
        /// RimMindStorytellerMod 不得再引用已删除的 StorytellerIncidentExecutedListener，
        /// 防止残留注册调用导致编译错误或运行时 NullReference。
        /// </summary>
        [Fact]
        public void RimMindStorytellerMod_DoesNotReference_StorytellerIncidentExecutedListener()
        {
            string content = ReadSource("RimMindStorytellerMod.cs");

            Assert.DoesNotContain("StorytellerIncidentExecutedListener", content);
        }

        // --- Task 5: 未使用预算变量移除 ---

        /// <summary>
        /// Source/ 下所有 .cs 文件（排除 obj/ 自动生成代码）不得再出现
        /// `float budget =` 赋值。Task 5 移除了 Director 与 Dialogue 中
        /// 未使用的 budget 变量，该变量从未被传入 envelope builder。
        /// </summary>
        [Fact]
        public void NoUnusedBudgetAssignments_InSource()
        {
            var csFiles = Directory.GetFiles(SourceDir, "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                .ToList();

            Assert.NotEmpty(csFiles);

            foreach (var file in csFiles)
            {
                string content = File.ReadAllText(file);
                Assert.DoesNotContain(
                    "float budget =",
                    content);
            }
        }
    }
}
