using System;
using System.IO;
using RimMind.Storyteller.Extensions;
using RimMind.Storyteller.Memory;
using RimMind.Testing;
using Xunit;

namespace RimMind.Storyteller.Tests.Contracts
{
    public sealed class StorytellerRequestContextContracts
    {
        [Fact]
        public void Stable_request_context_and_memory_boundaries()
        {
            ContractCaseRunner.Run(
                ("tension clamps decay and day conversion stay deterministic", TensionMathRemainsDeterministic),
                ("all context providers are storyteller scenario scoped", ContextProvidersRemainScenarioScoped),
                ("requests and agent control use public Core APIs", RequestArchitectureRemainsCoreOnly),
                ("context providers stay behind the composition entry", ContextProvidersAreIsolated));
        }

        private static void TensionMathRemainsDeterministic()
        {
            Assert.Equal(0f, TensionMath.Clamp01(-0.1f));
            Assert.Equal(1f, TensionMath.Clamp01(1.1f));
            Assert.Equal(0.65f, TensionMath.ApplyDelta(0.5f, 0.15f), 3);
            Assert.Equal(0.44f, TensionMath.ComputeDecay(0.5f, 0.03f, 120000), 3);
            Assert.Equal(1, TensionMath.TicksToDay(0));
            Assert.Equal(2, TensionMath.TicksToDay(60000));
        }

        private static void ContextProvidersRemainScenarioScoped()
        {
            Assert.True(StorytellerContextPolicy.IsApplicable(
                "Storyteller", "Storyteller", pawnId: 7));
            Assert.False(StorytellerContextPolicy.IsApplicable(
                "Dialogue", "Storyteller", pawnId: 7));
            Assert.False(StorytellerContextPolicy.IsApplicable(
                "Storyteller", "Storyteller", pawnId: 0));
            Assert.True(StorytellerContextPolicy.IsApplicable(
                "Storyteller", "Storyteller", pawnId: 0, requiresPawn: false));

            Assert.Equal(
                "custom\n\ngenerated",
                StorytellerContextPolicy.ComposeTaskInstruction(" custom ", "generated"));
            Assert.Equal(
                "generated",
                StorytellerContextPolicy.ComposeTaskInstruction(" ", "generated"));
        }

        private static void RequestArchitectureRemainsCoreOnly()
        {
            var state = new StorytellerRequestState<string>();
            long token = 0;
            Assert.True(state.TryDispatch(
                captured =>
                {
                    token = captured;
                    Assert.True(state.IsCurrent(captured));
                },
                failureTick: 100,
                out var error));
            Assert.Null(error);
            Assert.False(state.TryDispatch(_ => { }, 101, out _));
            Assert.True(state.Publish(token, "incident", 120));
            Assert.False(state.HasPendingRequest);
            Assert.True(state.HasPendingResult);
            Assert.True(state.TryTake(out string? incident));
            Assert.Equal("incident", incident);
            Assert.False(state.TryTake(out _));
        }

        private static void ContextProvidersAreIsolated()
        {
            string entry = ReadStorytellerSource("RimMindStorytellerMod.cs");
            string registrar = ReadStorytellerSource("Storyteller/StorytellerContextProviderRegistrar.cs");

            Assert.Contains(
                "StorytellerContextProviderRegistrar.RegisterAll();",
                entry,
                StringComparison.Ordinal);
            Assert.DoesNotContain("ContextProviderDef", entry, StringComparison.Ordinal);
            Assert.Contains(
                "internal static class StorytellerContextProviderRegistrar",
                registrar,
                StringComparison.Ordinal);

            string[] providerIds =
            {
                "storyteller_dialogue",
                "storyteller_task",
                "storyteller_context",
                "storyteller_reactions",
                "storyteller_recent_incidents"
            };
            int previousIndex = -1;
            foreach (string providerId in providerIds)
            {
                int index = registrar.IndexOf($"\"{providerId}\"", StringComparison.Ordinal);
                Assert.True(index >= 0, $"Missing provider id: {providerId}");
                Assert.True(index > previousIndex, $"Provider id is out of order: {providerId}");
                Assert.DoesNotContain($"\"{providerId}\"", entry, StringComparison.Ordinal);
                previousIndex = index;
            }
        }

        private static string ReadStorytellerSource(string relativePath)
        {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "RimMind-Storyteller")))
                directory = directory.Parent;

            Assert.NotNull(directory);
            return File.ReadAllText(Path.Combine(
                directory!.FullName,
                "RimMind-Storyteller",
                "Source",
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

    }
}
