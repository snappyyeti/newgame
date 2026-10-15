using System.Linq;
using NUnit.Framework;
using SplashSort.Core;

namespace SplashSort.Tests
{
    public class BoardTests
    {
        [Test]
        public void NeighboursAreUpDownLeftRight()
        {
            var b = new Board(3, 4);
            CollectionAssert.AreEquivalent(new[] { 3, 1 }, b.Neighbors(0).ToArray());
            CollectionAssert.AreEquivalent(new[] { 1, 7, 3, 5 }, b.Neighbors(4).ToArray());
            CollectionAssert.AreEquivalent(new[] { 8, 10 }, b.Neighbors(11).ToArray());
        }

        [Test]
        public void TopRunCountsSameColorFromTheTop()
        {
            var b = Board.FromStrings("0111");
            b.TopRun(0, out int c, out int k);
            Assert.AreEqual(1, c);
            Assert.AreEqual(3, k);
            b.TopRun(1, out c, out k);
            Assert.AreEqual(-1, c);
            Assert.AreEqual(0, k);
        }

        [Test]
        public void RingsRespectSizeAndColorLimits()
        {
            var cfg = new SplashConfig();
            var rnd = new SystemRandom(5);
            for (int i = 0; i < 1000; i++)
            {
                var r = Ring.Random(cfg, 3, rnd);
                Assert.That(r.Size, Is.InRange(2, 6));
                Assert.That(r.DistinctColors, Is.InRange(1, 3));
                Assert.That(r.Colors.All(c => c >= 0 && c < 3));
            }
        }

        [Test]
        public void ColorTiersFollowScore()
        {
            var cfg = new SplashConfig();
            Assert.AreEqual(3, cfg.ColorsFor(0));
            Assert.AreEqual(3, cfg.ColorsFor(499));
            Assert.AreEqual(4, cfg.ColorsFor(500));
            Assert.AreEqual(5, cfg.ColorsFor(1500));
            Assert.AreEqual(6, cfg.ColorsFor(3000));
        }
    }

    public class ResolverTests
    {
        static readonly SplashConfig Cfg = new SplashConfig();

        static string Expect(params string[] pools) => Board.FromStrings(pools).ToString();

        [Test]
        public void ShorterRunMovesOntoLonger()
        {
            var b = Board.FromStrings("11");
            Resolver.Place(b, 1, new Ring(2, 1, 1, 1), Cfg);
            Assert.AreEqual(Expect("", "211111"), b.ToString());
        }

        [Test]
        public void LongerExistingRunPullsFromThePlacedRing()
        {
            var b = Board.FromStrings("1111");
            Resolver.Place(b, 1, new Ring(2, 1), Cfg);
            Assert.AreEqual(Expect("11111", "2"), b.ToString());
        }

        [Test]
        public void TieMovesOntoThePlacedPool()
        {
            var b = Board.FromStrings("22");
            Resolver.Place(b, 1, new Ring(3, 2, 2), Cfg);
            Assert.AreEqual(Expect("", "32222"), b.ToString());
        }

        [Test]
        public void DifferentTopColorsDoNotMerge()
        {
            var b = Board.FromStrings("11");
            var r = Resolver.Place(b, 1, new Ring(1, 2), Cfg);
            Assert.AreEqual(Expect("11", "12"), b.ToString());
            Assert.AreEqual(1, r.Events.Count);
        }

        [Test]
        public void MergesChainAcrossSeveralPools()
        {
            // Placing 22 between 22 and 222: pool 0's 22 joins the placed pool first (tie -> placed pool),
            // then the placed pool's 2222 is longer than 222, so pool 2 jumps in too. All seven end up together.
            var b = Board.FromStrings("22", "", "222");
            Resolver.Place(b, 1, new Ring(2, 2), Cfg);
            Assert.AreEqual(Expect("", "2222222"), b.ToString());
        }

        [Test]
        public void TenOnTopSplash()
        {
            var b = Board.FromStrings("1111111");
            var r = Resolver.Place(b, 1, new Ring(1, 1, 1), Cfg);
            Assert.IsTrue(b.IsCompletelyEmpty);
            Assert.AreEqual(100, r.Points);
            Assert.AreEqual(1, r.CascadeSteps);
        }

        [Test]
        public void CascadeMultipliesPoints()
        {
            // Pool 0 holds 22 under seven 1s, pool 3 (below) holds eight 2s.
            var b = Board.FromStrings("221111111", "", "", "22222222");
            var r = Resolver.Place(b, 1, new Ring(1, 1, 1), Cfg);
            // Wave 1: ten 1s splash on pool 0 (x1). Then 22 on pool 0 joins pool 3: ten 2s splash (x2).
            Assert.IsTrue(b.IsCompletelyEmpty, b.ToString());
            Assert.AreEqual(2, r.CascadeSteps);
            Assert.AreEqual(10 * 10 * 1 + 10 * 10 * 2, r.Points);
            var splashes = r.Events.Where(e => e.Kind == BoardEventKind.Splash).ToList();
            Assert.AreEqual(1, splashes[0].Step);
            Assert.AreEqual(2, splashes[1].Step);
        }

        [Test]
        public void MoreThanTenSplashTogether()
        {
            var b = Board.FromStrings("111111111");
            var r = Resolver.Place(b, 1, new Ring(1, 1, 1), Cfg);
            Assert.IsTrue(b.IsCompletelyEmpty);
            Assert.AreEqual(12 * 10, r.Points);
        }

        [Test]
        public void PlacingOnOccupiedPoolIsRejected()
        {
            var b = Board.FromStrings("1");
            Assert.Throws<System.InvalidOperationException>(() => Resolver.Place(b, 0, new Ring(1, 1), Cfg));
        }
    }

    public class RoundTests
    {
        [Test]
        public void TrayRefillsAfterAllThreeArePlaced()
        {
            var round = new Round(new SplashConfig(), new SystemRandom(1));
            round.SetTray(new Ring(0, 0), new Ring(1, 1), new Ring(2, 2));
            round.Place(0, 0);
            Assert.IsNull(round.Tray[0]);
            round.Place(1, 5);
            round.Place(2, 11);
            Assert.IsTrue(round.Tray.All(r => r != null));
        }

        [Test]
        public void GameEndsWhenNoPoolIsEmpty()
        {
            var round = new Round(new SplashConfig(), new SystemRandom(1));
            for (int pool = 0; pool < 12; pool++)
            {
                Assert.IsFalse(round.IsOver);
                // Alternate colors in a checkerboard so nothing ever merges.
                int c = (round.Board.Row(pool) + round.Board.Column(pool)) % 2;
                round.SetTray(new Ring(c, c));
                round.Place(0, pool);
            }
            Assert.IsTrue(round.IsOver);
        }

        [Test]
        public void RandomGamesAlwaysEnd()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var rnd = new SystemRandom(seed);
                var round = new Round(new SplashConfig(), rnd);
                int moves = 0;
                while (!round.IsOver && moves < 100000)
                {
                    var slots = Enumerable.Range(0, 3).Where(s => round.Tray[s] != null).ToList();
                    var pools = Enumerable.Range(0, 12).Where(p => round.Board.IsEmpty(p)).ToList();
                    Assert.IsNotEmpty(slots);
                    Assert.IsNotEmpty(pools);
                    round.Place(slots[rnd.Range(0, slots.Count)], pools[rnd.Range(0, pools.Count)]);
                    moves++;
                }
                Assert.IsTrue(round.IsOver, "seed " + seed);
            }
        }
    }
}
