using System;
using System.IO;
using Xunit;

namespace RimMind.Storyteller.Tests.ArchTests
{
    public sealed class P7_StorytellerAgentControlTests
    {
        private static readonly string ProjectRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

        private static readonly string SourceDir = Path.Combine(ProjectRoot, "Source");

        private static string ReadSource(string relativePath)
            => File.ReadAllText(Path.Combine(SourceDir, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        [Fact]
        public void StorytellerAgentController_Uses_Core_ScopedAgent_Interface()
        {
            string content = ReadSource("Agent/StorytellerAgentController.cs");

            Assert.Contains("IScopedAgentManager", content);
            Assert.Contains("GetOrCreate(ScopeType, ScopeId", content);
            Assert.Contains("NPC-storyteller", content);
            Assert.DoesNotContain("new ScopedAgent", content);
        }

        [Fact]
        public void StorytellerAgentWindow_Provides_Start_Pause_And_Open_Debug_Center()
        {
            string content = ReadSource("UI/Window_StorytellerAgentControl.cs");

            Assert.Contains("RimMind.Storyteller.UI.Agent.Start", content);
            Assert.Contains("RimMind.Storyteller.UI.Agent.Pause", content);
            Assert.Contains("Window_RimMindHub.OpenAIRequests()", content);
        }

        [Fact]
        public void StorytellerDebugActions_Expose_Open_Control_Window()
        {
            string content = ReadSource("Debug/StorytellerDebugActions.cs");

            Assert.Contains("Open Storyteller Agent Control", content);
            Assert.Contains("Window_StorytellerAgentControl", content);
        }
    }
}
