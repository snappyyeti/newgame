using System.Collections.Generic;
using NUnit.Framework;
using Titan.Core;

namespace Titan.Tests
{
    /// <summary>
    /// Golden values for SeededRandom. They were cross-checked against an independent SplitMix64 / FNV-1a
    /// implementation (and the published SplitMix64 reference output for seed 1234567). If one of these fails,
    /// every saved checkpoint, FTUE script and daily titan changes: do not "fix" the numbers, fix the code.
    /// </summary>
    public class SeededRandomTests
    {
        [Test]
        public void MatchesTheSplitMix64Reference()
        {
            var r = new SeededRandom(1234567);
            ulong[] expected = { 6457827717110365317UL, 3203168211198807973UL, 9817491932198370423UL, 4593380528125082431UL, 16408922859458223821UL };
            foreach (var e in expected) Assert.AreEqual(e, r.NextULong());
        }

        [Test]
        public void SameSeedGivesTheSameFirstThousandValues()
        {
            var a = new SeededRandom(42);
            var b = new SeededRandom(42);
            ulong fold = 0;
            var first = new List<ulong>();
            for (int i = 0; i < 1000; i++)
            {
                ulong x = a.NextULong();
                Assert.AreEqual(x, b.NextULong(), "value " + i);
                unchecked { fold = fold * 31 + x; }
                if (i < 3 || i == 999) first.Add(x);
            }
            Assert.AreEqual(13679457532755275413UL, first[0]);
            Assert.AreEqual(2949826092126892291UL, first[1]);
            Assert.AreEqual(5139283748462763858UL, first[2]);
            Assert.AreEqual(7352439375932947048UL, first[3]);
            Assert.AreEqual(14527259649307280909UL, fold, "golden fold of the first 1000 values");
        }

        [Test]
        public void RangeValueAndDoubleAreGolden()
        {
            var r = new SeededRandom(7);
            int[] range = { 87, 4, 46, 3, 74, 5, 98, 82 };
            foreach (var e in range) Assert.AreEqual(e, r.Range(0, 100));

            r = new SeededRandom(7);
            int[] signed = { 2, -1, 1, -2, -1, 0, 3, -3 };
            foreach (var e in signed) Assert.AreEqual(e, r.Range(-5, 5));

            r = new SeededRandom(7);
            long[] bits24 = { 6540257, 281660, 15112256, 9779947 };
            foreach (var e in bits24) Assert.AreEqual(e / 16777216.0, (double)r.Value(), 0.0);

            r = new SeededRandom(7);
            long[] bits53 = { 3511274219185729, 151215513962380, 8113330931062309, 5250569300928453 };
            foreach (var e in bits53) Assert.AreEqual(e / 9007199254740992.0, r.NextDouble(), 0.0);
        }

        [Test]
        public void ValuesStayInRange()
        {
            var r = new SeededRandom(99);
            for (int i = 0; i < 20000; i++)
            {
                float f = r.Value();
                Assert.That(f >= 0f && f < 1f);
                double d = r.NextDouble();
                Assert.That(d >= 0.0 && d < 1.0);
                int k = r.Range(3, 9);
                Assert.That(k >= 3 && k < 9);
            }
            Assert.AreEqual(5, r.Range(5, 5), "an empty range returns min");
            Assert.AreEqual(int.MinValue + 1, new SeededRandom(1).Range(int.MinValue + 1, int.MinValue + 2));
            int big = new SeededRandom(1).Range(int.MinValue, int.MaxValue);
            Assert.That(big >= int.MinValue && big < int.MaxValue);
        }

        [Test]
        public void DeriveDiffersForNeighbouringIndices()
        {
            foreach (ulong seed in new ulong[] { 0, 1, 42, 0xFFFFFFFFFFFFFFFFUL, SeededRandom.FromString("run") })
            {
                var seen = new HashSet<ulong>();
                for (ulong n = 0; n < 200; n++)
                {
                    Assert.AreNotEqual(SeededRandom.Derive(seed, n), SeededRandom.Derive(seed, n + 1));
                    Assert.IsTrue(seen.Add(SeededRandom.Derive(seed, n)), "Derive must not repeat for seed " + seed);
                }
            }
            Assert.AreEqual(6904877152625194467UL, SeededRandom.Derive(42, 2));
            Assert.AreEqual(SeededRandom.Derive(9, 3), SeededRandom.Derive(9, 3));
        }

        [Test]
        public void HashAndFromStringAreGolden()
        {
            // FNV-1a 64 test vectors.
            Assert.AreEqual(0xCBF29CE484222325UL, SeededRandom.FromString(""));
            Assert.AreEqual(0xAF63DC4C8601EC8CUL, SeededRandom.FromString("a"));
            Assert.AreEqual(0x85944171F73967E8UL, SeededRandom.FromString("foobar"));
            Assert.AreEqual(12530072810216569347UL, SeededRandom.FromString("titan-daily-v1:0"));

            Assert.AreEqual(13608149317741381227UL, SeededRandom.Hash(1, 2));
            Assert.AreNotEqual(SeededRandom.Hash(1, 2), SeededRandom.Hash(2, 1));
        }

        [Test]
        public void SeededGameIsReproducible()
        {
            var a = new TitanGame(new TitanConfig(), new SeededRandom(SeededRandom.Derive(5, 1)));
            var b = new TitanGame(new TitanConfig(), new SeededRandom(SeededRandom.Derive(5, 1)));
            for (int step = 0; step < 2400 && !a.IsOver; step++)
            {
                if (step % 20 == 0)
                    for (int i = 0; i < a.Tray.Length; i++)
                        if (a.CanDeploy(i))
                        {
                            Assert.AreEqual(a.Deploy(i), b.Deploy(i));
                            break;
                        }
                a.Tick(0.05f);
                b.Tick(0.05f);
            }
            Assert.AreEqual(a.Score, b.Score);
            Assert.AreEqual(a.Level, b.Level);
            Assert.AreEqual(a.Lost, b.Lost);
            Assert.AreEqual(a.Monster.Count, b.Monster.Count);
        }
    }
}
