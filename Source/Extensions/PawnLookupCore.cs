using System;
using System.Collections.Generic;

namespace RimMind.Storyteller.Extensions
{
    public static class PawnLookupCore
    {
        public static T? FindById<T>(
            int id,
            Func<IEnumerable<T>> worldFactory,
            Func<IEnumerable<T>?> mapFactory,
            Func<T, int> getId)
            where T : class
        {
            if (id <= 0)
                return null;
            if (worldFactory == null)
                throw new ArgumentNullException(nameof(worldFactory));
            if (mapFactory == null)
                throw new ArgumentNullException(nameof(mapFactory));
            if (getId == null)
                throw new ArgumentNullException(nameof(getId));

            IEnumerable<T> world = worldFactory()
                ?? throw new InvalidOperationException("World Pawn source returned null.");
            foreach (T candidate in world)
            {
                if (getId(candidate) == id)
                    return candidate;
            }

            IEnumerable<T>? map = mapFactory();
            if (map == null)
                return null;

            foreach (T candidate in map)
            {
                if (getId(candidate) == id)
                    return candidate;
            }

            return null;
        }
    }
}
