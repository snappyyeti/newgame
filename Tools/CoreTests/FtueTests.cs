using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Titan.Core;
using Titan.Core.Ads;

namespace Titan.Tests
{
    /// <summary>FTUE-1: the scripted first titan, HoldApproach, the 4 hints, the funnel and the ad gate.</summary>
    public class FtueTests
    {
        static readonly long[] Seeds = { 1, 2, 3, 42, -9, 77777, long.MaxValue, 123456789 };

        static TitanGame Tutorial(long seed, TitanConfig cfg = null) => new TitanGame(cfg ?? new TitanConfig(), seed, 1, 0, null, RunPlan.Tutorial());

        [Test]
        public void TheTutorialTitanHasTheFixedPalette()
        {
            var m = TutorialScript.BuildTutorialTitan();
            var reference = MonsterBuilder.Build(1, new SeededRandom(5));
            Assert.AreEqual(reference.Count, m.Count, "a level-1 titan: same shape as the builder's");
            int y = TutorialScript.Yellow, r = TutorialScript.Red, b = TutorialScript.Blue;
            foreach (var kv in m.Cubes)
            {
                var c = kv.Key;
                if (m.IsJoint(c)) Assert.AreEqual(y, kv.Value, "shoulders and neck are yellow " + c);
                else if (m.PartOf(c) == Monster.LeftArm || m.PartOf(c) == Monster.RightArm) Assert.AreEqual(b, kv.Value, "arms are blue " + c);
                else if (m.PartOf(c) == Monster.Head) Assert.IsTrue(kv.Value == b || kv.Value == y, "head blue, eyes yellow " + c);
                else if (c.Z == 0 && m.IsAnchored(c) && c.Y >= 3) Assert.AreEqual(r, kv.Value, "torso front and belt are red " + c);
                else Assert.AreEqual(y, kv.Value, "legs, boots and the core are yellow " + c);
            }
            Assert.AreEqual(2, m.Cubes.Count(kv => m.PartOf(kv.Key) == Monster.Head && kv.Value == y), "two yellow eyes");
            Assert.AreEqual(0, m.CountOfColor(3) + m.CountOfColor(4) + m.CountOfColor(5), "three colors only");
        }

        [Test]
        public void ScriptedTrayHasTheYellowCuttingCannon()
        {
            foreach (long seed in Seeds)
            {
                var g = Tutorial(seed);
                var c = g.Tray[0];
                Assert.IsNotNull(c);
                Assert.AreEqual(TutorialScript.Yellow, c.Color, "slot 0 is the yellow cannon");
                int expected = TutorialScript.ShotsToSeverAll(g.Monster, TutorialScript.Yellow, g.SlotX(0), g.Config.TargetOrder);
                Assert.AreEqual(expected, c.Ammo, "its ammo is exactly the shots that sever both shoulders and the neck");
                Assert.LessOrEqual(c.Ammo, g.Monster.CountOfColor(TutorialScript.Yellow));
                Assert.GreaterOrEqual(g.ShotsToSever(c), 0, "the TAIE! tag shows on it");
                Assert.IsNotNull(g.Tray[1]);
                Assert.IsNotNull(g.Tray[2]);
                Assert.IsTrue(g.HoldApproach, "the tutorial titan waits for the first tap");
            }
        }

        /// <summary>The giant cut in the first seconds: Deploy(0), then 4.0 s of Tick(0.05), at least 3 pieces fall (both arms and the head).</summary>
        [Test]
        public void ScriptedTitanGiantCut()
        {
            foreach (var order in new[] { TargetOrder.FarthestFirst })
                foreach (long seed in Seeds)
                {
                    var g = Tutorial(seed, new TitanConfig { TargetOrder = order });
                    g.Events.Clear();
                    Assert.IsTrue(g.Deploy(0));
                    var parts = new HashSet<int>();
                    int pieces = 0;
                    for (int i = 0; i < 80 && !g.IsOver; i++) // 80 × 0.05 = 4.0 s
                    {
                        g.Tick(0.05f);
                        foreach (var e in g.Events.Where(e => e.Kind == EventKind.PieceFell))
                        {
                            pieces++;
                            parts.Add(e.Part);
                        }
                        g.Events.Clear();
                    }
                    Assert.GreaterOrEqual(pieces, 3, "seed " + seed + ": at least 3 pieces within 4.0 s");
                    CollectionAssert.IsSupersetOf(parts, new[] { Monster.LeftArm, Monster.RightArm, Monster.Head }, "both arms and the head fall");
                    Assert.AreEqual(1, g.Level, "still titan 1");
                }
        }

        [Test]
        public void ScriptedTitanAmmoEqualsCubesAtSpawn()
        {
            foreach (long seed in Seeds)
            {
                var g = Tutorial(seed);
                int ammo = 0;
                for (int color = 0; color < MonsterBuilder.MaxColors; color++)
                {
                    Assert.AreEqual(g.Monster.CountOfColor(color), g.AmmoAvailable(color), "seed " + seed + ", color " + color + ": ammo == cubes");
                    ammo += g.AmmoAvailable(color);
                }
                Assert.AreEqual(g.Monster.Count, ammo, "total ammo == total cubes");
            }
        }

        [Test]
        public void HoldApproachKeepsTheTitanStillUntilTheFirstDeploy()
        {
            var g = Tutorial(5);
            float start = g.Distance;
            for (int i = 0; i < 1200; i++) g.Tick(0.05f); // 60 s, nobody taps
            Assert.IsFalse(g.IsOver);
            Assert.AreEqual(start, g.Distance, "the titan does not step before the first tap");
            Assert.IsFalse(g.Events.Any(e => e.Kind == EventKind.Step));
            Assert.IsTrue(g.Deploy(1));
            Assert.IsFalse(g.HoldApproach, "the first deploy releases it");
            for (int i = 0; i < 40; i++) g.Tick(0.05f);
            Assert.Less(g.Distance, start, "then it walks");

            // Settable by hand too (the AC case): holds while set, on any titan.
            var h = new TitanGame(new TitanConfig(), 9L) { HoldApproach = true };
            float d0 = h.Distance;
            for (int i = 0; i < 400; i++) h.Tick(0.05f);
            Assert.AreEqual(d0, h.Distance);
        }

        [Test]
        public void OnlyTitanOneOfTheFirstThreeRunsIsScripted()
        {
            var p = new Profile();
            for (int runs = 0; runs < 6; runs++)
            {
                p.totalRuns = runs;
                var plan = Ftue.PlanFor(p);
                Assert.AreEqual(runs < 3, plan != null, "runs " + runs);
            }
            var g = Tutorial(11);
            var reference = new TitanGame(new TitanConfig(), 11L);
            // Titan 2 of a scripted run is the normal seeded titan 2.
            var g2 = new TitanGame(new TitanConfig(), 11L, 2, 0, null, RunPlan.Tutorial());
            var r2 = new TitanGame(new TitanConfig(), 11L, 2);
            CollectionAssert.AreEquivalent(r2.Monster.Cubes.ToDictionary(k => k.Key, k => k.Value), g2.Monster.Cubes.ToDictionary(k => k.Key, k => k.Value));
            Assert.IsFalse(g2.HoldApproach, "only the scripted titan waits");
            Assert.IsTrue(g.HoldApproach);
            Assert.IsFalse(reference.HoldApproach, "an endless titan 1 walks at once");
        }

        // ------------------------------------------------------------ hints

        static (Profile, Telemetry, FtueHints) Hints()
        {
            var p = new Profile();
            var t = new Telemetry(() => p, null, () => 1);
            return (p, t, new FtueHints(p, t));
        }

        /// <summary>A titan whose only cubes are color 0 (anchored) with one hidden color-1 cube behind it.</summary>
        static Monster HiddenColorTitan()
        {
            var m = new Monster();
            for (int x = 0; x < 6; x++) m.Set(new Int3(x, 0, 0), 0, true);
            m.Set(new Int3(0, 0, 1), 1, true);
            m.Set(new Int3(1, 0, 1), 2, true);
            m.Set(new Int3(2, 0, 1), 3, true);
            return m;
        }

        [Test]
        public void H1ShowsOnTheScriptedTitanAndEndsOnTheFirstTap()
        {
            var (p, t, h) = Hints();
            var g = Tutorial(3);
            Assert.IsTrue(h.BeginRun(g));
            Assert.AreEqual(Ftue.H1TapYellow, h.Active);
            Assert.AreEqual("Atinge tunul galben", h.Text);
            for (int i = 0; i < 100; i++)
            {
                g.Tick(0.05f);
                h.Update(g, 0.05f);
            }
            Assert.AreEqual(Ftue.H1TapYellow, h.Active, "it waits for the tap, no timeout");
            g.Deploy(0);
            foreach (var e in g.Events) h.OnEvent(e);
            Assert.AreEqual(0, h.Active);
            CollectionAssert.AreEqual(new[] { "tut_step_1_shown", "tut_step_1_done" }, p.log.Select(l => l.e).Where(e => e.StartsWith("tut_")).ToArray());

            // Once per install: the next scripted run shows nothing.
            Assert.IsFalse(h.BeginRun(Tutorial(4)));
            Assert.AreEqual(0, h.Active);
            // And never on a normal titan 1.
            var (p2, t2, h2) = Hints();
            Assert.IsFalse(h2.BeginRun(new TitanGame(new TitanConfig(), 3L)));
        }

        [Test]
        public void H2AfterOneSecondIdleThenH3NeverTogether()
        {
            var (p, t, h) = Hints();
            p.ftueFlags = Ftue.Bit(Ftue.H1TapYellow);
            var g = new TitanGame(new TitanConfig { MinApproachSeconds = 10000f }, new SeededRandom(1), 1, n => HiddenColorTitan());
            h.BeginRun(g);
            // Park hidden-color cannons by hand (the deck holds colors 1, 2, 3 as hidden only).
            PutOnLine(g, 0, 1);
            int shownAt = -1;
            for (int i = 0; i < 40; i++)
            {
                g.Tick(0.05f);
                if (h.Update(g, 0.05f) && shownAt < 0) shownAt = i + 1;
            }
            Assert.AreEqual(20, shownAt, "H2 after 1.0 s of idle (20 × 0.05)");
            Assert.AreEqual(Ftue.H2Zzz, h.Active);
            Assert.AreEqual(0, h.Slot);
            StringAssert.StartsWith("zzz = culoarea e ascunsă", h.Text);

            // 3 idle cannons while H2 is up: H3 waits (one hint at a time).
            PutOnLine(g, 1, 2);
            PutOnLine(g, 2, 3);
            int h3At = -1, both = 0;
            for (int i = 0; i < 100; i++)
            {
                g.Tick(0.05f);
                bool shown = h.Update(g, 0.05f);
                if (shown && h.Active == Ftue.H3DontFill && h3At < 0) h3At = i;
                if (h.Active != 0 && h.Active != Ftue.H2Zzz && h.Active != Ftue.H3DontFill) both++;
            }
            Assert.Greater(h3At, 0, "H3 follows once H2 is gone");
            Assert.AreEqual(0, h.Active, "H3 lasts 2.5 s");
            var steps = p.log.Select(l => l.e).Where(e => e.StartsWith("tut_")).ToArray();
            CollectionAssert.AreEqual(new[] { "tut_step_2_shown", "tut_step_2_done", "tut_step_3_shown", "tut_step_3_done" }, steps, "never two at once");
            Assert.AreEqual(Ftue.Bit(1) | Ftue.Bit(2) | Ftue.Bit(3), p.ftueFlags);

            // Once per install.
            var g2 = new TitanGame(new TitanConfig { MinApproachSeconds = 10000f }, new SeededRandom(1), 1, n => HiddenColorTitan());
            h.BeginRun(g2);
            PutOnLine(g2, 0, 1);
            PutOnLine(g2, 1, 2);
            PutOnLine(g2, 2, 3);
            for (int i = 0; i < 100; i++)
            {
                g2.Tick(0.05f);
                Assert.IsFalse(h.Update(g2, 0.05f));
            }
        }

        static void PutOnLine(TitanGame g, int slot, int color) => g.Line[slot] = new Cannon(color, 1);

        [Test]
        public void H4GivesTheCauseOfTheFirstGameOverOnly()
        {
            Assert.AreEqual("Toate cele 5 locuri erau pline cu tunuri care așteptau.", FtueText.Cause(LoseReason.SlotsFull));
            Assert.AreEqual("Titanul a ajuns la zid.", FtueText.Cause(LoseReason.WallReached));
            Assert.AreEqual("Niciun tun nu mai avea în ce trage.", FtueText.Cause(LoseReason.Deadlock));
            Assert.IsNull(FtueText.Cause(LoseReason.Completed), "a won daily is not a game over");

            var (p, t, h) = Hints();
            Assert.IsFalse(h.OnRunEnded(LoseReason.Completed));
            Assert.IsTrue(h.OnRunEnded(LoseReason.WallReached));
            Assert.AreEqual(Ftue.H4GameOver, h.Active);
            Assert.AreEqual("Titanul a ajuns la zid.", h.CauseLine);
            Assert.AreEqual("Sfat: lasă mereu 1-2 locuri libere.", h.Text);
            h.LeaveResults();
            Assert.AreEqual(0, h.Active);
            Assert.IsFalse(h.OnRunEnded(LoseReason.SlotsFull), "only the first game over ever");
            CollectionAssert.AreEqual(new[] { "tut_step_4_shown", "tut_step_4_done" }, p.log.Select(l => l.e).Where(e => e.StartsWith("tut_")).ToArray());
        }

        // ------------------------------------------------------------ funnel and ad gate

        [Test]
        public void FunnelEventsAreLogged()
        {
            var (p, t, h) = Hints();
            for (int run = 1; run <= 4; run++)
            {
                var s = new RunSession(p, t, new TitanConfig(), RunStart.Fresh(run), null);
                h.BeginRun(s.Game);
                foreach (var e in s.Game.Events) s.Process(e);
                s.Game.Events.Clear();
                for (int i = 0; i < 4000 && !s.Game.IsOver; i++)
                {
                    s.Game.Tick(0.05f);
                    s.Update(0.05f);
                    foreach (var e in s.Game.Events) s.Process(e);
                    s.Game.Events.Clear();
                }
                Assert.IsTrue(s.Game.IsOver);
            }
            var names = p.log.Select(l => l.e).ToList();
            Assert.AreEqual(1, names.Count(n => n == Telemetry.Run3SameSession), "the 3rd run of the install, same session");
            Assert.AreEqual(1, names.Count(n => n == Telemetry.TitanReached), "titan 1 reached once ever (nobody taps)");
            Assert.AreEqual(4, names.Count(n => n == Telemetry.RunLength));
            var len = p.log.First(l => l.e == Telemetry.RunLength);
            Assert.AreEqual((int)LoseReason.WallReached, len.b);
            Assert.Greater(len.a, 10, "run_length(seconds, reason)");
            Assert.AreEqual("titan_n_reached", Telemetry.TitanReached);
            Assert.AreEqual("run_3_started_same_session", Telemetry.Run3SameSession);
            Assert.AreEqual("tut_step_3_done", Telemetry.TutStep(3, true));
        }

        [Test]
        public void NoAdOrOfferBeforeThreeRunsAndTenMinutes()
        {
            var cfg = new AdConfig();
            Assert.AreEqual(Ftue.ScriptedRuns, cfg.min_lifetime_runs_any_ad);
            Assert.AreEqual(600f, cfg.min_lifetime_play_s_any_ad);
            var clock = new FakeClock();
            var gov = AdKit.ReadyForInter(clock);
            foreach (var (runs, play) in new[] { (0, 5000f), (2, 5000f), (20, 0f), (20, 599f), (2, 599f) })
            {
                var o = AdKit.PassingOffer();
                o.lifetimeRuns = runs;
                o.lifetimePlaySec = play;
                foreach (var placement in new[] { Placement.ReviveSecondWind, Placement.ExtraSlot, Placement.AdBreak })
                {
                    Assert.IsFalse(gov.CanOffer(placement, o, out var why), placement + " at " + runs + " runs / " + play + " s");
                    Assert.AreEqual(OfferReason.FtueGate, why);
                    Assert.IsFalse(gov.FirstFreeAvailable(placement) && gov.CanOffer(placement, o, out _), "free first uses too");
                }
                var i = AdKit.PassingInter();
                i.lifetimeRuns = runs;
                i.lifetimePlaySec = play;
                Assert.IsFalse(gov.ShouldShowInterstitial(i, out _));
            }
        }
    }
}
