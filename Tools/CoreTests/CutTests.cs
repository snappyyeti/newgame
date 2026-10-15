using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Titan.Core;

namespace Titan.Tests
{
    /// <summary>CORE-2: earned cuts (farthest-first targeting, part tags, the honest "TAIE!" prediction).</summary>
    public class CutTests
    {
        static int ArmColor(Monster m, int armPart) => m.ColorAt(m.Cubes.First(kv => m.PartOf(kv.Key) == armPart).Key);

        [TestCase(1)]
        [TestCase(4)]
        [TestCase(7)]
        public void LimbCannonNeverSeversLimb(int level)
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var m = MonsterBuilder.Build(level, new SeededRandom((ulong)seed));
                Assert.AreEqual(TargetOrder.FarthestFirst, m.Order);
                int arm = ArmColor(m, Monster.LeftArm);
                Assert.AreEqual(arm, ArmColor(m, Monster.RightArm));
                foreach (var j in m.Joints) Assert.AreNotEqual(arm, m.ColorAt(j), "seed " + seed + ": an arm color never matches a joint");
                int shots = 0;
                foreach (float x in new[] { -4.8f, 0f, 4.8f })
                {
                    var copy = m.Clone();
                    while (copy.PickTarget(arm, x, out var t))
                    {
                        var pieces = copy.Remove(t);
                        shots++;
                        Assert.IsEmpty(pieces, "level " + level + " seed " + seed + ": shot " + shots + " at " + t + " cut a piece with the arm color");
                    }
                    Assert.AreEqual(0, copy.RemainingPartSize(Monster.LeftArm) + copy.RemainingPartSize(Monster.RightArm), "both arms are eaten completely");
                }
            }
        }

        [TestCase(1)]
        [TestCase(5)]
        [TestCase(8)]
        public void JointCutDropsWholeArm(int level)
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var m = MonsterBuilder.Build(level, new SeededRandom((ulong)seed));
                var gloves = m.Cubes.Where(kv => m.IsGlove(kv.Key) && m.PartOf(kv.Key) == Monster.LeftArm).Select(kv => kv.Key).ToList();
                Assert.IsNotEmpty(gloves);
                int arm = ArmColor(m, Monster.LeftArm);
                Assert.IsTrue(gloves.All(g => m.ColorAt(g) == arm), "the hand has the arm color");
                Assert.IsTrue(gloves.All(g => g.Y == gloves.Min(x => x.Y)), "gloves are the bottom row of the arm");

                var fallen = new List<FallenPiece>();
                foreach (var j in m.Joints.Where(j => j.X < 0 && m.Has(j)).ToList()) fallen.AddRange(m.Remove(j));
                Assert.AreEqual(1, fallen.Count, "seed " + seed);
                var piece = fallen[0];
                Assert.AreEqual(Monster.LeftArm, piece.Part);
                Assert.AreEqual(m.OriginalPartSize(Monster.LeftArm), piece.Count, "the whole arm falls, gloves included");
                Assert.IsTrue(gloves.All(piece.Cubes.Contains));
                Assert.AreEqual(0, m.RemainingPartSize(Monster.LeftArm));
            }
        }

        [Test]
        public void PartApiCountsTheBuild()
        {
            for (int level = 1; level <= 8; level++)
            {
                var m = MonsterBuilder.Build(level, new SeededRandom((ulong)(100 + level)));
                for (int p = 0; p < Monster.MaxParts; p++)
                {
                    int tagged = m.Cubes.Count(kv => m.PartOf(kv.Key) == p);
                    Assert.AreEqual(tagged, m.OriginalPartSize(p), "level " + level + " part " + p);
                    Assert.AreEqual(tagged, m.RemainingPartSize(p));
                }
                Assert.Greater(m.OriginalPartSize(Monster.LeftArm), 0);
                Assert.AreEqual(m.OriginalPartSize(Monster.LeftArm), m.OriginalPartSize(Monster.RightArm));
                Assert.Greater(m.OriginalPartSize(Monster.Head), 0);
                Assert.IsTrue(m.Joints.All(j => m.PartOf(j) == Monster.Body), "joints stay with the body");
                Assert.IsTrue(m.Joints.All(j => !m.IsAnchored(j)));
                Assert.AreEqual(m.OriginalPartSize(4), 0, "4+ reserved");

                var armCube = m.Cubes.First(kv => m.PartOf(kv.Key) == Monster.RightArm && kv.Key.Y == m.Cubes.Where(c => m.PartOf(c.Key) == Monster.RightArm).Max(c => c.Key.Y) && m.IsVisible(kv.Key)).Key;
                int before = m.RemainingPartSize(Monster.RightArm);
                m.Remove(armCube);
                Assert.AreEqual(before - 1, m.RemainingPartSize(Monster.RightArm));
                Assert.AreEqual(m.OriginalPartSize(Monster.LeftArm), m.OriginalPartSize(Monster.RightArm), "original sizes never shrink");
                Assert.AreEqual(Monster.RightArm, m.PartOf(armCube), "tags stay with the position");
            }
        }

        [Test]
        public void HeadWinsTiesForThePiecePart()
        {
            var m = new Monster();
            m.Set(new Int3(0, 0, 0), 0, true);
            m.Set(new Int3(0, 1, 0), 1, false, Monster.Body, true);
            m.Set(new Int3(0, 2, 0), 2, false, Monster.Head);
            m.Set(new Int3(1, 2, 0), 2, false, Monster.LeftArm);
            var pieces = m.Remove(new Int3(0, 1, 0));
            Assert.AreEqual(1, pieces.Count);
            Assert.AreEqual(Monster.Head, pieces[0].Part);
        }

        [Test]
        public void FarthestFirstEatsALimbFromTheTip()
        {
            // An anchored post with an L-shaped arm: (1,3) shoulder, (2,3) elbow, (2,2) (2,1) forearm hanging down.
            var m = new Monster();
            for (int y = 0; y < 4; y++) m.Set(new Int3(0, y, 0), 0, true);
            m.Set(new Int3(1, 3, 0), 1, false, Monster.Body, true);
            m.Set(new Int3(2, 3, 0), 2, false, Monster.RightArm);
            m.Set(new Int3(2, 2, 0), 2, false, Monster.RightArm);
            m.Set(new Int3(2, 1, 0), 2, false, Monster.RightArm);
            Assert.AreEqual(4, m.SupportDistance(new Int3(2, 1, 0)));
            Assert.IsTrue(m.PickTarget(2, 0f, out var t));
            Assert.AreEqual(new Int3(2, 1, 0), t, "the hand first, although it is the lowest");
            m.Order = TargetOrder.HighestFirst;
            Assert.IsTrue(m.PickTarget(2, 0f, out t));
            Assert.AreEqual(new Int3(2, 3, 0), t);
        }

        [Test]
        public void PickTargetIsFastAndCachesTheBfs()
        {
            var m = MonsterBuilder.Build(8, new SeededRandom(8));
            Assert.Greater(m.Count, 900);
            var colors = Enumerable.Range(0, MonsterBuilder.MaxColors).Where(c => m.CountOfColor(c) > 0).ToArray();
            m.PickTarget(colors[0], 0f, out _); // JIT warm-up, first BFS
            int runs = m.SupportBfsRuns;
            var sw = Stopwatch.StartNew();
            int found = 0;
            for (int i = 0; i < 1000; i++)
                if (m.PickTarget(colors[i % colors.Length], (i % 5 - 2) * 2.4f, out _)) found++;
            sw.Stop();
            Assert.AreEqual(runs, m.SupportBfsRuns, "PickTarget must reuse the cached support distances");
            Assert.Greater(found, 0);
            TestContext.Progress.WriteLine("[perf] 1000 PickTarget calls on a level-8 titan (" + m.Count + " cubes): " + sw.Elapsed.TotalMilliseconds.ToString("F1") + " ms");
            Assert.Less(sw.Elapsed.TotalMilliseconds, 50, "1000 PickTarget calls on a level-8 titan");

            var cube = m.Cubes.First(kv => m.IsVisible(kv.Key) && !m.IsAnchored(kv.Key)).Key;
            m.Remove(cube);
            int afterRemove = m.SupportBfsRuns;
            m.PickTarget(colors[0], 0f, out _);
            m.PickTarget(colors[1], 0f, out _);
            Assert.AreEqual(afterRemove, m.SupportBfsRuns, "the BFS of DropUnsupported is reused as the new cache");
        }

        /// <summary>A copy rebuilt cube by cube: no cache, so its Remove runs the full BFS.</summary>
        static Monster Rebuild(Monster m)
        {
            var f = new Monster { Order = m.Order };
            foreach (var kv in m.Cubes) f.Set(kv.Key, kv.Value, m.IsAnchored(kv.Key));
            return f;
        }

        static string Key(IEnumerable<Int3> cubes) => string.Join(";", cubes.Select(c => c.ToString()).OrderBy(x => x, System.StringComparer.Ordinal));

        [Test]
        public void IncrementalSupportMatchesAFullBfs()
        {
            int cuts = 0;
            for (int level = 1; level <= 8; level++)
                for (int seed = 0; seed < 3; seed++)
                {
                    var m = MonsterBuilder.Build(level, new SeededRandom((ulong)(seed * 31 + level)));
                    var pick = new SeededRandom((ulong)(seed + 1000 * level));
                    m.SupportDistance(m.Cubes.First().Key); // cache on: every Remove below is incremental
                    int bfsRuns = m.SupportBfsRuns;
                    for (int step = 0; step < 120 && m.Count > 0; step++)
                    {
                        var all = m.Cubes.Select(kv => kv.Key).ToList();
                        // Mostly visible cubes (what cannons hit), sometimes any cube, joints and anchored body included.
                        var visible = all.Where(m.IsVisible).ToList();
                        var pool = pick.Value() < 0.7f && visible.Count > 0 ? visible : all;
                        var target = pool[pick.Range(0, pool.Count)];
                        var reference = Rebuild(m);
                        var expected = reference.Remove(target);
                        var got = m.Remove(target);
                        Assert.AreEqual(Key(expected.SelectMany(p => p.Cubes)), Key(got.SelectMany(p => p.Cubes)), "level " + level + " step " + step + ": fallen cubes");
                        Assert.AreEqual(expected.Count, got.Count, "piece count");
                        if (got.Count > 0) cuts++;
                        Assert.AreEqual(reference.Count, m.Count);
                        foreach (var kv in m.Cubes)
                            Assert.AreEqual(reference.SupportDistance(kv.Key), m.SupportDistance(kv.Key), "level " + level + " step " + step + " " + kv.Key);
                    }
                    Assert.AreEqual(bfsRuns, m.SupportBfsRuns, "no full BFS while the cache is warm");
                }
            Assert.Greater(cuts, 5);
        }

        // ---------------------------------------------------------------- TAIE!

        static readonly TitanConfig SlowConfig = new TitanConfig { MinApproachSeconds = 10000f };

        /// <summary>
        /// Rebuilds the same game state: same seed, same level, then <paramref name="warmup"/> seconds of seeded random
        /// taps. Deterministic, so a tray cannon can be checked in one copy and deployed in another.
        /// </summary>
        static TitanGame Replay(int seed, int level, float warmup)
        {
            var g = new TitanGame(SlowConfig, new SeededRandom(SeededRandom.Derive(4242, (ulong)seed)), level);
            var taps = new SeededRandom((ulong)seed);
            float next = 0.7f;
            for (float t = 0; t < warmup && !g.IsOver; t += 0.05f)
            {
                if (t >= next)
                {
                    next += 0.9f;
                    var legal = Enumerable.Range(0, g.Tray.Length).Where(g.CanDeploy).ToList();
                    if (legal.Count > 0) g.Deploy(legal[taps.Range(0, legal.Count)]);
                }
                g.Tick(0.05f);
            }
            g.Events.Clear();
            return g;
        }

        /// <summary>Deploys tray cannon i alone on an emptied line and returns the 1-based shot that made a piece fall (0 = none).</summary>
        static int DeployAloneAndWatch(TitanGame g, int i, out int fired)
        {
            for (int s = 0; s < g.ActiveSlots; s++) g.Line[s] = null;
            var cannon = g.Tray[i];
            int startLevel = g.Level;
            Assert.IsTrue(g.Deploy(i));
            fired = 0;
            for (int step = 0; step < 4000 && !g.IsOver && g.Level == startLevel; step++)
            {
                g.Tick(0.05f);
                foreach (var e in g.Events)
                {
                    if (e.Kind == EventKind.Shot && e.Slot == 0) fired++;
                    if (e.Kind == EventKind.PieceFell) return fired;
                }
                g.Events.Clear();
                if (g.Line[0] != cannon) break; // emptied or salvaged
                if (g.Line[0].Cooldown == 0f && !g.Monster.HasVisibleOfColor(cannon.Color)) break; // waiting forever alone
            }
            return 0;
        }

        [Test]
        public void TaieTagIsHonest()
        {
            int tags = 0, noTags = 0;
            for (int seed = 0; seed < 100; seed++)
            {
                int level = 1 + seed % 8;
                float warmup = (seed / 8) % 3 * 6f; // at spawn, after 6 s and after 12 s of play
                var probe = Replay(seed, level, warmup);
                if (probe.IsOver) continue;
                for (int s = 0; s < probe.ActiveSlots; s++) probe.Line[s] = null; // alone on an empty line
                for (int i = 0; i < probe.Tray.Length; i++)
                {
                    var c = probe.Tray[i];
                    if (c == null) continue;
                    int predicted = probe.ShotsToSever(c);
                    var g = Replay(seed, level, warmup);
                    Assert.AreEqual(c.Color, g.Tray[i].Color, "replay must rebuild the same tray");
                    int cutAt = DeployAloneAndWatch(g, i, out int fired);
                    string ctx = "seed " + seed + " level " + level + " t " + warmup + " tray " + i + " " + c;
                    if (predicted >= 0)
                    {
                        tags++;
                        Assert.Greater(cutAt, 0, ctx + ": TAIE! shown but no piece fell");
                        Assert.LessOrEqual(cutAt, c.Ammo, ctx);
                        Assert.AreEqual(predicted + 1, cutAt, ctx + ": predicted shot");
                    }
                    else
                    {
                        noTags++;
                        Assert.AreEqual(0, cutAt, ctx + ": a piece fell without a TAIE! tag");
                    }
                }
            }
            TestContext.Progress.WriteLine("[taie] tagged " + tags + ", untagged " + noTags);
            Assert.Greater(tags, 10, "the test must see real TAIE! tags");
            Assert.Greater(noTags, 10);
        }

        [Test]
        public void ShotsToSeverIsCachedPerEpoch()
        {
            var g = new TitanGame(SlowConfig, new SeededRandom(3), 8);
            var c = g.Tray.First(x => x != null);
            var sw = Stopwatch.StartNew();
            int a = g.ShotsToSever(c);
            double first = sw.Elapsed.TotalMilliseconds;
            for (int i = 0; i < 1000; i++) Assert.AreEqual(a, g.ShotsToSever(c));
            TestContext.Progress.WriteLine("[perf] ShotsToSever on level 8: first " + first.ToString("F2") + " ms, 1000 cached calls " + (sw.Elapsed.TotalMilliseconds - first).ToString("F2") + " ms");
            Assert.Less(sw.Elapsed.TotalMilliseconds - first, 50);
        }

        [Test]
        public void TargetOrderComesFromTheConfig()
        {
            Assert.AreEqual(TargetOrder.FarthestFirst, new TitanConfig().TargetOrder);
            var g = new TitanGame(new TitanConfig { TargetOrder = TargetOrder.HighestFirst }, new SeededRandom(1));
            Assert.AreEqual(TargetOrder.HighestFirst, g.Monster.Order);
        }
    }
}
