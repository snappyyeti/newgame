using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Titan.Core;

namespace Titan.Tests
{
    public class MonsterTests
    {
        [Test]
        public void FrontCubeHidesTheOneBehind()
        {
            var m = new Monster();
            m.Set(new Int3(0, 0, 0), 1);
            m.Set(new Int3(0, 0, 1), 2);
            Assert.IsTrue(m.IsVisible(new Int3(0, 0, 0)));
            Assert.IsFalse(m.IsVisible(new Int3(0, 0, 1)));
            Assert.IsFalse(m.HasVisibleOfColor(2));
            m.Remove(new Int3(0, 0, 0));
            Assert.IsTrue(m.HasVisibleOfColor(2));
        }

        [Test]
        public void CuttingTheNeckDropsTheHead()
        {
            var m = new Monster();
            m.Set(new Int3(0, 0, 0), 0);
            m.Set(new Int3(0, 1, 0), 1);
            m.Set(new Int3(0, 2, 0), 2);
            m.Set(new Int3(1, 2, 0), 2);
            var pieces = m.Remove(new Int3(0, 1, 0));
            Assert.AreEqual(1, pieces.Count);
            Assert.AreEqual(2, pieces[0].Count);
            Assert.AreEqual(1, m.Count);
        }

        [Test]
        public void TargetIsTheHighestVisibleCube()
        {
            var m = new Monster { Order = TargetOrder.HighestFirst };
            for (int y = 0; y < 4; y++) m.Set(new Int3(0, y, 0), 1);
            m.Set(new Int3(3, 2, 0), 1);
            Assert.IsTrue(m.PickTarget(1, 0f, out var t));
            Assert.AreEqual(new Int3(0, 3, 0), t);
            Assert.IsFalse(m.PickTarget(4, 0f, out _));
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(6)]
        [TestCase(12)]
        public void BuiltTitansStandAndAreSymmetric(int level)
        {
            var m = MonsterBuilder.Build(level, new SystemRandom(level));
            Assert.Greater(m.Count, 50);
            Assert.IsEmpty(m.DropUnsupported(), "a fresh titan must stand on its own");
            Assert.AreEqual(-m.MinX, m.MaxX);
            var colors = new HashSet<int>(m.Cubes.Select(kv => kv.Value));
            Assert.LessOrEqual(colors.Count, MonsterBuilder.ColorsForLevel(level));
        }

        [Test]
        public void ShootingTheHipsDoesNotToppleTheBody()
        {
            var m = MonsterBuilder.Build(1, new SystemRandom(3));
            // Level 1: legs are x = ±1..±2 and 3 rows tall, so row 2 is the top of both legs.
            var fallen = new List<FallenPiece>();
            foreach (var c in m.Cubes.Where(kv => kv.Key.Y == 2 && System.Math.Abs(kv.Key.X) <= 2).Select(kv => kv.Key).ToList())
                fallen.AddRange(m.Remove(c));
            Assert.IsEmpty(fallen);
        }

        [Test]
        public void CuttingAShoulderDropsTheWholeArm()
        {
            var m = MonsterBuilder.Build(1, new SystemRandom(3));
            int armX = m.MaxX;
            int shoulderX = armX - 2;
            var shoulder = m.Cubes.Where(kv => kv.Key.X == shoulderX).Select(kv => kv.Key).ToList();
            int armCubes = m.Cubes.Count(kv => kv.Key.X > shoulderX);
            Assert.IsNotEmpty(shoulder);
            Assert.IsTrue(shoulder.All(m.IsJoint), "the shoulder column is the joint");
            var fallen = new List<FallenPiece>();
            foreach (var c in shoulder) fallen.AddRange(m.Remove(c));
            Assert.AreEqual(1, fallen.Count);
            Assert.AreEqual(armCubes, fallen[0].Count);
            Assert.AreEqual(Monster.RightArm, fallen[0].Part);
        }

        [Test]
        public void CuttingTheNeckDropsTheBuiltHead()
        {
            var m = MonsterBuilder.Build(2, new SystemRandom(4));
            // The neck is the only row above the torso that is a single cube wide at level 2.
            int neckY = m.Cubes.Where(kv => kv.Key.X == 0).Select(kv => kv.Key.Y)
                .First(y => !m.Has(new Int3(1, y, 0)) && y > 3);
            int headCubes = m.Cubes.Count(kv => kv.Key.Y > neckY);
            var fallen = new List<FallenPiece>();
            foreach (var c in m.Cubes.Where(kv => kv.Key.Y == neckY).Select(kv => kv.Key).ToList()) fallen.AddRange(m.Remove(c));
            Assert.AreEqual(1, fallen.Count);
            Assert.AreEqual(headCubes, fallen[0].Count);
        }
    }

    public class GameTests
    {
        [Test]
        public void TrayHoldsThreeCannonsAndDeployFillsFirstFreeSlot()
        {
            var g = new TitanGame(new TitanConfig { TrayRefill = TrayRefill.Batch }, new SystemRandom(1));
            Assert.IsTrue(g.Tray.All(c => c != null));
            Assert.IsTrue(g.Deploy(1));
            Assert.IsNotNull(g.Line[0]);
            Assert.IsNull(g.Tray[1]);
            Assert.IsTrue(g.Deploy(0));
            Assert.IsNotNull(g.Line[1]);
        }

        [Test]
        public void TrayRefillsWhenAllThreeAreUsed()
        {
            var g = new TitanGame(new TitanConfig { TrayRefill = TrayRefill.Batch }, new SystemRandom(2));
            g.Deploy(0);
            g.Deploy(1);
            g.Deploy(2);
            if (!g.IsOver) Assert.IsTrue(g.Tray.Any(c => c != null));
        }

        [Test]
        public void AmmoMatchesCubesExactly()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                var g = new TitanGame(new TitanConfig(), new SystemRandom(seed), 1 + seed % 8);
                for (int color = 0; color < MonsterBuilder.MaxColors; color++)
                    Assert.AreEqual(g.Monster.CountOfColor(color), g.AmmoAvailable(color), $"seed {seed} color {color}");
                foreach (var c in g.Tray) Assert.That(c.Ammo, Is.InRange(1, 30));
            }
        }

        [Test]
        public void CannonsShootUntilEmpty()
        {
            var g = new TitanGame(new TitanConfig { MinApproachSeconds = 1000f }, new SystemRandom(5));
            int before = g.Monster.Count;
            for (int i = 0; i < 3; i++) g.Deploy(i);
            for (int i = 0; i < 400 && !g.IsOver; i++) g.Tick(0.05f);
            Assert.Less(g.Monster.Count, before);
            Assert.Greater(g.Score, 0);
            Assert.IsTrue(g.Events.Any(e => e.Kind == EventKind.Shot));
        }

        [Test]
        public void TitanReachingTheWallEndsTheGame()
        {
            var cfg = new TitanConfig();
            var g = new TitanGame(cfg, new SystemRandom(1));
            for (int i = 0; i < 2000 && !g.IsOver; i++) g.Tick(0.1f);
            Assert.AreEqual(LoseReason.WallReached, g.Lost);
            Assert.AreEqual(0f, g.Distance);
        }

        [Test]
        public void FiveIdleCannonsEndTheGame()
        {
            // A color that exists but is hidden inside the titan: the cannons wait, nothing to salvage.
            TitanGame g = null;
            int hidden = -1;
            for (int seed = 0; seed < 200 && hidden < 0; seed++)
            {
                g = new TitanGame(new TitanConfig(), new SeededRandom((ulong)seed), 1 + seed % 6);
                hidden = Enumerable.Range(0, MonsterBuilder.MaxColors)
                    .FirstOrDefault(c => g.Monster.CountOfColor(c) > 0 && !g.Monster.HasVisibleOfColor(c), -1);
            }
            Assert.GreaterOrEqual(hidden, 0, "no titan with a hidden-only color in 200 seeds");
            for (int s = 0; s < g.ActiveSlots; s++) g.Line[s] = new Cannon(hidden, 10);
            g.Tick(0.01f);
            Assert.AreEqual(LoseReason.SlotsFull, g.Lost);
        }

        [Test]
        public void FiveDeadCannonsEndTheGameWithoutSalvage()
        {
            var g = new TitanGame(new TitanConfig { SalvageEnabled = false }, new SystemRandom(1));
            Assert.IsFalse(g.SalvageActive);
            int missing = Enumerable.Range(0, MonsterBuilder.MaxColors).First(c => !g.Monster.HasVisibleOfColor(c));
            for (int s = 0; s < g.ActiveSlots; s++) g.Line[s] = new Cannon(missing, 10);
            g.Tick(0.01f);
            Assert.AreEqual(LoseReason.SlotsFull, g.Lost);
        }

        [Test]
        public void DefeatingATitanBringsABiggerOne()
        {
            var g = new TitanGame(new TitanConfig { MinApproachSeconds = 10000f, VisibleColorBias = 1f }, new SystemRandom(9));
            int firstSize = g.Monster.Count;
            for (int i = 0; i < 20000 && g.Level == 1 && !g.IsOver; i++)
            {
                for (int t = 0; t < g.Tray.Length; t++)
                    if (g.Tray[t] != null && g.Monster.HasVisibleOfColor(g.Tray[t].Color)) g.Deploy(t);
                if (g.Line.All(c => c == null))
                    for (int t = 0; t < g.Tray.Length; t++) if (g.Deploy(t)) break;
                g.Tick(0.05f);
            }
            Assert.AreEqual(2, g.Level, "lost: " + g.Lost);
            Assert.Greater(g.Monster.Count, firstSize);
            Assert.IsTrue(g.Events.Any(e => e.Kind == EventKind.TitanDefeated));
        }
    }
}
