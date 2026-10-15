using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Titan.Core;

namespace Titan.Tests
{
    /// <summary>CORE-1: severed pieces take their ammo with them (DESIGN.md 4.2.2).</summary>
    public class SalvageTests
    {
        /// <summary>
        /// The salvage invariant: no line cannon has a vanished color; for every color deck + tray ammo &lt;= cubes left;
        /// and the game is never short of ammo (deck + tray + line &gt;= cubes left).
        /// </summary>
        public static void AssertInvariant(TitanGame g, string context)
        {
            var violation = Violation(g);
            if (violation != null) Assert.Fail(context + ": " + violation);
        }

        /// <summary>Null when the invariant holds (cheap: called after every tick of 200 games).</summary>
        public static string Violation(TitanGame g)
        {
            if (g.IsOver) return null;
            var m = g.Monster;
            for (int s = 0; s < g.ActiveSlots; s++)
                if (g.Line[s] != null && m.CountOfColor(g.Line[s].Color) == 0)
                    return "line slot " + s + " holds a dead " + g.Line[s];
            for (int color = 0; color < MonsterBuilder.MaxColors; color++)
            {
                int left = m.CountOfColor(color);
                if (g.DeckAmmo(color) + g.TrayAmmo(color) > left) return "color " + color + ": deck+tray ammo over the " + left + " cubes left";
                if (g.AmmoAvailable(color) < left) return "color " + color + " is short of ammo";
            }
            return null;
        }

        static int CubeCount(Monster m, int color) => m.Cubes.Count(kv => kv.Value == color);

        [Test]
        public void CountOfColorFollowsSetRemoveAndFalls()
        {
            var m = new Monster();
            m.Set(new Int3(0, 0, 0), 0);
            m.Set(new Int3(0, 1, 0), 1);
            m.Set(new Int3(0, 2, 0), 2);
            m.Set(new Int3(1, 2, 0), 2);
            Assert.AreEqual(2, m.CountOfColor(2));
            m.Set(new Int3(1, 2, 0), 3); // a replaced cube leaves its old color
            Assert.AreEqual(1, m.CountOfColor(2));
            Assert.AreEqual(1, m.CountOfColor(3));
            var pieces = m.Remove(new Int3(0, 1, 0)); // the neck: both top cubes fall
            Assert.AreEqual(1, pieces.Count);
            for (int c = 0; c < MonsterBuilder.MaxColors; c++) Assert.AreEqual(CubeCount(m, c), m.CountOfColor(c), "color " + c);
            Assert.AreEqual(0, m.CountOfColor(99));
        }

        [Test]
        public void CuttingAnArmRemovesItsAmmo()
        {
            int lineSalvages = 0;
            for (int seed = 0; seed < 40; seed++)
            {
                var g = new TitanGame(new TitanConfig { MinApproachSeconds = 1000f }, new SeededRandom((ulong)seed));
                var m = g.Monster;
                var armCube = m.Cubes.First(kv => m.PartOf(kv.Key) == Monster.RightArm).Key;
                int armColor = m.ColorAt(armCube);

                // Park an arm-colored cannon on the line when the tray has one: it must fly off if the color vanishes.
                int armTray = System.Array.FindIndex(g.Tray, c => c != null && c.Color == armColor);
                if (armTray >= 0) g.Deploy(armTray);
                int parkedAmmo = g.Line[0] != null && g.Line[0].Color == armColor ? g.Line[0].Ammo : 0;
                int armTotalBefore = g.AmmoAvailable(armColor);
                g.Events.Clear();
                int scoreBefore = g.Score;

                var fallen = new List<FallenPiece>();
                foreach (var j in m.Joints.Where(j => j.X > 0 && m.Has(j)).ToList()) fallen.AddRange(m.Remove(j));
                Assert.AreEqual(1, fallen.Count, "seed " + seed + ": the right arm falls");
                Assert.AreEqual(m.OriginalPartSize(Monster.RightArm), fallen[0].Count, "seed " + seed);
                g.Tick(0.001f); // too short to shoot (deploy delay): only the salvage check runs

                AssertInvariant(g, "seed " + seed);
                // With nothing fired yet, ammo matches the cubes exactly again, unless the wall already holds more.
                for (int color = 0; color < MonsterBuilder.MaxColors; color++)
                {
                    int lineAmmo = g.Line.Where(c => c != null && c.Color == color).Sum(c => c.Ammo);
                    if (g.DeckAmmo(color) + g.TrayAmmo(color) > 0)
                        Assert.AreEqual(m.CountOfColor(color), g.AmmoAvailable(color), "seed " + seed + " color " + color);
                    else
                        Assert.GreaterOrEqual(lineAmmo, m.CountOfColor(color));
                }
                Assert.Less(g.AmmoAvailable(armColor), armTotalBefore, "seed " + seed + ": the arm's ammo left with it");

                var salvaged = g.Events.Where(e => e.Kind == EventKind.Salvaged).ToList();
                Assert.AreEqual(salvaged.Sum(e => e.Points), g.Score - scoreBefore, "salvage points are added to the score");
                foreach (var e in salvaged) Assert.AreEqual(e.Ammo * 5, e.Points);

                // Then the other arm: when the arm color is gone, the parked cannon flies off the wall.
                g.Events.Clear();
                scoreBefore = g.Score;
                foreach (var j in m.Joints.Where(j => j.X < 0 && m.Has(j)).ToList()) m.Remove(j);
                g.Tick(0.001f);
                AssertInvariant(g, "seed " + seed + " both arms");
                salvaged = g.Events.Where(e => e.Kind == EventKind.Salvaged).ToList();
                Assert.AreEqual(salvaged.Sum(e => e.Points), g.Score - scoreBefore);
                if (parkedAmmo > 0 && m.CountOfColor(armColor) == 0)
                {
                    var e = salvaged.Single(x => x.Slot == 0);
                    Assert.AreEqual(parkedAmmo, e.Ammo);
                    Assert.AreEqual(armColor, e.Color);
                    Assert.AreEqual(-1, e.TraySlot);
                    Assert.IsNull(g.Line[0]);
                    lineSalvages++;
                }
            }
            Assert.Greater(lineSalvages, 0, "at least one seed must exercise a line cannon flying off");
        }

        [Test]
        public void InvariantHoldsAfterEveryTick()
        {
            const float dt = 0.05f;
            int salvageEvents = 0, trayEvents = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                var g = new TitanGame(new TitanConfig(), new SeededRandom(SeededRandom.Derive(777, (ulong)seed)));
                var taps = new SeededRandom((ulong)seed);
                float nextTap = 1f;
                for (int step = 1; step <= 2400 && !g.IsOver; step++) // 120 s
                {
                    float t = step * dt;
                    if (t >= nextTap)
                    {
                        nextTap += 1f;
                        var legal = Enumerable.Range(0, g.Tray.Length).Where(g.CanDeploy).ToList();
                        if (legal.Count > 0)
                        {
                            g.Deploy(legal[taps.Range(0, legal.Count)]);
                            var v = Violation(g);
                            if (v != null) Assert.Fail("seed " + seed + " after the tap at " + t + " s: " + v);
                        }
                    }
                    g.Tick(dt);
                    var violation = Violation(g);
                    if (violation != null) Assert.Fail("seed " + seed + " t " + t + " s: " + violation);
                    foreach (var e in g.Events)
                        if (e.Kind == EventKind.Salvaged)
                        {
                            salvageEvents++;
                            if (e.TraySlot >= 0) trayEvents++;
                            if (e.Ammo <= 0 || e.Points != e.Ammo * g.Config.PointsPerSalvagedAmmo || (e.Slot >= 0) == (e.TraySlot >= 0))
                                Assert.Fail("seed " + seed + ": bad Salvaged event slot " + e.Slot + " tray " + e.TraySlot + " ammo " + e.Ammo + " points " + e.Points);
                        }
                    g.Events.Clear();
                }
            }
            Assert.Greater(salvageEvents, 0);
            Assert.Greater(trayEvents, 0);
        }

        [Test]
        public void NoSalvageWhenDisabled()
        {
            var g = new TitanGame(new TitanConfig { SalvageEnabled = false, MinApproachSeconds = 1000f }, new SeededRandom(5));
            var m = g.Monster;
            int total = Enumerable.Range(0, MonsterBuilder.MaxColors).Sum(g.AmmoAvailable);
            foreach (var j in m.Joints.ToList()) m.Remove(j);
            g.Tick(0.001f);
            Assert.IsFalse(g.Events.Any(e => e.Kind == EventKind.Salvaged));
            Assert.AreEqual(total, Enumerable.Range(0, MonsterBuilder.MaxColors).Sum(g.AmmoAvailable));
        }
    }
}
