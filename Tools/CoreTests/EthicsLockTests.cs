using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Titan.Core;
using Titan.Core.Ads;
using Titan.Sim;

namespace Titan.Tests
{
    /// <summary>
    /// Ethics lock (DESIGN.md 11): balance is measured with ad helpers OFF, bots cannot reach helpers or ads,
    /// and the visible-color bias is not a tuning knob. These always run (not part of the Balance category).
    /// </summary>
    public partial class EthicsLockTests
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


    /// <summary>
    /// Ethics lock for the ads (ADS-1, ADS-2; DESIGN.md 11): the game rules never see ad history, a decline never
    /// leads to an ad, the daily ranked run is ad-free, the FTUE gate holds, the floors are code, the rescues need
    /// salvage, and a tray tap can never open an ad card.
    /// </summary>
    public partial class EthicsLockTests
    {
        static string Code(string relative)
        {
            string code = File.ReadAllText(RepoFile(relative));
            code = Regex.Replace(code, @"//.*", "");
            code = Regex.Replace(code, "/\\*.*?\\*/", "", RegexOptions.Singleline);
            return Regex.Replace(code, "\"(\\\\.|[^\"\\\\])*\"", "\"\"");
        }

        [Test]
        public void VisibleColorBiasDefaultIsLocked()
        {
            // ethics lock: the 65% visible-color draw is the same for everyone, whatever their ad history.
            Assert.AreEqual(0.65f, new TitanConfig().VisibleColorBias);
        }

        [Test]
        public void GameRulesHaveNoAdDependency()
        {
            // Reflection: no member of the rule types mentions a type from Titan.Core.Ads.
            var ruleTypes = new[] { typeof(TitanGame), typeof(TitanConfig), typeof(Monster), typeof(MonsterBuilder), typeof(SeededRandom), typeof(RunSession), typeof(Profile) };
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            foreach (var t in ruleTypes)
            {
                foreach (var f in t.GetFields(all)) Assert.AreNotEqual("Titan.Core.Ads", f.FieldType.Namespace, t.Name + "." + f.Name);
                foreach (var p in t.GetProperties(all)) Assert.AreNotEqual("Titan.Core.Ads", p.PropertyType.Namespace, t.Name + "." + p.Name);
                foreach (var m in t.GetMethods(all))
                {
                    Assert.AreNotEqual("Titan.Core.Ads", m.ReturnType.Namespace, t.Name + "." + m.Name);
                    foreach (var prm in m.GetParameters()) Assert.AreNotEqual("Titan.Core.Ads", prm.ParameterType.Namespace, t.Name + "." + m.Name);
                }
                foreach (var c in t.GetConstructors(all))
                    foreach (var prm in c.GetParameters()) Assert.AreNotEqual("Titan.Core.Ads", prm.ParameterType.Namespace, t.Name + " ctor");
            }
            // Grep: the rule files name no ad API. (TitanGame.Revive and GrantExtraSlot are game rules: they move
            // cannons and open a slot, and know nothing about who asked or why.)
            var banned = new[]
            {
                @"\bTitan\.Core\.Ads\b", @"\bI?Ads?(Service|Governor|Config|Break|Pass|State|Log)?\b", @"\bAd[A-Z]\w*", @"\w*Rewarded\w*",
                @"\w*Interstitial\w*", @"\bPlacement\b", @"\w*[Gg]overnor\w*", @"\bLevelPlay\w*",
            };
            foreach (var file in new[] { "TitanGame.cs", "Monster.cs", "MonsterBuilder.cs", "SeededRandom.cs" })
            {
                string code = Code(Path.Combine("Assets", "Titan", "Scripts", "Core", file));
                foreach (var pattern in banned)
                {
                    var m = Regex.Match(code, pattern);
                    Assert.IsFalse(m.Success, file + " references '" + m.Value + "'");
                }
            }
            Assert.IsTrue(Regex.IsMatch("var g = new AdGovernor(cfg);", @"\w*[Gg]overnor\w*"), "the grep can fail");
        }

        /// <summary>Plays a seeded run with fixed taps and returns every event as text.</summary>
        static List<string> Trace(long seed)
        {
            var g = new TitanGame(new TitanConfig(), seed);
            var r = new SeededRandom(unchecked((ulong)seed) ^ 0xABCDEFUL);
            var trace = new List<string>();
            for (int i = 0; i < 2400 && !g.IsOver; i++)
            {
                if (i % 16 == 0)
                {
                    int k = r.Range(0, g.Tray.Length);
                    if (g.CanDeploy(k)) g.Deploy(k);
                }
                g.Tick(0.05f);
                foreach (var e in g.Events)
                    trace.Add(e.Kind + " " + e.Slot + " " + e.TraySlot + " " + e.Color + " " + e.Points + " " + e.Level + " " + e.Cube.X + "," + e.Cube.Y + "," + e.Cube.Z + " " + e.Reason);
                g.Events.Clear();
            }
            trace.Add("score " + g.Score);
            return trace;
        }

        [Test]
        public void GameBehavesTheSameWhateverTheAdHistory()
        {
            for (long seed = 1; seed <= 4; seed++)
            {
                // A player who never saw an ad...
                var clean = AdKit.Governor(new FakeClock(), bucket: 3, sessions: 1);
                var a = Trace(seed);
                // ...and one with a long ad history (interstitials, rewards, a pass, both first-frees used).
                var clock = new FakeClock();
                var heavy = AdKit.ReadyForInter(clock, bucket: 77);
                for (int i = 0; i < 6; i++)
                {
                    heavy.OnInterstitialShown();
                    heavy.OnRewardGranted(Placement.ExtraSlot);
                    clock.Advance(400);
                }
                heavy.GrantAdBreak();
                heavy.ConsumeFirstFree(Placement.ReviveSecondWind);
                heavy.ConsumeFirstFree(Placement.ExtraSlot);
                var b = Trace(seed);
                CollectionAssert.AreEqual(a, b, "seed " + seed + ": same seed and taps, same events");
                Assert.AreNotEqual(clean.State.IntersToday, heavy.State.IntersToday);
            }
        }

        [Test]
        public void ADeclineNeverLeadsToAnInterstitialAtThatGameOver()
        {
            var rnd = new Random(11);
            for (int k = 0; k < 300; k++)
            {
                var clock = new FakeClock();
                var gov = AdKit.ReadyForInter(clock, bucket: 10 + rnd.Next(90));
                gov.OnOfferShown(Placement.ReviveSecondWind);
                gov.OnOfferDeclined(Placement.ReviveSecondWind);
                var ctx = AdKit.PassingInter();
                ctx.runSeconds = rnd.Next(30, 900);
                ctx.resultsVisibleSeconds = rnd.Next(0, 30);
                ctx.adReady = rnd.Next(2) == 0;
                clock.Advance(rnd.Next(0, 4000));
                gov.AddPlaySeconds(rnd.Next(0, 3000));
                Assert.IsFalse(gov.ShouldShowInterstitial(ctx, out _));
                Assert.IsFalse(gov.ShouldShowInterstitial(ctx, out _, false), "not even the tag");
                // Declining the other cards does not lead to an ad either (they never make one more likely).
                var g2 = AdKit.ReadyForInter(new FakeClock());
                bool before = g2.ShouldShowInterstitial(ctx, out _);
                g2.OnOfferShown(Placement.AdBreak);
                g2.OnOfferDeclined(Placement.AdBreak);
                g2.OnOfferShown(Placement.ExtraSlot);
                g2.OnOfferDeclined(Placement.ExtraSlot);
                Assert.IsFalse(g2.ShouldShowInterstitial(ctx, out _) && !before);
            }
        }

        [Test]
        public void DailyRankedRunsGetNoOfferAndNoInterstitial()
        {
            var gov = AdKit.ReadyForInter(new FakeClock());
            foreach (Placement p in Enum.GetValues(typeof(Placement)))
            {
                var ctx = AdKit.PassingOffer();
                ctx.isDailyRanked = true;
                Assert.IsFalse(gov.CanOffer(p, ctx, out _), p.ToString());
            }
            var ictx = AdKit.PassingInter();
            ictx.isDailyRanked = true;
            Assert.IsFalse(gov.ShouldShowInterstitial(ictx, out _));
        }

        [Test]
        public void FtueGateBlocksEverythingBelowThreeRunsOrTenMinutes()
        {
            var gov = AdKit.ReadyForInter(new FakeClock());
            foreach (var (runs, play) in new[] { (2, 5000f), (0, 0f), (20, 599f), (2, 599f) })
            {
                foreach (Placement p in Enum.GetValues(typeof(Placement)))
                {
                    var ctx = AdKit.PassingOffer();
                    ctx.lifetimeRuns = runs;
                    ctx.lifetimePlaySec = play;
                    Assert.IsFalse(gov.CanOffer(p, ctx, out string r), p + " at " + runs + " runs / " + play + " s");
                }
                var ictx = AdKit.PassingInter();
                ictx.lifetimeRuns = runs;
                ictx.lifetimePlaySec = play;
                Assert.IsFalse(gov.ShouldShowInterstitial(ictx, out _));
            }
            var cfg = new AdConfig { inter_grace_runs = 1, inter_min_lifetime_play_s = 0f };
            var lax = AdKit.ReadyForInter(new FakeClock(), cfg);
            var c2 = AdKit.PassingInter();
            c2.lifetimeRuns = 2;
            Assert.IsFalse(lax.ShouldShowInterstitial(c2, out _), "the config cannot lower the any-ad gate");
            c2.lifetimeRuns = 20;
            c2.lifetimePlaySec = 599f;
            Assert.IsFalse(lax.ShouldShowInterstitial(c2, out _));
        }

        [Test]
        public void FloorsAreCodeNotConfig()
        {
            var cfg = new AdConfig();
            cfg.ApplyJson("{\"inter_preset\":\"firm\",\"inter_cooldown_s\":1,\"inter_min_play_s\":1,\"inter_max_per_10min\":60}");
            Assert.GreaterOrEqual(cfg.CooldownSeconds, AdConfig.FloorCooldownSeconds);
            Assert.GreaterOrEqual(cfg.MinPlaySeconds, AdConfig.FloorMinPlaySeconds);
            Assert.LessOrEqual(cfg.MaxPer10Min, AdConfig.CeilingPer10Min);
            Assert.AreEqual(120f, AdConfig.FloorCooldownSeconds);
            Assert.AreEqual(90f, AdConfig.FloorMinPlaySeconds);
            Assert.AreEqual(3.5f, AdConfig.CeilingPer10Min);
            Assert.IsTrue(typeof(AdConfig).GetField(nameof(AdConfig.FloorCooldownSeconds)).IsLiteral, "a const, not a field JSON can reach");
        }

        [Test]
        public void TrayTapPathCannotOpenAnAdCard()
        {
            // The interface seam: TrayTap.Handle takes the game and a slot, nothing that can open a card.
            var handle = typeof(TrayTap).GetMethod(nameof(TrayTap.Handle));
            CollectionAssert.AreEqual(new[] { typeof(TitanGame), typeof(int) }, handle.GetParameters().Select(p => p.ParameterType).ToArray());
            // GameController.HandleTap goes through it and names no ad API.
            string code = Code(Path.Combine("Assets", "Titan", "Scripts", "Game", "GameController.cs"));
            var m = Regex.Match(code, @"void HandleTap\(Vector2 \w+\)\s*\{");
            Assert.IsTrue(m.Success, "GameController.HandleTap exists");
            int depth = 0, end = m.Index + m.Length - 1;
            for (int i = end; i < code.Length; i++)
            {
                if (code[i] == '{') depth++;
                if (code[i] == '}' && --depth == 0)
                {
                    end = i;
                    break;
                }
            }
            string body = code.Substring(m.Index, end - m.Index + 1);
            StringAssert.Contains("TrayTap.Handle(", body);
            foreach (var pattern in new[] { @"\w*Offer\w*", @"\w*Card\w*", @"\bAd[A-Z]\w*", @"\w*Rewarded\w*", @"\w*Revive\w*", @"\w*ExtraSlot\w*", @"\w*[Gg]overnor\w*", @"\w*Interstitial\w*" })
            {
                var hit = Regex.Match(body, pattern);
                Assert.IsFalse(hit.Success, "HandleTap references '" + hit.Value + "'");
            }
        }

        [Test]
        public void BalanceSimHasNoHelperSwitch()
        {
            // The ADS-2 revive measurement lives in the runner and is off unless asked for; Bots.cs stays clean (above).
            string runner = Code(Path.Combine("Tools", "Sim", "SimRunner.cs"));
            StringAssert.Contains("RevivePushBack", runner);
            var defaults = new SimOptions();
            Assert.IsFalse(defaults.Revive, "helpers are OFF by default");
        }
    }
}
