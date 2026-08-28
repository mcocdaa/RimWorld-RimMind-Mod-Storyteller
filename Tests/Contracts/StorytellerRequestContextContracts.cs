using System;
using System.Collections;
using System.Collections.Generic;
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
                ("an absent memory mod is a silent no op", MissingMemoryModIsANoOp),
                ("the reflection bridge writes and reads narrator memory", MemoryBridgeRoundTripsNarrations),
                ("all context providers are storyteller scenario scoped", ContextProvidersRemainScenarioScoped),
                ("requests and agent control use public Core APIs", RequestArchitectureRemainsCoreOnly),
                ("request lifecycle has progressive disclosure boundaries", RequestLifecycleHasProgressiveDisclosureBoundaries));
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

        private static void MissingMemoryModIsANoOp()
        {
            ResetBridge();
            Assert.False(StorytellerMemoryBridge.IsMemoryModLoaded);
            Assert.False(StorytellerMemoryBridge.TryPushNarratorEntry("event", 60000, 0.8f));
            Assert.Equal(string.Empty, StorytellerMemoryBridge.GetRecentNarrations(5));
        }

        private static void MemoryBridgeRoundTripsNarrations()
        {
            RimMind.Memory.Data.RimMindMemoryWorldComponent.Instance.Reset();
            RimMind.Memory.RimMindMemoryMod.Settings.enableMemory = true;
            StorytellerMemoryBridge.AssemblyResolver =
                () => typeof(RimMind.Memory.Data.RimMindMemoryWorldComponent).Assembly;
            StorytellerMemoryBridge.Translate = key =>
                key == "RimMind.Storyteller.Prompt.RecentIncidents" ? "Recent incidents" : key;
            StorytellerMemoryBridge.Warn = message => throw new Xunit.Sdk.XunitException(message);

            try
            {
                Assert.True(StorytellerMemoryBridge.TryPushNarratorEntry("A difficult raid", 60000, 0.9f));
                string recent = StorytellerMemoryBridge.GetRecentNarrations(5);
                Assert.Contains("Recent incidents", recent);
                Assert.Contains("[Day 2] A difficult raid", recent);
            }
            finally
            {
                ResetBridge();
                RimMind.Memory.Data.RimMindMemoryWorldComponent.Instance.Reset();
            }
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

        private static void RequestLifecycleHasProgressiveDisclosureBoundaries()
        {
            string sourceDirectory = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "Source", "Storyteller"));
            string director = File.ReadAllText(Path.Combine(
                sourceDirectory,
                "StorytellerComp_RimMindDirector.cs"));

            Assert.True(File.Exists(Path.Combine(
                sourceDirectory,
                "StorytellerRequestCoordinator.cs")));
            Assert.True(File.Exists(Path.Combine(
                sourceDirectory,
                "StorytellerNotificationService.cs")));
            Assert.DoesNotContain("LlmRequestEnvelopeBuilder", director);
            Assert.DoesNotContain("OnAIResponseReceived", director);
            Assert.DoesNotContain("RegisterPendingRequest", director);
        }

        private static void ResetBridge()
        {
            StorytellerMemoryBridge.AssemblyResolver = null;
            StorytellerMemoryBridge.Warn = null;
            StorytellerMemoryBridge.Translate = null;
        }

    }
}

namespace RimMind.Memory.Data
{
    public enum MemoryType
    {
        Work,
        Event,
        Manual,
        Dark
    }

    public sealed class MemoryEntry
    {
        public string content = string.Empty;
        public int tick;
        public float importance;

        public static MemoryEntry Create(
            string content,
            MemoryType type,
            int tick,
            float importance,
            string? pawnId)
        {
            return new MemoryEntry { content = content, tick = tick, importance = importance };
        }
    }

    public sealed class NarratorMemoryStore
    {
        public List<MemoryEntry> Entries { get; } = new List<MemoryEntry>();

        public void AddActive(MemoryEntry entry, int maxActive, int maxArchive)
        {
            Entries.Insert(0, entry);
            if (Entries.Count > maxActive)
                Entries.RemoveAt(Entries.Count - 1);
        }
    }

    public sealed class RimMindMemoryWorldComponent
    {
        public static RimMindMemoryWorldComponent Instance { get; } = new RimMindMemoryWorldComponent();

        public NarratorMemoryStore NarratorStore { get; } = new NarratorMemoryStore();

        public IList GetNarratorMemories() => NarratorStore.Entries;

        public void Reset() => NarratorStore.Entries.Clear();
    }
}

namespace RimMind.Memory.Settings
{
    public sealed class RimMindMemorySettings
    {
        public bool enableMemory = true;
        public int narratorMaxActive = 30;
        public int narratorMaxArchive = 10;
    }
}

namespace RimMind.Memory
{
    public static class RimMindMemoryMod
    {
        public static Settings.RimMindMemorySettings Settings = new Settings.RimMindMemorySettings();
    }
}
