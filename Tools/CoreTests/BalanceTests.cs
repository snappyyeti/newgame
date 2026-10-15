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
    /// (Tools/Sim/*.cs compiled into this project), with every ad helper OFF.
    /// The guards are COARSE floors for the current loop (DESIGN.md 12.1 lists them); CORE-3 tightens them to the
    /// loop v2 targets. Runs by default; set SKIP_BALANCE=1 to skip.
    /// </summary>
    [TestFixture, Category("Balance")]
    public class BalanceTests
    {
        public const int GamesPerBot = 50;
        public const ulong MasterSeed = 20261015;

        static readonly Dictionary<BotKind, BotSummary> summaries = new Dictionary<BotKind, BotSummary>();
        static double elapsedSeconds;

        [OneTimeSetUp]
        public void RunBots()
        {
            if (Environment.GetEnvironmentVariable("SKIP_BALANCE") == "1") Assert.Ignore("SKIP_BALANCE=1");
            if (summaries.Count > 0) return;
            var cfg = new TitanConfig();
            var sw = Stopwatch.StartNew();
            foreach (BotKind bot in Enum.GetValues(typeof(BotKind)))
                summaries[bot] = BotSummary.From(bot, SimRunner.RunMany(bot, cfg, GamesPerBot, MasterSeed));
            elapsedSeconds = sw.Elapsed.TotalSeconds;
            foreach (var s in summaries.Values)
                TestContext.Progress.WriteLine("[balance] " + s.Bot + ": mean titans " + s.DefeatedMean.ToString("F2") + ", titan-1 loss " + (100 * s.TitanOneLoss).ToString("F0")
                    + "%, timeouts " + (100 * s.TimeoutShare).ToString("F0") + "%, pieces/titan " + s.PiecesPerTitan.ToString("F2"));
            TestContext.Progress.WriteLine("[balance] " + (GamesPerBot * summaries.Count) + " games in " + elapsedSeconds.ToString("F1") + " s");
        }

        [Test]
        public void EveryBotPlayedEveryGame()
        {
            foreach (var s in summaries.Values) Assert.AreEqual(GamesPerBot, s.Games, s.Bot.ToString());
        }

        [Test]
        public void RunsEndBeforeTheThirtyMinuteCap()
        {
            foreach (var s in summaries.Values) Assert.LessOrEqual(s.TimeoutShare, 0.02, s.Bot + " hits the 30-min cap too often");
        }

        [Test]
        public void TitansAreBeatable()
        {
            Assert.GreaterOrEqual(summaries[BotKind.Random].DefeatedMean, 1.4, "Random mean titans defeated");
            Assert.GreaterOrEqual(summaries[BotKind.Smart].DefeatedMean, 1.8, "Smart mean titans defeated");
        }

        [Test]
        public void SkillBeatsRandomTaps()
        {
            Assert.GreaterOrEqual(summaries[BotKind.Smart].DefeatedMean, summaries[BotKind.Random].DefeatedMean,
                "Smart must defeat at least as many titans as Random (target 1.7-2.3x after CORE-3)");
        }

        [Test]
        public void FirstTitanIsRarelyLost()
        {
            Assert.LessOrEqual(summaries[BotKind.Random].TitanOneLoss, 0.30, "Random titan-1 loss");
            Assert.LessOrEqual(summaries[BotKind.Smart].TitanOneLoss, 0.25, "Smart titan-1 loss");
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
            var cfg = new TitanConfig();
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
