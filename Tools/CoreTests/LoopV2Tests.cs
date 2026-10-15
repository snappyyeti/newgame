using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Titan.Core;

namespace Titan.Tests
{
    /// <summary>CORE-3: conveyor tray, titan clock (knockback, stun, near-miss) and the 5-titan cycles.</summary>
    public class LoopV2Tests
    {
        /// <summary>
        /// A tiny titan: a body column (color 0, anchored on the ground row), a joint (color 1) and a horizontal arm of
        /// <paramref name="armCubes"/> cubes (color 2) one row up (the ground row is always anchored).
        /// </summary>
        internal static Monster ArmTitan(int armCubes)
        {
            var m = new Monster();
            m.Set(new Int3(0, 0, 0), 0, true);
            m.Set(new Int3(0, 1, 0), 0);
            m.Set(new Int3(1, 1, 0), 1, false, Monster.Body, true);
            for (int i = 0; i < armCubes; i++) m.Set(new Int3(2 + i, 1, 0), 2, false, Monster.RightArm);
            return m;
        }

        /// <summary>A one-cube titan of color 0.</summary>
        static Monster OneCube()
        {
            var m = new Monster();
            m.Set(new Int3(0, 0, 0), 0, true);
            return m;
        }

        static void TickUntil(TitanGame g, Func<bool> done, float dt = 0.01f, int max = 100000)
        {
            for (int i = 0; i < max && !done() && !g.IsOver; i++) g.Tick(dt);
            Assert.IsTrue(done(), "condition never reached (lost: " + g.Lost + ")");
        }

        // ------------------------------------------------------------ A) conveyor tray

        [Test]
        public void PerSlotDeployRefillsThatSlotAtOnce()
        {
            for (int seed = 0; seed < 40; seed++)
            {
                var g = new TitanGame(new TitanConfig { MinApproachSeconds = 1000f }, new SeededRandom((ulong)seed), 1 + seed % 8);
                Assert.AreEqual(TrayRefill.PerSlot, g.Config.TrayRefill, "PerSlot is the default");
                var r = new SeededRandom((ulong)(1000 + seed));
                for (int k = 0; k < 4 && !g.IsOver; k++)
                {
                    int i = r.Range(0, g.Tray.Length);
                    if (!g.CanDeploy(i)) continue;
                    bool deckHadCannons = g.DeckCount > 0;
                    g.Events.Clear();
                    Assert.IsTrue(g.Deploy(i));
                    if (deckHadCannons) Assert.IsNotNull(g.Tray[i], $"seed {seed}: slot {i} must refill while the deck is not empty");
                    var refill = g.Events.FirstOrDefault(e => e.Kind == EventKind.TrayRefilled);
                    Assert.IsNotNull(refill, "a TrayRefilled event follows the deploy");
                    Assert.AreEqual(i, refill.TraySlot, "PerSlot refills name their slot");
                }
            }
        }

        [Test]
        public void AnEmptyDeckLeavesTheSlotEmpty()
        {
            var g = new TitanGame(new TitanConfig { MinApproachSeconds = 1000f }, new SeededRandom(3), 1, n => ArmTitan(10));
            // Three colors with 2, 1 and 10 cubes: three cannons, all in the tray, nothing in the deck.
            Assert.AreEqual(0, g.DeckCount);
            int slot = Enumerable.Range(0, 3).First(i => g.Tray[i] != null && g.Monster.HasVisibleOfColor(g.Tray[i].Color));
            Assert.IsTrue(g.Deploy(slot));
            Assert.IsNull(g.Tray[slot], "nothing to draw: the slot stays empty ('pachet gol')");
            Assert.IsFalse(g.CanDeploy(slot));
        }

        [Test]
        public void TrayNeverHoldsThreeOfAColorWhenTheDeckHasAnother()
        {
            int pairs = 0, triplesWithoutRule = 0;
            foreach (bool rule in new[] { true, false })
            {
                int seedDraws = 0;
                for (int seed = 0; seedDraws < 500; seed++)
                {
                    var cfg = new TitanConfig { MinApproachSeconds = 1000f, NoTripleColor = rule };
                    var g = new TitanGame(cfg, new SeededRandom((ulong)(seed * 7 + 1)), 1 + seed % 8);
                    var r = new SeededRandom((ulong)(5000 + seed));
                    for (int k = 0; k < 40 && seedDraws < 500 && !g.IsOver && g.DeckCount > 0; k++)
                    {
                        int i = r.Range(0, g.Tray.Length);
                        if (g.Tray[i] == null) continue;
                        var others = Enumerable.Range(0, g.Tray.Length).Where(j => j != i && g.Tray[j] != null).Select(j => g.Tray[j].Color).ToList();
                        bool pair = others.Count == 2 && others[0] == others[1];
                        bool deckHasOther = pair && Enumerable.Range(0, MonsterBuilder.MaxColors).Any(c => c != others[0] && g.DeckAmmo(c) > 0);
                        Assert.IsTrue(g.Deploy(i));
                        // Free the line again so the test can keep drawing (the draw itself is what is checked).
                        for (int s = 0; s < g.ActiveSlots; s++) g.Line[s] = null;
                        seedDraws++;
                        if (!pair || g.Tray[i] == null) continue;
                        bool triple = g.Tray[i].Color == others[0];
                        if (rule)
                        {
                            pairs++;
                            if (deckHasOther) Assert.IsFalse(triple, $"seed {seed}: three cannons of color {others[0]} while the deck held another color");
                        }
                        else if (triple && deckHasOther) triplesWithoutRule++;
                    }
                }
            }
            Assert.Greater(pairs, 25, "the test must hit the 'two of a kind' case often enough");
            Assert.Greater(triplesWithoutRule, 0, "without NoTripleColor the same draws do make triples (the test can fail)");
        }

        [Test]
        public void TrayHoldsThreeCannonsAndBatchKeepsTheSlotEmpty()
        {
            var g = new TitanGame(new TitanConfig { TrayRefill = TrayRefill.Batch }, new SystemRandom(1));
            Assert.IsTrue(g.Tray.All(c => c != null));
            Assert.IsTrue(g.Deploy(1));
            Assert.IsNull(g.Tray[1], "Batch: the slot stays empty until all three are used");
        }

        // ------------------------------------------------------------ B) titan clock

        [Test]
        public void ApproachFollowsTheFormula()
        {
            var cfg = new TitanConfig();
            Assert.AreEqual(Math.Max(12f, 4f + 0.11f * 142), cfg.ApproachSeconds(1, 142), 1e-3, "titan 1: tutorial pace");
            Assert.AreEqual((4f + 0.11f * 200) * (float)Math.Pow(0.98, 2), cfg.ApproachSeconds(3, 200), 1e-3, "titan 3: still tutorial");
            Assert.AreEqual((4f + 0.075f * 477) * (float)Math.Pow(0.98, 4), cfg.ApproachSeconds(5, 477), 1e-3, "titan 5: normal pace");
            Assert.AreEqual((4f + 0.075f * 996) * 0.6f, cfg.ApproachSeconds(60, 996), 1e-3, "the speedup stops at 0.6");
            Assert.AreEqual(12f, cfg.ApproachSeconds(40, 50), 1e-3, "never below 12 s");
        }

        [Test]
        public void FortyCubePiecePushesBackAndStunsTheTitan()
        {
            var cfg = new TitanConfig { MinApproachSeconds = 60f };
            Assert.AreEqual(1.2f, Math.Min(cfg.KnockbackMax, Math.Max(cfg.KnockbackMin, 40 * cfg.KnockbackPerCube)), 1e-5);
            var g = new TitanGame(cfg, new SeededRandom(1), 1, n => ArmTitan(40));
            for (int s = 0; s < g.ActiveSlots; s++) g.Line[s] = null;
            // Let it walk a few steps, then cut right after a step.
            TickUntil(g, () => g.Distance < cfg.StartDistance - 3 * g.StepLength);
            TickUntil(g, () => g.NextStepIn > 0.95f, 0.01f);
            g.Events.Clear();
            float before = g.Distance, nextStep = g.NextStepIn;
            g.Line[0] = new Cannon(1, 1); // the joint color, cooldown 0: it shoots on the next tick
            g.Tick(0.001f);
            var fell = g.Events.Single(e => e.Kind == EventKind.PieceFell);
            Assert.AreEqual(40, fell.Piece.Count);
            Assert.AreEqual(before + 1.2f, g.Distance, 1e-4, "40 cubes × 0.03 = 1.2 back");
            Assert.AreEqual(nextStep - 0.001f + 0.9f, g.NextStepIn, 1e-4, "next step 0.5 + 0.01 × 40 = 0.9 s later");
            var stagger = g.Events.Single(e => e.Kind == EventKind.Staggered);
            Assert.AreEqual(g.Distance, stagger.Distance);
            Assert.AreSame(fell.Piece, stagger.Piece);
        }

        [TestCase(5, 0.3f)]
        [TestCase(100, 1.5f)]
        public void KnockbackIsClamped(int cubes, float expected)
        {
            var cfg = new TitanConfig { MinApproachSeconds = 60f };
            var g = new TitanGame(cfg, new SeededRandom(2), 1, n => ArmTitan(cubes));
            for (int s = 0; s < g.ActiveSlots; s++) g.Line[s] = null;
            TickUntil(g, () => g.Distance < cfg.StartDistance - 3 * g.StepLength);
            TickUntil(g, () => g.NextStepIn > 0.95f, 0.01f);
            float before = g.Distance, nextStep = g.NextStepIn;
            g.Line[0] = new Cannon(1, 1);
            g.Tick(0.001f);
            Assert.AreEqual(before + expected, g.Distance, 1e-4);
            Assert.AreEqual(nextStep - 0.001f + Math.Min(1f, 0.5f + 0.01f * cubes), g.NextStepIn, 1e-4);
        }

        [Test]
        public void DistanceNeverExceedsTheStart()
        {
            // At spawn the titan is at the start: a cut cannot push it further.
            var g = new TitanGame(new TitanConfig(), new SeededRandom(4), 1, n => ArmTitan(60));
            for (int s = 0; s < g.ActiveSlots; s++) g.Line[s] = null;
            g.Line[0] = new Cannon(1, 1);
            g.Tick(0.001f);
            Assert.IsTrue(g.Events.Any(e => e.Kind == EventKind.Staggered));
            Assert.AreEqual(g.Config.StartDistance, g.Distance);

            // And over seeded play with many cuts.
            for (int seed = 0; seed < 40; seed++)
            {
                var game = new TitanGame(new TitanConfig(), new SeededRandom((ulong)seed), 1 + seed % 8);
                var r = new SeededRandom((ulong)(seed + 77));
                for (int t = 0; t < 1200 && !game.IsOver; t++)
                {
                    if (t % 16 == 0) game.Deploy(r.Range(0, game.Tray.Length));
                    game.Tick(0.05f);
                    Assert.LessOrEqual(game.Distance, game.Config.StartDistance);
                }
            }
        }

        [Test]
        public void NearMissMultipliesTheDefeatBonus()
        {
            var cfg = new TitanConfig { MinApproachSeconds = 20f };
            // Far away: plain bonus 200 × 1.
            var far = new TitanGame(cfg, new SeededRandom(1), 1, n => OneCube());
            for (int s = 0; s < far.ActiveSlots; s++) far.Line[s] = null;
            far.Line[0] = new Cannon(0, 1);
            far.Events.Clear();
            far.Tick(0.001f);
            var plain = far.Events.Single(e => e.Kind == EventKind.TitanDefeated);
            Assert.IsFalse(plain.NearMiss);
            Assert.AreEqual(200, plain.Points);

            // Within 15% of the wall: 200 × 1.5 = 300, and the event says so.
            var near = new TitanGame(cfg, new SeededRandom(1), 1, n => OneCube());
            for (int s = 0; s < near.ActiveSlots; s++) near.Line[s] = null;
            TickUntil(near, () => near.Distance <= 0.15f * cfg.StartDistance, 0.05f);
            near.Line[0] = new Cannon(0, 1);
            near.Events.Clear();
            near.Tick(0.001f);
            var close = near.Events.Single(e => e.Kind == EventKind.TitanDefeated);
            Assert.IsTrue(close.NearMiss);
            Assert.AreEqual(300, close.Points);
        }

        [Test]
        public void LeftoverAmmoIsNotMultiplied()
        {
            var cfg = new TitanConfig { MinApproachSeconds = 20f };
            var g = new TitanGame(cfg, new SeededRandom(1), 5, n => OneCube());
            for (int s = 0; s < g.ActiveSlots; s++) g.Line[s] = null;
            TickUntil(g, () => g.Distance <= 0.15f * cfg.StartDistance, 0.05f);
            g.Line[0] = new Cannon(0, 1);
            g.Line[1] = new Cannon(3, 7); // 7 shots left on the wall at the defeat (a color the titan does not have)
            g.Events.Clear();
            g.Tick(0.001f);
            var e = g.Events.Single(x => x.Kind == EventKind.TitanDefeated);
            Assert.AreEqual(TitanRole.Boss, e.Role);
            Assert.AreEqual(200 * 5 * 2 * 3 / 2 + 7 * 2, e.Points, "boss × near-miss on the bonus, leftover added after");
        }

        // ------------------------------------------------------------ C) cycles

        [Test]
        public void CycleMappingTable()
        {
            var cfg = new TitanConfig();
            int[] expected =
            {
                1, 2, 3, 1, 4,      // cycle 1
                2, 3, 4, 1, 5,      // cycle 2
                3, 4, 5, 2, 6,      // cycle 3
                4, 5, 6, 2, 7,      // cycle 4
                5, 6, 6, 2, 8,      // cycle 5
                5, 6, 6, 2, 8,      // cycle 6 (capped)
            };
            for (int n = 1; n <= 30; n++)
            {
                Assert.AreEqual(expected[n - 1], cfg.BuildLevel(n), "titan " + n);
                var role = (n - 1) % 5 == 3 ? TitanRole.Sprint : (n - 1) % 5 == 4 ? TitanRole.Boss : TitanRole.Normal;
                Assert.AreEqual(role, cfg.RoleOf(n), "titan " + n);
            }
            Assert.AreEqual(4, cfg.BuildLevel(5));
            Assert.AreEqual(TitanRole.Boss, cfg.RoleOf(5));
            Assert.AreEqual(1, cfg.BuildLevel(9));
            Assert.AreEqual(TitanRole.Sprint, cfg.RoleOf(9));
            Assert.AreEqual(2, cfg.BuildLevel(14));
            Assert.AreEqual(8, cfg.BuildLevel(25));
            var off = new TitanConfig { UseCycles = false };
            Assert.AreEqual(17, off.BuildLevel(17));
            Assert.AreEqual(TitanRole.Normal, off.RoleOf(5));
        }

        [Test]
        public void SpawnUsesTheCycleLevelAndAnnouncesTheRole()
        {
            for (int n = 1; n <= 15; n++)
            {
                var g = new TitanGame(new TitanConfig(), new SeededRandom((ulong)n), n);
                Assert.AreEqual(n, g.Level, "the titan number stays n");
                Assert.AreEqual(g.Config.BuildLevel(n), g.BuildLevel);
                Assert.AreEqual(MonsterBuilder.Build(g.BuildLevel, new SeededRandom((ulong)n)).Count, g.Monster.Count, "built at the cycle level");
                var spawned = g.Events.Single(e => e.Kind == EventKind.TitanSpawned);
                Assert.AreEqual(g.Config.RoleOf(n), spawned.Role);
                Assert.AreEqual(g.Role, spawned.Role);
            }
        }

        [Test]
        public void BossBonusDoublesOnEveryFifthTitan()
        {
            var cfg = new TitanConfig { MinApproachSeconds = 1000f };
            for (int n = 1; n <= 15; n++)
            {
                var g = new TitanGame(cfg, new SeededRandom(1), n, k => OneCube());
                for (int s = 0; s < g.ActiveSlots; s++) g.Line[s] = null;
                g.Line[0] = new Cannon(0, 1);
                g.Events.Clear();
                g.Tick(0.001f);
                var e = g.Events.Single(x => x.Kind == EventKind.TitanDefeated);
                Assert.AreEqual(200 * n * (n % 5 == 0 ? 2 : 1), e.Points, "titan " + n);
            }
        }

        [Test]
        public void BossExtraColorAddsAColorOnlyWhenOn()
        {
            int with = 0, without = 0;
            for (int seed = 0; seed < 20; seed++)
            {
                without += new TitanGame(new TitanConfig(), new SeededRandom((ulong)seed), 5).Monster.Cubes.Select(kv => kv.Value).Distinct().Count();
                with += new TitanGame(new TitanConfig { BossExtraColor = true }, new SeededRandom((ulong)seed), 5).Monster.Cubes.Select(kv => kv.Value).Distinct().Count();
            }
            Assert.Greater(with, without);
        }
    }

    /// <summary>CORE-4: the last-slot guard, the honest deadlock and the hidden-color count.</summary>
    public class ThreatTests
    {
        /// <summary>A game whose titan has a color with cubes but none visible; the line is emptied.</summary>
        static TitanGame WithHiddenColor(out int hidden, out int visible, TitanConfig cfg = null)
        {
            for (int seed = 0; seed < 400; seed++)
            {
                var g = new TitanGame(cfg ?? new TitanConfig { MinApproachSeconds = 1000f }, new SeededRandom((ulong)seed), 1 + seed % 6);
                hidden = Enumerable.Range(0, MonsterBuilder.MaxColors).FirstOrDefault(c => g.Monster.CountOfColor(c) > 0 && !g.Monster.HasVisibleOfColor(c), -1);
                // The hidden color's deck must absorb the test cannons below, so salvage never touches the tray.
                // and a visible color's deck absorbs the visible test cannon, so salvage never touches the tray.
                if (hidden < 0 || g.DeckAmmo(hidden) < 12) continue;
                visible = Enumerable.Range(0, MonsterBuilder.MaxColors).FirstOrDefault(c => g.Monster.HasVisibleOfColor(c) && g.DeckAmmo(c) >= 4, -1);
                if (visible < 0) continue;
                for (int s = 0; s < g.ActiveSlots; s++) g.Line[s] = null;
                g.Events.Clear();
                return g;
            }
            Assert.Fail("no titan with a hidden-only color");
            hidden = visible = -1;
            return null;
        }

        static void Park(TitanGame g, int color, int count)
        {
            for (int s = 0; s < count; s++) g.Line[s] = new Cannon(color, 1);
        }

        [Test]
        public void WouldJamBlocksADeployThatUsedToEndTheGame()
        {
            var g = WithHiddenColor(out int hidden, out int visible);
            Park(g, hidden, 4);
            g.Tray[0] = new Cannon(hidden, 1);
            g.Tray[1] = new Cannon(visible, 1);
            Assert.AreEqual(DeployBlockReason.WouldJam, g.DeployBlock(0));
            Assert.IsFalse(g.CanDeploy(0));
            Assert.IsFalse(g.Deploy(0), "the guard refuses the tap");
            Assert.IsFalse(g.IsOver);
            Assert.AreEqual(DeployBlockReason.None, g.DeployBlock(1), "a visible color is still allowed");

            // What that tap did before the guard: the 5th idle cannon ended the game at once.
            g.Line[4] = g.Tray[0];
            g.Tick(0.01f);
            Assert.AreEqual(LoseReason.SlotsFull, g.Lost);
        }

        [Test]
        public void GuardOnlyAppliesToTheLastSlotWhenNothingFires()
        {
            var g = WithHiddenColor(out int hidden, out int visible);
            g.Tray[0] = new Cannon(hidden, 1);
            Park(g, hidden, 3);
            Assert.AreEqual(DeployBlockReason.None, g.DeployBlock(0), "2 free slots: allowed");
            Park(g, hidden, 4);
            g.Line[0] = new Cannon(visible, 1);
            Assert.AreEqual(DeployBlockReason.None, g.DeployBlock(0), "a line cannon still fires: allowed");
            for (int s = 0; s < g.ActiveSlots; s++) g.Line[s] = new Cannon(hidden, 1);
            Assert.AreEqual(DeployBlockReason.LineFull, g.DeployBlock(0));
            g.Tray[2] = null;
            Assert.AreEqual(DeployBlockReason.NotAvailable, g.DeployBlock(2));
            Assert.AreEqual(DeployBlockReason.NotAvailable, g.DeployBlock(-1));
        }

        [Test]
        public void DeadlockEndsTheRunAtExactlyOneAndAHalfSeconds()
        {
            var g = WithHiddenColor(out int hidden, out _);
            Park(g, hidden, 4);
            for (int i = 0; i < g.Tray.Length; i++) g.Tray[i] = new Cannon(hidden, 1);
            for (int i = 0; i < 29; i++)
            {
                g.Tick(0.05f);
                Assert.IsFalse(g.IsOver, "still alive at " + ((i + 1) * 0.05f) + " s");
            }
            Assert.AreEqual(1.45f, g.StuckSeconds, 1e-3);
            g.Tick(0.05f);
            Assert.AreEqual(LoseReason.Deadlock, g.Lost, "BLOCAT! at 1.5 s");
            Assert.IsTrue(g.Events.Any(e => e.Kind == EventKind.GameOver && e.Reason == LoseReason.Deadlock));
        }

        [Test]
        public void DeadlockAlsoCoversAnEmptyTrayWithOneFreeSlot()
        {
            var g = WithHiddenColor(out int hidden, out _);
            Park(g, hidden, 4);
            for (int i = 0; i < g.Tray.Length; i++) g.Tray[i] = null;
            for (int i = 0; i < 31 && !g.IsOver; i++) g.Tick(0.05f);
            Assert.AreEqual(LoseReason.Deadlock, g.Lost);
        }

        [Test]
        public void NoDeadlockWhileAnyUsefulMoveExists()
        {
            // 1 free slot, but a tray cannon has a visible color: the player can still act.
            var a = WithHiddenColor(out int hidden, out int visible);
            Park(a, hidden, 4);
            a.Tray[0] = new Cannon(hidden, 1);
            a.Tray[1] = new Cannon(visible, 1);
            a.Tray[2] = new Cannon(hidden, 1);
            for (int i = 0; i < 100; i++) a.Tick(0.05f);
            Assert.IsFalse(a.IsOver);
            Assert.AreEqual(0f, a.StuckSeconds);
        }

        [Test]
        public void TwoFreeSlotsWithOnlyHiddenTrayCannonsNeverLose()
        {
            var g = WithHiddenColor(out int hidden, out _);
            Park(g, hidden, 3);
            for (int i = 0; i < g.Tray.Length; i++) g.Tray[i] = new Cannon(hidden, 1);
            for (int i = 0; i < 200; i++) g.Tick(0.05f);
            Assert.IsFalse(g.IsOver, "lost: " + g.Lost);
            Assert.AreEqual(0f, g.StuckSeconds);
        }

        [Test]
        public void DeadlockTimerResetsWhenAColorBecomesVisible()
        {
            var g = WithHiddenColor(out int hidden, out int visible);
            Park(g, hidden, 4);
            for (int i = 0; i < g.Tray.Length; i++) g.Tray[i] = new Cannon(hidden, 1);
            for (int i = 0; i < 20; i++) g.Tick(0.05f);
            Assert.AreEqual(1.0f, g.StuckSeconds, 1e-3);
            g.Tray[1] = new Cannon(visible, 1); // a useful tap exists again
            g.Tick(0.05f);
            Assert.AreEqual(0f, g.StuckSeconds);
            g.Tray[1] = new Cannon(hidden, 1);
            for (int i = 0; i < 29; i++) g.Tick(0.05f);
            Assert.IsFalse(g.IsOver, "the full 1.5 s starts again");
            g.Tick(0.05f);
            Assert.AreEqual(LoseReason.Deadlock, g.Lost);
        }

        [Test]
        public void OwnTapNeverEndsTheGame()
        {
            // Random taps over many seeded games: a tap that the game accepts never loses on the spot.
            int taps = 0;
            for (int seed = 0; seed < 60; seed++)
            {
                var g = new TitanGame(new TitanConfig(), new SeededRandom((ulong)seed), 1 + seed % 10);
                var r = new SeededRandom((ulong)(seed + 900));
                for (int t = 0; t < 2400 && !g.IsOver; t++)
                {
                    if (t % 12 == 0)
                    {
                        int i = r.Range(0, g.Tray.Length);
                        if (g.Deploy(i))
                        {
                            taps++;
                            Assert.IsFalse(g.IsOver, $"seed {seed}: the tap itself ended the game ({g.Lost})");
                        }
                    }
                    g.Tick(0.05f);
                }
            }
            Assert.Greater(taps, 1000);
        }

        [Test]
        public void VisibleAndHiddenColorCounts()
        {
            var g = WithHiddenColor(out int hidden, out int visible);
            var counts = g.VisibleCountByColor();
            Assert.AreEqual(MonsterBuilder.MaxColors, counts.Length);
            Assert.AreEqual(0, counts[hidden]);
            Assert.Greater(counts[visible], 0);
            int expected = Enumerable.Range(0, MonsterBuilder.MaxColors).Count(c => g.Monster.CountOfColor(c) > 0 && !g.Monster.HasVisibleOfColor(c));
            Assert.AreEqual(expected, g.HiddenOnlyColorCount());
            Assert.GreaterOrEqual(expected, 1);
            for (int c = 0; c < MonsterBuilder.MaxColors; c++)
                Assert.AreEqual(g.Monster.Cubes.Count(kv => kv.Value == c && g.Monster.IsVisible(kv.Key)), counts[c]);
            counts[visible] = -5; // a copy: the caller cannot corrupt the cache
            Assert.Greater(g.VisibleCountByColor()[visible], 0);
        }
    }
}
