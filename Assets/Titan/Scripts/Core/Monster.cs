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

    /// <summary>Which visible cube a cannon shoots first (DESIGN.md 4.2.3).</summary>
    public enum TargetOrder
    {
        /// <summary>The old rule: highest cube, then closest to the cannon. Limbs could fall without a joint shot.</summary>
        HighestFirst,
        /// <summary>The cube farthest from the anchored body (support distance), then highest, then closest. Limbs are eaten from the tip.</summary>
        FarthestFirst,
    }

    /// <summary>A piece that broke off: every cube in it with its color.</summary>
    public sealed class FallenPiece
    {
        public readonly List<Int3> Cubes = new List<Int3>();
        public readonly List<int> Colors = new List<int>();
        public int Count => Cubes.Count;
        /// <summary>The body part most of its cubes belong to (<see cref="Monster.PartOf"/>); the head wins ties.</summary>
        public int Part;
    }

    /// <summary>
    /// The voxel titan. Body cubes (legs and torso) are anchored: they stand on their own even when shot to pieces.
    /// Every other cube (arms, head) must stay connected to an anchored cube through face neighbours,
    /// otherwise it breaks off and falls.
    ///
    /// Part tags are the single API every system uses to talk about limbs: <see cref="PartOf"/> (0 body, 1 left arm,
    /// 2 right arm, 3 head, 4+ reserved), <see cref="IsJoint"/> (shoulder and neck cubes, tagged as body: they stay on
    /// the torso when the limb falls), <see cref="OriginalPartSize"/> and <see cref="RemainingPartSize"/>.
    /// Tags belong to positions: they keep answering for a cube that was shot or fell.
    /// </summary>
    public sealed class Monster
    {
        public const int Body = 0, LeftArm = 1, RightArm = 2, Head = 3;
        public const int MaxParts = 8;

        readonly Dictionary<Int3, int> cubes = new Dictionary<Int3, int>();
        readonly HashSet<Int3> anchored = new HashSet<Int3>();
        readonly int[] colorCount = new int[MonsterBuilder.MaxColors];
        readonly HashSet<Int3>[] byColor = new HashSet<Int3>[MonsterBuilder.MaxColors];
        // Tags never change after the build, so clones share them until someone calls Set or MarkGlove on a clone.
        Dictionary<Int3, byte> part = new Dictionary<Int3, byte>();
        HashSet<Int3> joints = new HashSet<Int3>();
        HashSet<Int3> gloves = new HashSet<Int3>();
        bool tagsShared;
        readonly int[] originalPartSize = new int[MaxParts];
        readonly int[] remainingPartSize = new int[MaxParts];

        /// <summary>Face-neighbour steps from the nearest anchored cube (anchored = 0). Null = stale; rebuilt on demand.</summary>
        Dictionary<Int3, int> supportDist;

        public Monster()
        {
            for (int c = 0; c < byColor.Length; c++) byColor[c] = new HashSet<Int3>();
        }

        /// <summary>Target rule, set by <see cref="TitanGame"/> from <see cref="TitanConfig.TargetOrder"/> at spawn.</summary>
        public TargetOrder Order = TargetOrder.FarthestFirst;

        /// <summary>Changes every time a cube is added, shot or falls. Caches (cut prediction) key on it.</summary>
        public int Epoch { get; private set; }

        /// <summary>How many times the support-distance BFS ran (for tests: it must not run per PickTarget call).</summary>
        public int SupportBfsRuns { get; private set; }

        public int Count => cubes.Count;
        public IEnumerable<KeyValuePair<Int3, int>> Cubes => cubes;
        public bool Has(Int3 c) => cubes.ContainsKey(c);
        public int ColorAt(Int3 c) => cubes[c];

        public int MinX { get; private set; } = int.MaxValue;
        public int MaxX { get; private set; } = int.MinValue;
        public int MaxY { get; private set; }
        public int MaxZ { get; private set; }

        public bool IsAnchored(Int3 c) => anchored.Contains(c);

        // ---------------------------------------------------------------- parts

        /// <summary>Body part of this position: 0 body, 1 left arm, 2 right arm, 3 head (4+ reserved).</summary>
        public int PartOf(Int3 c) => part.TryGetValue(c, out var p) ? p : Body;

        /// <summary>Shoulder and neck cubes: shooting all of them away severs the limb.</summary>
        public bool IsJoint(Int3 c) => joints.Contains(c);

        /// <summary>Bottom row of an arm (the hand). For the view only: a shape cue, never a different color.</summary>
        public bool IsGlove(Int3 c) => gloves.Contains(c);

        public IEnumerable<Int3> Joints => joints;

        /// <summary>Cubes of this part when the titan was built.</summary>
        public int OriginalPartSize(int p) => p >= 0 && p < MaxParts ? originalPartSize[p] : 0;

        /// <summary>Cubes of this part still standing.</summary>
        public int RemainingPartSize(int p) => p >= 0 && p < MaxParts ? remainingPartSize[p] : 0;

        public void MarkGlove(Int3 c)
        {
            OwnTags();
            gloves.Add(c);
        }

        void OwnTags()
        {
            if (!tagsShared) return;
            part = new Dictionary<Int3, byte>(part);
            joints = new HashSet<Int3>(joints);
            gloves = new HashSet<Int3>(gloves);
            tagsShared = false;
        }

        /// <summary>True when a joint cube of this color is still standing and visible.</summary>
        public bool HasVisibleJointOfColor(int color)
        {
            foreach (var j in joints)
                if (cubes.TryGetValue(j, out var col) && col == color && IsVisible(j)) return true;
            return false;
        }

        /// <summary>True when a joint cube of this color is still standing (visible or not).</summary>
        public bool HasJointOfColor(int color)
        {
            foreach (var j in joints)
                if (cubes.TryGetValue(j, out var col) && col == color) return true;
            return false;
        }

        // ---------------------------------------------------------------- building

        /// <summary>
        /// Adds (or replaces) a cube. Anchored cubes never fall; by default only the ground row is anchored.
        /// <paramref name="bodyPart"/> and <paramref name="joint"/> tag the position (see <see cref="PartOf"/>).
        /// </summary>
        public void Set(Int3 c, int color, bool anchor = false, int bodyPart = Body, bool joint = false)
        {
            if (color < 0 || color >= MonsterBuilder.MaxColors) throw new ArgumentOutOfRangeException(nameof(color));
            if (bodyPart < 0 || bodyPart >= MaxParts) throw new ArgumentOutOfRangeException(nameof(bodyPart));
            OwnTags();
            if (cubes.ContainsKey(c)) Forget(c);
            cubes[c] = color;
            colorCount[color]++;
            byColor[color].Add(c);
            part[c] = (byte)bodyPart;
            originalPartSize[bodyPart]++;
            remainingPartSize[bodyPart]++;
            if (joint) joints.Add(c);
            else joints.Remove(c);
            if (anchor || c.Y == 0) anchored.Add(c);
            MinX = Math.Min(MinX, c.X);
            MaxX = Math.Max(MaxX, c.X);
            MaxY = Math.Max(MaxY, c.Y);
            MaxZ = Math.Max(MaxZ, c.Z);
            supportDist = null;
            Epoch++;
        }

        /// <summary>Takes a cube out of every count (a replaced cube also leaves the original part size).</summary>
        void Forget(Int3 c)
        {
            int color = cubes[c];
            colorCount[color]--;
            byColor[color].Remove(c);
            int p = PartOf(c);
            originalPartSize[p]--;
            remainingPartSize[p]--;
        }

        /// <summary>Removes a standing cube from the titan (shot or fallen). Tags stay with the position.</summary>
        void Delete(Int3 c)
        {
            int color = cubes[c];
            cubes.Remove(c);
            colorCount[color]--;
            byColor[color].Remove(c);
            remainingPartSize[PartOf(c)]--;
        }

        /// <summary>A copy (cubes, anchors, tags, target order, support cache) for look-ahead simulations.</summary>
        public Monster Clone()
        {
            var m = new Monster { Order = Order };
            foreach (var kv in cubes)
            {
                m.cubes[kv.Key] = kv.Value;
                m.byColor[kv.Value].Add(kv.Key);
            }
            foreach (var a in anchored) m.anchored.Add(a);
            m.part = part;
            m.joints = joints;
            m.gloves = gloves;
            m.tagsShared = tagsShared = true;
            Array.Copy(colorCount, m.colorCount, colorCount.Length);
            Array.Copy(originalPartSize, m.originalPartSize, MaxParts);
            Array.Copy(remainingPartSize, m.remainingPartSize, MaxParts);
            m.MinX = MinX;
            m.MaxX = MaxX;
            m.MaxY = MaxY;
            m.MaxZ = MaxZ;
            if (supportDist != null) m.supportDist = new Dictionary<Int3, int>(supportDist);
            return m;
        }

        // ---------------------------------------------------------------- queries

        /// <summary>A cube is visible when nothing stands in front of it (lower Z) in its column.</summary>
        public bool IsVisible(Int3 c)
        {
            if (!cubes.ContainsKey(c)) return false;
            for (int z = 0; z < c.Z; z++)
                if (cubes.ContainsKey(new Int3(c.X, c.Y, z))) return false;
            return true;
        }

        /// <summary>Cubes of this color still standing. O(1).</summary>
        public int CountOfColor(int color) => color >= 0 && color < colorCount.Length ? colorCount[color] : 0;

        public bool HasVisibleOfColor(int color)
        {
            if (color < 0 || color >= byColor.Length) return false;
            foreach (var c in byColor[color])
                if (IsVisible(c)) return true;
            return false;
        }

        /// <summary>Support distance of a standing cube (0 = anchored), or -1 if it is not standing.</summary>
        public int SupportDistance(Int3 c)
        {
            EnsureSupportDist();
            return supportDist.TryGetValue(c, out var d) ? d : -1;
        }

        void EnsureSupportDist()
        {
            if (supportDist == null) supportDist = ComputeSupportDist();
        }

        /// <summary>Multi-source BFS over face neighbours from every anchored cube. Cubes it does not reach are loose.</summary>
        Dictionary<Int3, int> ComputeSupportDist()
        {
            SupportBfsRuns++;
            var dist = new Dictionary<Int3, int>(cubes.Count);
            var queue = new Queue<Int3>();
            foreach (var a in anchored)
            {
                if (!cubes.ContainsKey(a)) continue;
                dist[a] = 0;
                queue.Enqueue(a);
            }
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                int next = dist[cur] + 1;
                foreach (var d in Int3.Neighbours)
                {
                    var n = cur + d;
                    if (cubes.ContainsKey(n) && !dist.ContainsKey(n))
                    {
                        dist[n] = next;
                        queue.Enqueue(n);
                    }
                }
            }
            return dist;
        }

        /// <summary>
        /// The visible cube a cannon of this color shoots next. FarthestFirst: the largest support distance, then the
        /// highest, then the closest to the cannon's X, then the smallest X. HighestFirst skips the first key.
        /// Returns false when no cube of this color is visible.
        /// </summary>
        public bool PickTarget(int color, float cannonX, out Int3 target)
        {
            target = default;
            if (color < 0 || color >= byColor.Length) return false;
            bool farthest = Order == TargetOrder.FarthestFirst;
            if (farthest) EnsureSupportDist();
            bool found = false;
            float bestDx = 0;
            int bestDist = 0;
            foreach (var c in byColor[color])
            {
                if (!IsVisible(c)) continue;
                int dist = 0;
                if (farthest && !supportDist.TryGetValue(c, out dist)) dist = int.MaxValue; // loose: cannot happen after DropUnsupported
                float dx = Math.Abs(c.X - cannonX);
                bool better;
                if (!found) better = true;
                else if (farthest && dist != bestDist) better = dist > bestDist;
                else if (c.Y != target.Y) better = c.Y > target.Y;
                else if (dx != bestDx) better = dx < bestDx;
                else better = c.X < target.X;
                if (better)
                {
                    target = c;
                    bestDx = dx;
                    bestDist = dist;
                    found = true;
                }
            }
            return found;
        }

        // ---------------------------------------------------------------- changes

        /// <summary>
        /// Removes one cube and returns the pieces that lost their connection to the ground.
        /// When the support distances are cached they are updated in place: only cubes whose every shortest support
        /// path ran through the removed cube are recomputed (a cube eaten from a limb's tip touches nothing; a cut
        /// touches the severed limb only). The result is identical to a fresh BFS.
        /// </summary>
        public List<FallenPiece> Remove(Int3 c)
        {
            if (!cubes.ContainsKey(c))
            {
                anchored.Remove(c);
                return supportDist != null ? new List<FallenPiece>() : DropUnsupported();
            }
            if (supportDist == null || !supportDist.TryGetValue(c, out int removedDist))
            {
                Delete(c);
                anchored.Remove(c);
                supportDist = null;
                Epoch++;
                return DropUnsupported();
            }

            Delete(c);
            anchored.Remove(c);
            supportDist.Remove(c);
            Epoch++;

            // 1) Cubes that lost every shortest support path (processed by increasing distance).
            var invalid = new HashSet<Int3>();
            var queued = new HashSet<Int3>();
            var queue = new Queue<Int3>();
            foreach (var d in Int3.Neighbours)
            {
                var n = c + d;
                if (supportDist.TryGetValue(n, out int dn) && dn == removedDist + 1 && queued.Add(n)) queue.Enqueue(n);
            }
            while (queue.Count > 0)
            {
                var u = queue.Dequeue();
                int du = supportDist[u];
                bool held = false;
                foreach (var d in Int3.Neighbours)
                {
                    var v = u + d;
                    if (!invalid.Contains(v) && supportDist.TryGetValue(v, out int dv) && dv == du - 1)
                    {
                        held = true;
                        break;
                    }
                }
                if (held) continue;
                invalid.Add(u);
                foreach (var d in Int3.Neighbours)
                {
                    var w = u + d;
                    if (supportDist.TryGetValue(w, out int dw) && dw == du + 1 && queued.Add(w)) queue.Enqueue(w);
                }
            }
            if (invalid.Count == 0) return new List<FallenPiece>();

            // 2) New distances for them, from the cubes around that kept theirs (bucketed BFS, unit steps).
            foreach (var u in invalid) supportDist.Remove(u);
            var best = new Dictionary<Int3, int>();
            var buckets = new List<List<Int3>>();
            foreach (var u in invalid)
            {
                int b = int.MaxValue;
                foreach (var d in Int3.Neighbours)
                    if (supportDist.TryGetValue(u + d, out int dv) && dv + 1 < b) b = dv + 1;
                if (b == int.MaxValue) continue;
                best[u] = b;
                AddToBucket(buckets, b, u);
            }
            for (int dist = 0; dist < buckets.Count; dist++)
            {
                var layer = buckets[dist];
                if (layer == null) continue;
                foreach (var u in layer)
                {
                    if (best[u] != dist) continue;
                    foreach (var d in Int3.Neighbours)
                    {
                        var w = u + d;
                        if (!invalid.Contains(w)) continue;
                        if (best.TryGetValue(w, out int bw) && bw <= dist + 1) continue;
                        best[w] = dist + 1;
                        AddToBucket(buckets, dist + 1, w);
                    }
                }
            }
            foreach (var kv in best) supportDist[kv.Key] = kv.Value;

            // 3) Whatever found no way back to an anchored cube falls.
            if (best.Count == invalid.Count) return new List<FallenPiece>();
            var loose = new HashSet<Int3>();
            foreach (var u in invalid) if (!best.ContainsKey(u)) loose.Add(u);
            return Fall(loose);
        }

        static void AddToBucket(List<List<Int3>> buckets, int dist, Int3 c)
        {
            while (buckets.Count <= dist) buckets.Add(null);
            if (buckets[dist] == null) buckets[dist] = new List<Int3>();
            buckets[dist].Add(c);
        }

        /// <summary>
        /// Drops every cube no longer connected to an anchored cube, grouped into connected pieces (full BFS).
        /// The BFS becomes the new support-distance cache: distances of the cubes that stay do not depend on the
        /// cubes that fall.
        /// </summary>
        public List<FallenPiece> DropUnsupported()
        {
            var supported = ComputeSupportDist();
            supportDist = supported;
            if (supported.Count == cubes.Count) return new List<FallenPiece>();
            var loose = new HashSet<Int3>();
            foreach (var kv in cubes) if (!supported.ContainsKey(kv.Key)) loose.Add(kv.Key);
            return Fall(loose);
        }

        /// <summary>Groups loose cubes into connected pieces (an arm and a head fall separately) and takes them off the titan.</summary>
        List<FallenPiece> Fall(HashSet<Int3> loose)
        {
            var pieces = new List<FallenPiece>();
            var seen = new HashSet<Int3>();
            var queue = new Queue<Int3>();
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
                piece.Part = MajorityPart(piece.Cubes);
                pieces.Add(piece);
            }
            foreach (var piece in pieces)
                foreach (var cube in piece.Cubes)
                    Delete(cube);
            if (pieces.Count > 0) Epoch++;
            return pieces;
        }

        int MajorityPart(List<Int3> list)
        {
            var count = new int[MaxParts];
            foreach (var c in list) count[PartOf(c)]++;
            int best = Head;
            for (int p = 0; p < MaxParts; p++)
                if (count[p] > count[best]) best = p;
            return best;
        }
    }
}
