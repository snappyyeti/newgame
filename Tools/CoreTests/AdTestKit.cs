using System;
using System.Collections.Generic;
using System.Linq;
using Titan.Core;
using Titan.Core.Ads;

namespace Titan.Tests
{
    /// <summary>Fakes for the ad rules: a clock that only moves when told, a dictionary store, a scripted ad network.</summary>
    sealed class FakeClock : IClock
    {
        public double Now { get; set; } = 1_000_000;
        public int Today { get; set; } = 300;
        public void Advance(double seconds) => Now += seconds;
    }

    sealed class FakeStore : IStore
    {
        public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
        public int Flushes;
        public bool HasKey(string key) => Values.ContainsKey(key);
        public string GetString(string key) => Values.TryGetValue(key, out var v) ? v : null;
        public void SetString(string key, string value) => Values[key] = value;
        public void Flush() => Flushes++;
    }

    sealed class RecordingLog : IAdLog
    {
        public readonly List<(string Name, string Param, int Value, int Bucket, string Experiments)> Events = new List<(string, string, int, int, string)>();
        public void Track(string name, string param, int value, int bucket, string experiments) => Events.Add((name, param, value, bucket, experiments));
        public int Count(string name) => Events.Count(e => e.Name == name);
    }

    /// <summary>A scripted ad network: readiness flags and the result every show returns (synchronously).</summary>
    sealed class FakeAds : IAdService
    {
        public bool RewardedReady = true, InterReady = true;
        public AdResult Result = AdResult.Completed;
        /// <summary>Like SimulatedAdService.ForceFail: never ready, every show fails.</summary>
        public bool ForceFail;
        public int RewardedShows, InterShows, Preloads;
        public bool IsRewardedReady => !ForceFail && RewardedReady;
        public bool IsInterstitialReady => !ForceFail && InterReady;
        public void Preload() => Preloads++;

        public void ShowRewarded(Placement placement, Action<AdResult> onDone)
        {
            RewardedShows++;
            onDone(ForceFail ? AdResult.Failed : Result);
        }

        public void ShowInterstitial(Placement placement, Action<AdResult> onDone)
        {
            InterShows++;
            onDone(ForceFail ? AdResult.Failed : Result);
        }

        public void SetPrivacy(bool personalized, bool childDirected) { }
    }

    static class AdKit
    {
        /// <summary>An install id whose bucket is <paramref name="bucket"/> (searched once and cached).</summary>
        public static string IdForBucket(int bucket)
        {
            if (ids == null)
            {
                ids = new string[100];
                int found = 0;
                for (int k = 0; found < 100; k++)
                {
                    string id = "install-" + k;
                    int b = AdState.BucketOf(id);
                    if (ids[b] != null) continue;
                    ids[b] = id;
                    found++;
                }
            }
            return ids[bucket];
        }

        static string[] ids;

        /// <summary>A governor on a fresh store with the given bucket (default 50: no holdout), after <paramref name="sessions"/> launches.</summary>
        public static AdGovernor Governor(FakeClock clock, AdConfig cfg = null, int bucket = 50, int sessions = 2, FakeStore store = null, RecordingLog log = null)
        {
            store = store ?? new FakeStore();
            string id = IdForBucket(bucket);
            var gov = new AdGovernor(cfg ?? new AdConfig(), clock, store, log, () => id);
            for (int i = 0; i < sessions; i++) gov.OnSessionStarted();
            return gov;
        }

        /// <summary>
        /// A governor where every interstitial rule passes: second session, lots of play since the last ad, two game
        /// overs this session. Pair it with <see cref="PassingInter"/>.
        /// </summary>
        public static AdGovernor ReadyForInter(FakeClock clock, AdConfig cfg = null, int bucket = 50, RecordingLog log = null, FakeStore store = null)
        {
            var gov = Governor(clock, cfg, bucket, 2, store, log);
            gov.AddPlaySeconds(1000f);
            gov.OnRunEnded(120f);
            gov.OnRunEnded(120f);
            return gov;
        }

        public static InterContext PassingInter() => new InterContext
        {
            lifetimeRuns = 20,
            lifetimePlaySec = 5000f,
            runSeconds = 120f,
            isDailyRanked = false,
            resultsVisibleSeconds = 3f,
            newRecordOrFurthest = false,
            adReady = true,
        };

        public static OfferContext PassingOffer() => new OfferContext
        {
            lifetimeRuns = 20,
            lifetimePlaySec = 5000f,
            sessionIndex = 2,
            runSeconds = 200f,
            isDailyRanked = false,
            revivesUsedThisRun = 0,
            helpsUsedThisRun = 0,
            helpUsedThisTitan = false,
            titanLevel = 4,
            progressFraction = 0.5f,
            titansThisRun = 3,
            salvageActive = true,
            rewardedReady = true,
        };

        public static string Reason(AdGovernor gov, InterContext ctx)
        {
            gov.ShouldShowInterstitial(ctx, out string reason);
            return reason;
        }
    }
}
