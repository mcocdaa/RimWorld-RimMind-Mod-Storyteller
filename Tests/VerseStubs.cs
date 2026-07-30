using System.Collections.Generic;

namespace Verse
{
    public enum LookMode
    {
        Undef,
        Value,
        Deep,
    }

    public static class ScribeRecorder
    {
        public static readonly List<(string Label, object? DefaultValue)> ValueCalls =
            new List<(string, object?)>();
        public static readonly List<(string Label, LookMode Mode)> CollectionCalls =
            new List<(string, LookMode)>();
        public static bool AssignNullCollections { get; set; }

        public static void Reset()
        {
            ValueCalls.Clear();
            CollectionCalls.Clear();
            AssignNullCollections = false;
        }
    }

    public static class Scribe_Values
    {
        public static void Look<T>(ref T value, string label, T defaultValue = default!)
            => ScribeRecorder.ValueCalls.Add((label, defaultValue));
    }

    public static class Scribe_Collections
    {
        public static void Look<T>(
            ref List<T> values,
            string label,
            LookMode lookMode = LookMode.Undef)
        {
            ScribeRecorder.CollectionCalls.Add((label, lookMode));
            if (ScribeRecorder.AssignNullCollections)
                values = null!;
        }
    }
}
