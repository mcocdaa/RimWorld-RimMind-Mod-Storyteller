using RimMind.Storyteller.Memory;

namespace RimMind.Storyteller
{
    public class StorytellerCompProperties_RimMindDirector : RimWorld.StorytellerCompProperties
    {
        public float mtbDays = 1.5f;

        public StorytellerCompProperties_RimMindDirector()
        {
            compClass = typeof(StorytellerComp_RimMindDirector);
        }
    }
}
