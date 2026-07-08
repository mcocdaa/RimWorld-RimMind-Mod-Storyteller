using System.Linq;
using Verse;

namespace RimMind.Storyteller.Extensions
{
    /// <summary>
    /// ContextKey 提供者共享的小人解析逻辑。
    /// 先在 WorldPawns 中查找，再回退到当前地图的自由殖民者。
    /// </summary>
    public static class PawnLookup
    {
        /// <summary>
        /// 按 thingIDNumber 查找小人。id 无效（&lt;= 0）或未找到时返回 null。
        /// </summary>
        public static Pawn? FindPawnById(int pawnId)
        {
            if (pawnId <= 0) return null;

            return Find.WorldPawns.AllPawnsAlive.FirstOrDefault(p => p.thingIDNumber == pawnId)
                ?? Find.CurrentMap?.mapPawns?.FreeColonists.FirstOrDefault(p => p.thingIDNumber == pawnId);
        }
    }
}
