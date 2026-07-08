using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.Application.Common.Interfaces.Client;
using RimMind.Application.Common.Interfaces.UI;
using RimMind.Application.Common.Models.UI;
using RimMind.Application.Features.Llm;
using RimMind.Domain.Llm;
using RimMind.Domain.ValueObjects;
using RimMind.Presentation.Api;
using RimMind.Application.Common.Interfaces.Extension;
using RimMind.Storyteller.Memory;
using RimMind.Storyteller.Settings;
using RimWorld;
using Verse;

namespace RimMind.Storyteller
{
    public class StorytellerComp_RimMindDirector : RimWorld.StorytellerComp
    {
        private StorytellerMemory _memory = null!;
        private bool _hasPendingRequest;
        private bool _hasPendingResult;
        private FiringIncident _pendingIncident = null!;
        private int _lastSuccessTick = -99999;
        private int _lastFailTick = -99999;

        private StorytellerCompProperties_RimMindDirector Props =>
            (StorytellerCompProperties_RimMindDirector)props;

        public bool IsActive => _hasPendingRequest || _hasPendingResult;

        public int LastSuccessTick => _lastSuccessTick;
        public int LastFailTick => _lastFailTick;

        public int GetEstimatedTicksUntilNextEvent()
        {
            float mtb = RimMindStorytellerMod.Settings?.mtbDays ?? Props.mtbDays;
            return (int)(mtb * 60000f);
        }

        public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
        {
            if (!target.IncidentTargetTags().Contains(IncidentTargetTagDefOf.Map_PlayerHome))
                yield break;

            var map = target as Map;
            if (map == null) yield break;

            EnsureMemory();
            if (_memory == null) yield break;
            _memory.ApplyDecayAndCleanup();

            if (_hasPendingResult && _pendingIncident != null)
            {
                var incident = _pendingIncident;
                _hasPendingResult = false;
                _pendingIncident = null!;

                if (RimMindStorytellerMod.Settings?.debugLogging == true)
                    Log.Message($"[RimMind-Storyteller] AI incident firing: {incident.def.defName}");

                yield return incident;
                yield break;
            }

            if (_hasPendingRequest)
                yield break;

            if (!RimMindAPI.IsConfigured())
                yield break;

            if (!(RimMindStorytellerMod.Settings?.enableIntervalTrigger ?? false))
                yield break;

            if (RimMindAPI.ShouldSkipStorytellerIncident())
                yield break;

            float mtb = RimMindStorytellerMod.Settings?.mtbDays ?? Props.mtbDays;
            if (!Rand.MTBEventOccurs(mtb, 60000f, 1000f))
                yield break;

            _hasPendingRequest = true;

            _memory.ConsumeReactions(20);

            string npcId = RimMindAPI.GetNpcForMap(map) ?? "NPC-storyteller";
            string scenario = RimMindAPI.Context.ScenarioStoryteller;

            TrySelectIncidentWithStructuredOutput(npcId, scenario, 400, 0.8f, target);

            yield break;
        }

        private void OnAIResponseReceived(Result<LlmResponse, RimMindError> result, IIncidentTarget target)
        {
            _hasPendingRequest = false;

            if (result.IsErr)
            {
                _lastFailTick = Find.TickManager.TicksGame;
                RimMindErrors.Warn($"[RimMind-Storyteller] AI request failed: {result.Error}");
                return;
            }

            var response = result.Value;

            if (RimMindStorytellerMod.Settings?.debugLogging == true)
                Log.Message($"[RimMind-Storyteller] AI raw response: {response.Content}");

            var (incident, incidentResponse) = RimMindIncidentSelector.ParseResponse(response.Content, target, this);
            if (incident == null)
            {
                _lastFailTick = Find.TickManager.TicksGame;
                RimMindErrors.Warn($"[RimMind-Storyteller] AI response parse failed or event cannot fire: {response.Content}");
                return;
            }

            _lastSuccessTick = Find.TickManager.TicksGame;
            _hasPendingResult = true;
            _pendingIncident = incident;

            if (incidentResponse != null)
            {
                if (incidentResponse.chain != null)
                {
                    _memory.RecordChainStep(
                        incidentResponse.chain.chain_id,
                        incidentResponse.chain.chain_step,
                        incidentResponse.chain.chain_total,
                        incidentResponse.chain.next_hint ?? string.Empty,
                        incident.def.defName,
                        Find.TickManager.TicksGame,
                        incident.parms.points,
                        incident.parms.faction?.def?.defName ?? string.Empty);
                }

                if (ShouldNotifyPlayer(incident.def))
                    RegisterEventNotification(incident, incidentResponse);
            }

            Log.Message($"[RimMind-Storyteller] AI selected event: {incident.def.defName}, pending fire on next interval");
        }

        public bool ForceRequest(IIncidentTarget target)
        {
            if (_hasPendingRequest)
            {
                RimMindErrors.Warn("[RimMind-Storyteller] ForceRequest: overriding existing pending request");
                _hasPendingRequest = false;
            }

            var map = target as Map;
            if (map == null) return false;

            if (!RimMindAPI.IsConfigured())
            {
                RimMindErrors.Warn("[RimMind-Storyteller] ForceRequest: API not configured");
                return false;
            }

            EnsureMemory();

            _memory.ConsumeReactions(20);

            _hasPendingRequest = true;

            RimMindAPI.ClearModCooldown("Storyteller");

            string npcId = RimMindAPI.GetNpcForMap(map) ?? "NPC-storyteller";
            string scenario = RimMindAPI.Context.ScenarioStoryteller;

            Log.Message("[RimMind-Storyteller] ForceRequest: sending structured AI request");
            TrySelectIncidentWithStructuredOutput(npcId, scenario, 400, 0.8f, target);
            return true;
        }

        private bool ShouldNotifyPlayer(IncidentDef incidentDef)
        {
            if (!(RimMindStorytellerMod.Settings?.enableEventNotification ?? true))
                return false;

            return incidentDef.category == IncidentCategoryDefOf.ThreatBig
                || incidentDef.category == IncidentCategoryDefOf.ThreatSmall;
        }

        private void RegisterEventNotification(FiringIncident incident, IncidentResponse incidentResponse)
        {
            bool isBigThreat = incident.def.category == IncidentCategoryDefOf.ThreatBig;

            string titleKey = isBigThreat
                ? "RimMind.Storyteller.UI.DeclareTitle"
                : "RimMind.Storyteller.UI.WhisperTitle";
            string title = titleKey.Translate(incident.def.LabelCap);

            string description;
            if (!string.IsNullOrEmpty(incidentResponse.announce))
            {
                description = incidentResponse.announce!;
            }
            else if (!string.IsNullOrEmpty(incidentResponse.reason))
            {
                description = incidentResponse.reason.Length > 20
                    ? incidentResponse.reason.Substring(0, 20) + "..."
                    : incidentResponse.reason;
            }
            else
            {
                description = "RimMind.Storyteller.UI.DefaultDesc".Translate(incident.def.LabelCap);
            }

            string optShock = "RimMind.Storyteller.UI.Shock".Translate();
            string optExcited = "RimMind.Storyteller.UI.Excited".Translate();
            string optAccept = "RimMind.Storyteller.UI.Accept".Translate();

            string tooltip = "RimMind.Storyteller.UI.NoInterfere".Translate();

            var capturedMemory = _memory;
            var capturedDefName = incident.def.defName;
            var capturedLabel = incident.def.LabelCap.ToString();

            var entry = new RequestEntry
            {
                source = "storyteller",
                title = title,
                description = description,
                options = new[] { optShock, optExcited, optAccept },
                optionTooltips = new[] { tooltip, tooltip, tooltip },
                expireTicks = RimMindStorytellerMod.Settings?.requestExpireTicks ?? 30000,
                callback = choice =>
                {
                    string reaction;
                    string reactionLabel;
                    float tensionDelta;

                    if (choice == optShock)
                    {
                        reaction = "shock";
                        reactionLabel = optShock;
                        tensionDelta = 0.05f;
                    }
                    else if (choice == optExcited)
                    {
                        reaction = "excited";
                        reactionLabel = optExcited;
                        tensionDelta = -0.05f;
                    }
                    else
                    {
                        reaction = "accept";
                        reactionLabel = optAccept;
                        tensionDelta = 0f;
                    }

                    capturedMemory.RecordPlayerReaction(
                        capturedDefName,
                        capturedLabel,
                        reaction,
                        reactionLabel,
                        Find.TickManager.TicksGame);

                    if (tensionDelta != 0f)
                        capturedMemory.ApplyTensionDelta(tensionDelta);
                }
            };

            RimMindAPI.RegisterPendingRequest(entry);
        }

        private void TrySelectIncidentWithStructuredOutput(string npcId, string scenario, int maxTokens, float temperature, IIncidentTarget target)
        {
            var schema = RimMindAPI.Context.SchemaIncidentOutput;

            var envelope = LlmRequestEnvelopeBuilder
                .ForScenario(scenario)
                .WithModId("RimMind.Storyteller")
                .WithSchema(schema)
                .WithMaxTokens(maxTokens)
                .WithTemperature(temperature)
                .WithNpcId(npcId)
                .Build();
            RimMindAPI.Request.Send(envelope, result => OnAIResponseReceived(result, target));
        }

        private void EnsureMemory()
        {
            if (_memory == null)
            {
                _memory = StorytellerMemory.Instance!;
                if (_memory == null && Find.World != null)
                {
                    _memory = Find.World.components.OfType<StorytellerMemory>().FirstOrDefault();
                }
                if (_memory == null)
                    RimMindErrors.Warn("[RimMind-Storyteller] StorytellerMemory not found, skipping.");
            }
        }
    }
}
