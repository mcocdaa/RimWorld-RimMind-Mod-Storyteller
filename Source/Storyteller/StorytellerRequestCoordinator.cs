using System;
using System.Linq;
using RimMind.Application.Features.Llm;
using RimMind.Domain.Llm;
using RimMind.Domain.ValueObjects;
using RimMind.Presentation.Api;
using RimMind.Storyteller.Memory;
using RimWorld;
using Verse;

namespace RimMind.Storyteller
{
    internal sealed class StorytellerRequestCoordinator
    {
        private readonly RimWorld.StorytellerComp _source;
        private readonly StorytellerRequestState<FiringIncident> _requestState = new();
        private readonly StorytellerNotificationService _notificationService = new();
        private StorytellerMemory _memory = null!;

        public StorytellerRequestCoordinator(RimWorld.StorytellerComp source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public bool IsActive =>
            _requestState.HasPendingRequest || _requestState.HasPendingResult;

        public bool HasPendingRequest => _requestState.HasPendingRequest;
        public int LastSuccessTick => _requestState.LastSuccessTick;
        public int LastFailTick => _requestState.LastFailTick;

        public bool ApplyWorldMaintenance()
        {
            if (!TryResolveMemory())
                return false;

            _memory.ApplyDecayAndCleanup();
            return true;
        }

        public bool TryTakePendingIncident(out FiringIncident? incident)
            => _requestState.TryTake(out incident);

        public bool TryDispatch(IIncidentTarget target)
            => Dispatch(target, forced: false);

        public bool ForceRequest(IIncidentTarget target)
        {
            if (_requestState.HasPendingRequest)
            {
                RimMindErrors.Warn(
                    "[RimMind-Storyteller] ForceRequest: overriding existing pending request");
                _requestState.CancelRequest();
            }

            if (target is not Map)
                return false;

            if (!RimMindAPI.IsConfigured())
            {
                RimMindErrors.Warn(
                    "[RimMind-Storyteller] ForceRequest: API not configured");
                return false;
            }

            return Dispatch(target, forced: true);
        }

        private bool Dispatch(IIncidentTarget target, bool forced)
        {
            if (target is not Map map || !TryResolveMemory())
                return false;

            int dispatchTick = Find.TickManager.TicksGame;
            bool dispatched = _requestState.TryDispatch(
                token =>
                {
                    _memory.ConsumeReactions(20);
                    if (forced)
                    {
                        RimMindAPI.ClearModCooldown("Storyteller");
                        Log.Message(
                            "[RimMind-Storyteller] ForceRequest: sending structured AI request");
                    }

                    string npcId = RimMindAPI.GetNpcForMap(map) ?? "NPC-storyteller";
                    SendRequest(npcId, target, token);
                },
                dispatchTick,
                out Exception? dispatchError);

            if (!dispatched && dispatchError != null)
            {
                string operation = forced ? "ForceRequest" : "AI request";
                RimMindErrors.Warn(
                    $"[RimMind-Storyteller] {operation} dispatch failed: {dispatchError.Message}");
            }

            return dispatched;
        }

        private void SendRequest(
            string npcId,
            IIncidentTarget target,
            long requestToken)
        {
            var envelope = LlmRequestEnvelopeBuilder
                .ForScenario(RimMindAPI.Context.ScenarioStoryteller)
                .WithModId("RimMind.Storyteller")
                .WithSchema(RimMindAPI.Context.SchemaIncidentOutput)
                .WithMaxTokens(400)
                .WithTemperature(0.8f)
                .WithNpcId(npcId)
                .Build();

            RimMindAPI.Request.Send(
                envelope,
                result => OnResponseReceived(result, target, requestToken));
        }

        private void OnResponseReceived(
            Result<LlmResponse, RimMindError> result,
            IIncidentTarget target,
            long requestToken)
        {
            if (!_requestState.IsCurrent(requestToken))
                return;

            int now = Find.TickManager.TicksGame;
            if (result.IsErr)
            {
                _requestState.Fail(requestToken, now);
                RimMindErrors.Warn(
                    $"[RimMind-Storyteller] AI request failed: {result.Error}");
                return;
            }

            string content = result.Value.Content;
            if (RimMindStorytellerMod.Settings?.debugLogging == true)
                Log.Message($"[RimMind-Storyteller] AI raw response: {content}");

            var (incident, response) = RimMindIncidentSelector.ParseResponse(
                content,
                target,
                _source);
            if (incident == null)
            {
                _requestState.Fail(requestToken, now);
                RimMindErrors.Warn(
                    $"[RimMind-Storyteller] AI response parse failed or event cannot fire: {content}");
                return;
            }

            if (!_requestState.Publish(requestToken, incident, now))
                return;

            if (response != null)
            {
                RecordChainStep(incident, response, now);
                if (ShouldNotifyPlayer(incident.def))
                    _notificationService.Register(incident, response, _memory);
            }

            Log.Message(
                $"[RimMind-Storyteller] AI selected event: {incident.def.defName}, pending fire on next interval");
        }

        private void RecordChainStep(
            FiringIncident incident,
            IncidentResponse response,
            int tick)
        {
            if (response.chain == null)
                return;

            _memory.RecordChainStep(
                response.chain.chain_id,
                response.chain.chain_step,
                response.chain.chain_total,
                response.chain.next_hint ?? string.Empty,
                incident.def.defName,
                tick,
                incident.parms.points,
                incident.parms.faction?.def?.defName ?? string.Empty);
        }

        private static bool ShouldNotifyPlayer(IncidentDef incidentDef)
        {
            StorytellerIncidentKind kind =
                incidentDef.category == IncidentCategoryDefOf.ThreatBig
                    ? StorytellerIncidentKind.ThreatBig
                    : incidentDef.category == IncidentCategoryDefOf.ThreatSmall
                        ? StorytellerIncidentKind.ThreatSmall
                        : StorytellerIncidentKind.Other;

            return IncidentSelectionPolicy.ShouldNotify(
                RimMindStorytellerMod.Settings?.enableEventNotification ?? true,
                kind);
        }

        private bool TryResolveMemory()
        {
            if (_memory != null)
                return true;

            _memory = StorytellerMemory.Instance!;
            if (_memory == null && Find.World != null)
            {
                _memory = Find.World.components
                    .OfType<StorytellerMemory>()
                    .FirstOrDefault()!;
            }

            if (_memory != null)
                return true;

            RimMindErrors.Warn(
                "[RimMind-Storyteller] StorytellerMemory not found, skipping.");
            return false;
        }
    }
}
