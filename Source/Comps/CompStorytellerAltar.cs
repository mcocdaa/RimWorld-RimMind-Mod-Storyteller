using System.Collections.Generic;
using RimMind.Storyteller.UI;
using Verse;

namespace RimMind.Storyteller.Comps
{
    public class CompStorytellerAltar : ThingComp
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return new Command_Action
            {
                defaultLabel = "RimMind.Storyteller.Altar.Communicate".Translate(),
                defaultDesc = "RimMind.Storyteller.Altar.CommunicateDesc".Translate(),
                icon = Verse.ContentFinder<UnityEngine.Texture2D>.Get("UI/StorytellerAltarIcon", reportFailure: false),
                action = () =>
                {
                    var map = parent.Map;
                    if (map == null) return;
                    Find.WindowStack.Add(new Window_StorytellerDialogue(map));
                }
            };
        }
    }

    public class CompProperties_StorytellerAltar : CompProperties
    {
        public CompProperties_StorytellerAltar()
        {
            compClass = typeof(CompStorytellerAltar);
        }
    }
}
