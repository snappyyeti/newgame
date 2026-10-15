using System;

namespace Titan.Core.Ads
{
    /// <summary>The only ad placements that ship (DESIGN.md 10.2). No banner, no in-run interstitial.</summary>
    public enum Placement
    {
        /// <summary>"A doua suflare": the once-per-run revive (rewarded).</summary>
        ReviveSecondWind,
        /// <summary>"+1 Loc": the 6th firing slot until the titan falls (rewarded, from the gold wall button only).</summary>
        ExtraSlot,
        /// <summary>"Pauză de reclame": 30 minutes without interstitials (rewarded).</summary>
        AdBreak,
        /// <summary>The interstitial between runs, from the results panel buttons only.</summary>
        InterRunEnd,
    }

    /// <summary>How an ad ended. A reward is granted ONLY on Completed.</summary>
    public enum AdResult
    {
        Completed,
        ClosedEarly,
        Failed,
        NotReady,
    }

    /// <summary>
    /// The ad network seam (DESIGN.md 10.1). Today: SimulatedAdService (Game/Ads), a clearly labelled placeholder
    /// for LevelPlay, which comes later on a separate branch. Callbacks run on the main thread.
    /// </summary>
    public interface IAdService
    {
        bool IsRewardedReady { get; }
        bool IsInterstitialReady { get; }
        void Preload();
        void ShowRewarded(Placement placement, Action<AdResult> onDone);
        void ShowInterstitial(Placement placement, Action<AdResult> onDone);
        /// <summary>Default (until the age gate exists): SetPrivacy(false, true): non-personalized, child-directed.</summary>
        void SetPrivacy(bool personalized, bool childDirected);
    }

    /// <summary>Wall-clock time for the ad rules. Tests use a fake.</summary>
    public interface IClock
    {
        /// <summary>UTC seconds (unix).</summary>
        double Now { get; }
        /// <summary>The local calendar day number (<see cref="DayClock.DayNumber"/>).</summary>
        int Today { get; }
    }

    public sealed class WallClock : IClock
    {
        public double Now => (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        public int Today => DayClock.DayNumber(DateTime.Now);
    }

    /// <summary>A small string key-value store for <see cref="AdState"/> (Unity: PlayerPrefs; tests: a dictionary).</summary>
    public interface IStore
    {
        bool HasKey(string key);
        string GetString(string key);
        void SetString(string key, string value);
        void Flush();
    }

    /// <summary>Where ad events go. Every event carries the bucket and the experiments string (DESIGN.md 10.4).</summary>
    public interface IAdLog
    {
        void Track(string name, string param, int value, int bucket, string experiments);
    }

    /// <summary>
    /// Ad events into the RET-1 telemetry (the local log ring and its sink). LogEntry has a name and two numbers, so
    /// the name carries the parameter and the experiments string: e = "name[:param]|experiments", a = value,
    /// b = bucket.
    /// </summary>
    public sealed class TelemetryAdLog : IAdLog
    {
        readonly Telemetry telemetry;

        public TelemetryAdLog(Telemetry telemetry)
        {
            this.telemetry = telemetry;
        }

        public static string Format(string name, string param, string experiments) =>
            name + (string.IsNullOrEmpty(param) ? "" : ":" + param) + "|" + experiments;

        public void Track(string name, string param, int value, int bucket, string experiments) =>
            telemetry?.Track(Format(name, param, experiments), value, bucket);
    }

    /// <summary>What the governor needs to know about the moment of an offer (DESIGN.md 10.2 A-B, D).</summary>
    public struct OfferContext
    {
        public int lifetimeRuns;
        public float lifetimePlaySec;
        public int sessionIndex;
        public float runSeconds;
        public bool isDailyRanked;
        public int revivesUsedThisRun;
        /// <summary>"+1 Loc" uses this run (the in-run help budget).</summary>
        public int helpsUsedThisRun;
        public bool helpUsedThisTitan;
        public int titanLevel;
        /// <summary>Cubes destroyed / spawn cubes of the current titan.</summary>
        public float progressFraction;
        /// <summary>Titans defeated in this run.</summary>
        public int titansThisRun;
        /// <summary>Salvage (CORE-1) is on: the ship gate of both rescue placements.</summary>
        public bool salvageActive;
        public bool rewardedReady;
    }

    /// <summary>What the interstitial rules need at a results-panel button tap (DESIGN.md 10.2 C).</summary>
    public struct InterContext
    {
        public int lifetimeRuns;
        public float lifetimePlaySec;
        /// <summary>Seconds of play of the run that just ended.</summary>
        public float runSeconds;
        public bool isDailyRanked;
        /// <summary>Seconds the results panel has been on screen.</summary>
        public float resultsVisibleSeconds;
        /// <summary>The run made a new record or reached a new furthest titan (RunSession.NewBest / NewFurthest).</summary>
        public bool newRecordOrFurthest;
        /// <summary>The interstitial became ready within the wait (rule 17).</summary>
        public bool adReady;
    }

    /// <summary>
    /// The offer card's two buttons (DESIGN.md 10.1): side by side, IDENTICAL size and style. Pure layout so the
    /// parity is unit tested; the IMGUI card also asserts it.
    /// </summary>
    public static class OfferCardLayout
    {
        public struct Box
        {
            public float X, Y, W, H;
            public Box(float x, float y, float w, float h)
            {
                X = x;
                Y = y;
                W = w;
                H = h;
            }
        }

        /// <summary>Accept on the left, decline on the right, same width and height, inside a row.</summary>
        public static void Buttons(float rowX, float rowY, float rowW, float rowH, float gap, out Box accept, out Box decline)
        {
            float w = (rowW - gap) / 2f;
            accept = new Box(rowX, rowY, w, rowH);
            decline = new Box(rowX + w + gap, rowY, w, rowH);
        }
    }
}
