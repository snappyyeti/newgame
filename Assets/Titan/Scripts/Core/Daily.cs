using System;
using System.Globalization;
using System.Threading.Tasks;

namespace Titan.Core
{
    /// <summary>The settings of Titanul Zilei and the streak (RET-3, DESIGN.md 9.4-9.5).</summary>
    public sealed class DailyConfig
    {
        public const string SeedPrefix = "titan-daily-v1:";

        /// <summary>Build levels of daily titans 1-3 (titan 3 is a boss).</summary>
        public int[] DailyBuildLevels = { 3, 4, 5 };
        /// <summary>The validator found no fair salt: salt 0 with these easier levels.</summary>
        public int[] FallbackBuildLevels = { 2, 3, 4 };
        /// <summary>The n the titan clock uses for daily titans 1-3 (speedup exponent).</summary>
        public int[] DailyClockN = { 6, 7, 8 };
        public double DailyMinClearRate = 0.35;
        public double DailyMinTitan1Rate = 0.90;
        public int DailyMaxSalt = 3;
        /// <summary>Greedy-bot games per salt, its tap rhythm (1.0 ± 0.4 s) and the time cap of one simulated attempt.</summary>
        public int ValidatorGames = 40;
        public double ValidatorTapSeconds = 1.0, ValidatorTapJitter = 0.4, ValidatorMaxSeconds = 600.0;
        public const float ValidatorDt = 0.05f;

        public int StreakMaxShields = 2;
        public int StreakShieldEvery = 7;
        /// <summary>Line 4 of the share text. A placeholder until the store pages exist.</summary>
        public string StoreLink = "https://example.com/titanul-din-cuburi";
    }

    /// <summary>What <see cref="DailyValidator.PickSalt"/> chose for a day.</summary>
    public struct DailyPick
    {
        public int Day;
        public int Salt;
        /// <summary>No salt passed: salt 0 with <see cref="DailyConfig.FallbackBuildLevels"/>.</summary>
        public bool Fallback;
        /// <summary>Measured shares of the accepted salt (the last one tried for a fallback); NaN when read from the cache.</summary>
        public double ClearRate, Titan1Rate;
    }

    /// <summary>Seeds and the run plan of Titanul Zilei: the same 3 titans for everyone on a local day.</summary>
    public static class Daily
    {
        public const int Titans = 3;

        /// <summary>daySeed = FromString("titan-daily-v1:" + day) + salt; titan k uses Derive(daySeed, k) (the seeded TitanGame does that).</summary>
        public static ulong DaySeed(int day, int salt) => unchecked(SeededRandom.FromString(DailyConfig.SeedPrefix + day.ToString(CultureInfo.InvariantCulture)) + (ulong)salt);

        /// <summary>3 titans at the given build levels, titan 3 a boss, the clock of titans 6-8, titan 1 waits for the first tap.</summary>
        public static RunPlan Plan(int[] buildLevels, DailyConfig cfg)
        {
            return new RunPlan
            {
                MaxTitans = Titans,
                BuildLevels = (int[])buildLevels.Clone(),
                Roles = new[] { TitanRole.Normal, TitanRole.Normal, TitanRole.Boss },
                ClockN = (int[])cfg.DailyClockN.Clone(),
                HoldFirstTitan = true,
            };
        }

        public static RunPlan Plan(DailyPick pick, DailyConfig cfg) => Plan(pick.Fallback ? cfg.FallbackBuildLevels : cfg.DailyBuildLevels, cfg);

        /// <summary>The daily run of <paramref name="pick"/>, from titan <paramref name="titan"/> with <paramref name="score"/>.</summary>
        public static RunStart Start(DailyPick pick, DailyConfig cfg, bool official, int titan = 1, int score = 0)
        {
            return new RunStart
            {
                Seed = unchecked((long)DaySeed(pick.Day, pick.Salt)),
                Titan = Math.Max(1, Math.Min(Titans, titan)),
                Score = Math.Max(0, score),
                Mode = official ? Telemetry.ModeDailyOfficial : Telemetry.ModeDailyPractice,
                Plan = Plan(pick, cfg),
                Day = pick.Day,
            };
        }

        public static bool IsDailyMode(int mode) => mode == Telemetry.ModeDailyOfficial || mode == Telemetry.ModeDailyPractice;
    }

    /// <summary>
    /// Fairness check of a day (RET-3): for salt 0..3, 40 Greedy-bot attempts (dt 0.05, a tap every 1.0 ± 0.4 s,
    /// jitter from SeededRandom(daySeed + i)). The first salt where all 3 titans fall in ≥ 35% of the attempts and
    /// titan 1 in ≥ 90% is the day's salt; otherwise salt 0 with build levels {2, 3, 4}. Pure and deterministic:
    /// every phone gets the same answer. The bot here is the game's own minimal Greedy (Core has no simulator), and it
    /// never uses any helper.
    /// </summary>
    public static class DailyValidator
    {
        /// <param name="parallel">Play each batch of attempts on the thread pool (off when the caller already runs days in parallel).</param>
        public static DailyPick PickSalt(int day, DailyConfig cfg = null, TitanConfig titanConfig = null, bool parallel = true)
        {
            cfg = cfg ?? new DailyConfig();
            titanConfig = titanConfig ?? new TitanConfig();
            var last = new DailyPick { Day = day, Salt = 0, Fallback = true };
            for (int salt = 0; salt <= cfg.DailyMaxSalt; salt++)
            {
                bool ok = Measure(Daily.DaySeed(day, salt), Daily.Plan(cfg.DailyBuildLevels, cfg), cfg, titanConfig, out double clear, out double titan1, parallel);
                last.ClearRate = clear;
                last.Titan1Rate = titan1;
                if (ok) return new DailyPick { Day = day, Salt = salt, Fallback = false, ClearRate = clear, Titan1Rate = titan1 };
            }
            return last;
        }

        /// <summary>
        /// Plays the attempts of one salt in small parallel batches and stops as soon as the answer is certain (counts
        /// only grow, so an early answer is the answer of all 40). The rates are over the attempts played.
        /// </summary>
        public static bool Measure(ulong daySeed, RunPlan plan, DailyConfig cfg, TitanConfig titanConfig, out double clearRate, out double titan1Rate, bool parallel = true)
        {
            int games = cfg.ValidatorGames;
            int needClear = (int)Math.Ceiling(cfg.DailyMinClearRate * games - 1e-9);
            int needTitan1 = (int)Math.Ceiling(cfg.DailyMinTitan1Rate * games - 1e-9);
            int clear = 0, titan1 = 0, played = 0;
            const int Batch = 4;
            var titans = new int[Batch];
            bool? answer = null;
            while (played < games && answer == null)
            {
                int n = Math.Min(Batch, games - played);
                int first = played;
                if (parallel) Parallel.For(0, n, i => titans[i] = Attempt(titanConfig, plan, daySeed, (ulong)(first + i), cfg));
                else for (int i = 0; i < n; i++) titans[i] = Attempt(titanConfig, plan, daySeed, (ulong)(first + i), cfg);
                for (int i = 0; i < n; i++)
                {
                    if (titans[i] >= Daily.Titans) clear++;
                    if (titans[i] >= 1) titan1++;
                }
                played += n;
                int left = games - played;
                if (clear >= needClear && titan1 >= needTitan1) answer = true;
                else if (clear + left < needClear || titan1 + left < needTitan1) answer = false;
            }
            clearRate = clear / (double)played;
            titan1Rate = titan1 / (double)played;
            return answer ?? (clear >= needClear && titan1 >= needTitan1);
        }

        /// <summary>One Greedy-bot attempt of the daily with this seed: the titans it defeated (3 = cleared).</summary>
        public static int Attempt(TitanConfig titanConfig, RunPlan plan, ulong daySeed, ulong index, DailyConfig cfg)
        {
            var g = new TitanGame(titanConfig, unchecked((long)daySeed), 1, 0, null, plan);
            var jitter = new SeededRandom(unchecked(daySeed + index));
            double t = 0, next = NextTap(jitter, cfg);
            while (!g.IsOver && t < cfg.ValidatorMaxSeconds)
            {
                if (t >= next)
                {
                    int pick = Greedy(g, jitter);
                    if (pick >= 0) g.Deploy(pick);
                    next = t + NextTap(jitter, cfg);
                }
                g.Tick(DailyConfig.ValidatorDt);
                g.Events.Clear();
                t += DailyConfig.ValidatorDt;
            }
            return g.TitansDefeated;
        }

        static double NextTap(SeededRandom r, DailyConfig cfg) => cfg.ValidatorTapSeconds + (r.NextDouble() * 2.0 - 1.0) * cfg.ValidatorTapJitter;

        /// <summary>The biggest deployable cannon with a visible color; otherwise a random deployable one when nothing fires or ≥ 4 slots are free.</summary>
        static int Greedy(TitanGame g, IRandom r)
        {
            int best = -1, options = 0;
            for (int i = 0; i < g.Tray.Length; i++)
            {
                if (!g.CanDeploy(i)) continue;
                options++;
                if (g.Monster.HasVisibleOfColor(g.Tray[i].Color) && (best < 0 || g.Tray[i].Ammo > g.Tray[best].Ammo)) best = i;
            }
            if (best >= 0 || options == 0) return best;
            bool firing = false;
            for (int s = 0; s < g.ActiveSlots; s++)
                if (g.Line[s] != null && g.Monster.HasVisibleOfColor(g.Line[s].Color)) firing = true;
            if (firing && g.FreeSlots < 4) return -1;
            int k = r.Range(0, options);
            for (int i = 0; i < g.Tray.Length; i++)
                if (g.CanDeploy(i) && k-- == 0) return i;
            return -1;
        }
    }

    /// <summary>The forgiving streak rules (RET-3, DESIGN.md 9.5) as pure functions.</summary>
    public static class StreakRules
    {
        /// <summary>
        /// An official attempt of day <paramref name="day"/> was finalized: the streak grows when the day is new for it.
        /// The first daily ever gives 1 shield, every 7th day of a streak another; at most 2. Returns the old number.
        /// </summary>
        public static int OnOfficialFinished(StreakState st, int day, DailyConfig cfg)
        {
            int old = st.current;
            if (st.lastDay != Profile.NoDay && st.lastDay >= day) return old;
            st.current++;
            st.lastDay = day;
            st.best = Math.Max(st.best, st.current);
            if (!st.firstDailyDone)
            {
                st.firstDailyDone = true;
                st.shields = Math.Min(cfg.StreakMaxShields, st.shields + 1);
            }
            if (cfg.StreakShieldEvery > 0 && st.current % cfg.StreakShieldEvery == 0) st.shields = Math.Min(cfg.StreakMaxShields, st.shields + 1);
            return old;
        }

        public enum Outcome { Nothing, ShieldUsed, Reset }

        /// <summary>
        /// At launch, before any UI: gap = today − lastDay − 1 missed days. gap ≤ 0: nothing; gap ≤ shields: they are
        /// spent and the streak goes on (lastDay moves to yesterday); else the streak starts again from 0 (and lastDay is
        /// forgotten). Idempotent: a second call the same day changes nothing. Shields left over are kept.
        /// </summary>
        public static Outcome Resolve(StreakState st, int today, out int gap)
        {
            gap = 0;
            if (st.lastDay == Profile.NoDay || st.current <= 0) return Outcome.Nothing;
            gap = today - st.lastDay - 1;
            if (gap <= 0) return Outcome.Nothing;
            if (gap <= st.shields)
            {
                st.shields -= gap;
                st.lastDay = today - 1;
                return Outcome.ShieldUsed;
            }
            st.current = 0;
            st.lastDay = Profile.NoDay;
            return Outcome.Reset;
        }
    }

    /// <summary>The player-facing texts of Titanul Zilei (Romanian; UX-1 moves them into the RO/EN table).</summary>
    public static class DailyText
    {
        public const string Title = "Titanul Zilei";
        public const string Intro = "Încercarea de azi e fără ajutoare și fără reclame — aceeași provocare pentru toți.";
        public const string Practice = "ANTRENAMENT";
        public const string ResumeOfficial = "Continuă încercarea oficială";
        public const string QuitConfirm = "Închei încercarea oficială?";
        public const string ShieldUsed = "Un scut ți-a păstrat seria.";
        public const string StreakReset = "Bine ai revenit! Începe o serie nouă azi.";
        public const string Copied = "Copiat! Lipește-l unde vrei.";
        public const string CopyButton = "Copiază rezultatul";

        public const string Green = "\U0001F7E9", Red = "\U0001F7E5", Grey = "⬜";

        /// <summary>Square k (1-3) of a finished attempt: 1 defeated (green), 2 the titan it ended on (red), 0 not reached (grey).</summary>
        public static int Square(int k, int defeated, int lostOn)
        {
            if (k <= defeated) return 1;
            if (k == lostOn) return 2;
            return 0;
        }

        /// <summary>m:ss (minutes not padded).</summary>
        public static string Clock(float seconds)
        {
            int s = Math.Max(0, (int)Math.Floor(seconds));
            return (s / 60).ToString(CultureInfo.InvariantCulture) + ":" + (s % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The 4-line share text: "Titanul din Cuburi · Ziua #{day}", the three squares as emoji, "Scor {score} · {m:ss} ·
        /// {pieces} bucăți rupte" and the store link. Nothing about the board (spoiler-free).
        /// </summary>
        public static string Share(int day, int defeated, int lostOn, int score, float seconds, int pieces, string storeLink)
        {
            var squares = "";
            for (int k = 1; k <= Daily.Titans; k++)
            {
                int sq = Square(k, defeated, lostOn);
                squares += sq == 1 ? Green : sq == 2 ? Red : Grey;
            }
            return "Titanul din Cuburi · Ziua #" + day.ToString(CultureInfo.InvariantCulture) + "\n"
                + squares + "\n"
                + "Scor " + HudText.Num(score) + " · " + Clock(seconds) + " · " + pieces.ToString(CultureInfo.InvariantCulture) + " bucăți rupte\n"
                + storeLink;
        }

        /// <summary>The button status: "neîncercat", "în curs: 1/3" (resumable) or "2/3".</summary>
        public static string Status(DailyState d)
        {
            if (d.status == 0) return "neîncercat";
            if (d.status == 1) return "în curs: " + d.defeated + "/" + Daily.Titans;
            return d.defeated + "/" + Daily.Titans;
        }

        /// <summary>A real countdown to local midnight: "h:mm:ss".</summary>
        public static string UntilMidnight(DateTime localNow)
        {
            var left = localNow.Date.AddDays(1) - localNow;
            int s = Math.Max(0, (int)Math.Ceiling(left.TotalSeconds));
            return (s / 3600).ToString(CultureInfo.InvariantCulture) + ":" + (s / 60 % 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (s % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        public static string BestStreak(int best) => "Cea mai lungă serie: " + best;
        public static string CurrentStreak(int current) => current == 1 ? "Serie: 1 zi" : "Serie: " + current + " zile";
        public static string Shields(int shields) => shields == 1 ? "1 scut" : shields + " scuturi";
    }

    /// <summary>
    /// Titanul Zilei for one profile (RET-3): the day roll, the cached salt, the official attempt (starts at the first
    /// deploy, saved at every defeated titan, resumable after a kill, finalized at its end, on an explicit quit or when
    /// its day is over), practice, the streak and its launch message. Pure C#; the game saves the profile when a method
    /// returns true. Nothing here knows about ads: the game passes isDailyRanked to the ad rules for every daily run.
    /// </summary>
    public sealed class DailyChallenge
    {
        public readonly DailyConfig Config;
        readonly Profile profile;
        readonly Telemetry telemetry;
        readonly TitanConfig titanConfig;
        readonly Func<int, DailyPick> picker;

        /// <summary>The streak message of this launch ("Un scut ți-a păstrat seria." / "Bine ai revenit! …"), or null.</summary>
        public string LaunchMessage { get; private set; }
        public int Today { get; private set; } = Profile.NoDay;

        /// <param name="picker">Tests may replace the validator (it is slow on purpose: 160 simulated attempts at most).</param>
        public DailyChallenge(Profile profile, Telemetry telemetry, DailyConfig config = null, TitanConfig titanConfig = null, Func<int, DailyPick> picker = null)
        {
            this.profile = profile;
            this.telemetry = telemetry;
            Config = config ?? new DailyConfig();
            this.titanConfig = titanConfig ?? new TitanConfig();
            this.picker = picker ?? (day => DailyValidator.PickSalt(day, Config, this.titanConfig));
        }

        public DailyState State => profile.daily;
        public StreakState Streak => profile.streak;

        /// <summary>App launch, before any UI: the day roll (an unfinished attempt of an earlier day is finalized), then the streak.</summary>
        public void OnLaunch(int today)
        {
            Roll(today);
            var outcome = StreakRules.Resolve(profile.streak, today, out int gap);
            int old = profile.streak.current;
            if (outcome == StreakRules.Outcome.ShieldUsed)
            {
                LaunchMessage = DailyText.ShieldUsed;
                telemetry?.Track(Telemetry.ShieldUsed, gap, profile.streak.shields);
            }
            else if (outcome == StreakRules.Outcome.Reset)
            {
                LaunchMessage = DailyText.StreakReset;
                telemetry?.Track(Telemetry.Streak, 0, old);
            }
        }

        /// <summary>
        /// A new local day: an attempt still in progress from an earlier day is finalized with its saved progress (local
        /// midnight), and the daily state starts over. <paramref name="officialRunning"/>: an official run is on screen
        /// right now; it finishes first and is recorded under its own day.
        /// </summary>
        public bool Roll(int today, bool officialRunning = false)
        {
            if (officialRunning) return false;
            Today = today;
            var d = profile.daily;
            if (d.day == today) return false;
            if (d.day != Profile.NoDay && d.status == 1) Finish(d.defeated, d.defeated + 1, d.score, d.pieces, d.seconds);
            profile.daily = new DailyState { day = today };
            return true;
        }

        /// <summary>Today's pick, computed once per day and cached in the profile.</summary>
        public DailyPick Pick()
        {
            var d = profile.daily;
            if (d.day == Today && d.salt >= 0) return new DailyPick { Day = Today, Salt = d.salt, Fallback = d.fallback, ClearRate = double.NaN, Titan1Rate = double.NaN };
            var pick = picker(Today);
            if (d.day != Today) profile.daily = d = new DailyState { day = Today };
            d.salt = pick.Salt;
            d.fallback = pick.Fallback;
            return pick;
        }

        public bool PickCached => profile.daily.day == Today && profile.daily.salt >= 0;

        /// <summary>Stores a pick computed elsewhere (the game runs the validator on a worker thread). Ignored for another day.</summary>
        public bool ApplyPick(DailyPick pick)
        {
            if (pick.Day != Today || PickCached) return false;
            if (profile.daily.day != Today) profile.daily = new DailyState { day = Today };
            profile.daily.salt = pick.Salt;
            profile.daily.fallback = pick.Fallback;
            return true;
        }
        public bool OfficialNotStarted => profile.daily.status == 0;
        public bool OfficialInProgress => profile.daily.status == 1;
        public bool OfficialFinished => profile.daily.status == 2;

        /// <summary>The official attempt: titan 1, or, after a kill, the titan after the last defeated one with its score.</summary>
        public RunStart OfficialStart()
        {
            var d = profile.daily;
            var pick = Pick();
            return OfficialInProgress ? Daily.Start(pick, Config, true, d.defeated + 1, d.score) : Daily.Start(pick, Config, true);
        }

        public RunStart PracticeStart() => Daily.Start(Pick(), Config, false);

        // ------------------------------------------------------------ during a daily run

        /// <summary>The official run on screen: pieces and seconds of earlier parts of the attempt (a resume).</summary>
        int basePieces;
        float baseSeconds;
        RunSession run;

        /// <summary>A daily run starts (official or practice). Practice is logged at once; the official attempt at its first deploy.</summary>
        public void BeginRun(RunSession session)
        {
            run = session;
            basePieces = profile.daily.pieces;
            baseSeconds = profile.daily.seconds;
            if (session.Start.Mode == Telemetry.ModeDailyPractice) telemetry?.Track(Telemetry.DailyStart, Today, Telemetry.ModeDailyPractice);
        }

        bool IsOfficialRun => run != null && run.Start.Mode == Telemetry.ModeDailyOfficial;

        /// <summary>True while the official run on screen has not been tapped yet (backing out does not use the attempt).</summary>
        public bool OfficialRunUntouched => IsOfficialRun && OfficialNotStarted;

        /// <summary>Feed every event of a daily run. Returns true when the profile must be saved now.</summary>
        public bool Process(GameEvent e)
        {
            if (run == null) return false;
            var d = profile.daily;
            switch (e.Kind)
            {
                case EventKind.Deployed:
                    if (IsOfficialRun && d.status == 0)
                    {
                        d.status = 1;
                        telemetry?.Track(Telemetry.DailyStart, Today, Telemetry.ModeDailyOfficial);
                        return true;
                    }
                    break;
                case EventKind.TitanDefeated:
                    if (IsOfficialRun && d.status == 1)
                    {
                        d.defeated = Math.Max(d.defeated, e.Level);
                        d.score = run.Game.Score;
                        d.pieces = basePieces + run.Game.PiecesCut;
                        d.seconds = baseSeconds + run.Seconds;
                        return true;
                    }
                    break;
                case EventKind.GameOver:
                    if (IsOfficialRun && d.status == 1)
                    {
                        int defeated = run.Game.TitansDefeated + run.Game.StartTitan - 1;
                        Finish(defeated, e.Reason == LoseReason.Completed ? 0 : e.Level, run.Game.Score, basePieces + run.Game.PiecesCut, baseSeconds + run.Seconds);
                        return true;
                    }
                    if (run.Start.Mode == Telemetry.ModeDailyPractice)
                    {
                        d.bestPracticeScore = Math.Max(d.bestPracticeScore, run.Game.Score);
                        return true;
                    }
                    break;
            }
            return false;
        }

        /// <summary>"Închei încercarea oficială?" → yes: the attempt ends now, on the titan on screen, with the score on screen.</summary>
        public bool QuitOfficial()
        {
            if (!IsOfficialRun || profile.daily.status != 1) return false;
            int defeated = run.Game.TitansDefeated + run.Game.StartTitan - 1;
            Finish(defeated, run.Game.Level, run.Game.Score, basePieces + run.Game.PiecesCut, baseSeconds + run.Seconds);
            return true;
        }

        /// <summary>The player leaves a practice run: its score so far still counts for the day's practice best.</summary>
        public bool LeavePractice()
        {
            if (run == null || run.Start.Mode != Telemetry.ModeDailyPractice) return false;
            profile.daily.bestPracticeScore = Math.Max(profile.daily.bestPracticeScore, run.Game.Score);
            return true;
        }

        /// <summary>The daily run on screen is over or was left.</summary>
        public void EndRun() => run = null;

        /// <summary>The run on screen is today's official attempt (started or not).</summary>
        public bool OfficialRunOnScreen => IsOfficialRun;

        void Finish(int defeated, int lostOn, int score, int pieces, float seconds)
        {
            var d = profile.daily;
            d.status = 2;
            d.defeated = Math.Max(0, Math.Min(Daily.Titans, defeated));
            d.lostOn = d.defeated >= Daily.Titans ? 0 : Math.Max(d.defeated + 1, Math.Min(Daily.Titans, lostOn));
            d.score = score;
            d.pieces = pieces;
            d.seconds = seconds;
            int old = StreakRules.OnOfficialFinished(profile.streak, d.day, Config);
            telemetry?.Track(Telemetry.DailyEnd, d.day, d.defeated);
            if (profile.streak.current != old) telemetry?.Track(Telemetry.Streak, profile.streak.current, old);
        }

        /// <summary>The share text of today's finished official attempt (null before it is finished).</summary>
        public string ShareText()
        {
            var d = profile.daily;
            if (d.status != 2) return null;
            return DailyText.Share(d.day, d.defeated, d.lostOn, d.score, d.seconds, d.pieces, Config.StoreLink);
        }

        public void OnShareTapped() => telemetry?.Track(Telemetry.ShareTap, profile.daily.day, 0);
    }
}
