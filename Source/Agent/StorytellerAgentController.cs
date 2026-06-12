using RimMind.Application.Common.Interfaces;
using RimMind.Application.Common.Interfaces.Agent;
using RimMind.Application.Common.Interfaces.Internal;
using RimMind.Domain.Enums;
using Verse;

namespace RimMind.Storyteller.Agent
{
    public static class StorytellerAgentController
    {
        public const string ScopeType = "storyteller";
        public const string ScopeId = "NPC-storyteller";

        public static IScopedAgent? Find()
        {
            var manager = RimMindServiceLocator.TryGet<IScopedAgentManager>();
            return manager?.Find(ScopeType, ScopeId);
        }

        public static IScopedAgent? GetOrCreate()
        {
            var manager = RimMindServiceLocator.TryGet<IScopedAgentManager>();
            var bus = RimMindServiceLocator.TryGet<IAgentBus>();
            if (manager == null || bus == null)
            {
                Log.Warning("[RimMind-Storyteller] Storyteller agent services are not available.");
                return null;
            }

            return manager.GetOrCreate(ScopeType, ScopeId, bus, Verse.Find.CurrentMap?.Index);
        }

        public static bool Start()
        {
            var agent = GetOrCreate();
            if (agent == null) return false;

            return agent.TransitionTo(AgentState.Active);
        }

        public static bool Pause()
        {
            var agent = Find();
            if (agent == null) return false;

            return agent.TransitionTo(AgentState.Paused);
        }

        public static bool ForceThink()
        {
            var agent = Find();
            if (agent == null) return false;

            agent.ForceThink();
            return true;
        }
    }
}
