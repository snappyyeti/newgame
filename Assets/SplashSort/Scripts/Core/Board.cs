using System;
using System.Collections.Generic;

namespace SplashSort.Core
{
    /// <summary>Every tunable number of the test prototype.</summary>
    public sealed class SplashConfig
    {
        public int Columns = 3;
        public int Rows = 4;
        public int SplashRun = 10;
        public int PointsPerSwimmer = 10;
        public int TraySize = 3;
        public int MinRingSize = 2;
        public int MaxRingSize = 6;
        public int MinRingColors = 1;
        public int MaxRingColors = 3;

        /// <summary>(score, colors in play), sorted by score.</summary>
        public int[][] ColorTiers =
        {
            new[] { 0, 3 },
            new[] { 500, 4 },
            new[] { 1500, 5 },
            new[] { 3000, 6 },
        };

        public int ColorsFor(int score)
        {
            int colors = ColorTiers[0][1];
            foreach (var tier in ColorTiers)
                if (score >= tier[0]) colors = tier[1];
            return colors;
        }
    }

    /// <summary>A ring of swimmers, listed bottom to top exactly as they will stand in the pool.</summary>
    public sealed class Ring
    {
        public readonly int[] Colors;

        public Ring(params int[] colors)
        {
            if (colors == null || colors.Length == 0) throw new ArgumentException("A ring needs at least one swimmer.");
            Colors = colors;
        }

        public int Size => Colors.Length;

        public int DistinctColors => new HashSet<int>(Colors).Count;

        public override string ToString() => string.Concat(Array.ConvertAll(Colors, c => c.ToString()));

        /// <summary>2–6 swimmers, 1–3 colors, each color in one contiguous block.</summary>
        public static Ring Random(SplashConfig cfg, int colorsInPlay, IRandom random)
        {
            int size = random.Range(cfg.MinRingSize, cfg.MaxRingSize + 1);
            int maxDistinct = Math.Min(Math.Min(cfg.MaxRingColors, size), colorsInPlay);
            int minDistinct = Math.Min(cfg.MinRingColors, maxDistinct);
            int distinct = random.Range(minDistinct, maxDistinct + 1);

            var palette = new List<int>();
            for (int c = 0; c < colorsInPlay; c++) palette.Add(c);
            var chosen = new int[distinct];
            for (int i = 0; i < distinct; i++)
            {
                int j = random.Range(0, palette.Count);
                chosen[i] = palette[j];
                palette.RemoveAt(j);
            }

            var lengths = new int[distinct];
            for (int i = 0; i < distinct; i++) lengths[i] = 1;
            for (int extra = size - distinct; extra > 0; extra--) lengths[random.Range(0, distinct)]++;

            var colors = new int[size];
            int k = 0;
            for (int i = 0; i < distinct; i++)
                for (int n = 0; n < lengths[i]; n++)
                    colors[k++] = chosen[i];
            return new Ring(colors);
        }
    }

    /// <summary>
    /// 12 pools in a grid of 3 columns × 4 rows, numbered row by row from the top-left (0) to the bottom-right (11).
    /// Each pool is empty or holds a stack of swimmer colors, bottom to top.
    /// </summary>
    public sealed class Board
    {
        readonly List<int>[] pools;
        public readonly int Columns;
        public readonly int Rows;

        public Board(int columns, int rows)
        {
            Columns = columns;
            Rows = rows;
            pools = new List<int>[columns * rows];
            for (int i = 0; i < pools.Length; i++) pools[i] = new List<int>();
        }

        public int Count => pools.Length;
        public IReadOnlyList<int> Stack(int pool) => pools[pool];
        public int Height(int pool) => pools[pool].Count;
        public bool IsEmpty(int pool) => pools[pool].Count == 0;
        public int Column(int pool) => pool % Columns;
        public int Row(int pool) => pool / Columns;

        public bool HasEmptyPool
        {
            get
            {
                foreach (var p in pools) if (p.Count == 0) return true;
                return false;
            }
        }

        public bool IsCompletelyEmpty
        {
            get
            {
                foreach (var p in pools) if (p.Count > 0) return false;
                return true;
            }
        }

        /// <summary>Up, down, left, right.</summary>
        public IEnumerable<int> Neighbors(int pool)
        {
            int r = Row(pool), c = Column(pool);
            if (r > 0) yield return pool - Columns;
            if (r < Rows - 1) yield return pool + Columns;
            if (c > 0) yield return pool - 1;
            if (c < Columns - 1) yield return pool + 1;
        }

        /// <summary>The same-colored run at the top of the pool. Color -1 and length 0 when empty.</summary>
        public void TopRun(int pool, out int color, out int length)
        {
            var s = pools[pool];
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

        public void Push(int pool, int color) => pools[pool].Add(color);
        public void PopTop(int pool, int count) => pools[pool].RemoveRange(pools[pool].Count - count, count);

        public Board Clone()
        {
            var b = new Board(Columns, Rows);
            for (int i = 0; i < pools.Length; i++) b.pools[i].AddRange(pools[i]);
            return b;
        }

        /// <summary>Test helper: one string per pool, one digit per swimmer, bottom to top. Missing pools are empty.</summary>
        public static Board FromStrings(params string[] stacks)
        {
            var b = new Board(3, 4);
            for (int i = 0; i < stacks.Length; i++)
                foreach (var ch in stacks[i]) b.Push(i, ch - '0');
            return b;
        }

        public override string ToString()
        {
            var parts = new string[pools.Length];
            for (int i = 0; i < pools.Length; i++) parts[i] = string.Concat(pools[i]);
            return string.Join("|", parts);
        }
    }

    public interface IRandom
    {
        /// <summary>Integer in [min, maxExclusive).</summary>
        int Range(int min, int maxExclusive);
    }

    public sealed class SystemRandom : IRandom
    {
        readonly Random random;
        public SystemRandom() { random = new Random(); }
        public SystemRandom(int seed) { random = new Random(seed); }
        public int Range(int min, int maxExclusive) => random.Next(min, maxExclusive);
    }
}
