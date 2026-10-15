using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Titan.Core;

namespace Titan.Tests
{
    /// <summary>CORE-5: clean cuts, chains (one multiplier model) and the 3-star titan grade.</summary>
    public class ScoringTests
    {
        static readonly TitanConfig Slow = new TitanConfig { MinApproachSeconds = 100000f };

        /// <summary>Anchored body column at x = 0 (rows 0..rows-1, color 0) with an arm at every row in <paramref name="armRows"/>:
        /// a joint at x = 1 (color = jointColors[i]) and <paramref name="armCubes"/> arm cubes (color 2) to the right.</summary>
        static Monster ArmsTitan(int rows, int[] armRows, int[] jointColors, int armCubes, int[] parts = null)
        {
            var m = new Monster();
            for (int y = 0; y < rows; y++) m.Set(new Int3(0, y, 0), 0, true);
            for (int i = 0; i < armRows.Length; i++)
            {
                int y = armRows[i];
                m.Set(new Int3(1, y, 0), jointColors[i], false, Monster.Body, true);
                int part = parts != null ? parts[i] : Monster.RightArm;
                for (int x = 0; x < armCubes; x++) m.Set(new Int3(2 + x, y, 0), 2, false, part);
            }
            return m;
        }

        /// <summary>A body column of <paramref name="bodyRows"/> (color 0, anchored), a neck (color 1, joint) and a 3-wide head (color 2).</summary>
        static Monster NeckTitan(int bodyRows)
        {
            var m = new Monster();
            for (int y = 0; y < bodyRows; y++) m.Set(new Int3(0, y, 0), 0, true);
            m.Set(new Int3(0, bodyRows, 0), 1, false, Monster.Body, true);
            for (int x = -1; x <= 1; x++) m.Set(new Int3(x, bodyRows + 1, 0), 2, false, Monster.Head);
            m.Set(new Int3(0, bodyRows + 2, 0), 2, false, Monster.Head);
            return m;
        }

        static TitanGame Game(Func<int, Monster> titan, TitanConfig cfg = null)
        {
            var g = new TitanGame(cfg ?? Slow, new SeededRandom(7), 1, titan);
            for (int i = 0; i < g.Tray.Length; i++) g.Tray[i] = null; // the tests place cannons on the line themselves
            g.Events.Clear();
            return g;
        }

        /// <summary>Puts a cannon straight onto the line (ready to fire on the next tick).</summary>
        static void Place(TitanGame g, int slot, int color, int ammo) => g.Line[slot] = new Cannon(color, ammo) { Cooldown = 0f };

        static void Run(TitanGame g, float seconds)
        {
            for (float t = 0; t < seconds - 1e-4f && !g.IsOver; t += 0.05f) g.Tick(0.05f);
        }

        static List<GameEvent> Pieces(TitanGame g) => g.Events.Where(e => e.Kind == EventKind.PieceFell).ToList();

        /// <summary>Shoots the titan down one cannon at a time (never more than one on the wall) and returns the defeat event.</summary>
        static GameEvent Finish(TitanGame g)
        {
            for (int guard = 0; guard < 20000; guard++)
            {
                var defeated = g.Events.FirstOrDefault(e => e.Kind == EventKind.TitanDefeated);
                if (defeated != null) return defeated;
                Assert.IsFalse(g.IsOver, "the titan must not win in a scoring test");
                if (g.Line.All(c => c == null))
                {
                    int color = Enumerable.Range(0, MonsterBuilder.MaxColors).First(c => g.Monster.HasVisibleOfColor(c));
                    Place(g, 0, color, 1000);
                }
                g.Tick(0.05f);
            }
            Assert.Fail("the titan was never defeated");
            return null;
        }

        [Test]
        public void AnIntactArmCutIsCleanAndDoubled()
        {
            var g = Game(n => ArmsTitan(3, new[] { 1 }, new[] { 1 }, 10));
            Assert.IsTrue(g.IsCleanEligible(Monster.RightArm), "an intact arm is clean-eligible (gold stitch rim)");
            Place(g, 0, 1, 1);
            Run(g, 0.1f);
            var e = Pieces(g).Single();
            Assert.AreEqual(Monster.RightArm, e.Part);
            Assert.IsTrue(e.Clean);
            Assert.AreEqual(1, e.Chain);
            Assert.AreEqual(2, e.Mult);
            Assert.AreEqual(10 * 25 * 2, e.Points);
            Assert.AreEqual(1, g.CleanCuts);
            Assert.AreEqual(10 * 25 * 2, g.MaxPiecePoints);
            Assert.IsFalse(g.IsCleanEligible(Monster.RightArm), "a fallen arm is no longer eligible");
            Assert.IsFalse(g.IsCleanEligible(Monster.Body), "the body never falls");
        }

        [TestCase(2, true)]  // 8 of 10 left: exactly 80%, still clean
        [TestCase(3, false)] // 7 of 10: eroded below 80%
        [TestCase(6, false)]
        public void AnErodedArmIsNotClean(int eaten, bool clean)
        {
            var g = Game(n => ArmsTitan(3, new[] { 1 }, new[] { 1 }, 10));
            for (int x = 0; x < eaten; x++) Assert.AreEqual(0, g.Monster.Remove(new Int3(11 - x, 1, 0)).Count, "eating from the tip cuts nothing");
            Assert.AreEqual(clean, g.IsCleanEligible(Monster.RightArm));
            Place(g, 0, 1, 1);
            Run(g, 0.1f);
            var e = Pieces(g).Single();
            Assert.AreEqual(10 - eaten, e.Piece.Count);
            Assert.AreEqual(clean, e.Clean);
            Assert.AreEqual((10 - eaten) * 25 * (clean ? 2 : 1), e.Points);
        }

        [Test]
        public void ChainGrowsWithinFourSecondsAndResetsAfter()
        {
            var g = Game(n => ArmsTitan(7, new[] { 1, 3, 5 }, new[] { 1, 3, 4 }, 3));
            Place(g, 0, 1, 1);
            Run(g, 0.05f);
            Assert.AreEqual(1, Pieces(g).Single().Chain);
            Assert.Greater(g.ChainTimeLeft, 3.9f);
            g.Events.Clear();

            Run(g, 3.5f); // 3.5 s later: still in the window
            Assert.Greater(g.ChainTimeLeft, 0f);
            Place(g, 1, 3, 1);
            Run(g, 0.05f);
            var second = Pieces(g).Single();
            Assert.AreEqual(2, second.Chain);
            Assert.AreEqual(2 * (second.Clean ? 2 : 1), second.Mult);
            g.Events.Clear();

            Run(g, 4.5f); // past the window
            Assert.AreEqual(0f, g.ChainTimeLeft);
            Place(g, 2, 4, 1);
            Run(g, 0.05f);
            Assert.AreEqual(1, Pieces(g).Single().Chain, "the chain starts over after 4 s");
            Assert.AreEqual(2, g.MaxChain);
        }

        [Test]
        public void ChainMultiplierCapsAtFive()
        {
            var rows = new[] { 1, 3, 5, 7, 9, 11, 13 };
            var g = Game(n => ArmsTitan(15, rows, rows.Select(_ => 1).ToArray(), 3));
            Place(g, 0, 1, rows.Length); // one cannon, 0.3 s between shots: every arm inside the window
            Run(g, 3f);
            var pieces = Pieces(g);
            Assert.AreEqual(rows.Length, pieces.Count);
            for (int i = 0; i < pieces.Count; i++)
            {
                Assert.AreEqual(i + 1, pieces[i].Chain);
                int clean = pieces[i].Clean ? 2 : 1;
                Assert.AreEqual(clean * Math.Min(i + 1, 5), pieces[i].Mult, "piece " + i);
                Assert.AreEqual(3 * 25 * pieces[i].Mult, pieces[i].Points);
            }
            Assert.AreEqual(7, g.MaxChain);
        }

        [Test]
        public void ChainResetsOnANewTitan()
        {
            var g = Game(n => ArmsTitan(3, new[] { 1 }, new[] { 1 }, 3));
            Place(g, 0, 1, 1);
            Run(g, 0.05f);
            Assert.AreEqual(1, g.Chain);
            Finish(g);
            Assert.AreEqual(2, g.Level);
            Assert.AreEqual(0, g.Chain);
            Assert.AreEqual(0f, g.ChainTimeLeft);
            for (int i = 0; i < g.Tray.Length; i++) g.Tray[i] = null;
            g.Events.Clear();
            Place(g, 0, 1, 1);
            Run(g, 0.05f);
            Assert.AreEqual(1, Pieces(g).Single().Chain, "titan 2's first piece starts its own chain");
        }

        [Test]
        public void PiecePointsAreTheOnlyMultipliedPoints()
        {
            var g = Game(n => ArmsTitan(7, new[] { 1, 3, 5 }, new[] { 1, 1, 1 }, 4));
            int before = g.Score;
            Place(g, 0, 1, 3);
            Run(g, 1.2f);
            var shots = g.Events.Where(e => e.Kind == EventKind.Shot).Sum(e => e.Points);
            var pieces = Pieces(g);
            Assert.AreEqual(3, pieces.Count);
            Assert.AreEqual(shots + pieces.Sum(e => e.Points) + g.Events.Where(e => e.Kind == EventKind.Salvaged).Sum(e => e.Points), g.Score - before);
            Assert.AreEqual(pieces.Max(e => e.Points), g.MaxPiecePoints);
            Assert.AreEqual(3, g.PiecesCut);
        }

        [Test]
        public void ANeckOnlyCutEarnsTheDecapitationStar()
        {
            var g = Game(n => NeckTitan(6));
            Place(g, 0, 1, 1);
            Run(g, 0.1f);
            var head = Pieces(g).Single();
            Assert.AreEqual(Monster.Head, head.Part);
            Assert.IsTrue(head.Clean);
            Assert.AreEqual("DECAPITARE!", HudText.PieceLabel(head.Clean, head.Part));
            Assert.IsTrue(g.DecapitatedThisTitan);
            var defeat = Finish(g);
            Assert.AreEqual(Stars.Defeated | Stars.Decapitation | Stars.CleanLine, defeat.Stars);
            Assert.AreEqual(1, defeat.MaxLine);
            Assert.AreEqual(1, g.HeadsCut);
            Assert.AreEqual(3, g.StarsEarned);
            Assert.AreEqual(1, g.TitansDefeated);
            Assert.IsFalse(g.DecapitatedThisTitan, "the next titan starts without the star");
        }

        [Test]
        public void AnErodedHeadEarnsNoDecapitation()
        {
            var g = Game(n => NeckTitan(6));
            g.Monster.Remove(new Int3(0, 8, 0)); // eat the top of the head: 3 of 4 cubes left (75%)
            Place(g, 0, 1, 1);
            Run(g, 0.1f);
            var head = Pieces(g).Single();
            Assert.IsFalse(head.Clean);
            Assert.AreEqual("BUCATĂ RUPTĂ!", HudText.PieceLabel(head.Clean, head.Part));
            var defeat = Finish(g);
            Assert.AreEqual(0, defeat.Stars & Stars.Decapitation);
            Assert.AreEqual("Capul nu a căzut întreg", HudText.MissingStarReason(Stars.Decapitation, defeat.MaxLine));
        }

        /// <summary>CleanLineMax = 4 by default (tuned in Tools/Sim, DESIGN.md 6.3); the spec's case "≤ 3 cannons gives s3" holds for both 3 and 4.</summary>
        [TestCase(4, 3, true)]
        [TestCase(4, 4, true)]
        [TestCase(4, 5, false)]
        [TestCase(3, 3, true)]
        [TestCase(3, 4, false)]
        public void TheCleanLineStarCountsTheFullestLine(int cleanLineMax, int cannons, bool star)
        {
            Assert.AreEqual(4, new TitanConfig().CleanLineMax, "the tuned default");
            var cfg = new TitanConfig { MinApproachSeconds = 100000f, CleanLineMax = cleanLineMax };
            var g = Game(n => NeckTitan(6), cfg);
            for (int s = 0; s < cannons; s++) Place(g, s, 0, 1); // k cannons on the wall at once
            Run(g, 0.05f);
            Assert.AreEqual(cannons, g.MaxOccupiedLine);
            Assert.AreEqual(star, g.CleanLineAlive);
            var defeat = Finish(g);
            Assert.AreEqual(star, (defeat.Stars & Stars.CleanLine) != 0);
            Assert.AreEqual(cannons, defeat.MaxLine);
            Assert.AreNotEqual(0, defeat.Stars & Stars.Defeated);
        }

        [Test]
        public void DeployTracksTheFullestLine()
        {
            var g = new TitanGame(Slow, new SeededRandom(11), 5);
            Assert.AreEqual(0, g.MaxOccupiedLine);
            int deployed = 0;
            for (int k = 0; k < 10 && deployed < 4; k++)
                for (int i = 0; i < g.Tray.Length; i++)
                    if (g.CanDeploy(i) && g.Deploy(i))
                    {
                        deployed++;
                        break;
                    }
            Assert.AreEqual(deployed, g.Line.Count(c => c != null));
            Assert.AreEqual(deployed, g.MaxOccupiedLine);
            Assert.AreEqual(deployed <= g.Config.CleanLineMax, g.CleanLineAlive);
        }

        [Test]
        public void PopupTextsFollowTheSpec()
        {
            Assert.AreEqual("TĂIETURĂ CURATĂ!", HudText.PieceLabel(true, Monster.LeftArm));
            Assert.AreEqual("BUCATĂ RUPTĂ!", HudText.PieceLabel(false, Monster.Head));
            Assert.AreEqual(1f, HudText.PopupScale(1), 1e-6);
            Assert.AreEqual(1.3f, HudText.PopupScale(3), 1e-6);
            Assert.AreEqual(1.6f, HudText.PopupScale(5), 1e-6);
            Assert.AreEqual(1.6f, HudText.PopupScale(9), 1e-6);
            Assert.AreEqual("Ai avut 4 tunuri pe zid", HudText.MissingStarReason(Stars.CleanLine, 4));
            Assert.IsNull(HudText.MissingStarReason(Stars.Defeated, 4));
            Assert.AreEqual(2, Stars.Count(Stars.Defeated | Stars.CleanLine));
        }
    }
}
