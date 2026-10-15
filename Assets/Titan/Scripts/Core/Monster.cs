using System;
using System.Collections.Generic;

namespace Titan.Core
{
    /// <summary>Integer voxel coordinate. X = left/right, Y = up, Z = depth (0 is the layer facing the player).</summary>
    public struct Int3 : IEquatable<Int3>
    {
        public readonly int X, Y, Z;

        public Int3(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(Int3 o) => X == o.X && Y == o.Y && Z == o.Z;
        public override bool Equals(object obj) => obj is Int3 o && Equals(o);
        public override int GetHashCode() => (X * 73856093) ^ (Y * 19349663) ^ (Z * 83492791);
        public override string ToString() => $"({X},{Y},{Z})";

        public static readonly Int3[] Neighbours =
        {
            new Int3(1, 0, 0), new Int3(-1, 0, 0),
            new Int3(0, 1, 0), new Int3(0, -1, 0),
            new Int3(0, 0, 1), new Int3(0, 0, -1),
        };

        public static Int3 operator +(Int3 a, Int3 b) => new Int3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }

    /// <summary>A piece that broke off: every cube in it with its color.</summary>
    public sealed class FallenPiece
    {
        public readonly List<Int3> Cubes = new List<Int3>();
        public readonly List<int> Colors = new List<int>();
        public int Count => Cubes.Count;
    }

    /// <summary>
    /// The voxel titan. Body cubes (legs and torso) are anchored: they stand on their own even when shot to pieces.
    /// Every other cube (arms, head) must stay connected to an anchored cube through face neighbours,
    /// otherwise it breaks off and falls.
    /// </summary>
    public sealed class Monster
    {
        readonly Dictionary<Int3, int> cubes = new Dictionary<Int3, int>();
        readonly HashSet<Int3> anchored = new HashSet<Int3>();

        public int Count => cubes.Count;
        public IEnumerable<KeyValuePair<Int3, int>> Cubes => cubes;
        public bool Has(Int3 c) => cubes.ContainsKey(c);
        public int ColorAt(Int3 c) => cubes[c];

        public int MinX { get; private set; } = int.MaxValue;
        public int MaxX { get; private set; } = int.MinValue;
        public int MaxY { get; private set; }
        public int MaxZ { get; private set; }

        public bool IsAnchored(Int3 c) => anchored.Contains(c);

        /// <summary>Adds a cube. Anchored cubes never fall; by default only the ground row is anchored.</summary>
        public void Set(Int3 c, int color, bool anchor = false)
        {
            cubes[c] = color;
            if (anchor || c.Y == 0) anchored.Add(c);
            MinX = Math.Min(MinX, c.X);
            MaxX = Math.Max(MaxX, c.X);
            MaxY = Math.Max(MaxY, c.Y);
            MaxZ = Math.Max(MaxZ, c.Z);
        }

        /// <summary>A cube is visible when nothing stands in front of it (lower Z) in its column.</summary>
        public bool IsVisible(Int3 c)
        {
            if (!cubes.ContainsKey(c)) return false;
            for (int z = 0; z < c.Z; z++)
                if (cubes.ContainsKey(new Int3(c.X, c.Y, z))) return false;
            return true;
        }

        public int CountOfColor(int color)
        {
            int n = 0;
            foreach (var kv in cubes) if (kv.Value == color) n++;
            return n;
        }

        public bool HasVisibleOfColor(int color)
        {
            foreach (var kv in cubes)
                if (kv.Value == color && IsVisible(kv.Key)) return true;
            return false;
        }

        /// <summary>
        /// The visible cube a cannon of this color shoots next: the highest one, then the one closest to the cannon's X.
        /// Returns false when no cube of this color is visible.
        /// </summary>
        public bool PickTarget(int color, float cannonX, out Int3 target)
        {
            target = default;
            bool found = false;
            float bestDx = 0;
            foreach (var kv in cubes)
            {
                if (kv.Value != color || !IsVisible(kv.Key)) continue;
                var c = kv.Key;
                float dx = Math.Abs(c.X - cannonX);
                if (!found || c.Y > target.Y || (c.Y == target.Y && (dx < bestDx || (dx == bestDx && c.X < target.X))))
                {
                    target = c;
                    bestDx = dx;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>Removes one cube and returns the pieces that lost their connection to the ground.</summary>
        public List<FallenPiece> Remove(Int3 c)
        {
            cubes.Remove(c);
            anchored.Remove(c);
            return DropUnsupported();
        }

        public List<FallenPiece> DropUnsupported()
        {
            var supported = new HashSet<Int3>();
            var queue = new Queue<Int3>();
            foreach (var a in anchored)
            {
                supported.Add(a);
                queue.Enqueue(a);
            }
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                foreach (var d in Int3.Neighbours)
                {
                    var n = cur + d;
                    if (cubes.ContainsKey(n) && supported.Add(n)) queue.Enqueue(n);
                }
            }

            var pieces = new List<FallenPiece>();
            if (supported.Count == cubes.Count) return pieces;

            // Group the loose cubes into connected pieces, so an arm and a head fall separately.
            var loose = new HashSet<Int3>();
            foreach (var kv in cubes) if (!supported.Contains(kv.Key)) loose.Add(kv.Key);
            var seen = new HashSet<Int3>();
            foreach (var start in loose)
            {
                if (!seen.Add(start)) continue;
                var piece = new FallenPiece();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var cur = queue.Dequeue();
                    piece.Cubes.Add(cur);
                    piece.Colors.Add(cubes[cur]);
                    foreach (var d in Int3.Neighbours)
                    {
                        var n = cur + d;
                        if (loose.Contains(n) && seen.Add(n)) queue.Enqueue(n);
                    }
                }
                pieces.Add(piece);
            }
            foreach (var piece in pieces)
                foreach (var cube in piece.Cubes)
                    cubes.Remove(cube);
            return pieces;
        }
    }
}
