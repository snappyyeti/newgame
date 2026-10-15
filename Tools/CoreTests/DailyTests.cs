using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using Titan.Core;
using Titan.Core.Ads;

namespace Titan.Tests
{
    /// <summary>RET-3: Titanul Zilei (seed, fairness salt, attempt, resume, share text) and the forgiving streak.</summary>
    public class DailyTests
    {
        static readonly DailyConfig Cfg = new DailyConfig();

        static Dictionary<Int3, int> Layout(Monster m) => m.Cubes.ToDictionary(kv => kv.Key, kv => kv.Value);

        // ------------------------------------------------------------ mode

        [Test]
        public void TheDailyIsThreeTitansAndEndsWithAWin()
        {
            var start = Daily.Start(new DailyPick { Day = 287, Salt = 1 }, Cfg, true);
            Assert.AreEqual(unchecked((long)Daily.DaySeed(287, 1)), start.Seed);
            Assert.AreEqual(Telemetry.ModeDailyOfficial, start.Mode);
            var g = new TitanGame(new TitanConfig(), start.Seed, 1, 0, null, start.Plan);
            Assert.AreEqual(3, g.MaxTitans);
            var cfg = new TitanConfig();
            int[] levels = { 3, 4, 5 }, clock = { 6, 7, 8 };
            for (int k = 1; k <= 3; k++)
            {
                var t = new TitanGame(cfg, start.Seed, k, 0, null, start.Plan);
                Assert.AreEqual(levels[k - 1], t.BuildLevel, "titan " + k);
                Assert.AreEqual(k == 3 ? TitanRole.Boss : TitanRole.Normal, t.Role);
                Assert.AreEqual(cfg.ApproachSeconds(clock[k - 1], t.Monster.Count), t.ApproachSeconds, 1e-4, "the clock of titan " + clock[k - 1]);
                // Titan k uses Derive(daySeed, k): the same layout as an endless seeded titan k built at that level.
                var reference = MonsterBuilder.Build(levels[k - 1], new SeededRandom(SeededRandom.Derive(Daily.DaySeed(287, 1), (ulong)k)));
                CollectionAssert.AreEquivalent(Layout(reference), Layout(t.Monster));
            }
            Assert.IsTrue(g.HoldApproach, "the attempt starts at the first deploy: titan 1 waits for it");

            // Defeating titan 3 ends the run with Completed (a win); the bonus is still paid.
            var last = new TitanGame(cfg, start.Seed, 3, 1000, n => LoopV2Tests.ArmTitan(3), start.Plan);
            for (int guard = 0; guard < 4000 && !last.IsOver; guard++)
            {
                for (int i = 0; i < 3; i++)
                    if (last.CanDeploy(i) && last.Monster.HasVisibleOfColor(last.Tray[i].Color)) last.Deploy(i);
                last.Tick(0.05f);
            }
            Assert.AreEqual(LoseReason.Completed, last.Lost);
            Assert.AreEqual(3, last.Level, "no titan 4");
            Assert.IsTrue(last.Events.Any(e => e.Kind == EventKind.TitanDefeated && e.Level == 3));
            Assert.IsFalse(last.Events.Any(e => e.Kind == EventKind.TitanSpawned && e.Level == 4));
            Assert.IsFalse(last.CanRevive, "a win is never revived");
        }

        // ------------------------------------------------------------ fairness salt

        static void CheckPick(DailyPick p)
        {
            Assert.That(p.Salt, Is.InRange(0, Cfg.DailyMaxSalt), "day " + p.Day);
            if (p.Fallback)
            {
                Assert.AreEqual(0, p.Salt, "the documented fallback: salt 0 with levels {2, 3, 4}");
                return;
            }
            Assert.GreaterOrEqual(p.ClearRate, Cfg.DailyMinClearRate - 1e-9, "day " + p.Day + ": clear-all");
            Assert.GreaterOrEqual(p.Titan1Rate, Cfg.DailyMinTitan1Rate - 1e-9, "day " + p.Day + ": titan 1");
        }

        /// <summary>
        /// Days 0..730 (DAILY_SWEEP=1; about a minute) or every 10th day by default: every pick is a salt in 0..3 that
        /// meets clear ≥ 35% and titan 1 ≥ 90%, or the documented fallback.
        /// </summary>
        [Test]
        public void PickSaltIsFairForEveryDay()
        {
            bool full = Environment.GetEnvironmentVariable("DAILY_SWEEP") == "1";
            var days = Enumerable.Range(0, 731).Where(d => full || d % 10 == 0).ToArray();
            var picks = new DailyPick[days.Length];
            Parallel.For(0, days.Length, i => picks[i] = DailyValidator.PickSalt(days[i], Cfg, null, false));
            foreach (var p in picks) CheckPick(p);
            var bySalt = picks.GroupBy(p => p.Fallback ? "fallback" : "salt " + p.Salt).OrderBy(g => g.Key).Select(g => g.Key + ": " + g.Count());
            TestContext.Out.WriteLine(days.Length + " days: " + string.Join(", ", bySalt));
        }

        /// <summary>
        /// 30 random days (in parallel, as a desktop tool would) in under 2 s. Timed twice after a JIT warm-up and the
        /// faster run counts: a shared CI machine can stall one run.
        /// </summary>
        [Test]
        public void ThirtyRandomDaysTakeUnderTwoSeconds()
        {
            DailyValidator.PickSalt(5, Cfg); // JIT warm-up
            var r = new SeededRandom(20261015);
            var days = Enumerable.Range(0, 30).Select(_ => r.Range(0, 731)).ToArray();
            double best = double.MaxValue;
            for (int run = 0; run < 2 && best >= 2.0; run++)
            {
                var picks = new DailyPick[days.Length];
                var sw = Stopwatch.StartNew();
                Parallel.For(0, days.Length, i => picks[i] = DailyValidator.PickSalt(days[i], Cfg, null, false));
                sw.Stop();
                foreach (var p in picks) CheckPick(p);
                best = Math.Min(best, sw.Elapsed.TotalSeconds);
                TestContext.Out.WriteLine("30 random days: " + sw.ElapsedMilliseconds + " ms on " + Environment.ProcessorCount + " cores");
            }
            Assert.Less(best, 2.0, "30 random days");
        }

        [Test]
        public void TheSameDayGivesTheSameSaltAndTitanEverywhere()
        {
            foreach (int day in new[] { 0, 287, 730 })
            {
                var a = DailyValidator.PickSalt(day, new DailyConfig());
                var b = DailyValidator.PickSalt(day, new DailyConfig());
                Assert.AreEqual(a.Salt, b.Salt);
                Assert.AreEqual(a.Fallback, b.Fallback);
                var ga = new TitanGame(new TitanConfig(), Daily.Start(a, Cfg, true).Seed, 1, 0, null, Daily.Start(a, Cfg, true).Plan);
                var gb = new TitanGame(new TitanConfig(), Daily.Start(b, Cfg, true).Seed, 1, 0, null, Daily.Start(b, Cfg, true).Plan);
                CollectionAssert.AreEquivalent(Layout(ga.Monster), Layout(gb.Monster), "identical titan 1 layout");
                CollectionAssert.AreEqual(ga.Tray.Select(c => c.ToString()), gb.Tray.Select(c => c.ToString()), "and tray");
            }
            Assert.AreNotEqual(Daily.DaySeed(1, 0), Daily.DaySeed(2, 0));
            Assert.AreEqual(Daily.DaySeed(9, 0) + 3, Daily.DaySeed(9, 3), "daySeed = FromString(...) + salt");
            Assert.AreEqual(SeededRandom.FromString("titan-daily-v1:9"), Daily.DaySeed(9, 0));
        }

        [Test]
        public void TheValidatorAnswerEqualsTheFullFortyGames()
        {
            // The early stop never changes the answer: compare with all 40 attempts played.
            foreach (int day in new[] { 3, 148, 185, 400 })
                for (int salt = 0; salt <= 1; salt++)
                {
                    ulong seed = Daily.DaySeed(day, salt);
                    var plan = Daily.Plan(Cfg.DailyBuildLevels, Cfg);
                    int clear = 0, titan1 = 0;
                    for (int i = 0; i < Cfg.ValidatorGames; i++)
                    {
                        int t = DailyValidator.Attempt(new TitanConfig(), plan, seed, (ulong)i, Cfg);
                        if (t >= 3) clear++;
                        if (t >= 1) titan1++;
                    }
                    bool full = clear >= 14 && titan1 >= 36;
                    Assert.AreEqual(full, DailyValidator.Measure(seed, plan, Cfg, new TitanConfig(), out _, out _), "day " + day + " salt " + salt);
                }
        }

        // ------------------------------------------------------------ no ads, no helpers

        [Test]
        public void TheDailyGetsNoOfferAndNoInterstitial()
        {
            var clock = new FakeClock();
            var gov = AdKit.ReadyForInter(clock);
            var o = AdKit.PassingOffer();
            Assert.IsTrue(gov.CanOffer(Placement.ReviveSecondWind, o, out _), "the same context passes outside the daily");
            o.isDailyRanked = true;
            foreach (Placement p in Enum.GetValues(typeof(Placement)))
            {
                Assert.IsFalse(gov.CanOffer(p, o, out var why), p.ToString());
                Assert.IsFalse(gov.FirstFreeAvailable(p) && gov.CanOffer(p, o, out _), "no free teaching use either");
                if (p != Placement.InterRunEnd) Assert.AreEqual(OfferReason.DailyRanked, why);
            }
            var i = AdKit.PassingInter();
            Assert.IsTrue(gov.ShouldShowInterstitial(i, out _));
            i.isDailyRanked = true;
            Assert.IsFalse(gov.ShouldShowInterstitial(i, out var skip));
            Assert.AreEqual(SkipReason.DailyRanked, skip);
            Assert.IsFalse(gov.ShouldShowProactiveAdBreak(o, out _));
        }

        [Test]
        public void TheGameMarksEveryDailyRunAsRanked()
        {
            // GameController.Ads.cs builds both ad contexts with the daily flag (the run on screen or the run asked for).
            string code = File.ReadAllText(EthicsLockTestsAccess.RepoFile("Assets/Titan/Scripts/Game/Ads/GameController.Ads.cs"));
            Assert.AreEqual(2, System.Text.RegularExpressions.Regex.Matches(code, @"isDailyRanked = DailyRanked\b").Count, "OfferCtx and InterCtx");
            Assert.IsFalse(code.Contains("isDailyRanked = false"));
        }

        [Test]
        public void TheDailyAndFtuePathsReferenceNoAdApi()
        {
            var files = new[]
            {
                "Assets/Titan/Scripts/Core/Daily.cs", "Assets/Titan/Scripts/Core/Ftue.cs", "Assets/Titan/Scripts/Core/RunPlan.cs",
                "Assets/Titan/Scripts/Game/DailyMenus.cs", "Assets/Titan/Scripts/Game/FtueHud.cs",
            };
            var banned = new[]
            {
                @"\bRevive\w*", @"\w*ExtraSlot\w*", @"\bRescue\w*", @"\w*Rewarded\w*", @"\w*Interstitial\w*",
                @"\bI?Ads?(Service|Governor|Config|Break|Pass)?\b", @"\bAd[A-Z]\w*", @"\w*Advert\w*", @"\bLevelPlay\w*", @"\bgovernor\b",
            };
            foreach (var file in files)
            {
                string code = File.ReadAllText(EthicsLockTestsAccess.RepoFile(file));
                code = System.Text.RegularExpressions.Regex.Replace(code, @"//.*", "");
                code = System.Text.RegularExpressions.Regex.Replace(code, "/\\*.*?\\*/", "", System.Text.RegularExpressions.RegexOptions.Singleline);
                code = System.Text.RegularExpressions.Regex.Replace(code, "\"(\\\\.|[^\"\\\\])*\"", "\"\"");
                foreach (var pattern in banned)
                {
                    var m = System.Text.RegularExpressions.Regex.Match(code, pattern);
                    Assert.IsFalse(m.Success, file + " references '" + m.Value + "' (the daily and the FTUE are ad-free)");
                }
            }
        }

        // ------------------------------------------------------------ streak

        static StreakState Streak(int current, int lastDay, int shields) => new StreakState { current = current, best = current, lastDay = lastDay, shields = shields, firstDailyDone = true };

        [Test]
        public void ShieldsCoverMissedDays()
        {
            var s = Streak(5, 100, 1);
            Assert.AreEqual(StreakRules.Outcome.ShieldUsed, StreakRules.Resolve(s, 102, out int gap), "1-day gap, 1 shield");
            Assert.AreEqual(1, gap);
            Assert.AreEqual(0, s.shields);
            Assert.AreEqual(5, s.current);
            Assert.AreEqual(101, s.lastDay);

            s = Streak(5, 100, 2);
            Assert.AreEqual(StreakRules.Outcome.ShieldUsed, StreakRules.Resolve(s, 103, out gap), "2-day gap, 2 shields");
            Assert.AreEqual(2, gap);
            Assert.AreEqual(0, s.shields);
            Assert.AreEqual(5, s.current);

            s = Streak(5, 100, 2);
            Assert.AreEqual(StreakRules.Outcome.Reset, StreakRules.Resolve(s, 104, out gap), "3-day gap resets");
            Assert.AreEqual(3, gap);
            Assert.AreEqual(0, s.current);
            Assert.AreEqual(5, s.best, "the best stays");

            s = Streak(5, 100, 0);
            Assert.AreEqual(StreakRules.Outcome.Nothing, StreakRules.Resolve(s, 101, out _), "played yesterday");
            Assert.AreEqual(StreakRules.Outcome.Nothing, StreakRules.Resolve(s, 100, out _), "played today");
            Assert.AreEqual(5, s.current);
        }

        [Test]
        public void ResolutionIsIdempotentTheSameDay()
        {
            foreach (var (shields, today) in new[] { (1, 102), (2, 103), (2, 110), (0, 101) })
            {
                var s = Streak(4, 100, shields);
                StreakRules.Resolve(s, today, out _);
                var snapshot = JsonSerializer.Serialize(s, new JsonSerializerOptions { IncludeFields = true });
                Assert.AreEqual(StreakRules.Outcome.Nothing, StreakRules.Resolve(s, today, out _), "second launch the same day");
                Assert.AreEqual(snapshot, JsonSerializer.Serialize(s, new JsonSerializerOptions { IncludeFields = true }));
            }
            // Through DailyChallenge: the message shows on the first launch of the day only.
            var p = new Profile { streak = Streak(3, 50, 1) };
            var a = new DailyChallenge(p, null, Cfg, null, d => new DailyPick { Day = d });
            a.OnLaunch(52);
            Assert.AreEqual(DailyText.ShieldUsed, a.LaunchMessage);
            var b = new DailyChallenge(p, null, Cfg, null, d => new DailyPick { Day = d });
            b.OnLaunch(52);
            Assert.IsNull(b.LaunchMessage);
            Assert.AreEqual(3, p.streak.current);
        }

        [Test]
        public void ShieldsAreEarnedAndCappedAtTwo()
        {
            var s = new StreakState();
            StreakRules.OnOfficialFinished(s, 10, Cfg);
            Assert.AreEqual(1, s.current);
            Assert.AreEqual(1, s.shields, "the first daily ever gives a shield");
            Assert.IsTrue(s.firstDailyDone);
            StreakRules.OnOfficialFinished(s, 10, Cfg);
            Assert.AreEqual(1, s.current, "one per day");
            for (int day = 11; day <= 16; day++) StreakRules.OnOfficialFinished(s, day, Cfg);
            Assert.AreEqual(7, s.current);
            Assert.AreEqual(2, s.shields, "day 7 of the streak gives one more");
            for (int day = 17; day <= 30; day++) StreakRules.OnOfficialFinished(s, day, Cfg);
            Assert.AreEqual(21, s.current);
            Assert.AreEqual(2, s.shields, "never more than 2");
            Assert.AreEqual(21, s.best);
        }

        [Test]
        public void NoLossFramedCopy()
        {
            var texts = new[] { DailyText.Intro, DailyText.ShieldUsed, DailyText.StreakReset, DailyText.Copied, DailyText.QuitConfirm, DailyText.BestStreak(3), DailyText.CurrentStreak(2) };
            foreach (var t in texts) StringAssert.DoesNotContain("vei pierde", t.ToLowerInvariant());
            Assert.AreEqual("Încercarea de azi e fără ajutoare și fără reclame — aceeași provocare pentru toți.", DailyText.Intro);
            Assert.AreEqual("Un scut ți-a păstrat seria.", DailyText.ShieldUsed);
            Assert.AreEqual("Bine ai revenit! Începe o serie nouă azi.", DailyText.StreakReset);
            Assert.AreEqual("Cea mai lungă serie: 9", DailyText.BestStreak(9));
        }

        // ------------------------------------------------------------ attempt, resume, share

        sealed class Json : ISerializer
        {
            static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
            public string Write(Profile p) => JsonSerializer.Serialize(p, Options);
            public Profile Read(string text) => JsonSerializer.Deserialize<Profile>(text, Options);
        }

        sealed class Prefs : IPrefs
        {
            readonly Dictionary<string, string> s = new Dictionary<string, string>();
            readonly Dictionary<string, int> i = new Dictionary<string, int>();
            public bool HasKey(string key) => s.ContainsKey(key) || i.ContainsKey(key);
            public string GetString(string key) => s.TryGetValue(key, out var v) ? v : null;
            public int GetInt(string key, int fallback) => i.TryGetValue(key, out var v) ? v : fallback;
            public void SetString(string key, string value) => s[key] = value;
            public void SetInt(string key, int value) => i[key] = value;
            public void Flush() { }
        }

        static readonly Func<int, DailyPick> Salt2 = d => new DailyPick { Day = d, Salt = 2 };

        /// <summary>Plays a daily run with the scripted player, feeding the session and the daily, until <paramref name="stop"/>.</summary>
        static void Play(RunSession s, DailyChallenge daily, Func<bool> stop, Action save = null)
        {
            Action<GameEvent> feed = e =>
            {
                s.Process(e);
                if (daily.Process(e)) save?.Invoke();
            };
            foreach (var e in s.Game.Events) feed(e);
            s.Game.Events.Clear();
            for (int k = 0; k < 4000 && !s.Game.IsOver && !stop(); k++)
            {
                RetentionTests.PlayScripted(s.Game, 0.3f, feed, stop);
                s.Update(0.3f);
            }
        }

        [Test]
        public void OpeningAndBackingOutDoesNotUseTheAttempt()
        {
            var p = new Profile();
            var daily = new DailyChallenge(p, null, Cfg, null, Salt2);
            daily.OnLaunch(300);
            var s = new RunSession(p, null, new TitanConfig(), daily.OfficialStart());
            daily.BeginRun(s);
            for (int i = 0; i < 400; i++) s.Game.Tick(0.05f);
            foreach (var e in s.Game.Events) daily.Process(e);
            Assert.IsTrue(daily.OfficialRunUntouched);
            Assert.AreEqual(0, p.daily.status, "no tap: the attempt is not used");
            daily.EndRun();
            Assert.IsTrue(daily.OfficialNotStarted);

            var s2 = new RunSession(p, null, new TitanConfig(), daily.OfficialStart());
            daily.BeginRun(s2);
            int slot = Enumerable.Range(0, 3).First(i => s2.Game.CanDeploy(i));
            s2.Game.Deploy(slot);
            foreach (var e in s2.Game.Events) daily.Process(e);
            Assert.AreEqual(1, p.daily.status, "the first deploy starts the official attempt");
        }

        [Test]
        public void AKillMidAttemptResumesFromTheLastDefeatedTitan()
        {
            var dir = Path.Combine(Path.GetTempPath(), "titan_daily_test_" + Guid.NewGuid().ToString("N"));
            var prefs = new Prefs();
            var store = new ProfileStore(dir, new Json(), prefs);
            var p = store.Load();
            var daily = new DailyChallenge(p, null, Cfg, null, Salt2);
            daily.OnLaunch(400);
            var start = daily.OfficialStart();
            var s = new RunSession(p, null, new TitanConfig(), start);
            daily.BeginRun(s);
            Play(s, daily, () => p.daily.defeated >= 1, () => store.Save(p));
            Assert.AreEqual(1, p.daily.defeated, "titan 1 fell");
            int scoreAfter1 = p.daily.score;
            Assert.Greater(scoreAfter1, 0);
            int piecesAfter1 = p.daily.pieces;
            // Play on a little into titan 2, then the app is killed: nothing after the last save survives.
            RetentionTests.PlayScripted(s.Game, 3f);
            Assert.AreEqual(2, s.Game.Level);

            var reloaded = new ProfileStore(dir, new Json(), prefs).Load();
            Assert.AreEqual(1, reloaded.daily.status, "in progress");
            var again = new DailyChallenge(reloaded, null, Cfg, null, d => throw new AssertionException("the salt is cached in the profile"));
            again.OnLaunch(400);
            Assert.IsTrue(again.OfficialInProgress, "'Continuă încercarea oficială'");
            var resume = again.OfficialStart();
            Assert.AreEqual(start.Seed, resume.Seed, "same seed");
            Assert.AreEqual(2, resume.Titan, "from the titan after the last defeated one");
            Assert.AreEqual(scoreAfter1, resume.Score, "with its score");
            var g = new TitanGame(new TitanConfig(), resume.Seed, resume.Titan, resume.Score, null, resume.Plan);
            var reference = new TitanGame(new TitanConfig(), start.Seed, 2, 0, null, start.Plan);
            CollectionAssert.AreEquivalent(Layout(reference.Monster), Layout(g.Monster), "the same titan 2");
            Assert.AreEqual(scoreAfter1, g.Score);

            // The resumed attempt finishes: the result adds up both parts.
            var s2 = new RunSession(reloaded, null, new TitanConfig(), resume);
            again.BeginRun(s2);
            Play(s2, again, () => false);
            Assert.AreEqual(2, reloaded.daily.status);
            Assert.GreaterOrEqual(reloaded.daily.pieces, piecesAfter1);
            Assert.AreEqual(s2.Game.Score, reloaded.daily.score);
            Assert.AreEqual(1, reloaded.streak.current, "a finished official attempt grows the streak");
            Directory.Delete(dir, true);
        }

        [Test]
        public void MidnightFinalizesAnUnfinishedAttemptOnItsOwnDay()
        {
            var p = new Profile();
            var t = new Telemetry(() => p, null, () => 1);
            var daily = new DailyChallenge(p, t, Cfg, null, Salt2);
            daily.OnLaunch(500);
            daily.Pick();
            p.daily.status = 1;
            p.daily.defeated = 2;
            p.daily.score = 9000;
            p.daily.pieces = 7;
            p.daily.seconds = 95f;

            var next = new DailyChallenge(p, t, Cfg, null, Salt2);
            next.OnLaunch(501);
            Assert.AreEqual(1, p.streak.current, "finalized for day 500");
            Assert.AreEqual(500, p.streak.lastDay);
            Assert.IsNull(next.LaunchMessage, "played yesterday: nothing to say");
            Assert.AreEqual(501, p.daily.day, "a new day starts clean");
            Assert.IsTrue(next.OfficialNotStarted);
            var end = p.log.Single(l => l.e == Telemetry.DailyEnd);
            Assert.AreEqual(500, end.a);
            Assert.AreEqual(2, end.b);
            Assert.IsTrue(p.log.Any(l => l.e == Telemetry.Streak && l.a == 1 && l.b == 0), "streak(new, old)");
        }

        [Test]
        public void PracticeKeepsOnlyItsBestScore()
        {
            var p = new Profile();
            var daily = new DailyChallenge(p, null, Cfg, null, Salt2);
            daily.OnLaunch(600);
            p.daily.salt = 2;
            p.daily.status = 2;
            p.daily.defeated = 1;
            p.daily.lostOn = 2;
            p.daily.score = 4321;
            var s = new RunSession(p, null, new TitanConfig(), daily.PracticeStart());
            Assert.AreEqual(Telemetry.ModeDailyPractice, s.Start.Mode);
            daily.BeginRun(s);
            Play(s, daily, () => false);
            Assert.IsTrue(s.Game.IsOver);
            Assert.AreEqual(s.Game.Score, p.daily.bestPracticeScore);
            Assert.AreEqual(4321, p.daily.score, "the official result never changes");
            Assert.AreEqual(1, p.daily.defeated);
            Assert.AreEqual(0, p.streak.current, "practice does not count for the streak");
            Assert.AreEqual(0, p.bestScore, "nor for the endless record");
            Assert.AreEqual(0, p.bestTitanN);
            Assert.IsFalse(p.HasCheckpoint);
            Assert.AreEqual(1, p.totalRuns, "it is a run");
        }

        [Test]
        public void QuitEndsTheAttemptOnTheTitanOnScreen()
        {
            var p = new Profile();
            var daily = new DailyChallenge(p, null, Cfg, null, Salt2);
            daily.OnLaunch(700);
            var s = new RunSession(p, null, new TitanConfig(), daily.OfficialStart());
            daily.BeginRun(s);
            Assert.IsFalse(daily.QuitOfficial(), "nothing to end before the first tap");
            Play(s, daily, () => p.daily.status == 1 && s.Game.GameTime > 2f);
            Assert.IsTrue(daily.QuitOfficial());
            Assert.AreEqual(2, p.daily.status);
            Assert.AreEqual(1, p.daily.lostOn, "red square on titan 1");
            Assert.AreEqual(0, p.daily.defeated);
            Assert.AreEqual(s.Game.Score, p.daily.score);
            Assert.AreEqual(1, p.streak.current);
            StringAssert.Contains("\U0001F7E5⬜⬜", daily.ShareText());
        }

        [Test]
        public void ShareTextGolden()
        {
            string text = DailyText.Share(288, 2, 3, 41200, 167.9f, 9, "https://example.com/titan");
            Assert.AreEqual(
                "Titanul din Cuburi · Ziua #288\n" +
                "\U0001F7E9\U0001F7E9\U0001F7E5\n" +
                "Scor 41.200 · 2:47 · 9 bucăți rupte\n" +
                "https://example.com/titan", text);
            Assert.AreEqual("Titanul din Cuburi · Ziua #5\n\U0001F7E9\U0001F7E9\U0001F7E9\nScor 0 · 0:05 · 1 bucăți rupte\nL",
                DailyText.Share(5, 3, 0, 0, 5f, 1, "L"));
            Assert.AreEqual("\U0001F7E5⬜⬜", DailyText.Share(1, 0, 1, 10, 1f, 0, "L").Split('\n')[1]);
            Assert.AreEqual("\U0001F7E9", DailyText.Green);
            Assert.AreEqual("⬜", DailyText.Grey);
            Assert.AreEqual("12:05", DailyText.Clock(725.2f));
            Assert.AreEqual("1:00:00", DailyText.UntilMidnight(new DateTime(2026, 10, 15, 23, 0, 0)));
            Assert.AreEqual("0:00:01", DailyText.UntilMidnight(new DateTime(2026, 10, 15, 23, 59, 59)));
            Assert.AreEqual("neîncercat", DailyText.Status(new DailyState()));
            Assert.AreEqual("2/3", DailyText.Status(new DailyState { status = 2, defeated = 2 }));
        }

        [Test]
        public void DailyRunsNeverTouchTheEndlessRecord()
        {
            var p = new Profile { bestScore = 5, bestTitanN = 1 };
            var t = new Telemetry(() => p, null, () => 1);
            var daily = new DailyChallenge(p, t, Cfg, null, Salt2);
            daily.OnLaunch(800);
            var s = new RunSession(p, t, new TitanConfig(), daily.OfficialStart());
            daily.BeginRun(s);
            int records = 0;
            Action<GameEvent> feed = e =>
            {
                if ((s.Process(e) & (SessionSignal.NewFurthestTitan | SessionSignal.CheckpointSaved)) != 0) records++;
                daily.Process(e);
            };
            foreach (var e in s.Game.Events) feed(e);
            s.Game.Events.Clear();
            for (int k = 0; k < 4000 && !s.Game.IsOver; k++)
            {
                RetentionTests.PlayScripted(s.Game, 0.3f, feed);
                if ((s.Update(0.3f) & SessionSignal.NewRecord) != 0) records++;
            }
            Assert.Greater(s.Game.Score, 5);
            Assert.AreEqual(0, records, "no record, furthest titan or checkpoint banner");
            Assert.IsFalse(s.NewBest);
            Assert.IsFalse(s.NewFurthest);
            Assert.AreEqual(5, p.bestScore);
            Assert.AreEqual(1, p.bestTitanN);
            Assert.AreEqual(1, p.totalRuns);
            Assert.AreEqual(2, p.daily.status);
            Assert.IsTrue(p.log.Any(l => l.e == Telemetry.DailyStart && l.a == 800 && l.b == Telemetry.ModeDailyOfficial));
            Assert.IsTrue(p.log.Any(l => l.e == Telemetry.RunStart && l.b == Telemetry.ModeDailyOfficial));
            Assert.IsFalse(p.log.Any(l => l.e == Telemetry.TitanReached));
        }

        [Test]
        public void TheMenuShowsForTheDailyAfterTheFirstRunsOnly()
        {
            var p = new Profile { totalRuns = 2 };
            Assert.IsFalse(RunSession.ShowLaunchOverlay(p, true), "FTUE: the first 3 runs start at once");
            p.totalRuns = 3;
            Assert.IsTrue(RunSession.ShowLaunchOverlay(p, true), "today's daily is open");
            Assert.IsFalse(RunSession.ShowLaunchOverlay(p, false), "nothing waits: straight into the game");
            p.SaveCheckpoint(6, 100, 1);
            Assert.IsTrue(RunSession.ShowLaunchOverlay(p, false), "a checkpoint still shows it (RET-1)");
        }

        // ------------------------------------------------------------ the incremental visibility the validator relies on

        [Test]
        public void VisibleSetsMatchAFullRecount()
        {
            for (int level = 1; level <= 8; level += 3)
                for (int seed = 0; seed < 3; seed++)
                {
                    var m = MonsterBuilder.Build(level, new SeededRandom((ulong)(seed * 31 + level)));
                    var r = new SeededRandom((ulong)(900 + seed));
                    for (int step = 0; step < 150 && m.Count > 0; step++)
                    {
                        var all = m.Cubes.Select(kv => kv.Key).ToList();
                        m.Remove(all[r.Range(0, all.Count)]);
                        var expected = new HashSet<Int3>[MonsterBuilder.MaxColors];
                        for (int color = 0; color < expected.Length; color++) expected[color] = new HashSet<Int3>();
                        foreach (var kv in m.Cubes) if (m.IsVisible(kv.Key)) expected[kv.Value].Add(kv.Key);
                        for (int color = 0; color < MonsterBuilder.MaxColors; color++)
                        {
                            Assert.AreEqual(expected[color].Count, m.VisibleCountOfColor(color), $"L{level} seed {seed} step {step} color {color}");
                            Assert.IsTrue(expected[color].SetEquals(m.VisibleOfColor(color)), $"L{level} seed {seed} step {step} color {color}: the same cubes");
                        }
                        var clone = m.Clone();
                        for (int color = 0; color < MonsterBuilder.MaxColors; color++) Assert.AreEqual(m.VisibleCountOfColor(color), clone.VisibleCountOfColor(color));
                    }
                }
            // Set in front of an existing column hides the old front; replacing keeps visibility with the new color.
            var t = new Monster();
            t.Set(new Int3(0, 0, 2), 1, true);
            Assert.AreEqual(1, t.VisibleCountOfColor(1));
            t.Set(new Int3(0, 0, 0), 2, true);
            Assert.AreEqual(0, t.VisibleCountOfColor(1));
            Assert.AreEqual(1, t.VisibleCountOfColor(2));
            t.Set(new Int3(0, 0, 0), 3, true);
            Assert.AreEqual(0, t.VisibleCountOfColor(2));
            Assert.AreEqual(1, t.VisibleCountOfColor(3));
            t.Set(new Int3(0, 0, 1), 4, true);
            Assert.AreEqual(0, t.VisibleCountOfColor(4), "behind the front: hidden");
        }
    }
}
