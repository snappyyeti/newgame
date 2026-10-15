using System;
using System.Collections.Generic;
using System.Globalization;

namespace Titan.Core.Ads
{
    /// <summary>
    /// What the ad rules remember across launches, one <see cref="IStore"/> key each ("ad_*"). Kept apart from the
    /// game profile on purpose: nothing in the game rules (TitanGame, the draw, the clock, the titans) can read it
    /// (ethics lock, DESIGN.md 11.1).
    /// </summary>
    public sealed class AdState
    {
        public string InstallId = "";
        /// <summary>Stable cohort bucket 0..99 = FNV-1a 64 of the install id, mod 100.</summary>
        public int Bucket;
        /// <summary>Sessions started so far (the first session is 1).</summary>
        public int SessionCount;
        public double LastInterUtc = double.NegativeInfinity;
        public double LastRewardedUtc = double.NegativeInfinity;
        /// <summary>Seconds of play since the last interstitial (or the last rewarded ad).</summary>
        public double PlaySinceInter;
        /// <summary>Seconds of play counted by the governor since install (the clock of the 10-minute rate cap).</summary>
        public double PlayClock;
        /// <summary>PlayClock values at the recent interstitials (the last 10 minutes of play are enough).</summary>
        public readonly List<double> InterPlayStamps = new List<double>();
        /// <summary>The local day the daily counters belong to.</summary>
        public int CounterDay = Profile.NoDay;
        public int IntersToday;
        public int AdBreaksToday;
        public int ProactiveDay = Profile.NoDay;
        /// <summary>UTC seconds until which interstitials are off (Pauză de reclame); 0 = never granted.</summary>
        public double AdFreeUntil;
        /// <summary>The pass ran out and the next results screen has not said "Pauza de reclame s-a încheiat." yet.</summary>
        public bool AdFreeEndNoticePending;
        public bool FirstFreeReviveUsed;
        public bool FirstFreeExtraSlotUsed;

        const string P = "ad_";

        public static AdState Load(IStore store, Func<string> newInstallId = null)
        {
            var s = new AdState();
            s.InstallId = Str(store, "install_id", "");
            if (string.IsNullOrEmpty(s.InstallId))
            {
                s.InstallId = (newInstallId ?? (() => Guid.NewGuid().ToString("N")))();
                store.SetString(P + "install_id", s.InstallId);
            }
            s.Bucket = BucketOf(s.InstallId);
            s.SessionCount = Int(store, "session_count", 0);
            s.LastInterUtc = Dbl(store, "last_inter", double.NegativeInfinity);
            s.LastRewardedUtc = Dbl(store, "last_rewarded", double.NegativeInfinity);
            s.PlaySinceInter = Dbl(store, "play_since_inter", 0);
            s.PlayClock = Dbl(store, "play_clock", 0);
            foreach (var part in Str(store, "inter_stamps", "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                if (double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) s.InterPlayStamps.Add(v);
            s.CounterDay = Int(store, "counter_day", Profile.NoDay);
            s.IntersToday = Int(store, "inters_today", 0);
            s.AdBreaksToday = Int(store, "breaks_today", 0);
            s.ProactiveDay = Int(store, "proactive_day", Profile.NoDay);
            s.AdFreeUntil = Dbl(store, "adfree_until", 0);
            s.AdFreeEndNoticePending = Int(store, "adfree_notice", 0) == 1;
            s.FirstFreeReviveUsed = Int(store, "free_revive_used", 0) == 1;
            s.FirstFreeExtraSlotUsed = Int(store, "free_extra_used", 0) == 1;
            return s;
        }

        public void Save(IStore store)
        {
            store.SetString(P + "install_id", InstallId);
            Set(store, "session_count", SessionCount);
            SetD(store, "last_inter", LastInterUtc);
            SetD(store, "last_rewarded", LastRewardedUtc);
            SetD(store, "play_since_inter", PlaySinceInter);
            SetD(store, "play_clock", PlayClock);
            var parts = new List<string>();
            foreach (var v in InterPlayStamps) parts.Add(v.ToString("R", CultureInfo.InvariantCulture));
            store.SetString(P + "inter_stamps", string.Join(";", parts));
            Set(store, "counter_day", CounterDay);
            Set(store, "inters_today", IntersToday);
            Set(store, "breaks_today", AdBreaksToday);
            Set(store, "proactive_day", ProactiveDay);
            SetD(store, "adfree_until", AdFreeUntil);
            Set(store, "adfree_notice", AdFreeEndNoticePending ? 1 : 0);
            Set(store, "free_revive_used", FirstFreeReviveUsed ? 1 : 0);
            Set(store, "free_extra_used", FirstFreeExtraSlotUsed ? 1 : 0);
            store.Flush();
        }

        /// <summary>The cohort bucket of an install id: FNV-1a 64 over UTF-8, mod 100 (stable on every platform).</summary>
        public static int BucketOf(string installId) => (int)(SeededRandom.FromString(installId ?? "") % 100UL);

        static string Str(IStore s, string key, string fallback) => s.HasKey(P + key) ? s.GetString(P + key) ?? fallback : fallback;

        static int Int(IStore s, string key, int fallback) =>
            int.TryParse(Str(s, key, null), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;

        static double Dbl(IStore s, string key, double fallback)
        {
            string text = Str(s, key, null);
            if (text == "-inf") return double.NegativeInfinity;
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : fallback;
        }

        static void Set(IStore s, string key, int v) => s.SetString(P + key, v.ToString(CultureInfo.InvariantCulture));

        static void SetD(IStore s, string key, double v) =>
            s.SetString(P + key, double.IsNegativeInfinity(v) ? "-inf" : v.ToString("R", CultureInfo.InvariantCulture));
    }
}
