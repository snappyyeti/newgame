using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Titan.Core;
using Titan.Core.Ads;

namespace Titan.Tests
{
    /// <summary>ADS-1: the interstitial rules (one test per rule), the frequency caps, the ad-break pass, cohorts, flows.</summary>
    public class InterstitialRuleTests
    {
        FakeClock clock;
        AdGovernor gov;
        InterContext ctx;

        [SetUp]
        public void SetUp()
        {
            clock = new FakeClock();
            gov = AdKit.ReadyForInter(clock);
            ctx = AdKit.PassingInter();
        }

        [Test]
        public void BaselinePassesEveryRule()
        {
            Assert.IsTrue(gov.ShouldShowInterstitial(ctx, out string reason), reason);
            Assert.IsNull(reason);
        }

        [Test]
        public void Rule01_Disabled()
        {
            gov.Config.inter_enabled = false;
            Assert.AreEqual(SkipReason.Disabled, AdKit.Reason(gov, ctx));
        }

        [Test]
        public void Rule02_FirstSession()
        {
            var g = AdKit.Governor(clock, sessions: 1);
            g.AddPlaySeconds(1000f);
            g.OnRunEnded(120f);
            g.OnRunEnded(120f);
            Assert.AreEqual(SkipReason.FirstSession, AdKit.Reason(g, ctx));
        }

        [Test]
        public void Rule03_GraceRuns()
        {
            ctx.lifetimeRuns = 7; // light: 8 grace runs
            Assert.AreEqual(SkipReason.GraceRuns, AdKit.Reason(gov, ctx));
            ctx.lifetimeRuns = 8;
            Assert.IsNull(AdKit.Reason(gov, ctx));
        }

        [Test]
        public void Rule04_LifetimePlay()
        {
            ctx.lifetimePlaySec = 599f;
            Assert.AreEqual(SkipReason.MinLifetimePlay, AdKit.Reason(gov, ctx));
        }

        [Test]
        public void Rule05_AdBreakPassActive()
        {
            gov.GrantAdBreak();
            Assert.AreEqual(SkipReason.AdBreakActive, AdKit.Reason(gov, ctx));
        }

        [Test]
        public void Rule06_Cooldown()
        {
            gov.OnInterstitialShown();
            clock.Advance(239);
            gov.AddPlaySeconds(1000f);
            Assert.AreEqual(SkipReason.Cooldown, AdKit.Reason(gov, ctx), "light: 240 s between interstitials");
            clock.Advance(1);
            Assert.IsNull(AdKit.Reason(gov, ctx));
        }

        [Test]
        public void Rule07_PlaySinceLastInterstitial()
        {
            gov.OnInterstitialShown();
            clock.Advance(1000);
            gov.AddPlaySeconds(209f);
            Assert.AreEqual(SkipReason.MinPlay, AdKit.Reason(gov, ctx), "light: 210 s of play since the last one");
            gov.AddPlaySeconds(1f);
            Assert.IsNull(AdKit.Reason(gov, ctx));
        }

        [Test]
        public void Rule08_AfterRewarded()
        {
            gov.OnRewardGranted(Placement.ExtraSlot);
            gov.AddPlaySeconds(300f);
            clock.Advance(179);
            Assert.AreEqual(SkipReason.AfterRewarded, AdKit.Reason(gov, ctx));
            clock.Advance(1);
            Assert.IsNull(AdKit.Reason(gov, ctx));
        }

        [Test]
        public void Rule09_ShortRun()
        {
            ctx.runSeconds = 44.9f;
            Assert.AreEqual(SkipReason.ShortRun, AdKit.Reason(gov, ctx));
        }

        [Test]
        public void Rule10_DeclinedReviveAtThisGameOver()
        {
            gov.OnOfferShown(Placement.ReviveSecondWind);
            gov.OnOfferDeclined(Placement.ReviveSecondWind);
            Assert.AreEqual(SkipReason.DeclinedRevive, AdKit.Reason(gov, ctx), "a declined revive never leads to an ad");
            gov.OnRunStarted();
            Assert.IsNull(AdKit.Reason(gov, ctx), "the flag belongs to that game over only");
        }

        [Test]
        public void Rule11_FirstGameOverOfTheSession()
        {
            var g = AdKit.Governor(clock);
            g.AddPlaySeconds(1000f);
            g.OnRunEnded(120f);
            Assert.AreEqual(SkipReason.FirstGameOver, AdKit.Reason(g, ctx));
            g.OnRunEnded(120f);
            Assert.IsNull(AdKit.Reason(g, ctx));
        }

        [Test]
        public void Rule12_SessionCap()
        {
            for (int i = 0; i < 5; i++)
            {
                Assert.IsNull(AdKit.Reason(gov, ctx), "interstitial " + (i + 1));
                gov.OnInterstitialShown();
                clock.Advance(300);
                gov.AddPlaySeconds(700f);
            }
            Assert.AreEqual(SkipReason.SessionCap, AdKit.Reason(gov, ctx));
        }

        [Test]
        public void Rule13_DailyCap()
        {
            for (int i = 0; i < 15; i++)
            {
                if (i > 0 && i % 5 == 0)
                {
                    gov.OnSessionStarted();
                    gov.OnRunEnded(120f);
                    gov.OnRunEnded(120f);
                }
                Assert.IsNull(AdKit.Reason(gov, ctx), "interstitial " + (i + 1));
                gov.OnInterstitialShown();
                clock.Advance(300);
                gov.AddPlaySeconds(700f);
            }
            gov.OnSessionStarted();
            gov.OnRunEnded(120f);
            gov.OnRunEnded(120f);
            Assert.AreEqual(SkipReason.DailyCap, AdKit.Reason(gov, ctx));
            clock.Today++;
            Assert.IsNull(AdKit.Reason(gov, ctx), "a new local day resets the count");
        }

        [Test]
        public void Rule14_ResultsVisibleTooShort()
        {
            ctx.resultsVisibleSeconds = 1.4f;
            Assert.AreEqual(SkipReason.ResultsTooShort, AdKit.Reason(gov, ctx));
        }

        [Test]
        public void Rule15_NewRecordOrFurthestTitan()
        {
            ctx.newRecordOrFurthest = true;
            Assert.AreEqual(SkipReason.NewRecord, AdKit.Reason(gov, ctx));
        }

        [Test]
        public void Rule16_DailyRanked()
        {
            ctx.isDailyRanked = true;
            Assert.AreEqual(SkipReason.DailyRanked, AdKit.Reason(gov, ctx));
        }

        [Test]
        public void Rule17_NoFillWithinOneSecond()
        {
            ctx.adReady = false;
            Assert.AreEqual(SkipReason.NoFill, AdKit.Reason(gov, ctx));

            // Through the gate: an ad that never becomes ready is skipped after 1.0 s, and the run still starts.
            var log = new RecordingLog();
            var g = AdKit.ReadyForInter(clock, log: log);
            var ads = new FakeAds { InterReady = false };
            var gate = new InterstitialGate(g, ads);
            int started = 0;
            Assert.IsTrue(gate.TryShow(AdKit.PassingInter(), () => started++));
            Assert.AreEqual(InterstitialGate.Phase.WaitingFill, gate.Current);
            gate.Update(0.5f);
            Assert.AreEqual(0, started);
            gate.Update(0.5f);
            Assert.AreEqual(1, started);
            Assert.AreEqual(InterstitialGate.Phase.Idle, gate.Current);
            Assert.AreEqual(0, ads.InterShows);
            Assert.AreEqual(SkipReason.NoFill, log.Events.Single(e => e.Name == "ad_inter_skipped").Param);
        }

        [Test]
        public void TheTagAsksTheSameRulesWithoutTheFill()
        {
            ctx.adReady = false;
            Assert.IsTrue(gov.ShouldShowInterstitial(ctx, out _, false), "'Reclamă înainte de rundă' shows when rules 1-16 pass");
            ctx.resultsVisibleSeconds = 1f;
            Assert.IsFalse(gov.ShouldShowInterstitial(ctx, out _, false));
        }
    }

    public class InterstitialFrequencyTests
    {
        /// <summary>
        /// Plays a session of runs of <paramref name="runSeconds"/>: each run adds its play, ends, the results panel
        /// stays 3 s, then "Joacă din nou" goes through the gate (the ad is always ready and shown 5 s).
        /// Returns the interstitials shown and the play seconds.
        /// </summary>
        static (int shown, double play, List<double> stamps) Session(string preset, float runSeconds, double sessionSeconds, int lifetimeRuns = 20)
        {
            var clock = new FakeClock();
            var cfg = new AdConfig { inter_preset = preset };
            var gov = AdKit.Governor(clock, cfg);
            var ads = new FakeAds();
            var gate = new InterstitialGate(gov, ads);
            double elapsed = 0, play = 0;
            int shown = 0, runs = lifetimeRuns;
            var stamps = new List<double>();
            while (elapsed < sessionSeconds)
            {
                gov.OnRunStarted();
                for (float t = 0; t < runSeconds; t += 1f)
                {
                    gov.AddPlaySeconds(1f);
                    clock.Advance(1);
                }
                play += runSeconds;
                elapsed += runSeconds;
                runs++;
                gov.OnRunEnded(runSeconds);
                clock.Advance(3);
                elapsed += 3;
                var ctx = new InterContext { lifetimeRuns = runs, lifetimePlaySec = 5000f + (float)play, runSeconds = runSeconds, resultsVisibleSeconds = 3f };
                int before = ads.InterShows;
                bool started = false;
                gate.TryShow(ctx, () => started = true);
                for (int k = 0; k < 40 && !started; k++)
                {
                    gate.Update(0.1f);
                    clock.Advance(0.1);
                    elapsed += 0.1;
                }
                Assert.IsTrue(started, "the run always starts");
                if (ads.InterShows > before)
                {
                    shown++;
                    stamps.Add(play);
                    clock.Advance(5);
                    elapsed += 5;
                }
            }
            return (shown, play, stamps);
        }

        [Test]
        public void TwentyConsecutiveFortySecondRunsYieldZero()
        {
            foreach (var preset in new[] { "light", "standard", "firm" })
            {
                var r = Session(preset, 40f, 20 * 43 - 1);
                Assert.AreEqual(0, r.shown, preset + ": runs under 45 s never get an interstitial");
            }
        }

        [Test]
        public void ThirtyMinuteSessionOfThreeMinuteRuns()
        {
            var light = Session("light", 180f, 1800);
            Assert.LessOrEqual(light.shown, 6, "light");
            Assert.Greater(light.shown, 0, "the rules do let interstitials through");
            foreach (var preset in new[] { "light", "standard", "firm" })
            {
                var r = Session(preset, 180f, 1800);
                Assert.LessOrEqual(r.shown / (r.play / 600.0), 3.5 + 1e-9, preset + ": per 10 min of play");
                // And in any window of 10 minutes of play, never more than 3.5.
                foreach (var start in r.stamps)
                    Assert.LessOrEqual(r.stamps.Count(s => s >= start && s < start + 600.0), 3, preset + ": sliding 10-minute window");
                TestContext.WriteLine(preset + ": " + r.shown + " interstitials in " + r.play + " s of play");
            }
        }

        [Test]
        public void FiveMinuteRunsUnderFirmStayUnderTheCeiling()
        {
            var r = Session("firm", 125f, 3600);
            Assert.LessOrEqual(r.shown / (r.play / 600.0), 3.5 + 1e-9);
        }

        [Test]
        public void FloorsCannotBeConfiguredLower()
        {
            var cfg = new AdConfig { inter_cooldown_s = 30f, inter_min_play_s = 10f, inter_max_per_10min = 9f, inter_grace_runs = 1 };
            Assert.AreEqual(120f, cfg.CooldownSeconds);
            Assert.AreEqual(90f, cfg.MinPlaySeconds);
            Assert.AreEqual(3.5f, cfg.MaxPer10Min);
            Assert.AreEqual(3, cfg.GraceRuns, "never below the any-ad gate");
            var json = new AdConfig();
            json.ApplyJson("{\"inter_cooldown_s\": 5, \"inter_min_play_s\": 1, \"inter_max_per_10min\": 100}");
            Assert.AreEqual(120f, json.CooldownSeconds);
            Assert.AreEqual(90f, json.MinPlaySeconds);
            Assert.AreEqual(3.5f, json.MaxPer10Min);
        }

        [Test]
        public void PresetsHaveTheSpecValues()
        {
            var l = new AdConfig();
            Assert.AreEqual("light", l.PresetName, "light is the soft-launch default");
            Assert.AreEqual((240f, 210f, 8), (l.CooldownSeconds, l.MinPlaySeconds, l.GraceRuns));
            var s = new AdConfig { inter_preset = "standard" };
            Assert.AreEqual((180f, 150f, 5), (s.CooldownSeconds, s.MinPlaySeconds, s.GraceRuns));
            var f = new AdConfig { inter_preset = "firm" };
            Assert.AreEqual((150f, 120f, 4), (f.CooldownSeconds, f.MinPlaySeconds, f.GraceRuns));
            Assert.AreEqual("light", new AdConfig { inter_preset = "aggressive" }.PresetName, "unknown presets fall back to the gentlest");
        }

        [Test]
        public void RateCapHoldsEvenWithTheFloorValues()
        {
            // cooldown 120 / play 90 would allow 5 per 10 min of play; the code ceiling keeps it at 3.
            var clock = new FakeClock();
            var gov = AdKit.ReadyForInter(clock, new AdConfig { inter_preset = "firm", inter_cooldown_s = 120f, inter_min_play_s = 90f });
            var ctx = AdKit.PassingInter();
            int shown = 0;
            for (int i = 0; i < 12; i++)
            {
                if (i % 5 == 0 && i > 0)
                {
                    gov.OnSessionStarted();
                    gov.OnRunEnded(100f);
                    gov.OnRunEnded(100f);
                }
                if (gov.ShouldShowInterstitial(ctx, out _))
                {
                    gov.OnInterstitialShown();
                    shown++;
                }
                gov.AddPlaySeconds(100f);
                clock.Advance(130);
            }
            Assert.LessOrEqual(shown / (1200.0 / 600.0), 3.5);
            Assert.GreaterOrEqual(shown, 4, "the cap limits, it does not forbid");
        }
    }

    public class AdBreakPassTests
    {
        [Test]
        public void PassStacksToSixtyMinutesAtMost()
        {
            var clock = new FakeClock();
            var gov = AdKit.Governor(clock);
            var ctx = AdKit.PassingOffer();
            Assert.IsTrue(gov.CanOffer(Placement.AdBreak, ctx, out _));
            gov.GrantAdBreak();
            Assert.AreEqual(1800.0, gov.AdBreakRemainingSeconds, 1e-6);
            Assert.IsTrue(gov.CanOffer(Placement.AdBreak, ctx, out string r1), "30 min left: a grant still adds 30 (" + r1 + ")");
            gov.GrantAdBreak();
            Assert.AreEqual(3600.0, gov.AdBreakRemainingSeconds, 1e-6);
            Assert.IsFalse(gov.CanOffer(Placement.AdBreak, ctx, out string r2));
            Assert.AreEqual(OfferReason.StackFull, r2, "hidden when the stack is full");
            gov.GrantAdBreak(); // even if granted anyway: min(max(now, until) + 30 min, now + 60 min)
            Assert.AreEqual(3600.0, gov.AdBreakRemainingSeconds, 1e-6);
            clock.Advance(1200);
            gov.GrantAdBreak();
            Assert.AreEqual(3600.0, gov.AdBreakRemainingSeconds, 1e-6, "40 min left + 30 → capped at 60");
        }

        [Test]
        public void FourPassesPerDay()
        {
            var clock = new FakeClock();
            var gov = AdKit.Governor(clock);
            var ctx = AdKit.PassingOffer();
            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(gov.CanOffer(Placement.AdBreak, ctx, out string r), "pass " + (i + 1) + ": " + r);
                gov.GrantAdBreak();
                clock.Advance(2 * 3600);
            }
            Assert.IsFalse(gov.CanOffer(Placement.AdBreak, ctx, out string reason));
            Assert.AreEqual(OfferReason.DailyCap, reason);
            clock.Today++;
            Assert.IsTrue(gov.CanOffer(Placement.AdBreak, ctx, out _), "a new day");
        }

        [Test]
        public void ProactiveCardAtMostOncePerLocalDay()
        {
            var clock = new FakeClock();
            var gov = AdKit.Governor(clock);
            var ctx = AdKit.PassingOffer();
            Assert.IsFalse(gov.ShouldShowProactiveAdBreak(ctx, out string r0));
            Assert.AreEqual(OfferReason.NotYet, r0, "only after today's 2nd interstitial");
            gov.OnInterstitialShown();
            Assert.IsFalse(gov.ShouldShowProactiveAdBreak(ctx, out _));
            gov.OnInterstitialShown();
            Assert.IsTrue(gov.ShouldShowProactiveAdBreak(ctx, out string r1), r1);
            gov.OnProactiveAdBreakShown();
            gov.OnOfferDeclined(Placement.AdBreak);
            gov.OnInterstitialShown();
            Assert.IsFalse(gov.ShouldShowProactiveAdBreak(ctx, out string r2));
            Assert.AreEqual(OfferReason.AlreadyToday, r2);
            clock.Today++;
            Assert.IsFalse(gov.ShouldShowProactiveAdBreak(ctx, out _), "new day, no interstitial yet");
            gov.OnInterstitialShown();
            gov.OnInterstitialShown();
            Assert.IsTrue(gov.ShouldShowProactiveAdBreak(ctx, out _), "once per local day: the next day it may come again");
        }

        [Test]
        public void ProactiveCardNeverAfterAReviveCard()
        {
            var clock = new FakeClock();
            var gov = AdKit.Governor(clock);
            gov.OnInterstitialShown();
            gov.OnInterstitialShown();
            gov.OnOfferShown(Placement.ReviveSecondWind);
            gov.OnOfferDeclined(Placement.ReviveSecondWind);
            Assert.IsFalse(gov.ShouldShowProactiveAdBreak(AdKit.PassingOffer(), out string r));
            Assert.AreEqual(OfferReason.ReviveShown, r);
            gov.OnRunStarted();
            Assert.IsTrue(gov.ShouldShowProactiveAdBreak(AdKit.PassingOffer(), out _));
        }

        [Test]
        public void PassSuppressesInterstitialsOnlyAndThenSaysItEnded()
        {
            var clock = new FakeClock();
            var gov = AdKit.ReadyForInter(clock);
            var ctx = AdKit.PassingInter();
            gov.GrantAdBreak();
            Assert.AreEqual(SkipReason.AdBreakActive, AdKit.Reason(gov, ctx));
            Assert.IsTrue(gov.CanOffer(Placement.ReviveSecondWind, AdKit.PassingOffer(), out _), "rewarded offers stay the player's choice");
            Assert.IsFalse(gov.TakeAdBreakEndNotice(), "no notice while it runs");
            clock.Advance(1799);
            Assert.AreEqual(SkipReason.AdBreakActive, AdKit.Reason(gov, ctx));
            clock.Advance(1);
            Assert.IsFalse(gov.AdBreakActive);
            Assert.IsTrue(gov.TakeAdBreakEndNotice(), "'Pauza de reclame s-a încheiat.' once");
            Assert.IsFalse(gov.TakeAdBreakEndNotice());
            Assert.AreNotEqual(SkipReason.AdBreakActive, AdKit.Reason(gov, ctx));
            Assert.AreEqual("Fără reclame încă 12:40", AdText.AdBreakRemaining(760));
        }

        [Test]
        public void PassSurvivesARestart()
        {
            var clock = new FakeClock();
            var store = new FakeStore();
            var gov = AdKit.Governor(clock, store: store);
            gov.GrantAdBreak();
            var again = new AdGovernor(new AdConfig(), clock, store);
            Assert.IsTrue(again.AdBreakActive);
            Assert.AreEqual(1800.0, again.AdBreakRemainingSeconds, 1e-6);
            Assert.AreEqual(gov.Bucket, again.Bucket, "the bucket is stable across launches");
        }
    }

    public class AdCohortTests
    {
        [Test]
        public void HoldoutBucketsNeverShowAnInterstitial()
        {
            for (int bucket = 0; bucket < 100; bucket++)
            {
                var clock = new FakeClock();
                var gov = AdKit.ReadyForInter(clock, bucket: bucket);
                Assert.AreEqual(bucket, gov.Bucket);
                bool shows = gov.ShouldShowInterstitial(AdKit.PassingInter(), out string reason);
                if (bucket <= 9)
                {
                    Assert.IsFalse(shows, "bucket " + bucket + " is the control group");
                    Assert.AreEqual(SkipReason.Disabled, reason);
                }
                else Assert.IsTrue(shows, "bucket " + bucket + ": " + reason);

                bool extra = gov.CanOffer(Placement.ExtraSlot, AdKit.PassingOffer(), out string er);
                Assert.AreEqual(bucket < 10 || bucket > 14, extra, "bucket " + bucket + " extra slot: " + er);
            }
        }

        [Test]
        public void BucketIsAStableHashOfTheInstallId()
        {
            Assert.AreEqual((int)(SeededRandom.FromString("abc") % 100UL), AdState.BucketOf("abc"));
            var counts = new int[100];
            for (int i = 0; i < 20000; i++) counts[AdState.BucketOf(Guid.NewGuid().ToString("N"))]++;
            Assert.Greater(counts.Min(), 100, "roughly uniform");
            var store = new FakeStore();
            var a = new AdGovernor(new AdConfig(), new FakeClock(), store);
            var b = new AdGovernor(new AdConfig(), new FakeClock(), store);
            Assert.AreEqual(a.State.InstallId, b.State.InstallId, "the install id is created once");
            Assert.AreEqual(32, a.State.InstallId.Length, "a GUID");
        }

        [Test]
        public void EveryAdEventCarriesBucketAndExperiments()
        {
            var clock = new FakeClock();
            var log = new RecordingLog();
            var gov = AdKit.ReadyForInter(clock, bucket: 42, log: log);
            gov.Config.experiments = "ab_pass30";
            var gate = new InterstitialGate(gov, new FakeAds());
            gate.TryShow(AdKit.PassingInter(), () => { });
            for (int i = 0; i < 20; i++) gate.Update(0.1f);
            gov.EvaluateOffer(Placement.AdBreak, AdKit.PassingOffer(), out _);
            Assert.Greater(log.Events.Count, 3);
            foreach (var e in log.Events)
            {
                Assert.AreEqual(42, e.Bucket, e.Name);
                StringAssert.Contains("inter=light", e.Experiments);
                StringAssert.Contains("ab_pass30", e.Experiments);
            }
            Assert.AreEqual("ad_inter_skipped:cooldown|inter=light", TelemetryAdLog.Format("ad_inter_skipped", "cooldown", "inter=light"));
            CollectionAssert.IsSubsetOf(new[] { "ad_inter_eligible", "ad_show_result", "ad_inter_shown", "ad_inter_closed", "ad_offer_eligible" }, log.Events.Select(e => e.Name).ToList());
        }

        [Test]
        public void KillSwitchesTurnEachPlacementOff()
        {
            var ctx = AdKit.PassingOffer();
            var cfg = new AdConfig { revive_enabled = false, extra_slot_enabled = false, adbreak_enabled = false };
            var gov = AdKit.Governor(new FakeClock(), cfg);
            foreach (var p in new[] { Placement.ReviveSecondWind, Placement.ExtraSlot, Placement.AdBreak })
            {
                Assert.IsFalse(gov.CanOffer(p, ctx, out string r), p.ToString());
                Assert.AreEqual(OfferReason.Disabled, r);
            }
            Assert.IsFalse(gov.CanOffer(Placement.InterRunEnd, ctx, out _), "the interstitial is never an offer");
            Assert.IsFalse(new AdConfig().banner_enabled, "no banner");
        }

        [Test]
        public void ConfigJsonOverridesKnownFieldsOnly()
        {
            var cfg = new AdConfig();
            var ignored = new List<string>();
            Assert.IsTrue(cfg.ApplyJson("{ \"inter_preset\": \"standard\", \"revive_enabled\": false, \"adbreak_max_per_day\": 3, \"revive_push_back\": 0.25, \"nope\": 1, \"inter_max_per_session\": \"x\" }", ignored));
            Assert.AreEqual("standard", cfg.inter_preset);
            Assert.IsFalse(cfg.revive_enabled);
            Assert.AreEqual(3, cfg.adbreak_max_per_day);
            Assert.AreEqual(0.25f, cfg.revive_push_back);
            CollectionAssert.AreEquivalent(new[] { "nope", "inter_max_per_session" }, ignored);
            Assert.AreEqual(5, cfg.inter_max_per_session, "a wrong type changes nothing");
            var broken = new AdConfig();
            Assert.IsFalse(broken.ApplyJson("{ \"inter_preset\": "));
            Assert.AreEqual("light", broken.inter_preset);
        }
    }

    public class AdFlowTests
    {
        static (AdGovernor gov, FakeAds ads, RecordingLog log) Setup(bool firstFreeUsed = true)
        {
            var log = new RecordingLog();
            var gov = AdKit.Governor(new FakeClock(), log: log);
            if (firstFreeUsed)
            {
                gov.State.FirstFreeReviveUsed = true;
                gov.State.FirstFreeExtraSlotUsed = true;
            }
            return (gov, new FakeAds(), log);
        }

        static RewardedOffer Revive(AdGovernor gov, FakeAds ads, Action<bool> grant) =>
            new RewardedOffer(Placement.ReviveSecondWind, gov, ads, grant, gov.Config.revive_load_wait_s, true, true);

        static RewardedOffer Extra(AdGovernor gov, FakeAds ads, Action<bool> grant) =>
            new RewardedOffer(Placement.ExtraSlot, gov, ads, grant, 0f, false, false);

        [Test]
        public void RewardOnlyOnCompleted()
        {
            foreach (AdResult r in Enum.GetValues(typeof(AdResult)))
            {
                var (gov, ads, _) = Setup();
                ads.Result = r;
                int granted = 0;
                var offer = Extra(gov, ads, free => granted++);
                offer.Accept();
                Assert.AreEqual(r == AdResult.Completed ? 1 : 0, granted, r.ToString());
                Assert.IsFalse(offer.IsOpen, r + ": the card closes, nothing else changes");
                Assert.IsNull(gov.ActiveSurface);
            }
        }

        [Test]
        public void ReviveClosedEarlyReturnsToTheSameCard()
        {
            var (gov, ads, _) = Setup();
            ads.Result = AdResult.ClosedEarly;
            int granted = 0;
            var offer = Revive(gov, ads, free => granted++);
            offer.Accept();
            Assert.AreEqual(OfferPhase.Card, offer.Phase);
            Assert.AreEqual(0, granted);
            ads.Result = AdResult.Completed;
            offer.Accept();
            Assert.AreEqual(OfferPhase.Granted, offer.Phase);
            Assert.AreEqual(1, granted);
        }

        [Test]
        public void ForceFailNeverLeavesAFlowStuck()
        {
            // Revive: spinner up to 3 s, then the once-per-session fallback grant.
            var (gov, ads, log) = Setup();
            ads.ForceFail = true;
            int granted = 0;
            var offer = Revive(gov, ads, free => granted++);
            offer.Accept();
            Assert.AreEqual(OfferPhase.Loading, offer.Phase);
            for (int i = 0; i < 29; i++) offer.Update(0.1f);
            Assert.AreEqual(OfferPhase.Loading, offer.Phase, "the spinner waits up to 3 s");
            offer.Update(0.2f);
            Assert.AreEqual(OfferPhase.Granted, offer.Phase);
            Assert.IsTrue(offer.FallbackUsed, "toast: 'Reclama nu s-a încărcat. Îți dăm revenirea oricum.'");
            Assert.AreEqual(1, granted);
            Assert.AreEqual(1, log.Count("ad_fallback_grant"));

            // The second one in the same session: "Nicio reclamă disponibilă acum", accept disabled, decline works.
            var second = Revive(gov, ads, free => granted++);
            second.Accept();
            for (int i = 0; i < 40; i++) second.Update(0.1f);
            Assert.AreEqual(OfferPhase.NoAd, second.Phase);
            Assert.IsFalse(second.AcceptEnabled);
            second.Accept();
            Assert.AreEqual(OfferPhase.NoAd, second.Phase);
            Assert.IsTrue(second.DeclineEnabled);
            second.Decline();
            Assert.IsFalse(second.IsOpen);
            Assert.AreEqual(1, granted);

            // A new session has a new fallback.
            gov.OnSessionStarted();
            var third = Revive(gov, ads, free => granted++);
            third.Accept();
            for (int i = 0; i < 40 && third.IsOpen; i++) third.Update(0.1f);
            Assert.AreEqual(OfferPhase.Granted, third.Phase);

            // "+1 Loc" and the ad-break pass: nothing changes, the card closes.
            foreach (var p in new[] { Placement.ExtraSlot, Placement.AdBreak })
            {
                var o = new RewardedOffer(p, gov, ads, free => granted++, 0f, false, false);
                o.Accept();
                for (int i = 0; i < 40 && o.IsOpen; i++) o.Update(0.1f);
                Assert.IsFalse(o.IsOpen, p + " closes");
                Assert.AreEqual(OfferPhase.Closed, o.Phase);
            }
            Assert.AreEqual(2, granted);
            Assert.IsNull(gov.ActiveSurface, "no surface left open");

            // The interstitial: the requested run starts on any result.
            var clock = new FakeClock();
            var g2 = AdKit.ReadyForInter(clock);
            var gate = new InterstitialGate(g2, ads);
            int started = 0;
            gate.TryShow(AdKit.PassingInter(), () => started++);
            for (int i = 0; i < 30; i++) gate.Update(0.1f);
            Assert.AreEqual(1, started);
            Assert.IsFalse(gate.Blocking);
            ads.ForceFail = false;
            ads.InterReady = true;
            ads.Result = AdResult.Failed;
            gate.TryShow(AdKit.PassingInter(), () => started++);
            for (int i = 0; i < 30; i++) gate.Update(0.1f);
            Assert.AreEqual(2, started, "a failed show starts the run too");
            Assert.AreEqual(InterstitialGate.Phase.Idle, gate.Current);
        }

        [Test]
        public void FlakyAdCannotSpinForever()
        {
            var (gov, ads, _) = Setup();
            ads.Result = AdResult.Failed; // ready, but every show fails
            gov.OnFallbackGrant(Placement.ReviveSecondWind); // the session's fallback is spent
            var offer = Revive(gov, ads, free => { });
            offer.Accept();
            for (int i = 0; i < 50 && offer.Phase != OfferPhase.NoAd; i++) offer.Update(0.1f);
            Assert.AreEqual(OfferPhase.NoAd, offer.Phase);
            Assert.LessOrEqual(ads.RewardedShows, 40);
        }

        [Test]
        public void FirstLifetimeUseIsFreeWithNoAd()
        {
            var (gov, ads, _) = Setup(false);
            int granted = 0;
            var offer = Revive(gov, ads, free => { granted++; Assert.IsTrue(free); });
            Assert.IsTrue(offer.Free, "'Revino gratis'");
            offer.Accept();
            Assert.AreEqual(1, granted);
            Assert.AreEqual(0, ads.RewardedShows);
            Assert.IsFalse(Revive(gov, ads, free => { }).Free, "only the first one");
            Assert.IsTrue(Extra(gov, ads, free => { }).Free, "'Încearcă gratis' is separate");
            Assert.AreEqual(double.NegativeInfinity, gov.State.LastRewardedUtc, "no ad was watched: the interstitial clock is untouched");
        }

        [Test]
        public void EachInterstitialEvaluationLogsExactlyOneShownOrSkipped()
        {
            var clock = new FakeClock();
            var log = new RecordingLog();
            var gov = AdKit.ReadyForInter(clock, log: log);
            var ads = new FakeAds();
            var gate = new InterstitialGate(gov, ads);
            var rnd = new Random(7);
            for (int k = 0; k < 60; k++)
            {
                ads.InterReady = rnd.Next(4) > 0;
                ads.Result = (AdResult)rnd.Next(4);
                var ctx = AdKit.PassingInter();
                ctx.runSeconds = rnd.Next(2) == 0 ? 30f : 200f;
                int before = log.Count("ad_inter_shown") + log.Count("ad_inter_skipped");
                bool done = false;
                gate.TryShow(ctx, () => done = true);
                for (int i = 0; i < 40 && !done; i++) gate.Update(0.1f);
                Assert.IsTrue(done);
                Assert.AreEqual(before + 1, log.Count("ad_inter_shown") + log.Count("ad_inter_skipped"), "evaluation " + k);
                gov.AddPlaySeconds(300f);
                clock.Advance(300);
            }
            Assert.AreEqual(60, gate.Evaluations);
        }

        [Test]
        public void OneSurfaceAtATimeReviveFirst()
        {
            var (gov, ads, _) = Setup();
            var ctx = AdKit.PassingOffer();
            gov.OnOfferShown(Placement.ReviveSecondWind);
            Assert.IsFalse(gov.CanOffer(Placement.AdBreak, ctx, out string r));
            Assert.AreEqual(OfferReason.SurfaceBusy, r);
            gov.OnOfferDeclined(Placement.ReviveSecondWind);
            gov.OnOfferShown(Placement.AdBreak);
            Assert.IsTrue(gov.CanOffer(Placement.ReviveSecondWind, ctx, out _), "the revive outranks the ad-break card");
        }

        [Test]
        public void CardButtonsHaveIdenticalSizes()
        {
            for (int w = 200; w <= 1400; w += 37)
            {
                OfferCardLayout.Buttons(10, 500, w, 90, 12, out var a, out var d);
                Assert.AreEqual(a.W, d.W);
                Assert.AreEqual(a.H, d.H);
                Assert.AreEqual(a.Y, d.Y);
                Assert.LessOrEqual(a.X + a.W, d.X, "side by side, no overlap");
            }
        }

        [Test]
        public void PlayerTextsAreHonest()
        {
            Assert.AreEqual("Titanul #5 · 62% distrus · Scor 31.400", AdText.ReviveStake(5, 0.62f, 31400));
            Assert.AreEqual("Ajutoare rămase în rundă: 2", AdText.ExtraSlotFooter(2));
            foreach (var text in new[] { AdText.ReviveTitle, AdText.ReviveValue, AdText.ReviveFooter, AdText.ExtraSlotValue, AdText.AdBreakValue })
                StringAssert.DoesNotContain("record", text.ToLowerInvariant(), "no record-gap or fear line on a card");
            Assert.AreEqual(AdText.AdBreakAccept, AdText.ExtraSlotAccept);
        }
    }
}
