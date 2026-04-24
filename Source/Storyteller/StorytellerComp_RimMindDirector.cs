using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.Core;
using RimMind.Core.Client;
using RimMind.Core.Context;
using RimMind.Core.UI;
using RimMind.Core.Npc;
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
            _cachedTarget = target;

            float budget = GetStorytellerBudget();
            var ctxRequest = new ContextRequest
            {
                NpcId = NpcManager.Instance?.GetNpcForMap(map) ?? "NPC-storyteller",
                Scenario = ScenarioIds.Storyteller,
                Budget = budget,
                MaxTokens = 200,
                Temperature = 0.8f,
            };

            TrySelectIncidentWithStructuredOutput(ctxRequest, target);

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

                if (ShouldNotifyPlayer(incident.def))
                    RegisterEventNotification(incident, incidentResponse);
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

            float budget = GetStorytellerBudget();
            var ctxRequest = new ContextRequest
            {
                NpcId = NpcManager.Instance?.GetNpcForMap(map) ?? "NPC-storyteller",
                Scenario = ScenarioIds.Storyteller,
                Budget = budget,
                MaxTokens = 200,
                Temperature = 0.8f,
            };

            var schema = RimMind.Core.Context.SchemaRegistry.IncidentOutput;
            Log.Message("[RimMind-Storyteller] ForceRequest: sending structured AI request");
            RimMindAPI.RequestStructured(ctxRequest, schema, response => OnAIResponseReceived(response, target));
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

        private void TrySelectIncidentWithStructuredOutput(ContextRequest request, IIncidentTarget target)
        {
            var schema = RimMind.Core.Context.SchemaRegistry.IncidentOutput;
            RimMindAPI.RequestStructured(request, schema, response => OnAIResponseReceived(response, target));
        }

        internal static float GetStorytellerBudget()
        {
            var settings = RimMind.Core.RimMindCoreMod.Settings?.Context;
            if (settings == null) return 0.6f;
            return settings.ContextBudget;
        }

        private void EnsureMemory()
        {
            if (_memory == null)
            {
                _memory = StorytellerMemory.Instance;
                if (_memory == null && Find.World != null)
                {
                    _memory = Find.World.components.OfType<StorytellerMemory>().FirstOrDefault();
                }
                if (_memory == null)
                    Log.WarningOnce("[RimMind-Storyteller] StorytellerMemory not found, skipping.", 91827364);
            }
        }
    }
}
