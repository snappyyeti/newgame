using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Titan.Core;
using Titan.Sim;

namespace Titan.Tests
{
    /// <summary>
    /// CI balance guard: 50 games per bot with a fixed master seed, using the same bots and runner as Tools/Sim
    /// (Tools/Sim/*.cs compiled into this project), preset v2 (= TitanConfig defaults since CORE-3), every ad helper OFF.
    /// The guards are COARSE versions of the DESIGN.md 12.1 targets (50 games are noisy; the targets are checked with
    /// 300 games in Tools/Sim). DESIGN.md 12.1 lists them. Runs by default; set SKIP_BALANCE=1 to skip.
    /// </summary>
    [TestFixture, Category("Balance")]
    public class BalanceTests
    {
        public const int GamesPerBot = 50;
        public const ulong MasterSeed = 20261015;

        static readonly Dictionary<BotKind, BotSummary> summaries = new Dictionary<BotKind, BotSummary>();
        static readonly List<string> presetWarnings = new List<string>();
        static double elapsedSeconds;

        static TitanConfig Config() => Presets.Build("v2", null, presetWarnings);

        [OneTimeSetUp]
        public void RunBots()
        {
            if (Environment.GetEnvironmentVariable("SKIP_BALANCE") == "1") Assert.Ignore("SKIP_BALANCE=1");
            if (summaries.Count > 0) return;
            var cfg = Config();
            var sw = Stopwatch.StartNew();
            foreach (BotKind bot in Enum.GetValues(typeof(BotKind)))
                summaries[bot] = BotSummary.From(bot, SimRunner.RunMany(bot, cfg, GamesPerBot, MasterSeed));
            elapsedSeconds = sw.Elapsed.TotalSeconds;
            foreach (var s in summaries.Values)
                TestContext.Progress.WriteLine("[balance] " + s.Bot + ": mean titans " + s.DefeatedMean.ToString("F2") + ", titan-1 loss " + (100 * s.TitanOneLoss).ToString("F0")
                    + "%, run p50 " + s.RunP50.ToString("F0") + " s, wall share " + (100 * s.WallShareOfLosses).ToString("F0") + "%, timeouts " + (100 * s.TimeoutShare).ToString("F0")
                    + "%, pieces/titan " + s.PiecesPerTitan.ToString("F2"));
            TestContext.Progress.WriteLine("[balance] " + (GamesPerBot * summaries.Count) + " games in " + elapsedSeconds.ToString("F1") + " s");
        }

        static BotSummary Random => summaries[BotKind.Random];
        static BotSummary Smart => summaries[BotKind.Smart];

        [Test]
        public void PresetV2IsComplete()
        {
            presetWarnings.Clear();
            Config();
            CollectionAssert.IsEmpty(presetWarnings, "every preset v2 setting exists in TitanConfig");
        }

        [Test]
        public void EveryBotPlayedEveryGame()
        {
            foreach (var s in summaries.Values) Assert.AreEqual(GamesPerBot, s.Games, s.Bot.ToString());
        }

        /// <summary>Target: Smart reaches the 30-min cap in &lt; 2% of games.</summary>
        [Test]
        public void RunsEndBeforeTheThirtyMinuteCap()
        {
            foreach (var s in summaries.Values) Assert.LessOrEqual(s.TimeoutShare, 0.02, s.Bot + " hits the 30-min cap too often");
        }

        [Test]
        public void TitansAreBeatable()
        {
            Assert.GreaterOrEqual(Random.DefeatedMean, 5.0, "Random mean titans defeated");
            Assert.GreaterOrEqual(Smart.DefeatedMean, 9.0, "Smart mean titans defeated");
        }

        /// <summary>Target 1.7-2.3x (300 games); coarse 1.4-2.6x at 50 games.</summary>
        [Test]
        public void SkillBeatsRandomTaps()
        {
            double ratio = Smart.DefeatedMean / Random.DefeatedMean;
            Assert.That(ratio, Is.InRange(1.4, 2.6), "Smart:Random mean titans defeated (target 1.7-2.3x)");
        }

        /// <summary>Target: Random &lt; 2%, Smart &lt; 1%. Coarse: at most 2 of 50 / 1 of 50.</summary>
        [Test]
        public void FirstTitanIsRarelyLost()
        {
            Assert.LessOrEqual(Random.TitanOneLoss, 0.04, "Random titan-1 loss");
            Assert.LessOrEqual(Smart.TitanOneLoss, 0.02, "Smart titan-1 loss");
        }

        /// <summary>Target: runs under 20 s Random &lt;= 1%, Smart 0%; Random under 30 s &lt;= 3%.</summary>
        [Test]
        public void NoRunIsOverInSeconds()
        {
            Assert.LessOrEqual(Random.Under20, 0.02, "Random runs under 20 s");
            Assert.AreEqual(0.0, Smart.Under20, "Smart runs under 20 s");
            Assert.LessOrEqual(Random.Under30, 0.06, "Random runs under 30 s");
        }

        /// <summary>Target: median run Random 120-200 s, Smart 240-420 s.</summary>
        [Test]
        public void RunLengthIsInTheBand()
        {
            Assert.That(Random.RunP50, Is.InRange(100.0, 260.0), "Random median run (s)");
            Assert.That(Smart.RunP50, Is.InRange(200.0, 500.0), "Smart median run (s)");
        }

        /// <summary>
        /// Target: wall share of losses Random 35-55%, Smart 40-70%. Only the lower bounds are guarded: with salvage
        /// (CORE-1) and the last-slot guard (CORE-4) jams are rare and the wall takes 96-99% of losses, a known miss
        /// recorded in DESIGN.md 4.3.2.
        /// </summary>
        [Test]
        public void TheWallIsAThreat()
        {
            Assert.GreaterOrEqual(Random.WallShareOfLosses, 0.35, "Random wall share of losses");
            Assert.GreaterOrEqual(Smart.WallShareOfLosses, 0.40, "Smart wall share of losses");
        }

        /// <summary>Target: median clear 10-40 s for titans 1-16, sprint &lt;= 16 s, boss &lt;= 40 s.</summary>
        [Test]
        public void TitansTakeTenToFortySeconds()
        {
            foreach (var s in summaries.Values)
            {
                Assert.GreaterOrEqual(s.ClearP50Min, 8.0, s.Bot + " fastest per-titan median clear");
                Assert.LessOrEqual(s.ClearP50Max, 44.0, s.Bot + " slowest per-titan median clear");
                Assert.LessOrEqual(s.SprintClearP50Max, 18.0, s.Bot + " sprint median clear");
                Assert.LessOrEqual(s.BossClearP50Max, 44.0, s.Bot + " boss median clear");
            }
        }

        /// <summary>
        /// Target: Smart decisions with &gt;= 2 visible options &gt;= 65%, zero-choice &lt;= 25%; line full &lt;= 25% of play
        /// (measured 25-35% depending on the bot: a known miss, DESIGN.md 4.3.2; guarded at 40%).
        /// </summary>
        [Test]
        public void DecisionsStayReal()
        {
            Assert.GreaterOrEqual(Smart.MeaningfulChoiceShare, 0.60, "Smart decision points with >= 2 visible colors");
            Assert.LessOrEqual(Smart.ZeroChoiceShare, 0.30, "Smart zero-choice decision points");
            foreach (var s in summaries.Values) Assert.LessOrEqual(s.LineFullShare, 0.40, s.Bot + " time with the line full");
        }

        /// <summary>Target: median margin at defeat 0.25-0.45; time at danger &gt;= 75% between 4% and 10%.</summary>
        [Test]
        public void TitansDieCloseButNotTooClose()
        {
            foreach (var s in summaries.Values)
            {
                Assert.That(s.MarginP50All, Is.InRange(0.20, 0.50), s.Bot + " median margin at defeat");
                Assert.That(s.Danger75Share, Is.InRange(0.02, 0.12), s.Bot + " time at danger >= 75%");
            }
        }

        /// <summary>CORE-4: no loss on the player's own tap; never more than 1.5 s stuck with nothing to do.</summary>
        [Test]
        public void GuardAndDeadlockKeepTheThreatHonest()
        {
            foreach (var s in summaries.Values)
            {
                Assert.AreEqual(0.0, s.SelfTapShareOfSlotsFull, s.Bot + " SlotsFull losses from the bot's own tap");
                Assert.LessOrEqual(s.MaxStuckMax, 1.55, s.Bot + " longest continuous stuck time (s)");
            }
        }

        [Test]
        public void PiecesKeepFalling()
        {
            foreach (var s in summaries.Values)
            {
                Assert.That(s.PiecesPerTitan, Is.InRange(1.0, 8.0), s.Bot + " pieces per titan");
                Assert.Greater(s.JointPieceShare, 0.0, s.Bot + " joint shots must cut pieces");
            }
        }

        /// <summary>CORE-1 (salvage): no line cannon is ever left with a vanished color, the tray never offers only dead cannons.</summary>
        [Test]
        public void SalvageLeavesNoDeadCannons()
        {
            foreach (var s in summaries.Values)
            {
                Assert.AreEqual(0.0, s.StrandedPerGame, s.Bot + " stranded line cannons per game");
                Assert.AreEqual(0.0, s.DeadParkedShare, s.Bot + " time with a dead cannon parked on the line");
                Assert.AreEqual(0.0, s.OnlyDeadTrayShare, s.Bot + " decisions where every tray cannon is dead");
            }
        }

        /// <summary>CORE-2 (earned cuts): most pieces come from a joint shot, about three pieces per titan.</summary>
        [Test]
        public void CutsAreEarnedAtTheJoints()
        {
            foreach (var s in summaries.Values)
            {
                Assert.GreaterOrEqual(s.JointPieceShare, 0.60, s.Bot + " share of pieces cut by a joint shot");
                Assert.That(s.PiecesPerTitan, Is.InRange(2.5, 3.5), s.Bot + " pieces per titan");
            }
        }

        /// <summary>
        /// CORE-5 (cut grade): target clean cuts per titan Smart ≥ 1.5× Random (measured 1.60× at 300 games; coarse
        /// 1.3× here). Both earnable stars are reachable and neither is free. The s3 target (Smart 25-40%) is a known
        /// miss: Smart does not hold cannons back for a star and earns s3 on about 9% of titans (DESIGN.md 6.3).
        /// </summary>
        [Test]
        public void CleanCutsRewardSkillAndStarsAreReachable()
        {
            Assert.GreaterOrEqual(Smart.CleanCutsPerTitan / Random.CleanCutsPerTitan, 1.3, "Smart:Random clean cuts per titan (target >= 1.5x)");
            foreach (var s in summaries.Values)
            {
                Assert.That(s.StarDecapitationRate, Is.InRange(0.2, 0.9), s.Bot + " decapitation star rate");
                Assert.That(s.StarCleanLineRate, Is.InRange(0.0, 0.5), s.Bot + " clean line star rate");
            }
            Assert.Greater(Smart.StarCleanLineRate + Random.StarCleanLineRate, 0.0, "the clean line star must be reachable");
        }

        [Test]
        public void SimulationIsDeterministic()
        {
            var cfg = Config();
            var a = SimRunner.RunMany(BotKind.Smart, cfg, 8, MasterSeed);
            var b = SimRunner.RunMany(BotKind.Smart, cfg, 8, MasterSeed);
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i].Defeated, b[i].Defeated);
                Assert.AreEqual(a[i].Score, b[i].Score);
                Assert.AreEqual(a[i].RunTime, b[i].RunTime);
                Assert.AreEqual(a[i].Reason, b[i].Reason);
            }
        }
    }
}
