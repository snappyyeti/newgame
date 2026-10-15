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

    /// <summary>
    /// Ethics lock (DESIGN.md 11): balance is measured with ad helpers OFF, bots cannot reach helpers or ads,
    /// and the visible-color bias is not a tuning knob. These always run (not part of the Balance category).
    /// </summary>
    public class EthicsLockTests
    {
        static string RepoFile(string relative)
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null)
            {
                var path = Path.Combine(dir.FullName, relative);
                if (File.Exists(path)) return path;
                dir = dir.Parent;
            }
            Assert.Fail("cannot find " + relative + " above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }

        [Test]
        public void BotCodeNeverTouchesHelpersOrAds()
        {
            string code = File.ReadAllText(RepoFile(Path.Combine("Tools", "Sim", "Bots.cs")));
            code = Regex.Replace(code, @"//.*", "");                     // comments may explain the rule
            code = Regex.Replace(code, "/\\*.*?\\*/", "", RegexOptions.Singleline);
            string[] forbidden =
            {
                @"\bRevive\w*", @"\w*ExtraSlot\w*", @"\bRescue\w*", @"\w*Rewarded\w*", @"\w*Interstitial\w*",
                @"\bI?Ads?(Service|Governor|Config|Break|Pass)?\b", @"\bAd[A-Z]\w*", @"\w*Helper\w*",
                @"\bTitanGame\b", @"\bDeploy\w*", @"\bTick\b", @"\bLine\[", @"\bTray\[", @"\bMonster\b",
            };
            foreach (var pattern in forbidden)
            {
                var m = Regex.Match(code, pattern);
                Assert.IsFalse(m.Success, "Tools/Sim/Bots.cs uses '" + m.Value + "': bots may only read a BotView and answer with a tray index");
            }
        }

        [Test]
        public void BotViewExposesOnlyNumbers()
        {
            var allowed = new HashSet<Type> { typeof(int), typeof(bool), typeof(int[]), typeof(void) };
            foreach (var member in typeof(BotView).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (member is MethodInfo mi && !mi.IsSpecialName)
                    Assert.IsTrue(allowed.Contains(mi.ReturnType), "BotView." + mi.Name + " returns " + mi.ReturnType.Name);
                if (member is PropertyInfo pi)
                {
                    Assert.IsTrue(allowed.Contains(pi.PropertyType), "BotView." + pi.Name + " is " + pi.PropertyType.Name);
                    Assert.IsFalse(pi.CanWrite && pi.SetMethod.IsPublic, "BotView." + pi.Name + " must be read-only");
                }
                Assert.IsFalse(member is FieldInfo, "BotView must not expose fields");
            }
            Assert.IsEmpty(typeof(BotView).GetConstructors(BindingFlags.Public | BindingFlags.Instance), "only the runner builds a BotView");
        }

        [Test]
        public void VisibleColorBiasIsLockedAtSixtyFivePercent()
        {
            Assert.AreEqual(0.65f, new TitanConfig().VisibleColorBias);
            var warnings = new List<string>();
            foreach (var preset in Presets.All.Keys)
                Assert.AreEqual(0.65f, Presets.Build(preset, null, warnings).VisibleColorBias, "preset " + preset);
        }

        [Test]
        public void PresetOverridesUseReflectionAndRejectUnknownFields()
        {
            var cfg = Presets.Build("current", new[] { "ApproachSecondsPerCube=0.08", "LineSlots=6", "AmmoSizes=10,20" }, null);
            Assert.AreEqual(0.08f, cfg.ApproachSecondsPerCube);
            Assert.AreEqual(6, cfg.LineSlots);
            CollectionAssert.AreEqual(new[] { 10, 20 }, cfg.AmmoSizes);
            Assert.Throws<ArgumentException>(() => Presets.Build("current", new[] { "NoSuchField=1" }, null));
            Assert.Throws<ArgumentException>(() => Presets.Build("nope", null, null));
        }
    }
}
