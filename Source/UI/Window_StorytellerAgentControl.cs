using RimMind.Presentation.Api;
using RimMind.Storyteller.Agent;
using UnityEngine;
using Verse;

namespace RimMind.Storyteller.UI
{
    public sealed class Window_StorytellerAgentControl : Window
    {
        public override Vector2 InitialSize => new(560f, 420f);

        public Window_StorytellerAgentControl()
        {
            doCloseX = true;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f),
                "RimMind.Storyteller.UI.Agent.Title".Translate());
            Text.Font = GameFont.Small;

            var agent = StorytellerAgentController.Find();
            float y = inRect.y + 42f;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 24f),
                "RimMind.Storyteller.UI.Agent.State".Translate(agent?.State.ToString() ?? "not_created"));
            y += 34f;

            if (Widgets.ButtonText(new Rect(inRect.x, y, 120f, 32f),
                "RimMind.Storyteller.UI.Agent.Start".Translate()))
            {
                StorytellerAgentController.Start();
            }

            if (Widgets.ButtonText(new Rect(inRect.x + 130f, y, 120f, 32f),
                "RimMind.Storyteller.UI.Agent.Pause".Translate()))
            {
                StorytellerAgentController.Pause();
            }

            if (Widgets.ButtonText(new Rect(inRect.x + 260f, y, 120f, 32f),
                "RimMind.Storyteller.UI.Agent.ForceThink".Translate()))
            {
                StorytellerAgentController.ForceThink();
            }

            if (Widgets.ButtonText(new Rect(inRect.x + 390f, y, 140f, 32f),
                "RimMind.Storyteller.UI.Agent.OpenRequests".Translate()))
            {
                RimMindAPI.Debug.OpenAIRequests();
            }

            y += 48f;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, inRect.height - y),
                agent?.GetDebugInfo() ?? "RimMind.Storyteller.UI.Agent.NotCreated".Translate());
        }
    }
}
