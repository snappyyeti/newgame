using System;
using System.Collections.Generic;

namespace Titan.Core.Ads
{
    /// <summary>The first failing interstitial rule (DESIGN.md 10.2 C), logged as ad_inter_skipped{skip_reason}.</summary>
    public static class SkipReason
    {
        public const string Disabled = "inter_disabled";             // 1 (also the holdout buckets 0-9)
        public const string FirstSession = "first_session";          // 2
        public const string GraceRuns = "grace_runs";                // 3
        public const string MinLifetimePlay = "min_lifetime_play";   // 4
        public const string AdBreakActive = "ad_break_active";       // 5
        public const string Cooldown = "cooldown";                   // 6
        public const string MinPlay = "min_play_since_inter";        // 7
        public const string RateCap = "rate_cap";                    // code floor: <= 3.5 per 10 min of play
        public const string AfterRewarded = "after_rewarded";        // 8
        public const string ShortRun = "short_run";                  // 9
        public const string DeclinedRevive = "declined_revive";      // 10
        public const string FirstGameOver = "first_gameover";        // 11
        public const string SessionCap = "session_cap";              // 12
        public const string DailyCap = "daily_cap";                  // 13
        public const string ResultsTooShort = "results_too_short";   // 14
        public const string NewRecord = "new_record";                // 15
        public const string DailyRanked = "daily_ranked";            // 16
        public const string NoFill = "no_fill";                      // 17
    }

    /// <summary>Why an offer is not made (ad_offer_eligible{reason}); "ok" when it is.</summary>
    public static class OfferReason
    {
        public const string Ok = "ok";
        public const string Disabled = "disabled";
        public const string Holdout = "holdout";
        public const string NoSalvage = "no_salvage";
        public const string FtueGate = "ftue_gate";
        public const string DailyRanked = "daily_ranked";
        public const string SurfaceBusy = "surface_busy";
        public const string ReviveUsed = "revive_used";
        public const string NoProgress = "no_progress";
        public const string FirstTitan = "first_titan";
        public const string UsedThisTitan = "used_this_titan";
        public const string HelpBudget = "help_budget";
        public const string NotReady = "not_ready";
        public const string DailyCap = "daily_cap";
        public const string StackFull = "stack_full";
        public const string NotAnOffer = "not_an_offer";
        public const string PassActive = "pass_active";
        public const string AlreadyToday = "already_today";
        public const string NotYet = "not_yet";
        public const string ReviveShown = "revive_shown";
    }

    /// <summary>
    /// Every ad decision (ADS-1, ADS-2; DESIGN.md 10). Pure C#, unit tested with a fake clock and a fake store.
    ///
    /// It knows the player's ad history (AdState) and NOTHING about it flows back into the game rules: TitanGame,
    /// the draw, the titan clock and the titans never see this class (ethics lock, EthicsLockTests). The game asks it
    /// "may I offer / show?" and tells it what happened.
    /// </summary>
    public sealed class AdGovernor
    {
        public readonly AdConfig Config;
        public readonly AdState State;
        readonly IClock clock;
        readonly IStore store;
        readonly IAdLog log;

        int intersThisSession;
        int gameoversThisSession;
        int fallbackGrantsThisSession;
        double backgroundAt = double.NaN;
        double lastAdClosedUtc = double.NegativeInfinity;

        public AdGovernor(AdConfig config, IClock clock, IStore store, IAdLog log = null, Func<string> newInstallId = null)
        {
            Config = config ?? new AdConfig();
            this.clock = clock;
            this.store = store;
            this.log = log;
            State = AdState.Load(store, newInstallId);
            State.Save(store);
        }

        public int Bucket => State.Bucket;

        /// <summary>Holdout: buckets 0-9 never see an interstitial (the control group, forever).</summary>
        public bool InterHoldout => IsInterHoldout(Config, State.Bucket);
        /// <summary>Holdout: buckets 10-14 never see "+1 Loc".</summary>
        public bool ExtraSlotHoldout => IsExtraSlotHoldout(Config, State.Bucket);

        public static bool IsInterHoldout(AdConfig c, int bucket) => bucket >= 0 && bucket <= c.holdout_inter_max_bucket;
        public static bool IsExtraSlotHoldout(AdConfig c, int bucket) => bucket >= c.holdout_extra_slot_min_bucket && bucket <= c.holdout_extra_slot_max_bucket;

        /// <summary>The experiments string on every ad event: the interstitial preset, the revive push-back, remote tags.</summary>
        public string Experiments =>
            "inter=" + Config.PresetName + ";rv_pb=" + Config.revive_push_back.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)
            + (string.IsNullOrEmpty(Config.experiments) ? "" : ";" + Config.experiments);

        public static string Name(Placement p)
        {
            switch (p)
            {
                case Placement.ReviveSecondWind: return "revive_second_wind";
                case Placement.ExtraSlot: return "extra_slot";
                case Placement.AdBreak: return "ad_break";
                default: return "inter_run_end";
            }
        }

        public int SessionIndex => State.SessionCount;
        public int IntersThisSession => intersThisSession;
        public int GameoversThisSession => gameoversThisSession;
        /// <summary>Ad events logged so far (tests count shown/skipped per evaluation).</summary>
        public int OffersShownTotal { get; private set; }

        void Track(string name, string param = null, int value = 0) => log?.Track(name, param, value, State.Bucket, Experiments);

        public void Save() => State.Save(store);

        // ------------------------------------------------------------ sessions and play time

        /// <summary>A session starts at launch, or on resume after ≥ session_timeout_s in the background.</summary>
        public void OnSessionStarted()
        {
            State.SessionCount++;
            intersThisSession = 0;
            gameoversThisSession = 0;
            fallbackGrantsThisSession = 0;
            RollDay();
            Save();
        }

        public void OnAppBackground()
        {
            backgroundAt = clock.Now;
            if (clock.Now - lastAdClosedUtc <= 30.0) Track("app_background_after_ad", null, (int)Math.Round(clock.Now - lastAdClosedUtc));
            Save();
        }

        /// <summary>Back from the background; true when that started a new session (≥ 1800 s away).</summary>
        public bool OnAppForeground()
        {
            if (double.IsNaN(backgroundAt)) return false;
            double away = clock.Now - backgroundAt;
            backgroundAt = double.NaN;
            if (away < Config.session_timeout_s) return false;
            OnSessionStarted();
            return true;
        }

        /// <summary>Seconds of real play (only while a run is ticking: never menus, cards or ads).</summary>
        public void AddPlaySeconds(float dt)
        {
            if (dt <= 0f) return;
            State.PlaySinceInter += dt;
            State.PlayClock += dt;
        }

        public bool ReviveDeclinedThisGameOver { get; private set; }
        /// <summary>The revive card was shown at this game over (then no proactive ad-break card on its results).</summary>
        public bool ReviveCardShownThisGameOver { get; private set; }

        public void OnRunStarted()
        {
            ReviveDeclinedThisGameOver = false;
            ReviveCardShownThisGameOver = false;
            ActiveSurface = null;
        }

        /// <summary>The run really ended (the results panel follows).</summary>
        public void OnRunEnded(float runSeconds)
        {
            gameoversThisSession++;
            Save();
        }

        void RollDay()
        {
            int today = clock.Today;
            if (State.CounterDay == today) return;
            State.CounterDay = today;
            State.IntersToday = 0;
            State.AdBreaksToday = 0;
        }

        // ------------------------------------------------------------ offers (rewarded)

        /// <summary>One ad offer per screen. Priority: revive &gt; "+1 Loc" &gt; ad-break card.</summary>
        public Placement? ActiveSurface { get; private set; }

        static int Priority(Placement p) => p == Placement.ReviveSecondWind ? 3 : p == Placement.ExtraSlot ? 2 : p == Placement.AdBreak ? 1 : 0;

        bool Enabled(Placement p)
        {
            switch (p)
            {
                case Placement.ReviveSecondWind: return Config.revive_enabled;
                case Placement.ExtraSlot: return Config.extra_slot_enabled;
                case Placement.AdBreak: return Config.adbreak_enabled;
                default: return Config.inter_enabled;
            }
        }

        /// <summary>The FTUE gate of every ad: ≥ 3 lifetime runs and ≥ 600 s of lifetime play.</summary>
        public bool PassesGlobalGate(int lifetimeRuns, float lifetimePlaySec) =>
            lifetimeRuns >= Config.min_lifetime_runs_any_ad && lifetimePlaySec >= Config.min_lifetime_play_s_any_ad;

        /// <summary>May this rewarded offer be made now? Pure (no logging, no state change).</summary>
        public bool CanOffer(Placement p, OfferContext ctx, out string reason)
        {
            reason = OfferReason.Ok;
            if (p == Placement.InterRunEnd)
            {
                reason = OfferReason.NotAnOffer;
                return false;
            }
            if (!Enabled(p)) reason = OfferReason.Disabled;
            else if (p == Placement.ExtraSlot && ExtraSlotHoldout) reason = OfferReason.Holdout;
            else if ((p == Placement.ReviveSecondWind || p == Placement.ExtraSlot) && !ctx.salvageActive) reason = OfferReason.NoSalvage; // never sell relief from the dead-ammo bug
            else if (!PassesGlobalGate(ctx.lifetimeRuns, ctx.lifetimePlaySec)) reason = OfferReason.FtueGate;
            else if (ctx.isDailyRanked) reason = OfferReason.DailyRanked;
            else if (ActiveSurface.HasValue && ActiveSurface.Value != p && Priority(ActiveSurface.Value) >= Priority(p)) reason = OfferReason.SurfaceBusy;
            else
            {
                switch (p)
                {
                    case Placement.ReviveSecondWind:
                        if (ctx.revivesUsedThisRun >= Config.revive_max_per_run) reason = OfferReason.ReviveUsed;
                        else if (ctx.titansThisRun < 1 && ctx.progressFraction < Config.revive_min_progress) reason = OfferReason.NoProgress;
                        break;
                    case Placement.ExtraSlot:
                        if (ctx.titanLevel < Config.extra_slot_min_titan) reason = OfferReason.FirstTitan;
                        else if (ctx.helpUsedThisTitan) reason = OfferReason.UsedThisTitan;
                        else if (ctx.helpsUsedThisRun >= Config.inrun_help_max_per_run) reason = OfferReason.HelpBudget;
                        else if (!ctx.rewardedReady && !FirstFreeAvailable(p)) reason = OfferReason.NotReady;
                        break;
                    case Placement.AdBreak:
                        RollDay();
                        if (State.AdBreaksToday >= Config.adbreak_max_per_day) reason = OfferReason.DailyCap;
                        else if (AdBreakStackFull) reason = OfferReason.StackFull;
                        break;
                }
            }
            return reason == OfferReason.Ok;
        }

        /// <summary><see cref="CanOffer"/> at a decision moment, logged as ad_offer_eligible{placement/reason}.</summary>
        public bool EvaluateOffer(Placement p, OfferContext ctx, out string reason)
        {
            bool ok = CanOffer(p, ctx, out reason);
            Track("ad_offer_eligible", Name(p) + "/" + reason);
            return ok;
        }

        /// <summary>The card is on screen. <paramref name="detail"/> and <paramref name="value"/> carry placement params (revive: loss reason, titans; value = % destroyed).</summary>
        public void OnOfferShown(Placement p, string detail = null, int value = 0)
        {
            ActiveSurface = p;
            OffersShownTotal++;
            if (p == Placement.ReviveSecondWind) ReviveCardShownThisGameOver = true;
            Track("ad_offer_shown", Name(p) + (string.IsNullOrEmpty(detail) ? "" : "/" + detail), value);
        }

        public void OnOfferAccepted(Placement p) => Track("ad_offer_accepted", Name(p));

        /// <summary>The player said no. A declined revive sets the flag that blocks the interstitial of this game over (rule 10).</summary>
        public void OnOfferDeclined(Placement p)
        {
            if (ActiveSurface == p) ActiveSurface = null;
            if (p == Placement.ReviveSecondWind) ReviveDeclinedThisGameOver = true;
            Track("ad_offer_declined", Name(p));
        }

        /// <summary>The card closed without a decline (granted, or the ad failed).</summary>
        public void OnOfferClosed(Placement p)
        {
            if (ActiveSurface == p) ActiveSurface = null;
        }

        public void OnShowResult(Placement p, AdResult r)
        {
            if (r == AdResult.Completed || r == AdResult.ClosedEarly) lastAdClosedUtc = clock.Now;
            Track("ad_show_result", Name(p) + "/" + r.ToString().ToLowerInvariant());
        }

        /// <summary>A rewarded ad was watched to the end and its reward given: no interstitial for a while (rule 8).</summary>
        public void OnRewardGranted(Placement p)
        {
            State.LastRewardedUtc = clock.Now;
            State.PlaySinceInter = 0;
            Track("ad_reward_granted", Name(p));
            Save();
        }

        public bool FirstFreeAvailable(Placement p)
        {
            if (p == Placement.ReviveSecondWind) return Config.first_free_revive && !State.FirstFreeReviveUsed;
            if (p == Placement.ExtraSlot) return Config.first_free_extra_slot && !State.FirstFreeExtraSlotUsed;
            return false;
        }

        /// <summary>The first lifetime use of a rescue is free ("Revino gratis" / "Încearcă gratis"), with no ad.</summary>
        public void ConsumeFirstFree(Placement p)
        {
            if (p == Placement.ReviveSecondWind) State.FirstFreeReviveUsed = true;
            if (p == Placement.ExtraSlot) State.FirstFreeExtraSlotUsed = true;
            Track("ad_free_grant", Name(p));
            Save();
        }

        /// <summary>The revive ad did not load: granted anyway once per session.</summary>
        public bool CanFallbackGrant => fallbackGrantsThisSession < Config.fallback_grants_per_session;

        public void OnFallbackGrant(Placement p)
        {
            fallbackGrantsThisSession++;
            Track("ad_fallback_grant", Name(p));
        }

        /// <summary>Free-form ad telemetry (revive_outcome, extra slot titan end) with the bucket and experiments.</summary>
        public void LogEvent(string name, string param, int value) => Track(name, param, value);

        // ------------------------------------------------------------ ad-break pass (10.2 D)

        public bool AdBreakActive => clock.Now < State.AdFreeUntil;
        public double AdBreakRemainingSeconds => Math.Max(0.0, State.AdFreeUntil - clock.Now);

        /// <summary>A grant would not add the full 30 minutes (the stack is at its 60-minute top): the link hides.</summary>
        public bool AdBreakStackFull => AdBreakRemainingSeconds > (Config.adbreak_max_stack_minutes - Config.adbreak_minutes) * 60.0 + 1e-6;

        /// <summary>adfree_until = min(max(now, adfree_until) + 30 min, now + 60 min). Counts toward the 4 per day.</summary>
        public void GrantAdBreak()
        {
            double now = clock.Now;
            RollDay();
            State.AdFreeUntil = Math.Min(Math.Max(now, State.AdFreeUntil) + Config.adbreak_minutes * 60.0, now + Config.adbreak_max_stack_minutes * 60.0);
            State.AdBreaksToday++;
            State.AdFreeEndNoticePending = true;
            Save();
        }

        /// <summary>
        /// The proactive "PAUZĂ DE RECLAME" card: on the results panel, once per local day, after today's 2nd
        /// interstitial, never when the revive card was shown at this game over, never while a pass runs.
        /// </summary>
        public bool ShouldShowProactiveAdBreak(OfferContext ctx, out string reason)
        {
            RollDay();
            if (!CanOffer(Placement.AdBreak, ctx, out reason)) return false;
            if (ReviveCardShownThisGameOver) reason = OfferReason.ReviveShown;
            else if (AdBreakActive) reason = OfferReason.PassActive;
            else if (State.ProactiveDay == clock.Today) reason = OfferReason.AlreadyToday;
            else if (State.IntersToday < Config.adbreak_proactive_after_inter) reason = OfferReason.NotYet;
            return reason == OfferReason.Ok;
        }

        /// <summary>The proactive card was opened (its RewardedOffer logs ad_offer_shown): no second one today.</summary>
        public void OnProactiveAdBreakShown()
        {
            State.ProactiveDay = clock.Today;
            Save();
        }

        /// <summary>True once, on the first results screen after a pass ran out: "Pauza de reclame s-a încheiat."</summary>
        public bool TakeAdBreakEndNotice()
        {
            if (!State.AdFreeEndNoticePending || AdBreakActive) return false;
            State.AdFreeEndNoticePending = false;
            Save();
            return true;
        }

        // ------------------------------------------------------------ interstitial (10.2 C)

        int InterstitialsInLastTenMinutesOfPlay()
        {
            int n = 0;
            foreach (var stamp in State.InterPlayStamps) if (State.PlayClock - stamp < 600.0) n++;
            return n;
        }

        /// <summary>
        /// The 17 rules in order; the first failing one is the skip reason. Pure: no logging, no state change (the
        /// "Reclamă înainte de rundă" tag asks the same question with <paramref name="checkFill"/> = false).
        /// </summary>
        public bool ShouldShowInterstitial(InterContext ctx, out string skipReason, bool checkFill = true)
        {
            RollDay();
            double now = clock.Now;
            skipReason = null;
            if (!Config.inter_enabled || InterHoldout) skipReason = SkipReason.Disabled;
            else if (State.SessionCount < 2) skipReason = SkipReason.FirstSession;
            else if (ctx.lifetimeRuns < Config.GraceRuns) skipReason = SkipReason.GraceRuns;
            else if (ctx.lifetimePlaySec < Math.Max(Config.inter_min_lifetime_play_s, Config.min_lifetime_play_s_any_ad)) skipReason = SkipReason.MinLifetimePlay;
            else if (AdBreakActive) skipReason = SkipReason.AdBreakActive;
            else if (now - State.LastInterUtc < Config.CooldownSeconds) skipReason = SkipReason.Cooldown;
            else if (State.PlaySinceInter < Config.MinPlaySeconds) skipReason = SkipReason.MinPlay;
            else if (InterstitialsInLastTenMinutesOfPlay() + 1 > Config.MaxPer10Min) skipReason = SkipReason.RateCap;
            else if (now - State.LastRewardedUtc < Config.inter_after_rewarded_s) skipReason = SkipReason.AfterRewarded;
            else if (ctx.runSeconds < Config.inter_min_run_s) skipReason = SkipReason.ShortRun;
            else if (ReviveDeclinedThisGameOver) skipReason = SkipReason.DeclinedRevive;
            else if (gameoversThisSession < 2) skipReason = SkipReason.FirstGameOver;
            else if (intersThisSession >= Config.inter_max_per_session) skipReason = SkipReason.SessionCap;
            else if (State.IntersToday >= Config.inter_max_per_day) skipReason = SkipReason.DailyCap;
            else if (ctx.resultsVisibleSeconds < Config.inter_results_min_s) skipReason = SkipReason.ResultsTooShort;
            else if (ctx.newRecordOrFurthest) skipReason = SkipReason.NewRecord;
            else if (ctx.isDailyRanked) skipReason = SkipReason.DailyRanked;
            else if (checkFill && !ctx.adReady) skipReason = SkipReason.NoFill;
            return skipReason == null;
        }

        public void LogInterEligible() => Track("ad_inter_eligible");
        public void LogInterSkipped(string reason) => Track("ad_inter_skipped", reason);

        /// <summary>An interstitial was on screen: cooldowns restart, counters grow.</summary>
        public void OnInterstitialShown()
        {
            RollDay();
            State.LastInterUtc = clock.Now;
            State.PlaySinceInter = 0;
            State.InterPlayStamps.Add(State.PlayClock);
            State.InterPlayStamps.RemoveAll(s => State.PlayClock - s >= 600.0);
            intersThisSession++;
            State.IntersToday++;
            Track("ad_inter_shown");
            Save();
        }

        public void OnInterstitialClosed(float seconds)
        {
            lastAdClosedUtc = clock.Now;
            Track("ad_inter_closed", null, (int)Math.Round(seconds));
        }
    }
}
