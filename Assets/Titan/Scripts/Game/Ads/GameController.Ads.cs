using System.Collections.Generic;
using Titan.Core;
using Titan.Core.Ads;
using UnityEngine;

namespace Titan.Game
{
    /// <summary>
    /// Every ad surface of the game (ADS-1, ADS-2; DESIGN.md 10), on top of the pure rules in Titan.Core.Ads:
    /// the revive card at a game over, the gold "+1 LOC" wall button, the between-runs interstitial (results panel
    /// buttons only), the "Pauză de reclame" link and card, the pause contract. The ad network is
    /// <see cref="SimulatedAdService"/>, a labelled placeholder for LevelPlay.
    ///
    /// Ethics lock: nothing here changes a game rule. The game is told only "revive now" or "open the 6th slot",
    /// both of which the player chose on a card; tray taps never reach this file (HandleTap → TrayTap).
    /// </summary>
    public sealed partial class GameController
    {
        AdConfig adConfig;
        AdGovernor governor;
        SimulatedAdService ads;
        InterstitialGate interGate;
        ExtraSlotButton slotButton;

        RewardedOffer reviveOffer, slotOffer, breakOffer;

        /// <summary>A lost run waiting for the revive decision: the session hears about it only when it really ends.</summary>
        GameEvent pendingGameOver;
        float reviveFreezeUntil;
        float reviveFadeStart = -10f;
        bool revivedThisRun;
        int reviveTitansAt;
        float reviveSecondsAt;

        int helpsUsedThisRun;
        bool helpUsedThisTitan;
        float slotButtonShownAt = -10f;
        float goldFlashStart = -10f;
        // "+1 Loc" telemetry at titan end: shots from slot 6, and the line as it last stood.
        int slot6Shots, lastOccupied, lastIdle;
        bool slot6ThisTitan;

        // The results panel (between runs).
        float resultsSince = -1f;
        bool adBreakEndedNotice;
        /// <summary>"Reclamă înainte de rundă" under the results buttons, or null (decided once per frame).</summary>
        string resultsButtonNote;
        /// <summary>Results-panel seconds used for that note: the same value goes to the gate at the tap.</summary>
        float noteVisibleSeconds;
        /// <summary>False while a card or the interstitial covers the results panel.</summary>
        bool resultsInteractive = true;

        void InitAds()
        {
            adConfig = new AdConfig();
            var json = Resources.Load<TextAsset>("ad_config");
            if (json != null)
            {
                var ignored = new List<string>();
                adConfig.ApplyJson(json.text, ignored);
                foreach (var key in ignored) Debug.LogWarning("[Titan] ad_config.json: ignored '" + key + "'");
            }
            governor = new AdGovernor(adConfig, new WallClock(), new PlayerPrefsAdStore(), new TelemetryAdLog(telemetry));
            governor.OnSessionStarted();
            ads = SimulatedAdService.Create(adConfig);
            ads.SetPrivacy(false, true); // non-personalized, child-directed until the age gate exists (required before store submission)
            interGate = new InterstitialGate(governor, ads);
            slotButton = new ExtraSlotButton(adConfig);
        }

        void ResetRunAds()
        {
            governor.OnRunStarted();
            slotButton.ResetRun();
            reviveOffer = slotOffer = breakOffer = null;
            pendingGameOver = null;
            revivedThisRun = false;
            reviveFadeStart = -10f;
            helpsUsedThisRun = 0;
            helpUsedThisTitan = false;
            slot6Shots = lastOccupied = lastIdle = 0;
            slot6ThisTitan = false;
            resultsSince = -1f;
            adBreakEndedNotice = false;
            resultsButtonNote = null;
            resultsInteractive = true;
            AdPause.Apply(false);
        }

        OfferContext OfferCtx() => new OfferContext
        {
            lifetimeRuns = profile.totalRuns,
            lifetimePlaySec = profile.totalPlaySeconds,
            sessionIndex = governor.SessionIndex,
            runSeconds = session.Seconds,
            isDailyRanked = false, // RET-3 (Titanul Zilei) does not exist yet; its official run must pass true
            revivesUsedThisRun = game.Revives,
            helpsUsedThisRun = helpsUsedThisRun,
            helpUsedThisTitan = helpUsedThisTitan,
            titanLevel = game.Level,
            progressFraction = game.ProgressFraction,
            titansThisRun = game.TitansDefeated,
            salvageActive = game.SalvageActive,
            rewardedReady = ads.IsRewardedReady,
        };

        InterContext InterCtx(float resultsSeconds) => new InterContext
        {
            lifetimeRuns = profile.totalRuns,
            lifetimePlaySec = profile.totalPlaySeconds,
            runSeconds = session.Seconds,
            isDailyRanked = false,
            resultsVisibleSeconds = resultsSeconds,
            newRecordOrFurthest = session.NewBest || session.NewFurthest,
        };

        /// <summary>A card or an ad is up: no tick, no tray taps (the pause contract).</summary>
        bool PlayFrozen => AdPause.Active;

        bool AnyAdSurfaceOpen => (reviveOffer != null && reviveOffer.IsOpen) || (slotOffer != null && slotOffer.IsOpen)
            || (breakOffer != null && breakOffer.IsOpen) || interGate.Blocking || ads.IsShowing;

        void SyncPause() => AdPause.Apply(AnyAdSurfaceOpen);

        // ---------------------------------------------------------------- per frame

        /// <summary>Called first in Update with real seconds (the pause contract sets Time.timeScale = 0).</summary>
        void UpdateAds(float realDt)
        {
            if (game == null) return;
#if ENABLE_LEGACY_INPUT_MANAGER
            // QA switch (editor only): F10 makes every simulated ad fail, to check that no flow sticks (ADS-1).
            if (Application.isEditor && Input.GetKeyDown(KeyCode.F10))
            {
                SimulatedAdService.ForceFail = !SimulatedAdService.ForceFail;
                ShowBanner("Reclame simulate: " + (SimulatedAdService.ForceFail ? "EȘUEAZĂ (ForceFail)" : "normal"), Color.white, 1.5f);
            }
#endif
            reviveOffer?.Update(realDt);
            slotOffer?.Update(realDt);
            breakOffer?.Update(realDt);
            interGate.Update(realDt);

            // The revive card comes after a 0.6 s freeze on the final frame (with the loss banner).
            if (pendingGameOver != null && reviveOffer == null && Time.realtimeSinceStartup >= reviveFreezeUntil)
            {
                string detail = pendingGameOver.Reason.ToString().ToLowerInvariant() + "/t" + game.TitansDefeated;
                reviveOffer = new RewardedOffer(Placement.ReviveSecondWind, governor, ads, GrantRevive, adConfig.revive_load_wait_s, true, true,
                    detail, Mathf.FloorToInt(100f * game.ProgressFraction));
            }
            CloseFinishedOffers();

            // The "+1 LOC" wall button: only while the run ticks.
            if (!game.IsOver && !PlayFrozen)
            {
                CountLine(out int occupied, out int idle);
                lastOccupied = occupied;
                lastIdle = idle;
                bool eligible = slotOffer == null && governor.CanOffer(Placement.ExtraSlot, OfferCtx(), out _);
                slotButton.Update(Time.deltaTime, game.GameTime, game.Level, occupied, idle, eligible);
                if (slotButton.JustAppeared)
                {
                    slotButtonShownAt = Time.realtimeSinceStartup;
                    governor.EvaluateOffer(Placement.ExtraSlot, OfferCtx(), out _);
                }
            }

            // The results panel: the honest "Reclamă înainte de rundă" note uses exactly what the gate will check.
            if (game.IsOver && pendingGameOver == null && resultsSince >= 0f)
            {
                noteVisibleSeconds = Time.realtimeSinceStartup - resultsSince;
                bool covered = interGate.Current != InterstitialGate.Phase.Idle || (breakOffer != null && breakOffer.IsOpen);
                resultsInteractive = !covered;
                resultsButtonNote = !covered && governor.ShouldShowInterstitial(InterCtx(noteVisibleSeconds), out _, false) ? AdText.BeforeRunTag : null;
            }
            SyncPause();
        }

        void CountLine(out int occupied, out int idle)
        {
            occupied = idle = 0;
            for (int s = 0; s < game.ActiveSlots; s++)
            {
                var c = game.Line[s];
                if (c == null) continue;
                occupied++;
                if (!game.Monster.HasVisibleOfColor(c.Color)) idle++;
            }
        }

        void CloseFinishedOffers()
        {
            if (reviveOffer != null && !reviveOffer.IsOpen)
            {
                if (reviveOffer.Phase == OfferPhase.Declined || reviveOffer.Phase == OfferPhase.Closed) FinishDeferredGameOver();
                reviveOffer = null;
            }
            if (slotOffer != null && !slotOffer.IsOpen)
            {
                if (slotOffer.Phase == OfferPhase.Declined) slotButton.OnDeclined(game.GameTime, game.Level);
                if (slotOffer.Phase == OfferPhase.Closed) ShowToast(AdText.NoAdNow);
                slotOffer = null;
            }
            if (breakOffer != null && !breakOffer.IsOpen)
            {
                if (breakOffer.Phase == OfferPhase.Closed) ShowToast(AdText.NoAdNow);
                breakOffer = null;
            }
            SyncPause();
        }

        void ShowToast(string text)
        {
            toast = text;
            toastUntil = Time.time + 1.8f;
        }

        /// <summary>Game events the ad telemetry needs (slot 6 shots, titan changes).</summary>
        void TrackAdEvent(GameEvent e)
        {
            switch (e.Kind)
            {
                case EventKind.Shot:
                    if (e.Slot >= cfg.LineSlots) slot6Shots++;
                    break;
                case EventKind.SlotAdded:
                    slot6ThisTitan = true;
                    goldFlashStart = Time.realtimeSinceStartup;
                    break;
                case EventKind.TitanDefeated:
                    LogSlot6TitanEnd();
                    break;
                case EventKind.TitanSpawned:
                    helpUsedThisTitan = false;
                    break;
            }
        }

        /// <summary>"+1 Loc" telemetry when a titan with the 6th slot ends: idle_on_line, occupied, slot6_used_shots.</summary>
        void LogSlot6TitanEnd()
        {
            if (!slot6ThisTitan) return;
            governor.LogEvent("extra_slot_titan_end", "idle" + lastIdle + "/occ" + lastOccupied, slot6Shots);
            slot6ThisTitan = false;
            slot6Shots = 0;
        }

        // ---------------------------------------------------------------- "A doua suflare"

        /// <summary>
        /// At a game over: if the revive may be offered, hold the GameOver event back (no run_end, no results yet),
        /// freeze 0.6 s on the final frame with the loss banner, then show the card. Returns true when held.
        /// </summary>
        bool OfferSecondWind(GameEvent e)
        {
            if (!game.CanRevive || pendingGameOver != null) return false;
            if (!governor.EvaluateOffer(Placement.ReviveSecondWind, OfferCtx(), out _)) return false;
            pendingGameOver = e;
            reviveFreezeUntil = Time.realtimeSinceStartup + adConfig.revive_freeze_s;
            ShowBanner(LossLine(e.Reason), ArenaView.BannerRed, adConfig.revive_freeze_s + 0.3f);
            return true;
        }

        static string LossLine(LoseReason r) => r == LoseReason.WallReached ? "Titanul a spart zidul!" : r == LoseReason.Deadlock ? "BLOCAT!" : "Locurile sunt pline!";

        /// <summary>Completed, the free first use or the fallback: the run continues (no countdown: the 2 s grace covers it).</summary>
        void GrantRevive(bool withoutAd)
        {
            if (pendingGameOver == null || !game.Revive(adConfig.revive_push_back, adConfig.revive_grace_s)) return;
            pendingGameOver = null;
            revivedThisRun = true;
            reviveTitansAt = game.TitansDefeated;
            reviveSecondsAt = session.Seconds;
            reviveFadeStart = Time.realtimeSinceStartup;
            banner = null;
            if (reviveOffer != null && reviveOffer.FallbackUsed) ShowToast(AdText.ReviveFallbackToast);
            SyncPause();
            // PlayEvents (in Update) plays Revived: the titan stumbles back, the sleeping cannons fly into the deck.
        }

        /// <summary>The revive was declined: the held GameOver reaches the session now, and the results follow.</summary>
        void FinishDeferredGameOver()
        {
            if (pendingGameOver == null) return;
            var e = pendingGameOver;
            pendingGameOver = null;
            var signal = session.Process(e);
            if ((signal & SessionSignal.RunEnded) != 0) RunReallyEnded();
            if ((signal & SessionSignal.Save) != 0) SaveProfile();
        }

        /// <summary>The run is over for good: counters, revive outcome, results timing, the ad-break notice and card.</summary>
        void RunReallyEnded()
        {
            telemetry.Track(Telemetry.Perf, FrameP95(), game.Level);
            governor.OnRunEnded(session.Seconds);
            LogSlot6TitanEnd();
            if (revivedThisRun)
                governor.LogEvent("revive_outcome", "t" + (game.TitansDefeated - reviveTitansAt) + "/best" + (session.NewBest ? 1 : 0), Mathf.RoundToInt(session.Seconds - reviveSecondsAt));
            resultsSince = Time.realtimeSinceStartup;
            adBreakEndedNotice = governor.TakeAdBreakEndNotice();
            if (breakOffer == null && governor.ShouldShowProactiveAdBreak(OfferCtx(), out _))
            {
                breakOffer = new RewardedOffer(Placement.AdBreak, governor, ads, free => governor.GrantAdBreak(), 0f, false, false, "proactive");
                governor.OnProactiveAdBreakShown();
            }
        }

        // ---------------------------------------------------------------- results panel (between runs)

        /// <summary>
        /// "Joacă din nou", "Reia ciclul" and "Joc nou" all come here: the single call site of the interstitial. The
        /// requested run starts on ANY ad result (or at once when the rules skip the ad).
        /// </summary>
        void LeaveResults(RunStart start)
        {
            if (!resultsInteractive || interGate.Current != InterstitialGate.Phase.Idle) return;
            // The same context the note was computed from: no "Reclamă înainte de rundă" on screen means no ad.
            var ctx = InterCtx(noteVisibleSeconds);
            interGate.TryShow(ctx, () =>
            {
                SyncPause();
                StartRun(start, true);
            });
        }

        float ResultsFooterHeight(float s) => 64 * s;

        /// <summary>Under the results buttons: the "Pauză de reclame" link or its countdown, and the end notice.</summary>
        void DrawResultsFooter(Rect r, float s)
        {
            var style = new GUIStyle(small) { fontSize = Mathf.RoundToInt(17 * s), fontStyle = FontStyle.Normal };
            style.normal.textColor = new Color(0.8f, 0.85f, 0.95f);
            float y = r.y;
            if (adBreakEndedNotice)
            {
                GUI.Label(new Rect(r.x, y, r.width, 24 * s), AdText.AdBreakEnded, style);
                y += 26 * s;
            }
            if (governor.AdBreakActive)
            {
                GUI.Label(new Rect(r.x, y, r.width, 26 * s), AdText.AdBreakRemaining(governor.AdBreakRemainingSeconds), style); // non-interactive
                return;
            }
            if (breakOffer != null || !governor.CanOffer(Placement.AdBreak, OfferCtx(), out _)) return;
            var link = new GUIStyle(style) { fontStyle = FontStyle.Bold };
            link.normal.textColor = new Color(0.65f, 0.85f, 1f);
            var linkRect = new Rect(r.center.x - 120 * s, y, 240 * s, 30 * s);
            if (GUI.Button(linkRect, AdText.AdBreakLink, link))
            {
                breakOffer = new RewardedOffer(Placement.AdBreak, governor, ads, free => governor.GrantAdBreak(), 0f, false, false, "link");
                SyncPause();
            }
            GUI.color = link.normal.textColor;
            GUI.DrawTexture(new Rect(linkRect.x + 20 * s, linkRect.yMax - 3 * s, linkRect.width - 40 * s, Mathf.Max(1f, s)), white);
            GUI.color = Color.white;
        }

        // ---------------------------------------------------------------- drawing

        /// <summary>Everything ad-related on top of the HUD: the wall button, the cards, the interstitial card, the fades.</summary>
        void DrawAdLayer(float s)
        {
            if (game == null) return;

            // Fade back in over 0.5 s after a revive.
            float fade = Time.realtimeSinceStartup - reviveFadeStart;
            if (fade >= 0f && fade < 0.5f) AdOfferCard.Dim(0.7f * (1f - fade / 0.5f));
            // The gold flash when the 6th slot opens.
            float flash = Time.realtimeSinceStartup - goldFlashStart;
            if (flash >= 0f && flash < 0.35f)
            {
                GUI.color = new Color(1f, 0.82f, 0.2f, 0.35f * (1f - flash / 0.35f));
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
                GUI.color = Color.white;
            }

            if (!game.IsOver && slotButton.Visible && slotOffer == null && !PlayFrozen) DrawWallButton(s);

            if (reviveOffer != null && reviveOffer.IsOpen) DrawReviveCard();
            if (slotOffer != null && slotOffer.IsOpen) DrawSlotCard();
            if (breakOffer != null && breakOffer.IsOpen) DrawBreakCard();
            if (interGate.Current == InterstitialGate.Phase.Card) AdOfferCard.DrawInterCard();
            else if (interGate.Current == InterstitialGate.Phase.Showing || ads.IsShowing) AdOfferCard.Dim();
        }

        /// <summary>The gold "+1 LOC" pill (110×110 dp) on the wall, right of slot 5, with a small "Reclamă" tag; one 0.3 s pulse.</summary>
        void DrawWallButton(float s)
        {
            float dp = Screen.dpi > 0f ? Screen.dpi / 160f : Screen.height / 800f;
            float size = Mathf.Min(110f * dp, Screen.width * 0.24f);
            var r = view.WallButtonRect(size);
#if UNITY_EDITOR
            var safe = Screen.safeArea;
            if (!wallButtonChecked && (r.xMin < safe.xMin || r.xMax > safe.xMax || view.OverlapsTray(r)))
            {
                wallButtonChecked = true;
                Debug.LogWarning("[Titan] '+1 LOC' button off screen or over the tray at " + Screen.width + "x" + Screen.height + ": " + r);
            }
#endif
            float t = Time.realtimeSinceStartup - slotButtonShownAt;
            if (t >= 0f && t < 0.3f && !Settings.ReducedFx)
            {
                float k = 1f + 0.12f * Mathf.Sin(Mathf.PI * t / 0.3f);
                r = new Rect(r.center.x - r.width * k / 2f, r.center.y - r.height * k / 2f, r.width * k, r.height * k);
            }
            GUI.color = new Color(0.45f, 0.3f, 0.02f, 0.9f);
            GUI.DrawTexture(new Rect(r.x - 3 * s, r.y - 3 * s, r.width + 6 * s, r.height + 6 * s), Glyphs.Dot);
            GUI.color = new Color(1f, 0.8f, 0.18f);
            GUI.DrawTexture(r, Glyphs.Dot);
            GUI.color = Color.white;
            var text = new GUIStyle(label) { fontSize = Mathf.RoundToInt(r.height * 0.24f), fontStyle = FontStyle.Bold };
            text.normal.textColor = new Color(0.25f, 0.15f, 0f);
            GUI.Label(new Rect(r.x, r.y + r.height * 0.18f, r.width, r.height * 0.5f), AdText.ExtraSlotButton, text);
            var tagStyle = new GUIStyle(small) { fontSize = Mathf.RoundToInt(r.height * 0.11f) };
            tagStyle.normal.textColor = new Color(0.25f, 0.15f, 0f);
            GUI.Label(new Rect(r.x, r.y + r.height * 0.62f, r.width, r.height * 0.16f), AdText.Tag, tagStyle);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                slotOffer = new RewardedOffer(Placement.ExtraSlot, governor, ads, GrantSlot, 0f, false, false);
                slotButton.OnUsed();
                SyncPause();
            }
        }

#if UNITY_EDITOR
        bool wallButtonChecked;
#endif

        void GrantSlot(bool withoutAd)
        {
            if (!game.GrantExtraSlot()) return;
            helpsUsedThisRun++;
            helpUsedThisTitan = true;
            fx.RecordFanfare();
            SyncPause();
        }

        void DrawReviveCard()
        {
            var o = reviveOffer;
            string status = o.Phase == OfferPhase.Loading ? "Se încarcă reclama" + new string('.', 1 + (int)(Time.realtimeSinceStartup * 3f) % 3)
                : o.Phase == OfferPhase.NoAd ? AdText.NoAdNow : null;
            int choice = AdOfferCard.Draw(AdText.ReviveTitle, AdText.ReviveValue, o.Free ? AdText.ReviveAcceptFree : AdText.ReviveAccept, AdText.Decline,
                AdText.ReviveFooter, o.AcceptEnabled, o.DeclineEnabled, status, !o.Free,
                AdText.ReviveStake(game.Level, game.ProgressFraction, game.Score));
            if (choice > 0) o.Accept();
            else if (choice < 0) o.Decline();
            if (choice != 0) CloseFinishedOffers();
        }

        void DrawSlotCard()
        {
            var o = slotOffer;
            int left = adConfig.inrun_help_max_per_run - helpsUsedThisRun;
            int choice = AdOfferCard.Draw(AdText.ExtraSlotTitle, AdText.ExtraSlotValue, o.Free ? AdText.ExtraSlotAcceptFree : AdText.ExtraSlotAccept,
                AdText.ExtraSlotDecline, AdText.ExtraSlotFooter(left), o.AcceptEnabled, o.DeclineEnabled, o.Phase == OfferPhase.Loading ? "Se încarcă reclama..." : null, !o.Free);
            if (choice > 0) o.Accept();
            else if (choice < 0) o.Decline();
            if (choice != 0) CloseFinishedOffers();
        }

        void DrawBreakCard()
        {
            var o = breakOffer;
            int choice = AdOfferCard.Draw(AdText.AdBreakTitle, AdText.AdBreakValue, AdText.AdBreakAccept, AdText.Decline, null,
                o.AcceptEnabled, o.DeclineEnabled, o.Phase == OfferPhase.Loading ? "Se încarcă reclama..." : null);
            if (choice > 0) o.Accept();
            else if (choice < 0) o.Decline();
            if (choice != 0) CloseFinishedOffers();
        }

        // ---------------------------------------------------------------- app lifecycle

        void OnAdsAppPause(bool paused)
        {
            if (governor == null) return;
            if (paused) governor.OnAppBackground();
            else governor.OnAppForeground(); // ≥ 1800 s away: a new session
        }
    }
}
