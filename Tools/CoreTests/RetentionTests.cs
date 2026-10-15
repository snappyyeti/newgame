using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Titan.Core;

namespace Titan.Tests
{
    /// <summary>RET-1 (profile, day clock, telemetry, seeded runs, checkpoints) and RET-2 (record chase texts).</summary>
    public class RetentionTests
    {
        static readonly TitanConfig Slow = new TitanConfig { MinApproachSeconds = 100000f };

        /// <summary>A deterministic player: every 0.3 s it taps the first cannon with a visible color, else any when the line has room.</summary>
        internal static void PlayScripted(TitanGame g, float seconds, Action<GameEvent> onEvent = null, Func<bool> stop = null)
        {
            const float dt = 0.05f;
            int step = 0;
            for (float t = 0; t < seconds && !g.IsOver; t += dt, step++)
            {
                if (step % 6 == 0) TapOnce(g);
                g.Tick(dt);
                if (onEvent != null) foreach (var e in g.Events) onEvent(e);
                g.Events.Clear();
                if (stop != null && stop()) return;
            }
        }

        static void TapOnce(TitanGame g)
        {
            for (int i = 0; i < g.Tray.Length; i++)
                if (g.CanDeploy(i) && g.Monster.HasVisibleOfColor(g.Tray[i].Color))
                {
                    g.Deploy(i);
                    return;
                }
            if (g.FreeSlots < 3) return;
            for (int i = 0; i < g.Tray.Length; i++)
                if (g.CanDeploy(i))
                {
                    g.Deploy(i);
                    return;
                }
        }

        static Dictionary<Int3, int> Layout(Monster m) => m.Cubes.ToDictionary(kv => kv.Key, kv => kv.Value);

        // ------------------------------------------------------------ seeded runs

        [Test]
        public void SameSeedAndTapsGiveTheSameRun()
        {
            foreach (long seed in new[] { 1L, 42L, -7L, long.MaxValue })
            {
                var a = new TitanGame(new TitanConfig(), seed);
                var b = new TitanGame(new TitanConfig(), seed);
                PlayScripted(a, 120f);
                PlayScripted(b, 120f);
                Assert.AreEqual(a.Score, b.Score, "seed " + seed);
                Assert.AreEqual(a.Level, b.Level, "seed " + seed);
                Assert.AreEqual(a.Lost, b.Lost, "seed " + seed);
                CollectionAssert.AreEquivalent(Layout(a.Monster), Layout(b.Monster), "seed " + seed);
                Assert.Greater(a.Level, 1, "the scripted player defeats titans in 120 s");
            }
            var other = new TitanGame(new TitanConfig(), 2L);
            CollectionAssert.AreNotEquivalent(Layout(new TitanGame(new TitanConfig(), 1L).Monster), Layout(other.Monster), "another seed, another titan");
        }

        [Test]
        public void TitanNDependsOnlyOnTheSeedAndN()
        {
            // The same titan 3 whatever happened in titans 1 and 2 (different taps, different draws).
            var a = new TitanGame(Slow, 99L, 3);
            var layout3 = Layout(a.Monster);
            var b = new TitanGame(Slow, 99L, 1);
            PlayScripted(b, 600f, stop: () => b.Level >= 3);
            Assert.AreEqual(3, b.Level);
            CollectionAssert.AreEquivalent(layout3, Layout(b.Monster));
        }

        // ------------------------------------------------------------ checkpoints

        [Test]
        public void CheckpointsAreSavedExactlyAtCycleStarts()
        {
            for (int n = 1; n <= 200; n++)
                Assert.AreEqual(n > 1 && (n - 1) % 5 == 0, RunSession.IsCheckpointTitan(n), "n = " + n);

            var profile = new Profile();
            var s = new RunSession(profile, null, Slow, RunStart.Fresh(5), n => LoopV2Tests.ArmTitan(3));
            var saved = new List<int>();
            var spawned = new List<int>();
            Action<GameEvent> feed = e =>
            {
                if (e.Kind == EventKind.TitanSpawned) spawned.Add(e.Level);
                if ((s.Process(e) & SessionSignal.CheckpointSaved) != 0)
                {
                    saved.Add(e.Level);
                    Assert.AreEqual(e.Level, profile.checkpoint.n);
                    Assert.AreEqual(e.Score, profile.checkpoint.score);
                    Assert.AreEqual(5L, profile.checkpoint.runSeed);
                }
            };
            foreach (var e in s.Game.Events) feed(e);
            s.Game.Events.Clear();
            PlayScripted(s.Game, 3000f, feed, () => s.Game.Level >= 20);
            Assert.GreaterOrEqual(s.Game.Level, 20, "the tiny titans must all fall");
            CollectionAssert.AreEqual(Enumerable.Range(1, 20), spawned);
            CollectionAssert.AreEqual(new[] { 6, 11, 16 }, saved);
        }

        [Test]
        public void ACheckpointRestartRebuildsTheSameTitanWithTheSavedScore()
        {
            int checkedRuns = 0;
            for (long seed = 1; seed <= 12 && checkedRuns < 2; seed++)
            {
                var profile = new Profile { totalRuns = 5 };
                var run = new RunSession(profile, null, Slow, RunStart.Fresh(seed));
                Dictionary<Int3, int> layout6 = null;
                string tray6 = null;
                int deck6 = -1;
                Action<GameEvent> feed = e =>
                {
                    run.Process(e);
                    if (e.Kind == EventKind.TitanSpawned && e.Level == 6)
                    {
                        layout6 = Layout(run.Game.Monster);
                        tray6 = string.Join(",", run.Game.Tray.Select(c => c?.ToString()));
                        deck6 = run.Game.DeckCount;
                    }
                };
                foreach (var e in run.Game.Events) feed(e);
                run.Game.Events.Clear();
                PlayScripted(run.Game, 1500f, feed, () => run.Game.Level >= 6);
                if (run.Game.Level < 6) continue; // this scripted run lost early: try the next seed

                Assert.AreEqual(6, profile.checkpoint.n);
                Assert.AreEqual(seed, profile.checkpoint.runSeed);
                int saved = profile.checkpoint.score;
                Assert.Greater(saved, 0);
                Assert.IsTrue(RunSession.ShowLaunchOverlay(profile), "5 runs and a checkpoint: the launch overlay shows");

                var resume = RunSession.ResumeStart(profile).Value;
                Assert.AreEqual(Telemetry.ModeCheckpoint, resume.Mode);
                var again = new RunSession(profile, null, Slow, resume);
                foreach (var e in again.Game.Events) again.Process(e);
                Assert.AreEqual(6, again.Game.Level);
                Assert.AreEqual(saved, again.Game.Score, "the score is the snapshot, not more");
                CollectionAssert.AreEquivalent(layout6, Layout(again.Game.Monster), "cube by cube");
                Assert.AreEqual(tray6, string.Join(",", again.Game.Tray.Select(c => c?.ToString())), "the same first tray too");
                Assert.AreEqual(deck6, again.Game.DeckCount);
                Assert.AreEqual(saved, profile.checkpoint.score, "starting on the checkpoint does not save it again");

                // "Joacă din nou" after a checkpoint run replays the checkpoint; "Joc nou" clears it.
                var replay = again.PlayAgainStart(12345);
                Assert.AreEqual(6, replay.Titan);
                Assert.AreEqual(seed, replay.Seed);
                Assert.AreEqual(saved, replay.Score);
                Assert.AreEqual(1, run.PlayAgainStart(777).Titan, "a run from titan 1 replays from titan 1");
                Assert.AreEqual(777, run.PlayAgainStart(777).Seed, "with a new seed");
                var fresh = RunSession.NewGameStart(profile, 31);
                Assert.AreEqual(1, fresh.Titan);
                Assert.IsFalse(profile.HasCheckpoint);
                Assert.IsNull(RunSession.ResumeStart(profile));
                checkedRuns++;
            }
            Assert.AreEqual(2, checkedRuns, "two seeds must reach titan 6");
        }

        [Test]
        public void LaunchOverlayNeedsThreeRunsAndACheckpoint()
        {
            var p = new Profile { totalRuns = 2 };
            p.SaveCheckpoint(11, 5000, 9);
            Assert.IsFalse(RunSession.ShowLaunchOverlay(p));
            p.totalRuns = 3;
            Assert.IsTrue(RunSession.ShowLaunchOverlay(p));
            p.ClearCheckpoint();
            Assert.IsFalse(RunSession.ShowLaunchOverlay(p));
        }

        [Test]
        public void NoCheckpointPathReferencesAnAdApi()
        {
            var files = new[]
            {
                "Assets/Titan/Scripts/Core/RunSession.cs", "Assets/Titan/Scripts/Core/Profile.cs", "Assets/Titan/Scripts/Core/ProfileStore.cs",
                "Assets/Titan/Scripts/Game/ProfileStore.cs", "Assets/Titan/Scripts/Game/RunMenus.cs",
            };
            var banned = new[]
            {
                @"\bRevive\w*", @"\w*ExtraSlot\w*", @"\bRescue\w*", @"\w*Rewarded\w*", @"\w*Interstitial\w*",
                @"\bI?Ads?(Service|Governor|Config|Break|Pass)?\b", @"\bAd[A-Z]\w*", @"\w*Advert\w*", @"\bLevelPlay\w*", @"\bIronSource\w*",
            };
            foreach (var file in files)
            {
                string code = File.ReadAllText(EthicsLockTestsAccess.RepoFile(file));
                code = Regex.Replace(code, @"//.*", "");
                code = Regex.Replace(code, "/\\*.*?\\*/", "", RegexOptions.Singleline);
                code = Regex.Replace(code, "\"(\\\\.|[^\"\\\\])*\"", "\"\""); // player-facing strings are checked by review, not here
                foreach (var pattern in banned)
                {
                    var m = Regex.Match(code, pattern);
                    Assert.IsFalse(m.Success, file + " references '" + m.Value + "' (checkpoints are never ad-gated)");
                }
            }
            // The grep can fail: an ad call would be caught.
            Assert.IsTrue(Regex.IsMatch("ShowRewardedAd(onDone);", @"\w*Rewarded\w*"));
        }

        // ------------------------------------------------------------ record chase and session signals

        [Test]
        public void RecordBannerFiresAtMostOncePerRun()
        {
            foreach (int best in new[] { 0, 30, 2000 })
            {
                var profile = new Profile { bestScore = best };
                var s = new RunSession(profile, null, Slow, RunStart.Fresh(3), n => LoopV2Tests.ArmTitan(3));
                int banners = 0;
                Action<GameEvent> feed = e => s.Process(e);
                for (int k = 0; k < 400 && !s.Game.IsOver; k++)
                {
                    PlayScripted(s.Game, 0.3f, feed);
                    if ((s.Update(0.3f) & SessionSignal.NewRecord) != 0) banners++;
                }
                Assert.Greater(s.Game.Score, 2000, "the run passes every record tried here");
                Assert.AreEqual(best > 0 ? 1 : 0, banners, "best " + best + ": the banner needs a record to beat, and fires once");
                Assert.IsTrue(s.NewBest);
                Assert.AreEqual(best, s.BestAtStart);
            }
        }

        [Test]
        public void FurthestTitanBannerAndBestAreKept()
        {
            var profile = new Profile { bestTitanN = 3, bestScore = 1 };
            var telemetry = new Telemetry(() => profile, null, () => 1000);
            var s = new RunSession(profile, telemetry, Slow, RunStart.Fresh(4), n => LoopV2Tests.ArmTitan(3));
            var furthest = new List<int>();
            Action<GameEvent> feed = e =>
            {
                if ((s.Process(e) & SessionSignal.NewFurthestTitan) != 0) furthest.Add(e.Level);
            };
            foreach (var e in s.Game.Events) feed(e);
            s.Game.Events.Clear();
            PlayScripted(s.Game, 500f, feed, () => s.Game.Level >= 6);
            CollectionAssert.AreEqual(new[] { 4 }, furthest, "once per run, on the first titan past the old furthest");
            Assert.AreEqual(6, profile.bestTitanN);
            Assert.IsTrue(s.NewFurthest);
            Assert.GreaterOrEqual(profile.bestScore, 1);

            var names = profile.log.Select(l => l.e).ToList();
            Assert.AreEqual(Telemetry.RunStart, names[0]);
            Assert.AreEqual(1, profile.log[0].a, "run_start(startTitan, mode)");
            Assert.AreEqual(Telemetry.ModeEndless, profile.log[0].b);
            Assert.AreEqual(5, names.Count(n => n == Telemetry.TitanDefeat));
            Assert.AreEqual(6, names.Count(n => n == Telemetry.TitanSpawn));
            Assert.IsTrue(profile.log.All(l => l.t == 1000));
        }

        [Test]
        public void GameOverCountsTheRunAndKeepsTheBest()
        {
            var profile = new Profile();
            var cfg = new TitanConfig { MinApproachSeconds = 12f, ApproachBaseSeconds = 0f, ApproachSecondsPerCube = 0f };
            var s = new RunSession(profile, new Telemetry(() => profile), cfg, RunStart.Fresh(8));
            var signals = SessionSignal.None;
            for (int i = 0; i < 2000 && !s.Game.IsOver; i++)
            {
                s.Game.Tick(0.05f); // nobody taps: the titan reaches the wall
                foreach (var e in s.Game.Events) signals |= s.Process(e);
                s.Game.Events.Clear();
            }
            Assert.AreEqual(LoseReason.WallReached, s.Game.Lost);
            Assert.IsTrue((signals & SessionSignal.RunEnded) != 0);
            Assert.IsTrue((signals & SessionSignal.Save) != 0);
            Assert.AreEqual(1, profile.totalRuns);
            var end = profile.log.Last();
            Assert.AreEqual(Telemetry.RunEnd, end.e);
            Assert.AreEqual(1, end.a);
            Assert.AreEqual((int)LoseReason.WallReached, end.b);
        }

        [Test]
        public void NearMissLineOnlyFromEightyFivePercent()
        {
            Assert.IsNull(HudText.NearMissText(8499, 10000));
            Assert.AreEqual("Ai fost la 85% din record!", HudText.NearMissText(8500, 10000));
            Assert.AreEqual("Ai fost la 97% din record!", HudText.NearMissText(9799, 10000), "floor, never rounded up");
            Assert.AreEqual("Ai fost la 100% din record!", HudText.NearMissText(10000, 10000), "a tie is true: 100%");
            Assert.IsNull(HudText.NearMissText(10001, 10000), "above the record it is a new record, not a near miss");
            Assert.IsNull(HudText.NearMissText(0, 0));
            Assert.IsNull(HudText.NearMissText(5, 0));
            Assert.AreEqual("Ai fost la 99% din record!", HudText.NearMissText(2_000_000_000, 2_000_000_001), "no int overflow");
            for (int score = 0; score <= 1000; score++)
            {
                var text = HudText.NearMissText(score, 1000);
                Assert.AreEqual(score >= 850, text != null, "score " + score);
            }

            Assert.AreEqual("RECORD NOU!", HudText.RecordLine(12000, 10000, true));
            Assert.AreEqual("Ai fost la 90% din record!", HudText.RecordLine(9000, 10000, false));
            Assert.AreEqual("Record: 10.000", HudText.RecordLine(8000, 10000, false));
        }

        [Test]
        public void RecordMarkerFromEightyPercent()
        {
            Assert.IsNull(HudText.PbMarker(7999, 10000));
            Assert.AreEqual("Record în 2.000", HudText.PbMarker(8000, 10000));
            Assert.AreEqual("Record în 1", HudText.PbMarker(9999, 10000));
            Assert.IsNull(HudText.PbMarker(10000, 10000));
            Assert.IsNull(HudText.PbMarker(50, 0));
            Assert.AreEqual("41.200", HudText.Num(41200));
            Assert.AreEqual("1.234.567", HudText.Num(1234567));
            Assert.AreEqual("0", HudText.Num(0));
        }

        [Test]
        public void GameOverGoalAndStatsLines()
        {
            Assert.AreEqual("Încă un titan până la punctul de salvare #11", HudText.NextGoal(10, 14, true));
            Assert.AreEqual("Titanul Zilei te așteaptă", HudText.NextGoal(9, 14, true));
            Assert.AreEqual("Cel mai departe: titanul #14", HudText.NextGoal(9, 14, false));
            Assert.AreEqual("Încă un titan până la punctul de salvare #6", HudText.NextGoal(5, 5, false));
            Assert.AreEqual("Titani: 7 · Stele: 12 · Bucăți rupte: 23", HudText.RunStats(7, 12, 23));
        }

        // ------------------------------------------------------------ day clock and log ring

        [Test]
        public void DayClockCountsLocalDaysAndResistsRollback()
        {
            Assert.AreEqual(0, DayClock.DayNumber(new DateTime(2026, 1, 1, 0, 0, 1)));
            Assert.AreEqual(0, DayClock.DayNumber(new DateTime(2026, 1, 1, 23, 59, 59)));
            Assert.AreEqual(287, DayClock.DayNumber(new DateTime(2026, 10, 15, 8, 0, 0)));

            var p = new Profile();
            DayClock.StartSession(p, new DateTime(2026, 10, 15, 9, 0, 0), out int since, out bool newDay);
            Assert.AreEqual(-1, since, "first session ever");
            Assert.IsTrue(newDay);
            Assert.AreEqual(287, p.firstPlayDay);
            Assert.AreEqual(287, p.lastPlayDay);

            DayClock.StartSession(p, new DateTime(2026, 10, 15, 22, 0, 0), out since, out newDay);
            Assert.AreEqual(0, since);
            Assert.IsFalse(newDay, "same day");

            DayClock.StartSession(p, new DateTime(2026, 10, 17, 7, 0, 0), out since, out newDay);
            Assert.AreEqual(2, since);
            Assert.IsTrue(newDay);
            Assert.AreEqual(289, p.maxSeenDay);

            // The clock is moved back 3 days: today is held at maxSeenDay - 1, never a new day.
            int today = DayClock.StartSession(p, new DateTime(2026, 10, 14, 7, 0, 0), out since, out newDay);
            Assert.AreEqual(288, today);
            Assert.IsFalse(newDay);
            Assert.AreEqual(289, p.lastPlayDay);
            Assert.AreEqual(289, p.maxSeenDay, "maxSeenDay never goes back");
            today = DayClock.StartSession(p, new DateTime(2026, 10, 18, 7, 0, 0), out since, out newDay);
            Assert.AreEqual(290, today);
            Assert.IsTrue(newDay, "real time catches up");
        }

        [Test]
        public void TheLogIsARingOfThreeHundred()
        {
            var p = new Profile();
            long clock = 0;
            var t = new Telemetry(() => p, null, () => clock);
            for (int i = 0; i < 1000; i++)
            {
                clock = i;
                t.Track("perf", i, 0);
            }
            Assert.AreEqual(300, Telemetry.LogRingSize);
            Assert.AreEqual(300, p.log.Count);
            Assert.AreEqual(700, p.log[0].a, "the oldest entries go first");
            Assert.AreEqual(999, p.log[299].a);
            p.log.AddRange(Enumerable.Range(0, 50).Select(i => new LogEntry { e = "x" }));
            p.Normalize();
            Assert.AreEqual(300, p.log.Count, "a loaded log is trimmed too");
        }

        [Test]
        public void TelemetryForwardsToTheSink()
        {
            var p = new Profile();
            var sink = new RecordingSink();
            new Telemetry(() => p, sink, () => 5).Track(Telemetry.RestartMs, 120);
            Assert.AreEqual("restart_ms:120:0", sink.Seen.Single());
            Assert.AreEqual(1, p.log.Count);
        }

        sealed class RecordingSink : IAnalyticsSink
        {
            public readonly List<string> Seen = new List<string>();
            public void Track(string e, int a, int b) => Seen.Add(e + ":" + a + ":" + b);
        }

        // ------------------------------------------------------------ profile store

        sealed class JsonSerializerForTests : ISerializer
        {
            static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
            public string Write(Profile p) => JsonSerializer.Serialize(p, Options);
            public Profile Read(string text) => JsonSerializer.Deserialize<Profile>(text, Options);
        }

        sealed class MemoryPrefs : IPrefs
        {
            public readonly Dictionary<string, string> Strings = new Dictionary<string, string>();
            public readonly Dictionary<string, int> Ints = new Dictionary<string, int>();
            public int Flushes;
            public bool HasKey(string key) => Strings.ContainsKey(key) || Ints.ContainsKey(key);
            public string GetString(string key) => Strings.TryGetValue(key, out var v) ? v : null;
            public int GetInt(string key, int fallback) => Ints.TryGetValue(key, out var v) ? v : fallback;
            public void SetString(string key, string value) => Strings[key] = value;
            public void SetInt(string key, int value) => Ints[key] = value;
            public void Flush() => Flushes++;
        }

        static string TempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "titan_profile_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Test]
        public void ANewProfileMigratesTheLegacyBest()
        {
            var prefs = new MemoryPrefs();
            prefs.Ints[ProfileStore.LegacyBestKey] = 48210;
            prefs.Ints[ProfileStore.LegacyReducedFxKey] = 1;
            var store = new ProfileStore(TempDir(), new JsonSerializerForTests(), prefs);
            var p = store.Load();
            Assert.AreEqual(2, store.LoadedFrom);
            Assert.AreEqual(48210, p.legacyBest, "shown as the old record");
            Assert.AreEqual(0, p.bestScore, "never compared with v2 scores");
            Assert.IsTrue(p.settings.reducedFx);
            Assert.IsFalse(p.HasCheckpoint);
        }

        [Test]
        public void SaveGoesThroughATempFileAndMirrorsToPrefs()
        {
            var dir = TempDir();
            var prefs = new MemoryPrefs();
            var store = new ProfileStore(dir, new JsonSerializerForTests(), prefs);
            var p = new Profile { bestScore = 1234, bestTitanN = 9, totalRuns = 4 };
            p.SaveCheckpoint(6, 777, -5);
            p.AddLog(new LogEntry { t = 1, e = "run_start", a = 1 });
            Assert.IsTrue(store.Save(p));
            Assert.IsTrue(File.Exists(store.FilePath));
            Assert.IsFalse(File.Exists(store.TempPath), "the temp file is gone after a save");
            Assert.IsTrue(prefs.HasKey(ProfileStore.PrefsKey));
            Assert.AreEqual(1234, prefs.Ints[ProfileStore.BestKey], "titan_best_v2");
            Assert.AreEqual(1, prefs.Flushes);

            var loaded = new ProfileStore(dir, new JsonSerializerForTests(), prefs).Load();
            Assert.AreEqual(1234, loaded.bestScore);
            Assert.AreEqual(6, loaded.checkpoint.n);
            Assert.AreEqual(777, loaded.checkpoint.score);
            Assert.AreEqual(-5L, loaded.checkpoint.runSeed);
            Assert.AreEqual("run_start", loaded.log.Single().e);
        }

        /// <summary>
        /// A kill in the middle of a save: during the temp write the file is untouched; during the copy the file can be
        /// torn, and the PlayerPrefs mirror of the previous save loads instead (logged as save_corrupt).
        /// </summary>
        [Test]
        public void AKillMidSaveLeavesALoadableProfile()
        {
            var dir = TempDir();
            var prefs = new MemoryPrefs();
            var ser = new JsonSerializerForTests();
            var store = new ProfileStore(dir, ser, prefs);
            store.Save(new Profile { bestScore = 100 });

            // 1) Killed while writing the temp file: a torn .tmp, the file is the last good save.
            File.WriteAllText(store.TempPath, ser.Write(new Profile { bestScore = 200 }).Substring(0, 20));
            var corrupt = new List<int>();
            var p = new ProfileStore(dir, ser, prefs).Load(corrupt.Add);
            Assert.AreEqual(100, p.bestScore);
            CollectionAssert.IsEmpty(corrupt);

            // 2) Killed during the copy: the file itself is torn; the PlayerPrefs mirror (previous save) loads.
            string full = ser.Write(new Profile { bestScore = 300 });
            File.WriteAllText(store.FilePath, full.Substring(0, full.Length / 2));
            var s2 = new ProfileStore(dir, ser, prefs);
            p = s2.Load(corrupt.Add);
            Assert.AreEqual(1, s2.LoadedFrom);
            Assert.AreEqual(100, p.bestScore);
            CollectionAssert.AreEqual(new[] { 0 }, corrupt, "save_corrupt(file)");

            // 3) The next save repairs the file and removes the stale temp file.
            Assert.IsTrue(s2.Save(p));
            Assert.IsFalse(File.Exists(s2.TempPath));
            Assert.AreEqual(100, new ProfileStore(dir, ser, prefs).Load().bestScore);

            // 4) Both torn: a new profile (never a crash).
            File.WriteAllText(store.FilePath, "{\"version\":");
            prefs.Strings[ProfileStore.PrefsKey] = "garbage";
            corrupt.Clear();
            var s4 = new ProfileStore(dir, ser, prefs);
            p = s4.Load(corrupt.Add);
            Assert.AreEqual(2, s4.LoadedFrom);
            CollectionAssert.AreEqual(new[] { 0, 1 }, corrupt);
            Assert.AreEqual(0, p.bestScore);
        }
    }

    /// <summary>Gives other fixtures the repo-file lookup of <see cref="EthicsLockTests"/>.</summary>
    static class EthicsLockTestsAccess
    {
        public static string RepoFile(string relative)
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
    }
}
