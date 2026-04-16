using HarmonyLib;
using RimMind.Core;
using RimMind.Storyteller.Memory;
using RimWorld;
using Verse;

namespace RimMind.Storyteller.Patch
{
    [HarmonyPatch(typeof(IncidentWorker), "TryExecute")]
    public static class Patch_IncidentWorker_TryExecute
    {
        static void Postfix(IncidentWorker __instance, IncidentParms parms, bool __result)
        {
            if (!__result) return;

            if (RimMindStorytellerMod.Settings == null) return;

            var memory = StorytellerMemory.Instance;
            if (memory == null) return;

            memory.RecordIncident(__instance.def, parms.target, Find.TickManager.TicksGame);
            memory.UpdateTension(__instance.def.category);

            if (RimMindAPI.CanTriggerDialogue)
            {
                var map = parms.target as Map;
                if (map != null)
                {
                    var colonists = map.mapPawns?.FreeColonistsSpawned;
                    if (colonists != null && colonists.Count > 0)
                    {
                        var pawn = colonists.RandomElement();
                        string incidentContext = "RimMind.Storyteller.Context.IncidentOccurred".Translate(__instance.def.label);
                        RimMindAPI.TriggerDialogue(pawn, incidentContext);
                    }
                }
            }
        }
    }
}
