using System.Collections.Generic;
using System.Linq;
using RimMind.Storyteller.Memory;
using RimMind.Testing;
using Verse;
using Xunit;

namespace RimMind.Storyteller.Tests.Contracts
{
    public sealed class StorytellerSaveErrorContracts
    {
        [Fact]
        public void Save_load_and_request_failure_boundaries()
        {
            ContractCaseRunner.Run(
                ("save schema includes every state needed for a round trip", SaveSchemaCoversRoundTripState),
                ("malformed saved collections are normalized after load", MalformedCollectionsAreNormalized),
                ("request failures clear in flight state without publishing an incident", RequestFailuresAreIsolated),
                ("invalid successful responses follow the same isolated failure state", InvalidResponsesAreIsolated),
                ("successful responses alone persist pending incident state", SuccessAlonePublishesPendingState));
        }

        private static void SaveSchemaCoversRoundTripState()
        {
            ScribeRecorder.Reset();
            var records = new List<object>();
            var dialogues = new List<object>();
            var reactions = new List<object>();
            var chains = new List<object>();
            string prompt = "custom";
            float tension = 0.8f;
            int lastDecay = 42;

            StorytellerPersistenceCodec.LookMemory(
                ref records,
                ref dialogues,
                ref reactions,
                ref prompt,
                ref tension,
                ref lastDecay,
                ref chains);

            Assert.Equal(
                new[]
                {
                    ("records", LookMode.Deep),
                    ("dialogueRecords", LookMode.Deep),
                    ("playerReactions", LookMode.Deep),
                    ("activeChains", LookMode.Deep),
                },
                ScribeRecorder.CollectionCalls);
            Assert.Equal(
                new (string Label, object? DefaultValue)[]
                {
                    ("customSystemPrompt", string.Empty),
                    ("tensionLevel", 0.5f),
                    ("lastTensionDecayTick", -1),
                },
                ScribeRecorder.ValueCalls);

            ScribeRecorder.Reset();
            string chainId = "arc";
            var steps = new List<object>();
            int current = 1;
            int total = 3;
            string nextHint = "next";
            int lastAdvanced = 12;
            string faction = "Pirate";
            float points = 500f;
            StorytellerPersistenceCodec.LookEventChain(
                ref chainId,
                ref steps,
                ref current,
                ref total,
                ref nextHint,
                ref lastAdvanced,
                ref faction,
                ref points);

            Assert.Equal(("steps", LookMode.Deep), Assert.Single(ScribeRecorder.CollectionCalls));
            Assert.Equal(
                new[] { "chainId", "currentStep", "totalSteps", "nextHint", "lastAdvancedTick", "lastFactionDefName", "lastPoints" },
                ScribeRecorder.ValueCalls.Select(call => call.Label));
        }

        private static void MalformedCollectionsAreNormalized()
        {
            ScribeRecorder.Reset();
            ScribeRecorder.AssignNullCollections = true;
            var records = new List<object>();
            var dialogues = new List<object>();
            var reactions = new List<object>();
            var chains = new List<object>();
            string prompt = string.Empty;
            float tension = 0.5f;
            int lastDecay = -1;

            StorytellerPersistenceCodec.LookMemory(
                ref records,
                ref dialogues,
                ref reactions,
                ref prompt,
                ref tension,
                ref lastDecay,
                ref chains);

            Assert.NotNull(records);
            Assert.NotNull(dialogues);
            Assert.NotNull(reactions);
            Assert.NotNull(chains);

            var steps = new List<object>();
            string chainId = string.Empty;
            int current = 0;
            int total = 0;
            string next = string.Empty;
            int advanced = 0;
            string faction = string.Empty;
            float points = 0f;
            StorytellerPersistenceCodec.LookEventChain(
                ref chainId,
                ref steps,
                ref current,
                ref total,
                ref next,
                ref advanced,
                ref faction,
                ref points);
            Assert.NotNull(steps);
        }

        private static void RequestFailuresAreIsolated()
        {
            var state = new StorytellerRequestState<string>();
            bool dispatched = state.TryDispatch(
                _ => throw new System.InvalidOperationException("send failed"),
                failureTick: 100,
                out var error);

            Assert.False(dispatched);
            Assert.IsType<System.InvalidOperationException>(error);
            Assert.False(state.HasPendingRequest);
            Assert.False(state.HasPendingResult);
            Assert.Equal(100, state.LastFailTick);
            Assert.False(state.TryTake(out _));
        }

        private static void InvalidResponsesAreIsolated()
        {
            var state = new StorytellerRequestState<string>();
            long token = 0;
            state.TryDispatch(captured => token = captured, 100, out _);
            Assert.True(state.Fail(token, 200));
            Assert.Equal(-99999, state.LastSuccessTick);
            Assert.Equal(200, state.LastFailTick);
            Assert.False(state.HasPendingResult);
        }

        private static void SuccessAlonePublishesPendingState()
        {
            var state = new StorytellerRequestState<string>();
            long staleToken = 0;
            state.TryDispatch(captured => staleToken = captured, 100, out _);
            state.CancelRequest();

            long currentToken = 0;
            state.TryDispatch(captured => currentToken = captured, 200, out _);
            Assert.False(state.Publish(staleToken, "OldIncident", 250));
            Assert.True(state.IsCurrent(currentToken));
            Assert.True(state.Publish(currentToken, "RaidEnemy", 300));

            Assert.False(state.HasPendingRequest);
            Assert.True(state.HasPendingResult);
            Assert.Equal(300, state.LastSuccessTick);
            Assert.True(state.TryTake(out string? incident));
            Assert.Equal("RaidEnemy", incident);
            Assert.False(state.HasPendingResult);
        }
    }
}
