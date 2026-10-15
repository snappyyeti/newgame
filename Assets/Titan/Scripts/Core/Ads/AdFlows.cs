using System;
using System.Globalization;

namespace Titan.Core.Ads
{
    /// <summary>
    /// The ONLY place an interstitial can start (DESIGN.md 10.2 C): a tap on "Joacă din nou", "Reia ciclul" or
    /// "Joc nou" on the results panel. Rules 1-16 are checked at the tap; then the ad gets up to 1.0 s to be ready
    /// (rule 17, else "no_fill"); then the honest 0.8 s card; then the ad. The requested run starts on ANY result:
    /// <c>onDone</c> is always called exactly once. Each evaluation logs exactly one ad_inter_shown or
    /// ad_inter_skipped.
    /// </summary>
    public sealed class InterstitialGate
    {
        public enum Phase
        {
            Idle,
            /// <summary>Rules 1-16 passed; waiting up to inter_ready_wait_s for the ad to be ready.</summary>
            WaitingFill,
            /// <summary>"Reclamă — Mulțumim că susții jocul gratuit." (0.8 s).</summary>
            Card,
            Showing,
        }

        readonly AdGovernor governor;
        readonly IAdService ads;
        InterContext ctx;
        Action onDone;
        float timer, showSeconds;

        public InterstitialGate(AdGovernor governor, IAdService ads)
        {
            this.governor = governor;
            this.ads = ads;
        }

        public Phase Current { get; private set; } = Phase.Idle;
        /// <summary>The card or the ad is on screen: the pause contract holds.</summary>
        public bool Blocking => Current == Phase.Card || Current == Phase.Showing;
        /// <summary>Evaluations finished (shown or skipped).</summary>
        public int Evaluations { get; private set; }

        /// <summary>
        /// Called from the results panel button. Returns false (and does nothing) while a previous call is still in
        /// progress; otherwise <paramref name="done"/> runs exactly once, possibly right away.
        /// </summary>
        public bool TryShow(InterContext context, Action done)
        {
            if (Current != Phase.Idle) return false;
            ctx = context;
            onDone = done;
            if (!governor.ShouldShowInterstitial(ctx, out string reason, false))
            {
                Skip(reason);
                return true;
            }
            if (!ads.IsInterstitialReady) ads.Preload();
            Current = Phase.WaitingFill;
            timer = 0f;
            Update(0f);
            return true;
        }

        /// <summary>Advance with REAL (unscaled) seconds: the pause contract sets Time.timeScale = 0.</summary>
        public void Update(float realDt)
        {
            switch (Current)
            {
                case Phase.WaitingFill:
                    timer += realDt;
                    bool ready = ads.IsInterstitialReady;
                    if (!ready && timer < governor.Config.inter_ready_wait_s) return;
                    ctx.adReady = ready;
                    if (!governor.ShouldShowInterstitial(ctx, out string reason))
                    {
                        Skip(reason);
                        return;
                    }
                    governor.LogInterEligible();
                    Current = Phase.Card;
                    timer = 0f;
                    return;
                case Phase.Card:
                    timer += realDt;
                    if (timer < governor.Config.inter_card_s) return;
                    Current = Phase.Showing;
                    showSeconds = 0f;
                    ads.ShowInterstitial(Placement.InterRunEnd, OnResult);
                    return;
                case Phase.Showing:
                    showSeconds += realDt;
                    return;
            }
        }

        void OnResult(AdResult r)
        {
            if (Current != Phase.Showing) return;
            governor.OnShowResult(Placement.InterRunEnd, r);
            if (r == AdResult.Completed || r == AdResult.ClosedEarly)
            {
                governor.OnInterstitialShown();
                governor.OnInterstitialClosed(showSeconds);
                Finish();
            }
            else Skip(SkipReason.NoFill);
        }

        void Skip(string reason)
        {
            governor.LogInterSkipped(reason);
            Finish();
        }

        void Finish()
        {
            Current = Phase.Idle;
            Evaluations++;
            var done = onDone;
            onDone = null;
            done?.Invoke();
        }
    }

    /// <summary>Where a rewarded offer card is (DESIGN.md 10.1, 10.2 A-B-D).</summary>
    public enum OfferPhase
    {
        /// <summary>The card with its two equal buttons.</summary>
        Card,
        /// <summary>Accepted, the ad is loading: a spinner (revive: up to revive_load_wait_s).</summary>
        Loading,
        /// <summary>The ad is on screen.</summary>
        Showing,
        /// <summary>No ad could load: "Nicio reclamă disponibilă acum", accept disabled, decline still works.</summary>
        NoAd,
        Granted,
        Declined,
        /// <summary>Closed without a reward and without a decline (the ad failed; nothing changes).</summary>
        Closed,
    }

    /// <summary>
    /// One rewarded offer card from open to close, shared by the revive, "+1 Loc" and the ad-break pass. The reward
    /// (<c>grant</c>) runs ONLY on Completed, on a first-lifetime free use, or on the revive's once-per-session
    /// fallback when the ad would not load. Every path ends in Granted, Declined or Closed: the card can always be
    /// left (the decline button never depends on the ad).
    /// </summary>
    public sealed class RewardedOffer
    {
        public readonly Placement Placement;
        readonly AdGovernor governor;
        readonly IAdService ads;
        readonly Action<bool> grant;
        readonly float loadWait;
        readonly bool fallbackGrant;
        readonly bool closedEarlyBackToCard;
        float timer;

        /// <param name="grant">Gives the reward; the argument says whether it came without an ad (free or fallback).</param>
        /// <param name="loadWaitSeconds">How long the spinner waits for an ad that is not ready (0: fail at once).</param>
        /// <param name="fallbackGrant">Revive only: after the wait, grant anyway once per session.</param>
        /// <param name="closedEarlyBackToCard">Revive: closing the ad early returns to the card; others close.</param>
        public RewardedOffer(Placement placement, AdGovernor governor, IAdService ads, Action<bool> grant,
            float loadWaitSeconds, bool fallbackGrant, bool closedEarlyBackToCard, string detail = null, int value = 0)
        {
            Placement = placement;
            this.governor = governor;
            this.ads = ads;
            this.grant = grant;
            loadWait = loadWaitSeconds;
            this.fallbackGrant = fallbackGrant;
            this.closedEarlyBackToCard = closedEarlyBackToCard;
            Free = governor.FirstFreeAvailable(placement);
            Phase = OfferPhase.Card;
            governor.OnOfferShown(placement, detail, value);
            if (!Free && !ads.IsRewardedReady) ads.Preload();
        }

        public OfferPhase Phase { get; private set; }
        /// <summary>First lifetime use: the primary button reads "Revino gratis" / "Încearcă gratis" and shows no ad.</summary>
        public bool Free { get; }
        /// <summary>The revive was given without an ad because none loaded: the toast says so.</summary>
        public bool FallbackUsed { get; private set; }
        /// <summary>The card (or the ad over it) is on screen: the pause contract holds.</summary>
        public bool IsOpen => Phase == OfferPhase.Card || Phase == OfferPhase.Loading || Phase == OfferPhase.Showing || Phase == OfferPhase.NoAd;
        public bool AcceptEnabled => Phase == OfferPhase.Card;
        public bool DeclineEnabled => Phase == OfferPhase.Card || Phase == OfferPhase.NoAd;

        public void Accept()
        {
            if (!AcceptEnabled) return;
            governor.OnOfferAccepted(Placement);
            if (Free)
            {
                governor.ConsumeFirstFree(Placement);
                Grant(true);
                return;
            }
            if (ads.IsRewardedReady) Show();
            else StartLoading(true);
        }

        public void Decline()
        {
            if (!DeclineEnabled) return;
            Phase = OfferPhase.Declined;
            governor.OnOfferDeclined(Placement);
        }

        /// <summary>Advance with real (unscaled) seconds.</summary>
        public void Update(float realDt)
        {
            if (Phase != OfferPhase.Loading) return;
            timer += realDt;
            if (ads.IsRewardedReady && timer < loadWait)
            {
                Show();
                return;
            }
            if (timer < loadWait) return;
            NothingLoaded();
        }

        /// <param name="restartWait">False after a failed show: the wait keeps counting, so a flaky ad cannot spin forever.</param>
        void StartLoading(bool restartWait)
        {
            ads.Preload();
            Phase = OfferPhase.Loading;
            if (restartWait) timer = 0f;
            if (loadWait <= 0f || timer >= loadWait) NothingLoaded();
        }

        void NothingLoaded()
        {
            if (fallbackGrant && governor.CanFallbackGrant)
            {
                governor.OnFallbackGrant(Placement);
                FallbackUsed = true;
                Grant(true);
                return;
            }
            if (fallbackGrant) Phase = OfferPhase.NoAd; // the card stays: "Nicio reclamă disponibilă acum", only "Nu, mulțumesc" works
            else Close();
        }

        void Show()
        {
            Phase = OfferPhase.Showing;
            ads.ShowRewarded(Placement, OnResult);
        }

        void OnResult(AdResult r)
        {
            if (Phase != OfferPhase.Showing) return;
            governor.OnShowResult(Placement, r);
            switch (r)
            {
                case AdResult.Completed:
                    governor.OnRewardGranted(Placement);
                    Grant(false);
                    break;
                case AdResult.ClosedEarly:
                    if (closedEarlyBackToCard) Phase = OfferPhase.Card;
                    else Close();
                    break;
                default:
                    if (loadWait > 0f) StartLoading(false); // the revive: spinner, then the fallback rule
                    else Close();
                    break;
            }
        }

        void Grant(bool withoutAd)
        {
            Phase = OfferPhase.Granted;
            governor.OnOfferClosed(Placement);
            grant?.Invoke(withoutAd);
        }

        void Close()
        {
            Phase = OfferPhase.Closed;
            governor.OnOfferClosed(Placement);
        }
    }

    /// <summary>
    /// The gold "+1 LOC" wall button (DESIGN.md 10.2 B): it appears when ≥ 4 slots are taken and ≥ 3 of them are idle
    /// has held for 1.0 s, and hides once that has been false for 2 s. A decline hides it for 20 s of game time;
    /// after 2 declines it stays hidden for the rest of that titan and the whole next titan. Eligibility (titan ≥ 2,
    /// budget, ready ad, gates) is the governor's; this class only holds the timing. A tray tap never opens it.
    /// </summary>
    public sealed class ExtraSlotButton
    {
        readonly AdConfig cfg;
        float trueFor, falseFor, hiddenUntil = float.NegativeInfinity;
        int declinesThisRun;
        int restUntilTitan;
        int titan = -1;

        public ExtraSlotButton(AdConfig cfg)
        {
            this.cfg = cfg;
        }

        public bool Visible { get; private set; }
        /// <summary>The 0.3 s pulse plays once when the button appears.</summary>
        public bool JustAppeared { get; private set; }
        public int DeclinesThisRun => declinesThisRun;

        /// <summary>The trigger condition alone (pure; also measured in the simulator).</summary>
        public static bool Condition(AdConfig cfg, int occupied, int idle) => occupied >= cfg.extra_slot_min_occupied && idle >= cfg.extra_slot_min_idle;

        public void ResetRun()
        {
            trueFor = falseFor = 0f;
            hiddenUntil = float.NegativeInfinity;
            declinesThisRun = 0;
            restUntilTitan = 0;
            titan = -1;
            Visible = false;
        }

        /// <param name="gameTime">TitanGame.GameTime (game seconds: the 20 s cooldown does not run while paused).</param>
        /// <param name="eligible">The governor's CanOffer(ExtraSlot) right now.</param>
        public void Update(float dt, float gameTime, int titanLevel, int occupied, int idle, bool eligible)
        {
            JustAppeared = false;
            if (titanLevel != titan)
            {
                titan = titanLevel;
                trueFor = falseFor = 0f;
                Visible = false;
            }
            bool cond = Condition(cfg, occupied, idle);
            if (cond)
            {
                trueFor += dt;
                falseFor = 0f;
            }
            else
            {
                falseFor += dt;
                trueFor = 0f;
            }
            bool allowed = eligible && gameTime >= hiddenUntil && titanLevel > restUntilTitan;
            if (!allowed)
            {
                Visible = false;
                return;
            }
            if (!Visible && cond && trueFor >= cfg.extra_slot_debounce_s - 1e-4f)
            {
                Visible = true;
                JustAppeared = true;
            }
            else if (Visible && !cond && falseFor >= cfg.extra_slot_hide_after_s - 1e-4f) Visible = false;
        }

        public void OnDeclined(float gameTime, int titanLevel)
        {
            Visible = false;
            declinesThisRun++;
            hiddenUntil = gameTime + cfg.extra_slot_decline_cooldown_s;
            if (declinesThisRun >= cfg.extra_slot_declines_rest_titan)
            {
                restUntilTitan = titanLevel + 1; // the rest of this titan and the whole next one
                declinesThisRun = 0;
            }
        }

        public void OnUsed()
        {
            Visible = false;
        }
    }

    /// <summary>The player-facing ad texts (from the RO/EN table, <see cref="Titan.Core.Loc"/>). Pure, unit tested.</summary>
    public static class AdText
    {
        public static string Tag => Loc.T("ad.tag");
        public static string BeforeRunTag => Loc.T("ad.beforeRun");
        public static string InterCard => Loc.T("ad.interCard");

        public static string ReviveTitle => Loc.T("ad.reviveTitle");
        public static string ReviveValue => Loc.T("ad.reviveValue");
        public static string ReviveAccept => Loc.T("ad.reviveAccept");
        public static string ReviveAcceptFree => Loc.T("ad.reviveAcceptFree");
        public static string Decline => Loc.T("ad.decline");
        public static string ReviveFooter => Loc.T("ad.reviveFooter");
        public static string ReviveFallbackToast => Loc.T("ad.reviveFallback");
        public static string NoAdNow => Loc.T("ad.noneNow");
        /// <summary>"Se încarcă reclama" (the card adds its own dots).</summary>
        public static string Loading => Loc.T("ad.loading");

        public static string ExtraSlotButton => Loc.T("ad.slotButton");
        public static string ExtraSlotTitle => Loc.T("ad.slotTitle");
        public static string ExtraSlotValue => Loc.T("ad.slotValue");
        public static string ExtraSlotAccept => Loc.T("ad.watch");
        public static string ExtraSlotAcceptFree => Loc.T("ad.slotAcceptFree");
        public static string ExtraSlotDecline => Loc.T("ad.slotDecline");

        public static string AdBreakTitle => Loc.T("ad.breakTitle");
        public static string AdBreakValue => Loc.T("ad.breakValue");
        public static string AdBreakAccept => Loc.T("ad.watch");
        public static string AdBreakLink => Loc.T("ad.breakLink");
        public static string AdBreakEnded => Loc.T("ad.breakEnded");

        /// <summary>"Titanul #5 · 62% distrus · Scor 31.400". No record-gap line (no loss-aversion pressure).</summary>
        public static string ReviveStake(int titan, float progressFraction, int score) =>
            Loc.T("ad.reviveStake", titan, (int)Math.Floor(100f * Math.Max(0f, Math.Min(1f, progressFraction)) + 1e-4f), HudText.Num(score));

        public static string ExtraSlotFooter(int helpsLeft) => Loc.T("ad.slotFooter", Math.Max(0, helpsLeft));

        /// <summary>"Fără reclame încă 12:40" (non-interactive while the pass runs).</summary>
        public static string AdBreakRemaining(double seconds)
        {
            int s = (int)Math.Ceiling(Math.Max(0.0, seconds));
            return Loc.T("ad.breakRemaining", (s / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (s % 60).ToString("00", CultureInfo.InvariantCulture));
        }
    }
}
