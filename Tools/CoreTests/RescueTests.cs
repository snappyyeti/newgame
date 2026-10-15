using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Titan.Core;
using Titan.Core.Ads;
using Titan.Sim;

namespace Titan.Tests
{
    /// <summary>ADS-2: TitanGame.Revive, the "+1 Loc" slot, the rescue eligibility and the wall button timing.</summary>
    public class ReviveTests
    {
        /// <summary>
        /// Plays a seeded game with a "careless" tapper that prefers hidden colors (so the line fills with idle cannons
        /// and the run ends by Deadlock, or at the wall) until it is lost. Null if it survives 600 s.
        /// </summary>
        internal static TitanGame PlayUntilLost(int seed, TitanConfig cfg = null)
        {
            var g = new TitanGame(cfg ?? new TitanConfig(), new SeededRandom((ulong)(5000 + seed)), 1 + seed % 6);
            var r = new SeededRandom((ulong)(9000 + seed));
            float next = 0f;
            for (int i = 0; i < 12000 && !g.IsOver; i++)
            {
                if (g.GameTime >= next)
                {
                    var options = Enumerable.Range(0, g.Tray.Length).Where(g.CanDeploy).ToList();
                    var hidden = options.Where(k => !g.Monster.HasVisibleOfColor(g.Tray[k].Color)).ToList();
                    var pool = hidden.Count > 0 ? hidden : options;
                    if (pool.Count > 0) g.Deploy(pool[r.Range(0, pool.Count)]);
                    next = g.GameTime + 0.3f + r.Value() * 0.4f;
                }
                if (!g.IsOver) g.Tick(0.05f);
            }
            return g.IsOver ? g : null;
        }

        static Dictionary<Int3, int> Cubes(Monster m) => m.Cubes.ToDictionary(kv => kv.Key, kv => kv.Value);

        static int[] Ammo(TitanGame g) => Enumerable.Range(0, MonsterBuilder.MaxColors).Select(g.AmmoAvailable).ToArray();

        /// <summary>The stuck condition of DESIGN.md 4.2.9 right now.</summary>
        static bool Stuck(TitanGame g)
        {
            bool firing = false, trayVisible = false;
            for (int s = 0; s < g.ActiveSlots; s++) if (g.Line[s] != null && g.Monster.HasVisibleOfColor(g.Line[s].Color)) firing = true;
            foreach (var c in g.Tray) if (c != null && g.Monster.HasVisibleOfColor(c.Color)) trayVisible = true;
            return !firing && (g.FreeSlots == 0 || (g.FreeSlots == 1 && !trayVisible));
        }

        [Test]
        public void ReviveKeepsTheTitanAndTheAmmo()
        {
            var reasons = new Dictionary<LoseReason, int>();
            int returnedTotal = 0;
            for (int seed = 0; seed < 60; seed++)
            {
                var g = PlayUntilLost(seed);
                if (g == null) continue;
                reasons[g.Lost] = reasons.TryGetValue(g.Lost, out int n) ? n + 1 : 1;
                Assert.IsTrue(g.CanRevive);
                var cubes = Cubes(g.Monster);
                var before = Ammo(g);
                int idleLive = 0, idleLiveAmmo = 0;
                for (int s = 0; s < g.ActiveSlots; s++)
                {
                    var c = g.Line[s];
                    if (c == null || g.Monster.HasVisibleOfColor(c.Color) || g.Monster.CountOfColor(c.Color) == 0) continue;
                    idleLive++;
                    idleLiveAmmo += c.Ammo;
                }
                int deckBefore = g.DeckCount;
                float distBefore = g.Distance;
                g.Events.Clear();

                Assert.IsTrue(g.Revive(0.4f, 2f), "seed " + seed);
                Assert.IsFalse(g.IsOver);
                Assert.AreEqual(1, g.Revives);
                CollectionAssert.AreEquivalent(cubes, Cubes(g.Monster), "seed " + seed + ": every cube stays (same titan, same damage)");
                for (int s = 0; s < g.ActiveSlots; s++)
                    if (g.Line[s] != null) Assert.IsTrue(g.Monster.HasVisibleOfColor(g.Line[s].Color), "only firing cannons stay on the wall");
                for (int color = 0; color < MonsterBuilder.MaxColors; color++)
                {
                    int left = g.Monster.CountOfColor(color);
                    if (left == 0) continue;
                    Assert.GreaterOrEqual(g.AmmoAvailable(color), left, "seed " + seed + ": never short of ammo");
                    if (before[color] <= left) Assert.AreEqual(before[color], g.AmmoAvailable(color), "seed " + seed + ": no live ammo dropped (color " + color + ")");
                }
                Assert.IsNull(SalvageTests.Violation(g), "seed " + seed + ": the salvage invariant holds");
                if (idleLive > 0) Assert.Greater(g.DeckCount + g.Tray.Count(c => c != null), deckBefore, "idle cannons with live colors go back into the deck");
                returnedTotal += idleLive;
                Assert.AreEqual(Math.Min(g.Config.StartDistance, distBefore + 0.4f * g.Config.StartDistance), g.Distance, 1e-4);
                Assert.AreEqual(g.Config.StepInterval + 2f, g.NextStepIn, 1e-4, "2 s of grace before the next step");
                Assert.AreEqual(0f, g.StuckSeconds);
                Assert.IsTrue(g.Events.Any(e => e.Kind == EventKind.Revived));
                Assert.IsFalse(Stuck(g), "seed " + seed + ": the revived game is never in the 'blocat' state");

                // Ticking on: no Deadlock right after a revive while the deck holds a visible-color cannon.
                for (int i = 0; i < 40 && !g.IsOver; i++) g.Tick(0.05f);
                Assert.AreNotEqual(LoseReason.Deadlock, g.Lost, "seed " + seed);
            }
            TestContext.WriteLine("losses revived: " + string.Join(", ", reasons.Select(kv => kv.Key + " " + kv.Value)) + "; idle cannons returned " + returnedTotal);
            Assert.That(reasons.ContainsKey(LoseReason.WallReached), "the harness produces wall losses");
            Assert.That(reasons.ContainsKey(LoseReason.SlotsFull), "the harness produces full-line losses");
            Assert.Greater(returnedTotal, 0);
        }

        /// <summary>
        /// The careless tapper with BLOCAT! off: when the line holds 4 idle cannons and the tray only hidden colors, a
        /// hidden tray cannon is moved onto the last slot (tray → line, ammo conserved; the jam guard would refuse
        /// the tap) and the next tick ends the run with SlotsFull. Null if that never happens.
        /// </summary>
        static TitanGame PlayUntilJammed(int seed)
        {
            var g = new TitanGame(new TitanConfig { DeadlockSeconds = 1000f, MinApproachSeconds = 400f }, new SeededRandom((ulong)(700 + seed)), 2 + seed % 5);
            var r = new SeededRandom((ulong)(800 + seed));
            float next = 0f;
            for (int i = 0; i < 8000 && !g.IsOver; i++)
            {
                bool firing = Enumerable.Range(0, g.ActiveSlots).Any(s => g.Line[s] != null && g.Monster.HasVisibleOfColor(g.Line[s].Color));
                int hiddenTray = Enumerable.Range(0, g.Tray.Length).FirstOrDefault(t => g.Tray[t] != null && !g.Monster.HasVisibleOfColor(g.Tray[t].Color), -1);
                if (g.FreeSlots == 1 && !firing && hiddenTray >= 0)
                {
                    int slot = Enumerable.Range(0, g.ActiveSlots).First(s => g.Line[s] == null);
                    g.Line[slot] = g.Tray[hiddenTray];
                    g.Tray[hiddenTray] = null;
                    g.Tick(0.01f);
                    return g.Lost == LoseReason.SlotsFull ? g : null;
                }
                if (g.GameTime >= next)
                {
                    var options = Enumerable.Range(0, g.Tray.Length).Where(g.CanDeploy).ToList();
                    var hidden = options.Where(k => !g.Monster.HasVisibleOfColor(g.Tray[k].Color)).ToList();
                    var pool = hidden.Count > 0 ? hidden : options;
                    if (pool.Count > 0) g.Deploy(pool[r.Range(0, pool.Count)]);
                    next = g.GameTime + 0.3f + r.Value() * 0.3f;
                }
                if (!g.IsOver) g.Tick(0.05f);
            }
            return null;
        }

        [Test]
        public void ReviveAfterSlotsFullRedrawsTheTrayFromVisibleColors()
        {
            int cases = 0, redrawn = 0;
            for (int seed = 0; seed < 120 && cases < 12; seed++)
            {
                var g = PlayUntilJammed(seed);
                if (g == null) continue;
                Assert.AreEqual(LoseReason.SlotsFull, g.Lost);
                bool trayHadVisible = g.Tray.Any(c => c != null && g.Monster.HasVisibleOfColor(c.Color));
                var before = Ammo(g);
                var cubes = Cubes(g.Monster);
                g.Events.Clear();
                Assert.IsTrue(g.Revive(0.4f, 2f));
                CollectionAssert.AreEquivalent(cubes, Cubes(g.Monster));
                Assert.AreEqual(g.ActiveSlots, g.FreeSlots, "every idle cannon left the wall");
                for (int color = 0; color < MonsterBuilder.MaxColors; color++)
                {
                    int left = g.Monster.CountOfColor(color);
                    if (left > 0 && before[color] <= left) Assert.AreEqual(before[color], g.AmmoAvailable(color), "seed " + seed + " color " + color);
                }
                Assert.IsNull(SalvageTests.Violation(g), "seed " + seed);
                if (!trayHadVisible)
                {
                    redrawn++;
                    Assert.IsTrue(g.Events.Any(e => e.Kind == EventKind.TrayRefilled && e.TraySlot == -1), "the view rebuilds the tray");
                    bool visibleExists = Enumerable.Range(0, MonsterBuilder.MaxColors).Any(c => g.Monster.HasVisibleOfColor(c));
                    if (visibleExists) Assert.IsTrue(g.Tray.Any(c => c != null && g.Monster.HasVisibleOfColor(c.Color)), "seed " + seed + ": the tray is redrawn from visible colors (bias 1.0)");
                }
                Assert.IsFalse(Stuck(g));
                for (int i = 0; i < 40 && !g.IsOver; i++) g.Tick(0.05f);
                Assert.IsFalse(g.IsOver, "seed " + seed + ": 2 s of grace, no jam");
                cases++;
            }
            TestContext.WriteLine(cases + " SlotsFull revives, " + redrawn + " with a redrawn tray");
            Assert.GreaterOrEqual(cases, 5, "the harness builds full-line losses");
        }

        [Test]
        public void ReviveAfterBlocatIsNeverBlocatAgain()
        {
            // BLOCAT! never happens to the bots (DESIGN.md 4.3.2), so it is built: 4 idle cannons of a hidden color on
            // the wall and only hidden colors in the tray, held for 1.5 s.
            int cases = 0;
            for (int seed = 0; seed < 300 && cases < 15; seed++)
            {
                var g = new TitanGame(new TitanConfig { MinApproachSeconds = 1000f }, new SeededRandom((ulong)seed), 3 + seed % 18);
                int hidden = Enumerable.Range(0, MonsterBuilder.MaxColors).FirstOrDefault(c => g.Monster.CountOfColor(c) > 0 && !g.Monster.HasVisibleOfColor(c), -1);
                bool deckHasVisible = Enumerable.Range(0, MonsterBuilder.MaxColors).Any(c => g.Monster.HasVisibleOfColor(c) && g.DeckAmmo(c) > 0);
                if (hidden < 0 || !deckHasVisible) continue;
                for (int s = 0; s < g.ActiveSlots; s++) g.Line[s] = s < 4 ? new Cannon(hidden, 1) : null;
                for (int i = 0; i < g.Tray.Length; i++) g.Tray[i] = new Cannon(hidden, 1);
                for (int i = 0; i < 31 && !g.IsOver; i++) g.Tick(0.05f);
                Assert.AreEqual(LoseReason.Deadlock, g.Lost, "seed " + seed);
                var cubes = Cubes(g.Monster);
                g.Events.Clear();
                Assert.IsTrue(g.Revive(0.4f, 2f));
                CollectionAssert.AreEquivalent(cubes, Cubes(g.Monster));
                Assert.AreEqual(g.ActiveSlots, g.FreeSlots, "the 4 sleeping cannons went back into the deck");
                Assert.IsTrue(g.Tray.Any(c => c != null && g.Monster.HasVisibleOfColor(c.Color)), "seed " + seed + ": the tray is redrawn from visible colors (bias 1.0)");
                Assert.IsTrue(g.Events.Any(e => e.Kind == EventKind.TrayRefilled && e.TraySlot == -1));
                Assert.IsFalse(Stuck(g));
                for (int i = 0; i < 60 && !g.IsOver; i++) g.Tick(0.05f);
                Assert.IsFalse(g.IsOver, "seed " + seed + ": not BLOCAT! again (3 s later)");
                cases++;
            }
            Assert.GreaterOrEqual(cases, 5, "built enough BLOCAT! losses");
        }

        [Test]
        public void ReviveOnlyAfterARevivableLoss()
        {
            var g = new TitanGame(new TitanConfig(), new SeededRandom(3));
            Assert.IsFalse(g.CanRevive);
            Assert.IsFalse(g.Revive(0.4f, 2f), "not over");
            Assert.AreEqual(0, g.Revives);
        }

        [Test]
        public void ReviveIsImpossibleTwiceInOneRun()
        {
            var g = PlayUntilLost(1);
            Assert.IsNotNull(g);
            var gov = AdKit.Governor(new FakeClock());
            var ctx = AdKit.PassingOffer();
            ctx.revivesUsedThisRun = g.Revives;
            ctx.titansThisRun = g.TitansDefeated;
            ctx.progressFraction = g.ProgressFraction;
            ctx.titansThisRun = Math.Max(1, ctx.titansThisRun);
            Assert.IsTrue(gov.CanOffer(Placement.ReviveSecondWind, ctx, out string r0), r0);
            Assert.IsTrue(g.Revive(gov.Config.revive_push_back, gov.Config.revive_grace_s));
            for (int i = 0; i < 20000 && !g.IsOver; i++) g.Tick(0.05f);
            Assert.IsTrue(g.IsOver, "lost again");
            ctx.revivesUsedThisRun = g.Revives;
            Assert.IsFalse(gov.CanOffer(Placement.ReviveSecondWind, ctx, out string r1), "the governor refuses a second revive");
            Assert.AreEqual(OfferReason.ReviveUsed, r1);
        }

        [Test]
        public void ReviveNeedsRealProgress()
        {
            var gov = AdKit.Governor(new FakeClock());
            var ctx = AdKit.PassingOffer();
            ctx.titansThisRun = 0;
            ctx.progressFraction = 0.149f;
            Assert.IsFalse(gov.CanOffer(Placement.ReviveSecondWind, ctx, out string r));
            Assert.AreEqual(OfferReason.NoProgress, r);
            ctx.progressFraction = 0.15f;
            Assert.IsTrue(gov.CanOffer(Placement.ReviveSecondWind, ctx, out _));
            ctx.progressFraction = 0f;
            ctx.titansThisRun = 1;
            Assert.IsTrue(gov.CanOffer(Placement.ReviveSecondWind, ctx, out _));
        }

        [Test]
        public void RescuesShipOnlyWithSalvage()
        {
            var gov = AdKit.Governor(new FakeClock());
            var ctx = AdKit.PassingOffer();
            ctx.salvageActive = false;
            foreach (var p in new[] { Placement.ReviveSecondWind, Placement.ExtraSlot })
            {
                Assert.IsFalse(gov.CanOffer(p, ctx, out string r), p.ToString());
                Assert.AreEqual(OfferReason.NoSalvage, r);
            }
            Assert.IsTrue(new TitanGame(new TitanConfig(), new SeededRandom(1)).SalvageActive);
            Assert.IsFalse(new TitanGame(new TitanConfig { SalvageEnabled = false }, new SeededRandom(1)).SalvageActive);
        }
    }

    public class ExtraSlotTests
    {
        static Monster OneCube(int color)
        {
            var m = new Monster();
            m.Set(new Int3(0, 0, 0), color, true);
            return m;
        }

        /// <summary>A game on a two-color titan whose line holds 5 cannons of the visible color.</summary>
        static TitanGame FullLine()
        {
            var g = new TitanGame(new TitanConfig { MinApproachSeconds = 1000f }, new SeededRandom(11), 3);
            int visible = Enumerable.Range(0, MonsterBuilder.MaxColors).First(c => g.Monster.HasVisibleOfColor(c));
            for (int s = 0; s < g.ActiveSlots; s++) g.Line[s] = new Cannon(visible, 1);
            return g;
        }

        [Test]
        public void GrantOpensASixthSlotOncePerTitan()
        {
            var g = FullLine();
            Assert.AreEqual(g.Config.LineSlots + 1, g.Line.Length, "Line is sized LineSlots + 1");
            Assert.AreEqual(5, g.ActiveSlots);
            Assert.AreEqual(DeployBlockReason.LineFull, g.DeployBlock(0));
            Assert.IsFalse(g.Deploy(0));
            Assert.IsNull(g.Line[5], "slot 5 stays empty before the grant");
            g.Events.Clear();
            Assert.IsTrue(g.GrantExtraSlot());
            Assert.AreEqual(6, g.ActiveSlots);
            var added = g.Events.Single(e => e.Kind == EventKind.SlotAdded);
            Assert.AreEqual(5, added.Slot);
            Assert.AreEqual(7.2f, g.SlotX(5), 1e-5, "the new slot sits right of the others, nobody moves");
            Assert.AreEqual(-4.8f, g.SlotX(0), 1e-5);
            Assert.IsFalse(g.GrantExtraSlot(), "once per titan");
            Assert.AreEqual(1, g.FreeSlots);
            Assert.IsTrue(g.Deploy(0));
            Assert.IsNotNull(g.Line[5], "the deploy fills slot 5 after the grant");
            Assert.AreEqual(5, g.Events.Last(e => e.Kind == EventKind.Deployed).Slot);
        }

        [Test]
        public void TheSlotFoldsAwayWithTheNextTitan()
        {
            var g = new TitanGame(new TitanConfig { MinApproachSeconds = 1000f }, new SeededRandom(5), 1, n => OneCube(0));
            Assert.IsTrue(g.GrantExtraSlot());
            for (int s = 0; s < 5; s++) g.Line[s] = new Cannon(1, 1); // no color 1 on this titan: idle, but slot 5 is free
            Assert.AreEqual(1, g.FreeSlots);
            int i = Enumerable.Range(0, g.Tray.Length).First(k => g.Tray[k] != null && g.Tray[k].Color == 0);
            Assert.IsTrue(g.Deploy(i));
            Assert.IsNotNull(g.Line[5]);
            for (int k = 0; k < 40 && g.Level == 1; k++) g.Tick(0.05f);
            Assert.AreEqual(2, g.Level, "the cannon in slot 5 defeated the titan");
            Assert.AreEqual(5, g.ActiveSlots, "SpawnTitan resets it");
            Assert.IsNull(g.Line[5]);
            Assert.IsTrue(g.GrantExtraSlot(), "a new titan, a new chance");
        }

        [Test]
        public void EveryLineLoopRespectsActiveSlots()
        {
            var g = new TitanGame(new TitanConfig { MinApproachSeconds = 1000f }, new SeededRandom(11), 3);
            int visible = Enumerable.Range(0, MonsterBuilder.MaxColors).First(c => g.Monster.HasVisibleOfColor(c));
            int cubes = g.Monster.CountOfColor(visible);
            g.Line[5] = new Cannon(visible, 5); // a stray cannon in the closed slot
            for (int i = 0; i < 40; i++) g.Tick(0.05f);
            Assert.AreEqual(cubes, g.Monster.CountOfColor(visible), "a closed slot never fires");
            Assert.AreEqual(5, g.FreeSlots, "and is never counted as free");
            Assert.AreEqual(0, g.DeploySlot);
            g.Line[5] = null;

            // Slots-full and the jam guard look at the active slots only.
            int hidden = Enumerable.Range(0, MonsterBuilder.MaxColors).FirstOrDefault(c => g.Monster.CountOfColor(c) > 0 && !g.Monster.HasVisibleOfColor(c), -1);
            if (hidden >= 0)
            {
                for (int s = 0; s < 4; s++) g.Line[s] = new Cannon(hidden, 1);
                g.Tray[0] = new Cannon(hidden, 1);
                Assert.AreEqual(DeployBlockReason.WouldJam, g.DeployBlock(0));
                Assert.IsTrue(g.GrantExtraSlot());
                Assert.AreEqual(DeployBlockReason.None, g.DeployBlock(0), "two free slots after the grant: the tap is allowed");
                g.Line[4] = new Cannon(hidden, 1);
                Assert.AreEqual(DeployBlockReason.WouldJam, g.DeployBlock(0), "the guard watches slot 5 now");
            }
        }

        [Test]
        public void SimulatorCountsOnlyActiveSlots()
        {
            // The 6th Line entry exists from the start (closed): bots and stats must not see it as a free slot.
            var g = new TitanGame(new TitanConfig(), new SeededRandom(2));
            Assert.AreEqual(5, SimRunner.Free(g));
            Assert.AreEqual(g.FreeSlots, SimRunner.Free(g));
            g.GrantExtraSlot();
            Assert.AreEqual(6, SimRunner.Free(g));
        }

        [Test]
        public void ExtraSlotEligibility()
        {
            var gov = AdKit.Governor(new FakeClock());
            gov.State.FirstFreeExtraSlotUsed = true;
            var ctx = AdKit.PassingOffer();
            Assert.IsTrue(gov.CanOffer(Placement.ExtraSlot, ctx, out _));
            var x = AdKit.PassingOffer();
            x.titanLevel = 1;
            Assert.IsFalse(gov.CanOffer(Placement.ExtraSlot, x, out string r1));
            Assert.AreEqual(OfferReason.FirstTitan, r1);
            x = AdKit.PassingOffer();
            x.helpUsedThisTitan = true;
            Assert.IsFalse(gov.CanOffer(Placement.ExtraSlot, x, out string r2));
            Assert.AreEqual(OfferReason.UsedThisTitan, r2);
            x = AdKit.PassingOffer();
            x.helpsUsedThisRun = 2;
            Assert.IsFalse(gov.CanOffer(Placement.ExtraSlot, x, out string r3));
            Assert.AreEqual(OfferReason.HelpBudget, r3);
            x = AdKit.PassingOffer();
            x.rewardedReady = false;
            Assert.IsFalse(gov.CanOffer(Placement.ExtraSlot, x, out string r4));
            Assert.AreEqual(OfferReason.NotReady, r4);
            gov.State.FirstFreeExtraSlotUsed = false;
            Assert.IsTrue(gov.CanOffer(Placement.ExtraSlot, x, out _), "the free first use needs no ad");
            x = AdKit.PassingOffer();
            x.isDailyRanked = true;
            Assert.IsFalse(gov.CanOffer(Placement.ExtraSlot, x, out string r5));
            Assert.AreEqual(OfferReason.DailyRanked, r5);
            x = AdKit.PassingOffer();
            x.lifetimeRuns = 2;
            Assert.IsFalse(gov.CanOffer(Placement.ExtraSlot, x, out string r6));
            Assert.AreEqual(OfferReason.FtueGate, r6);
        }

        [Test]
        public void WallButtonTiming()
        {
            var cfg = new AdConfig();
            var b = new ExtraSlotButton(cfg);
            float t = 0f;
            void Step(float seconds, int occupied, int idle, bool eligible = true, int titan = 3)
            {
                for (float k = 0; k < seconds - 1e-4f; k += 0.05f)
                {
                    t += 0.05f;
                    b.Update(0.05f, t, titan, occupied, idle, eligible);
                }
            }
            Step(2f, 4, 2);
            Assert.IsFalse(b.Visible, "needs >= 3 idle");
            Step(0.95f, 4, 3);
            Assert.IsFalse(b.Visible, "held for less than 1.0 s");
            t += 0.05f;
            b.Update(0.05f, t, 3, 4, 3, true);
            Assert.IsTrue(b.Visible, "held continuously for 1.0 s");
            Assert.IsTrue(b.JustAppeared, "one pulse on appear");
            Step(0.05f, 4, 3);
            Assert.IsFalse(b.JustAppeared);
            Step(1.9f, 3, 1);
            Assert.IsTrue(b.Visible, "stays while the condition is false for < 2 s");
            Step(0.15f, 3, 1);
            Assert.IsFalse(b.Visible, "hides after 2 s");

            // Decline: hidden for 20 s of game time.
            Step(1.05f, 5, 4);
            Assert.IsTrue(b.Visible);
            b.OnDeclined(t, 3);
            Step(19.9f, 5, 4);
            Assert.IsFalse(b.Visible);
            Step(1.2f, 5, 4);
            Assert.IsTrue(b.Visible, "back after 20 s");
            // Second decline: not on this titan, not on the next one.
            b.OnDeclined(t, 3);
            Step(30f, 5, 4);
            Assert.IsFalse(b.Visible);
            Step(30f, 5, 4, true, 4);
            Assert.IsFalse(b.Visible, "not shown again on the next titan");
            Step(1.05f, 5, 4, true, 5);
            Assert.IsTrue(b.Visible, "the titan after that");
            Step(0.1f, 5, 4, false, 5);
            Assert.IsFalse(b.Visible, "ineligible (budget spent, no ad): hidden");
        }

        [Test]
        public void TrayTapsNeverOpenAnOfferCard()
        {
            var gov = AdKit.Governor(new FakeClock());
            int blocked = 0;
            for (int seed = 0; seed < 30; seed++)
            {
                var g = ReviveTests.PlayUntilLost(seed) ?? new TitanGame(new TitanConfig(), new SeededRandom((ulong)seed));
                // Taps on a finished game and on full lines are all refused...
                for (int i = -1; i <= g.Tray.Length; i++)
                    if (TrayTap.Handle(g, i) != DeployBlockReason.None) blocked++;
                var h = new TitanGame(new TitanConfig { MinApproachSeconds = 1000f }, new SeededRandom((ulong)seed), 3);
                int hidden = Enumerable.Range(0, MonsterBuilder.MaxColors).FirstOrDefault(c => h.Monster.CountOfColor(c) > 0 && !h.Monster.HasVisibleOfColor(c), -1);
                for (int s = 0; s < h.ActiveSlots; s++) h.Line[s] = new Cannon(hidden >= 0 ? hidden : 0, 1);
                for (int i = 0; i < h.Tray.Length; i++)
                    if (TrayTap.Handle(h, i) != DeployBlockReason.None) blocked++;
                if (hidden >= 0)
                {
                    h.Line[4] = null;
                    h.Tray[0] = new Cannon(hidden, 1);
                    if (TrayTap.Handle(h, 0) == DeployBlockReason.WouldJam) blocked++;
                }
            }
            Assert.Greater(blocked, 100);
            Assert.AreEqual(0, gov.OffersShownTotal, "...and no ad card ever opened");
            Assert.IsNull(gov.ActiveSurface);
        }
    }
}
