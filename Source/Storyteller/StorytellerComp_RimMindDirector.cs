using System.Collections.Generic;
using System.Linq;
using RimMind.Core;
using RimMind.Core.Client;
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
        private IIncidentTarget _cachedTarget = null!;
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

            float mtb = RimMindStorytellerMod.Settings?.mtbDays ?? Props.mtbDays;
            if (!Rand.MTBEventOccurs(mtb, 60000f, 1000f))
                yield break;

            _hasPendingRequest = true;
            _cachedTarget = target;

            var request = new AIRequest
            {
                SystemPrompt = RimMindIncidentSelector.BuildSystemPrompt(_memory),
                UserPrompt = RimMindIncidentSelector.BuildUserPrompt(map, _memory, Props.maxCandidates),
                MaxTokens = 200,
                Temperature = 0.8f,
                RequestId = "Storyteller_Director",
                ModId = "Storyteller",
                ExpireAtTicks = Find.TickManager.TicksGame + (RimMindStorytellerMod.Settings?.requestExpireTicks ?? 30000),
                UseJsonMode = true,
            };

            RimMindAPI.RequestAsync(request, response => OnAIResponseReceived(response, target));

            yield break;
        }

        private void OnAIResponseReceived(AIResponse response, IIncidentTarget target)
        {
            _hasPendingRequest = false;

            if (!response.Success)
            {
                _lastFailTick = Find.TickManager.TicksGame;
                Log.Warning($"[RimMind-Storyteller] AI request failed: {response.Error}");
                return;
            }

            if (RimMindStorytellerMod.Settings?.debugLogging == true)
                Log.Message($"[RimMind-Storyteller] AI raw response: {response.Content}");

            var (incident, incidentResponse) = RimMindIncidentSelector.ParseResponse(response.Content, target, this);
            if (incident == null)
            {
                _lastFailTick = Find.TickManager.TicksGame;
                Log.Warning($"[RimMind-Storyteller] AI response parse failed or event cannot fire: {response.Content}");
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
                _memory.UpdateTension(incident.def.category);
            }

            Log.Message($"[RimMind-Storyteller] AI selected event: {incident.def.defName}, pending fire on next interval");
        }

        public bool ForceRequest(IIncidentTarget target)
        {
            if (_hasPendingRequest)
            {
                Log.Warning("[RimMind-Storyteller] ForceRequest: overriding existing pending request");
                _hasPendingRequest = false;
            }

            var map = target as Map;
            if (map == null) return false;

            if (!RimMindAPI.IsConfigured())
            {
                Log.Warning("[RimMind-Storyteller] ForceRequest: API not configured");
                return false;
            }

            EnsureMemory();

            _hasPendingRequest = true;
            _cachedTarget = target;

            Core.Internal.AIRequestQueue.Instance?.ClearCooldown("Storyteller");

            var request = new AIRequest
            {
                SystemPrompt = RimMindIncidentSelector.BuildSystemPrompt(_memory),
                UserPrompt = RimMindIncidentSelector.BuildUserPrompt(map, _memory, Props.maxCandidates),
                MaxTokens = 200,
                Temperature = 0.8f,
                RequestId = "Storyteller_Director",
                ModId = "Storyteller",
                ExpireAtTicks = Find.TickManager.TicksGame + RimMindStorytellerMod.Settings.requestExpireTicks,
                UseJsonMode = true,
            };

            Log.Message("[RimMind-Storyteller] ForceRequest: sending immediate AI request");
            RimMindAPI.RequestImmediate(request, response => OnAIResponseReceived(response, target));
            return true;
        }

        private void EnsureMemory()
        {
            if (_memory == null)
                _memory = StorytellerMemory.Instance ?? new StorytellerMemory(Find.World);
        }
    }
}
