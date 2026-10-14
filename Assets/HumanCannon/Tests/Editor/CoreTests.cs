using System.Collections.Generic;
using HumanCannon.Core;
using NUnit.Framework;

namespace HumanCannon.Tests
{
    /// <summary>Returns the given groups in order, then repeats the last one.</summary>
    sealed class ScriptedSource : IGroupSource
    {
        readonly Queue<Group> groups;
        Group last;
        public int Revives;

        public ScriptedSource(params Group[] groups) { this.groups = new Queue<Group>(groups); }

        public Group Next(Round round)
        {
            if (groups.Count > 0) last = groups.Dequeue();
            return new Group((int[])last.Colors.Clone());
        }

        public void OnRevive(Round round) => Revives++;
    }

    public class ResolverTests
    {
        static readonly GameConfig Cfg = new GameConfig();

        [Test]
        public void TopRunCountsSameColorFromTheTop()
        {
            var b = Board.FromStrings(12, "0011", "");
            b.TopRun(0, out int c, out int k);
            Assert.AreEqual(1, c);
            Assert.AreEqual(2, k);
            b.TopRun(1, out c, out k);
            Assert.AreEqual(-1, c);
            Assert.AreEqual(0, k);
        }

        [Test]
        public void GroupMustFitUnderCapacity()
        {
            var b = Board.FromStrings(12, "0101010101");
            Assert.IsTrue(b.CanPlace(0, 2));
            Assert.IsFalse(b.CanPlace(0, 3));
        }

        [Test]
        public void FiveOnTopClearAndWhatIsUnderneathStays()
        {
            var b = Board.FromStrings(12, "2000");
            var r = Resolver.Place(b, 0, new Group(0, 0), Cfg);
            Assert.AreEqual("2", b.ToString());
            Assert.AreEqual(50, r.Points);
            Assert.AreEqual(1, r.MaxStep);
        }

        [Test]
        public void CoveredRunDoesNotClear()
        {
            var b = Board.FromStrings(12, "000");
            var r = Resolver.Place(b, 0, new Group(0, 0, 1), Cfg);
            Assert.AreEqual("000001", b.ToString());
            Assert.AreEqual(0, r.Points);
        }

        [Test]
        public void PeopleOverFiveCountDouble()
        {
            var b = Board.FromStrings(12, "0000");
            var r = Resolver.Place(b, 0, new Group(0, 0), Cfg);
            Assert.AreEqual(10 * (5 + 2), r.Points);
        }

        [Test]
        public void ChainJumpsToNeighbourAndMultipliesPoints()
        {
            var b = Board.FromStrings(12, "2222", "22000");
            var r = Resolver.Place(b, 1, new Group(0, 0), Cfg);
            Assert.AreEqual(" | ", b.ToString());
            Assert.AreEqual(2, r.MaxStep);
            Assert.AreEqual(50 + 2 * 10 * (5 + 2), r.Points);
            var jump = r.Events.Find(e => e.Kind == BoardEventKind.Jump);
            Assert.AreEqual(1, jump.Pedestal);
            Assert.AreEqual(0, jump.Target);
            Assert.AreEqual(2, jump.Count);
        }

        [Test]
        public void ChainTieGoesLeft()
        {
            var b = Board.FromStrings(12, "333", "33111", "333");
            Resolver.Place(b, 1, new Group(1, 1), Cfg);
            Assert.AreEqual(" |  | 333", b.ToString());
        }

        [Test]
        public void ChainPrefersLongerRun()
        {
            var b = Board.FromStrings(12, "333", "33111", "3333");
            Resolver.Place(b, 1, new Group(1, 1), Cfg);
            Assert.AreEqual("333 |  | ", b.ToString());
        }

        [Test]
        public void NoJumpWhenItWouldNotClear()
        {
            var b = Board.FromStrings(12, "33", "33111");
            var r = Resolver.Place(b, 1, new Group(1, 1), Cfg);
            Assert.AreEqual("33 | 33", b.ToString());
            Assert.AreEqual(1, r.MaxStep);
        }

        [Test]
        public void NoJumpWhenNeighbourHasNoRoom()
        {
            var b = Board.FromStrings(12, "11111111333", "33111");
            Resolver.Place(b, 1, new Group(1, 1), Cfg);
            Assert.AreEqual("11111111333 | 33", b.ToString());
        }

        [Test]
        public void ChainContinuesAcrossSeveralPedestals()
        {
            var b = Board.FromStrings(12, "555", "5544", "44000");
            var r = Resolver.Place(b, 2, new Group(0, 0), Cfg);
            // 44 exposed on P2 + 44 on P1 is only 4 people, so nothing jumps.
            Assert.AreEqual(1, r.MaxStep);

            b = Board.FromStrings(12, "555", "55444", "44000");
            r = Resolver.Place(b, 2, new Group(0, 0), Cfg);
            // Step 1 clears 00000 on P2, step 2: 44 jumps onto P1's 444 and clears,
            // step 3: P1's exposed 55 jumps onto P0's 555 and clears.
            Assert.AreEqual(3, r.MaxStep);
            Assert.AreEqual(" |  | ", b.ToString());
            Assert.AreEqual(50 + 2 * 50 + 3 * 50, r.Points);
        }
    }

    public class RoundTests
    {
        static Round NewRound(IGroupSource source, int record = 0, GameConfig cfg = null)
        {
            return new Round(cfg ?? new GameConfig(), source, new SystemRandom(1), record, 10);
        }

        [Test]
        public void FiringAdvancesQueueAndScores()
        {
            var src = new ScriptedSource(new Group(0, 0, 0), new Group(0, 0), new Group(1, 1), new Group(2, 2));
            var round = NewRound(src);
            Assert.AreEqual("000", round.Cannon.ToString());
            var r = round.Fire(3);
            Assert.AreEqual(0, r.Points);
            Assert.AreEqual("00", round.Cannon.ToString());
            Assert.AreEqual("11", round.Queue[0].ToString());
            Assert.AreEqual("22", round.Queue[1].ToString());
            r = round.Fire(3);
            Assert.IsTrue(r.ArenaCleared);
            Assert.AreEqual(50 + 500, round.Score);
        }

        [Test]
        public void OneSwapBetweenShots()
        {
            var src = new ScriptedSource(new Group(0, 0), new Group(1, 1), new Group(2, 2), new Group(3, 3));
            var round = NewRound(src);
            Assert.IsTrue(round.CanSwap);
            round.Swap(); // empty reserve: cannon parks, next group comes in
            Assert.AreEqual("00", round.Reserve.ToString());
            Assert.AreEqual("11", round.Cannon.ToString());
            Assert.IsFalse(round.CanSwap);
            round.Fire(0);
            Assert.IsTrue(round.CanSwap);
            round.Swap(); // occupied reserve: the two trade places
            Assert.AreEqual("22", round.Reserve.ToString());
            Assert.AreEqual("00", round.Cannon.ToString());
        }

        [Test]
        public void RoundEndsWhenNeitherCannonNorReserveFits()
        {
            var cfg = new GameConfig { PedestalCount = 1, PedestalCapacity = 4 };
            var src = new ScriptedSource(new Group(0, 1), new Group(1, 0), new Group(2, 3), new Group(3, 2));
            var round = NewRound(src, 0, cfg);
            round.Fire(0);
            Assert.IsFalse(round.IsOver, "the reserve is empty, so the player can still park and draw");
            round.Fire(0);
            Assert.IsFalse(round.IsOver, "the reserve is still empty");
            round.Swap();
            Assert.IsTrue(round.IsOver);
        }

        [Test]
        public void SwapAllowedAgainWhenCannonFitsNowhere()
        {
            var cfg = new GameConfig { PedestalCount = 1, PedestalCapacity = 4 };
            var src = new ScriptedSource(new Group(0, 1, 0), new Group(1), new Group(2, 3));
            var round = NewRound(src, 0, cfg);
            round.Fire(0);  // board 010, cannon = 1
            round.Swap();   // reserve = 1, cannon = 23 which fits nowhere
            Assert.IsFalse(round.IsOver);
            Assert.IsTrue(round.CanSwap, "otherwise the player would be stuck");
            round.Swap();
            Assert.IsTrue(round.CanFire(0));
        }

        [Test]
        public void ReviveKnocksDownTallestAndAddsBoundedBonus()
        {
            var cfg = new GameConfig { PedestalCount = 4, PedestalCapacity = 4 };
            var src = new ScriptedSource(new Group(0, 1, 2, 3));
            var round = NewRound(src, 1000, cfg);
            for (int p = 0; p < 4; p++) round.Fire(p);
            Assert.IsFalse(round.IsOver, "the reserve is still empty");
            round.Swap();
            Assert.IsTrue(round.IsOver);
            Assert.IsTrue(round.CanRevive);
            var events = round.Revive(out int bonus);
            Assert.AreEqual(3, events.Count);
            Assert.AreEqual(0, events[0].Pedestal);
            Assert.That(bonus, Is.InRange(0, 100));
            Assert.AreEqual(bonus, round.Score);
            Assert.IsFalse(round.IsOver);
            Assert.AreEqual(4, round.Board.Height(3));
            Assert.AreEqual(1, src.Revives);
            Assert.IsFalse(round.CanRevive);
        }
    }

    public class DirectorTests
    {
        [Test]
        public void ClearingPlacementScoresHigherThanBurying()
        {
            var cfg = new GameConfig();
            var dir = new Director(cfg, new HiddenRules(cfg, new PlayerProfile()), new SystemRandom(3));
            var b = Board.FromStrings(12, "000", "111", "", "", "", "", "");
            var best = dir.EvaluateBest(b, new Group(0, 0));
            Assert.AreEqual(0, best.Pedestal);
            Assert.Greater(best.Score, dir.Evaluate(b, 1, new Group(0, 0)).Score);
        }

        [Test]
        public void GeneratedGroupsRespectTier()
        {
            var rnd = new SystemRandom(7);
            var tier = new ScoreTier(0, 4, 2, 3, 1, 2);
            for (int i = 0; i < 500; i++)
            {
                var g = GroupGenerator.Generate(tier, rnd);
                Assert.That(g.Size, Is.InRange(2, 3));
                Assert.That(g.DistinctColors, Is.InRange(1, 2));
                foreach (var c in g.Colors) Assert.That(c, Is.InRange(0, 3));
            }
        }

        [Test]
        public void AssistPicksMoreHelpfulGroupsThanHardNormal()
        {
            float Average(GameConfig cfg)
            {
                var profile = new PlayerProfile();
                float sum = 0;
                int n = 0;
                for (int seed = 0; seed < 30; seed++)
                {
                    var dir = new Director(cfg, new HiddenRules(cfg, profile), new SystemRandom(seed));
                    var round = new Round(cfg, dir, new SystemRandom(seed), 0, cfg.NewUserRuns + 1);
                    foreach (var g in round.Queue) { sum += g.DirectorH; n++; }
                }
                return sum / n;
            }

            var easy = new GameConfig { DStart = 0.05f, DAtCurveScore = 0.05f, DNoise = 0f };
            var hard = new GameConfig { DStart = 0.9f, DAtCurveScore = 0.9f, DCap = 0.9f, DNoise = 0f };
            Assert.Greater(Average(easy), Average(hard) + 0.3f);
        }

        [Test]
        public void SimulatedGamesAlwaysEnd()
        {
            for (int seed = 0; seed < 40; seed++)
            {
                var cfg = new GameConfig();
                var profile = new PlayerProfile();
                var dir = new Director(cfg, new HiddenRules(cfg, profile), new SystemRandom(seed));
                var round = new Round(cfg, dir, new SystemRandom(seed), 2000, 10);
                var bot = new SystemRandom(seed + 1000);
                int shots = 0;
                while (!round.IsOver && shots < 5000)
                {
                    var best = dir.EvaluateBest(round.Board, round.Cannon);
                    if (best.Fits && bot.Value() < 0.7f) round.Fire(best.Pedestal);
                    else if (round.CanSwap) round.Swap();
                    else if (best.Fits) round.Fire(best.Pedestal);
                    else Assert.Fail("Player has no legal move but the round is not over: " + round.Board);
                    shots++;
                }
                Assert.IsTrue(round.IsOver, "seed " + seed);
            }
        }
    }

    public class HiddenRulesTests
    {
        sealed class NullSource : IGroupSource
        {
            public Group Next(Round round) => new Group(0, 1);
            public void OnRevive(Round round) { }
        }

        static Round RoundAt(GameConfig cfg, int runNumber, int record = 1000)
        {
            return new Round(cfg, new NullSource(), new SystemRandom(0), record, runNumber);
        }

        [Test]
        public void NewUsersGetAssist()
        {
            var cfg = new GameConfig();
            var rules = new HiddenRules(cfg, new PlayerProfile());
            Assert.AreEqual(DirectorMode.Assist, rules.Decide(RoundAt(cfg, 1)).Mode);
            Assert.AreEqual("Base", rules.Decide(RoundAt(cfg, 4)).Rule);
        }

        [Test]
        public void LossStreakGivesMercyForFirstGroups()
        {
            var cfg = new GameConfig();
            var profile = new PlayerProfile();
            profile.AddRun(new RunRecord { Score = 100, RecordBefore = 1000 });
            profile.AddRun(new RunRecord { Score = 200, RecordBefore = 1000 });
            var rules = new HiddenRules(cfg, profile);
            var round = RoundAt(cfg, 10);
            Assert.AreEqual("LossStreakMercy", rules.Decide(round).Rule);

            profile.AddRun(new RunRecord { Score = 100, RecordBefore = 1000, UsedRevive = true });
            Assert.AreEqual("Base", rules.Decide(round).Rule);
        }

        [Test]
        public void ReviveWaveLastsConfiguredGroups()
        {
            var cfg = new GameConfig();
            var rules = new HiddenRules(cfg, new PlayerProfile());
            var round = RoundAt(cfg, 10);
            rules.OnRevive();
            for (int i = 0; i < cfg.ReviveClearWaveGroups; i++)
                Assert.AreEqual("ReviveClearWave", rules.Decide(round).Rule);
            Assert.AreEqual("Base", rules.Decide(round).Rule);
        }

        [Test]
        public void SwitchedOffTraitsDoNothing()
        {
            var cfg = new GameConfig();
            cfg.ApplyOverrides(new Dictionary<string, string> { { "trait_new_user", "false" }, { "trait_revive_clear_wave", "false" } });
            var rules = new HiddenRules(cfg, new PlayerProfile());
            rules.OnRevive();
            Assert.AreEqual("Base", rules.Decide(RoundAt(cfg, 1)).Rule);
        }

        [Test]
        public void BaseCurveFollowsDesign()
        {
            var cfg = new GameConfig();
            Assert.AreEqual(0.25f, cfg.BaseDifficulty(0), 1e-4);
            Assert.AreEqual(0.60f, cfg.BaseDifficulty(5000), 1e-4);
            Assert.AreEqual(0.70f, cfg.BaseDifficulty(50000), 1e-4);
        }
    }

    public class ConfigTests
    {
        [Test]
        public void OverridesApplyAndBadValuesAreIgnored()
        {
            var cfg = new GameConfig();
            cfg.ApplyOverrides(new Dictionary<string, string>
            {
                { "clear_run", "6" },
                { "d_assist", "0.1" },
                { "pedestal_count", "seven" },
                { "score_tiers", "0,3,2,2,1,1|500,4,2,3,1,2" },
            });
            Assert.AreEqual(6, cfg.ClearRun);
            Assert.AreEqual(0.1f, cfg.DAssist, 1e-6);
            Assert.AreEqual(7, cfg.PedestalCount);
            Assert.AreEqual(2, cfg.ScoreTiers.Count);
            Assert.AreEqual(4, cfg.TierFor(800).Colors);
        }

        [Test]
        public void TiersFollowScore()
        {
            var cfg = new GameConfig();
            Assert.AreEqual(4, cfg.TierFor(999).Colors);
            Assert.AreEqual(5, cfg.TierFor(1000).Colors);
            Assert.AreEqual(4, cfg.TierFor(2500).MaxSize);
            Assert.AreEqual(6, cfg.TierFor(9999).Colors);
        }
    }
}
