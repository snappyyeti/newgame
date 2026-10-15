using System;
using System.Collections.Generic;

namespace Titan.Core
{
    /// <summary>
    /// The player's saved profile (DESIGN.md 9.1, RET-1). Plain serializable data: public fields only, so Unity's
    /// JsonUtility (and any other serializer) reads and writes it. Field names are the JSON keys; never rename one.
    /// Add fields only when their feature ships (RET-3 and ADS-1 append theirs) and bump nothing for an added field:
    /// a missing key keeps the default below.
    /// </summary>
    [Serializable]
    public sealed class Profile
    {
        public const int CurrentVersion = 1;
        /// <summary>Day fields start here: "never".</summary>
        public const int NoDay = -1000000;

        public int version = CurrentVersion;
        /// <summary>Best score with the v2 scoring (CORE-5: clean cuts and chains). Old scores never compete with it.</summary>
        public int bestScore;
        /// <summary>The furthest titan reached (spawned) in any run.</summary>
        public int bestTitanN;
        /// <summary>Runs that ended (game over).</summary>
        public int totalRuns;
        public float totalPlaySeconds;
        /// <summary>FTUE hint bits (FTUE-1 owns their meaning).</summary>
        public int ftueFlags;
        /// <summary>The free checkpoint (n = 0: none).</summary>
        public Checkpoint checkpoint = new Checkpoint();
        public ProfileSettings settings = new ProfileSettings();
        public int firstPlayDay = NoDay, lastPlayDay = NoDay, maxSeenDay = NoDay;
        /// <summary>Local event log, a ring of <see cref="Telemetry.LogRingSize"/> entries (oldest first).</summary>
        public List<LogEntry> log = new List<LogEntry>();
        /// <summary>The best of the old scoring ("titan_best"), shown once as "Record vechi", never compared.</summary>
        public int legacyBest;
        /// <summary>Titanul Zilei (RET-3): today's cached salt and the official attempt.</summary>
        public DailyState daily = new DailyState();
        /// <summary>The forgiving daily streak with shields (RET-3).</summary>
        public StreakState streak = new StreakState();

        public bool HasCheckpoint => checkpoint != null && checkpoint.n > 1;

        /// <summary>Repairs what an old or hand-edited file may lack (null lists, negative counters).</summary>
        public void Normalize()
        {
            if (checkpoint == null) checkpoint = new Checkpoint();
            if (settings == null) settings = new ProfileSettings();
            if (log == null) log = new List<LogEntry>();
            if (daily == null) daily = new DailyState();
            if (streak == null) streak = new StreakState();
            if (settings.lang == null) settings.lang = "";
            if (version < 1) version = CurrentVersion;
            TrimLog(Telemetry.LogRingSize);
        }

        /// <summary>Appends to the log ring, dropping the oldest entries beyond <paramref name="ringSize"/>.</summary>
        public void AddLog(LogEntry entry, int ringSize = Telemetry.LogRingSize)
        {
            log.Add(entry);
            TrimLog(ringSize);
        }

        void TrimLog(int ringSize)
        {
            int extra = log.Count - ringSize;
            if (extra > 0) log.RemoveRange(0, extra);
        }

        public void SaveCheckpoint(int n, int score, long runSeed)
        {
            checkpoint = new Checkpoint { n = n, score = score, runSeed = runSeed };
        }

        public void ClearCheckpoint() => checkpoint = new Checkpoint();
    }

    /// <summary>A free checkpoint at the start of a cycle: titan n of the run with this seed, with the score it had then.</summary>
    [Serializable]
    public sealed class Checkpoint
    {
        public int n;
        public int score;
        public long runSeed;
    }

    /// <summary>
    /// Titanul Zilei (RET-3, DESIGN.md 9.4) for one local day: the salt the validator picked (computed once per day)
    /// and the official attempt. A new day resets all of it.
    /// </summary>
    [Serializable]
    public sealed class DailyState
    {
        /// <summary>The day this state belongs to (Profile.NoDay: never).</summary>
        public int day = Profile.NoDay;
        /// <summary>The cached DailyValidator pick for <see cref="day"/>: salt 0-3 (−1: not computed yet).</summary>
        public int salt = -1;
        /// <summary>The validator found no fair salt: salt 0 with the easier build levels.</summary>
        public bool fallback;
        /// <summary>The official attempt: 0 not started, 1 in progress (resumable), 2 finished.</summary>
        public int status;
        /// <summary>Titans defeated in the official attempt (0-3).</summary>
        public int defeated;
        /// <summary>Score after the last defeated titan (in progress), or the final score.</summary>
        public int score;
        /// <summary>Pieces cut and seconds played over the whole attempt (resumes included).</summary>
        public int pieces;
        public float seconds;
        /// <summary>The titan the finished attempt ended on (0: all 3 defeated).</summary>
        public int lostOn;
        /// <summary>Best "ANTRENAMENT" score of this day (practice never changes the official result).</summary>
        public int bestPracticeScore;
    }

    /// <summary>The forgiving streak (RET-3, DESIGN.md 9.5): official attempts on consecutive days, with up to 2 shields.</summary>
    [Serializable]
    public sealed class StreakState
    {
        public int current;
        public int best;
        /// <summary>The last day that counts for the streak (an official attempt, or a day a shield covered).</summary>
        public int lastDay = Profile.NoDay;
        public int shields;
        public bool firstDailyDone;
    }

    [Serializable]
    public sealed class ProfileSettings
    {
        public bool sfx = true;
        public bool haptics = true;
        public bool reducedFx;
        public bool colorSymbols = true;
        /// <summary>"" = the phone's language (UX-1), "ro" or "en".</summary>
        public string lang = "";
    }

    /// <summary>One gameplay event: unix seconds, name, two numbers. No device identifiers.</summary>
    [Serializable]
    public sealed class LogEntry
    {
        public long t;
        public string e;
        public int a;
        public int b;
    }

    /// <summary>The local calendar day (DESIGN.md 9.1). Days count from 2026-01-01 in the phone's LOCAL time zone.</summary>
    public static class DayClock
    {
        static readonly DateTime Epoch = new DateTime(2026, 1, 1);

        public static int DayNumber(DateTime local) => (int)(local.Date - Epoch).TotalDays;

        /// <summary>
        /// Today's day number with the rollback guard: a clock moved back cannot go below maxSeenDay − 1 (so it never
        /// yields a "new" day), and maxSeenDay only grows.
        /// </summary>
        public static int Today(int dayNumberNow, ref int maxSeenDay)
        {
            int today = maxSeenDay == Profile.NoDay ? dayNumberNow : Math.Max(dayNumberNow, maxSeenDay - 1);
            maxSeenDay = maxSeenDay == Profile.NoDay ? today : Math.Max(maxSeenDay, today);
            return today;
        }

        /// <summary>
        /// Starts a session: updates the profile's day fields and returns today. <paramref name="daysSinceLast"/> is
        /// −1 on the first session ever, 0 for another session the same day; <paramref name="newDay"/> is true when
        /// today is later than the last day played.
        /// </summary>
        public static int StartSession(Profile p, DateTime local, out int daysSinceLast, out bool newDay)
        {
            int today = Today(DayNumber(local), ref p.maxSeenDay);
            daysSinceLast = p.lastPlayDay == Profile.NoDay ? -1 : Math.Max(0, today - p.lastPlayDay);
            newDay = p.lastPlayDay == Profile.NoDay || today > p.lastPlayDay;
            if (p.firstPlayDay == Profile.NoDay) p.firstPlayDay = today;
            if (newDay) p.lastPlayDay = today;
            return today;
        }
    }

    /// <summary>Where analytics events go besides the local log (an SDK can plug in later without touching the game).</summary>
    public interface IAnalyticsSink
    {
        void Track(string e, int a, int b);
    }

    public sealed class NullSink : IAnalyticsSink
    {
        public static readonly NullSink Instance = new NullSink();
        public void Track(string e, int a, int b) { }
    }

    /// <summary>
    /// Gameplay telemetry (RET-1): every event goes into the profile's log ring and to the sink. The event names and
    /// their two numbers are fixed here. Gameplay events only, no device identifiers.
    /// </summary>
    public sealed class Telemetry
    {
        public const int LogRingSize = 300;

        public const string SessionStart = "session_start"; // (day, daysSinceLast)
        /// <summary>Superseded by the FTUE-1 funnel names below (<see cref="TutStep"/>); never emitted.</summary>
        public const string Ftue = "ftue";
        public const string RunStart = "run_start";         // (startTitan, mode)
        public const string TitanSpawn = "titan_spawn";     // (n, role)
        public const string TitanDefeat = "titan_defeat";   // (n, seconds)
        public const string RunEnd = "run_end";             // (n, reason)
        public const string Perf = "perf";                  // (p95 ms, n)
        public const string RestartMs = "restart_ms";       // (ms, 0)
        public const string SaveCorrupt = "save_corrupt";   // (source, 0): 0 file, 1 PlayerPrefs

        // FTUE-1 funnel (names are stable).
        public const string TitanReached = "titan_n_reached";                 // (n, run number): a titan reached for the first time ever (endless)
        public const string RunLength = "run_length";                         // (seconds, reason): every run end, next to run_end(n, reason)
        public const string Run3SameSession = "run_3_started_same_session";   // (0, 0): the 3rd run of the install started in the session of the 1st
        /// <summary>"tut_step_{1..4}_shown" / "tut_step_{1..4}_done" (step, 0).</summary>
        public static string TutStep(int step, bool done) => "tut_step_" + step + (done ? "_done" : "_shown");

        // RET-3 Titanul Zilei.
        public const string DailyStart = "daily_start";     // (day, mode): the official attempt started (first deploy), or a practice run
        public const string DailyEnd = "daily_end";         // (day, titans defeated): the official attempt was finalized
        public const string Streak = "streak";              // (new, old) whenever the streak number changes
        public const string ShieldUsed = "shield_used";     // (gap, shields left)
        public const string ShareTap = "share_tap";         // (day, 0)

        /// <summary>run_start modes.</summary>
        public const int ModeEndless = 0, ModeDailyOfficial = 1, ModeDailyPractice = 2, ModeCheckpoint = 3;

        readonly Func<Profile> profile;
        readonly IAnalyticsSink sink;
        readonly Func<long> clock;

        /// <param name="profile">The profile whose log ring receives the events (read on every call: the profile may be replaced).</param>
        /// <param name="clock">Unix seconds; defaults to the system clock.</param>
        public Telemetry(Func<Profile> profile, IAnalyticsSink sink = null, Func<long> clock = null)
        {
            this.profile = profile;
            this.sink = sink ?? NullSink.Instance;
            this.clock = clock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        public void Track(string e, int a = 0, int b = 0)
        {
            profile()?.AddLog(new LogEntry { t = clock(), e = e, a = a, b = b });
            sink.Track(e, a, b);
        }
    }
}
