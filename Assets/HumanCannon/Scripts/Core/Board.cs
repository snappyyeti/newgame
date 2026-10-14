using System;
using System.Collections.Generic;

namespace HumanCannon.Core
{
    /// <summary>A group of 2–4 people in the cannon, listed bottom to top (index 0 lands first).</summary>
    public sealed class Group
    {
        public readonly int[] Colors;

        /// <summary>Percentile the director gave this group when it was chosen (for analytics).</summary>
        public float DirectorH = -1f;
        public DirectorMode DirectorMode = DirectorMode.Normal;

        public Group(params int[] colors)
        {
            if (colors == null || colors.Length == 0) throw new ArgumentException("A group needs at least one person.");
            Colors = colors;
        }

        public int Size => Colors.Length;
        public int Bottom => Colors[0];

        public int DistinctColors
        {
            get
            {
                var seen = new HashSet<int>(Colors);
                return seen.Count;
            }
        }

        /// <summary>Number of places inside the group where two different colors touch.</summary>
        public int InternalBoundaries
        {
            get
            {
                int n = 0;
                for (int i = 1; i < Colors.Length; i++)
                    if (Colors[i] != Colors[i - 1]) n++;
                return n;
            }
        }

        public override string ToString() => string.Join("", Array.ConvertAll(Colors, c => c.ToString()));
    }

    /// <summary>P1–P7: each pedestal is a stack of color ids, bottom to top.</summary>
    public sealed class Board
    {
        readonly List<int>[] stacks;
        public readonly int Capacity;

        public Board(int pedestals, int capacity)
        {
            Capacity = capacity;
            stacks = new List<int>[pedestals];
            for (int i = 0; i < pedestals; i++) stacks[i] = new List<int>(capacity);
        }

        public int Count => stacks.Length;
        public IReadOnlyList<int> Stack(int p) => stacks[p];
        public int Height(int p) => stacks[p].Count;

        public bool IsEmpty
        {
            get
            {
                foreach (var s in stacks) if (s.Count > 0) return false;
                return true;
            }
        }

        public bool CanPlace(int p, int size) => p >= 0 && p < stacks.Length && stacks[p].Count + size <= Capacity;

        public bool FitsAnywhere(int size)
        {
            for (int p = 0; p < stacks.Length; p++) if (CanPlace(p, size)) return true;
            return false;
        }

        /// <summary>The run of same-colored people at the top of P. Color is -1 and length 0 when P is empty.</summary>
        public void TopRun(int p, out int color, out int length)
        {
            var s = stacks[p];
            if (s.Count == 0)
            {
                color = -1;
                length = 0;
                return;
            }
            color = s[s.Count - 1];
            length = 0;
            for (int i = s.Count - 1; i >= 0 && s[i] == color; i--) length++;
        }

        public void Push(int p, int color) => stacks[p].Add(color);

        public void PopTop(int p, int count) => stacks[p].RemoveRange(stacks[p].Count - count, count);

        public void ClearPedestal(int p) => stacks[p].Clear();

        public Board Clone()
        {
            var b = new Board(stacks.Length, Capacity);
            for (int i = 0; i < stacks.Length; i++) b.stacks[i].AddRange(stacks[i]);
            return b;
        }

        /// <summary>Builds a board from strings like "0011" (bottom to top, one digit per person). Handy in tests.</summary>
        public static Board FromStrings(int capacity, params string[] pedestals)
        {
            var b = new Board(pedestals.Length, capacity);
            for (int p = 0; p < pedestals.Length; p++)
                foreach (var ch in pedestals[p]) b.Push(p, ch - '0');
            return b;
        }

        public override string ToString()
        {
            var parts = new string[stacks.Length];
            for (int p = 0; p < stacks.Length; p++) parts[p] = string.Join("", stacks[p]);
            return string.Join(" | ", parts);
        }
    }
}
