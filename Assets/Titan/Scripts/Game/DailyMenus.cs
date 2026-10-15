using System.Threading.Tasks;
using Titan.Core;
using UnityEngine;

namespace Titan.Game
{
    /// <summary>
    /// Titanul Zilei on screen (RET-3, DESIGN.md 9.4-9.5): the button with its status and the real countdown to local
    /// midnight, the daily panel (result squares, share text, streak, official / practice / resume), the controls of a
    /// daily run (intro line and "Înapoi" before the first tap, "Închei" with its confirmation, "Ieși" in practice) and
    /// the day clock. The rules are Titan.Core.DailyChallenge's. Ad-free by rule: nothing here may touch an ad API
    /// (DailyTests greps this file); the ad contexts get isDailyRanked from GameController.Ads.cs.
    /// </summary>
    public sealed partial class GameController
    {
        /// <summary>"Închei încercarea oficială?" is on screen (the run is frozen).</summary>
        bool quitConfirm;
        /// <summary>Where "Înapoi" on the daily panel goes.</summary>
        UiScreen dailyReturn = UiScreen.Menu;
        float copiedUntil = -1f;
        float dayCheckAt;
        /// <summary>The fairness validator runs on a worker thread (pure C#: no Unity API there).</summary>
        Task<DailyPick> pickTask;

        /// <summary>After the first runs (FTUE) the results and the menu offer the daily.</summary>
        bool DailyButtonVisible => profile.totalRuns >= Ftue.ScriptedRuns;

        /// <summary>Something waits on the daily at launch: today's attempt is open, or a streak message.</summary>
        bool DailyWaiting => !daily.OfficialFinished || daily.LaunchMessage != null;

        void StartDailyPick()
        {
            if (daily.PickCached || (pickTask != null && !pickTask.IsCompleted)) return;
            int day = daily.Today;
            pickTask = Task.Run(() => DailyValidator.PickSalt(day));
        }

        /// <summary>Once per frame: the background pick, and once per second the local day (midnight rolls the daily).</summary>
        void UpdateDailyClock()
        {
            if (pickTask != null && pickTask.IsCompleted)
            {
                if (pickTask.Status == TaskStatus.RanToCompletion && daily.ApplyPick(pickTask.Result)) SaveProfile();
                pickTask = null;
            }
            if (Time.unscaledTime < dayCheckAt) return;
            dayCheckAt = Time.unscaledTime + 1f;
            int today = DayClock.Today(DayClock.DayNumber(System.DateTime.Now), ref profile.maxSeenDay);
            if (today == daily.Today) return;
            // An official run on screen finishes first and is recorded under its own day.
            bool officialRunning = game != null && !game.IsOver && daily.OfficialRunOnScreen;
            if (daily.Roll(today, officialRunning))
            {
                SaveProfile();
                StartDailyPick();
            }
        }

        bool PickReady => daily.PickCached;

        /// <summary>The panel; "Înapoi" returns to the results of the run on screen, or to the menu.</summary>
        void OpenDailyPanel()
        {
            dailyReturn = game != null && game.IsOver && !session.IsDaily ? UiScreen.Play : UiScreen.Menu;
            hints.LeaveResults();
            fx.StopRunEffects();
            screen = UiScreen.Daily;
            StartDailyPick();
        }

        void StartDailyRun(bool official)
        {
            if (!PickReady)
            {
                // The worker thread did not finish (or failed): compute it here once.
                if (pickTask == null || pickTask.IsCompleted) daily.Pick();
                else return;
            }
            var start = official ? daily.OfficialStart() : daily.PracticeStart();
            SaveProfile(); // the cached salt
            StartRun(start);
        }

        /// <summary>The run on screen is left for the daily panel (backing out before the first tap, or leaving practice).</summary>
        void LeaveDailyRun()
        {
            if (daily.LeavePractice()) SaveProfile();
            daily.EndRun();
            fx.StopRunEffects();
            session = null;
            quitConfirm = false;
            dailyReturn = UiScreen.Menu;
            screen = UiScreen.Daily;
        }

        /// <summary>"Titanul Zilei" with its status ("neîncercat", "2/3", …) and the countdown to the next one.</summary>
        bool DailyButton(Rect r, float s)
        {
            bool clicked = GUI.Button(r, GUIContent.none, button);
            var title = new GUIStyle(label) { fontSize = Mathf.RoundToInt(26 * s), fontStyle = FontStyle.Bold };
            Shadowed(new Rect(r.x, r.y + 2 * s, r.width, r.height * 0.55f), DailyText.Title + " · " + DailyText.Status(profile.daily), title);
            var sub = new GUIStyle(small) { fontSize = Mathf.RoundToInt(15 * s), fontStyle = FontStyle.Normal };
            sub.normal.textColor = new Color(0.85f, 0.9f, 1f);
            Shadowed(new Rect(r.x, r.y + r.height * 0.52f, r.width, r.height * 0.42f), Loc.T("daily.nextIn", DailyText.UntilMidnight(System.DateTime.Now)), sub);
            return clicked;
        }

        string StreakLine()
        {
            var st = profile.streak;
            return DailyText.CurrentStreak(st.current) + " · " + DailyText.Shields(st.shields) + " · " + DailyText.BestStreak(st.best);
        }

        /// <summary>
        /// 3 drawn squares: green with a check = defeated, red with an X = the titan the attempt ended on, white outline =
        /// now, grey = not reached. The check and the X carry the state without color (UX-1).
        /// </summary>
        void DailySquares(Vector2 center, int defeated, int lostOn, float s, int current = 0, float size = 18f)
        {
            float d = size * s, gap = d * 0.5f;
            float x0 = center.x - (3 * d + 2 * gap) / 2f;
            for (int k = 1; k <= Daily.Titans; k++)
            {
                var r = new Rect(x0 + (k - 1) * (d + gap), center.y - d / 2f, d, d);
                int sq = DailyText.Square(k, defeated, lostOn);
                GUI.color = new Color(0, 0, 0, 0.5f);
                GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, r.width, r.height), white);
                GUI.color = sq == 1 ? new Color(0.3f, 0.82f, 0.35f) : sq == 2 ? new Color(0.92f, 0.25f, 0.2f) : new Color(0.62f, 0.62f, 0.66f);
                GUI.DrawTexture(r, white);
                if (sq != 0)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.95f);
                    GUI.DrawTexture(new Rect(r.x + d * 0.1f, r.y + d * 0.1f, d * 0.8f, d * 0.8f), sq == 1 ? Glyphs.Check : Glyphs.Ex);
                }
                if (k == current && sq == 0)
                {
                    float t = Mathf.Max(1f, 3 * s * size / 18f);
                    GUI.color = Color.white;
                    GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), white);
                    GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), white);
                    GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), white);
                    GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), white);
                }
            }
            GUI.color = Color.white;
        }

        /// <summary>
        /// The Titanul Zilei panel: from the menu or the results (<paramref name="afterRun"/> = false, full screen), or
        /// over the last frame of a daily run. Spoiler-free: squares, score, time, pieces; nothing about the board.
        /// </summary>
        void DrawDailyPanel(float w, float s, bool afterRun)
        {
            var d = profile.daily;
            bool finished = daily.OfficialFinished;
            float height = 660 * s;
            var panel = new Rect(w * 0.05f, Mathf.Max(Screen.height * 0.08f, (Screen.height - height) / 2f), w * 0.9f, height);
            if (!afterRun)
            {
                GUI.color = new Color(0.05f, 0.06f, 0.1f, 0.94f);
                GUI.DrawTexture(new Rect(0, 0, w, Screen.height), white);
            }
            GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.9f);
            GUI.DrawTexture(panel, white);
            GUI.color = Color.white;

            float y = panel.y + 14 * s;
            Shadowed(new Rect(panel.x, y, panel.width, 44 * s), DailyText.Title + " #" + daily.Today, new GUIStyle(big) { fontSize = Mathf.RoundToInt(36 * s) });
            y += 48 * s;
            var note = new GUIStyle(small) { fontSize = Mathf.RoundToInt(16 * s), wordWrap = true, fontStyle = FontStyle.Normal };
            note.normal.textColor = new Color(0.8f, 0.85f, 0.95f);
            Shadowed(new Rect(panel.x + 14 * s, y, panel.width - 28 * s, 44 * s), DailyText.Intro, note);
            y += 52 * s;

            var line = new GUIStyle(small) { fontSize = Mathf.RoundToInt(20 * s), wordWrap = true };
            if (finished)
            {
                DailySquares(new Vector2(panel.center.x, y + 22 * s), d.defeated, d.lostOn, s, 0, 40f);
                y += 54 * s;
                Shadowed(new Rect(panel.x, y, panel.width, 30 * s), DailyText.ResultLine(d.score, d.seconds, d.pieces), line);
                y += 34 * s;
                float cw = panel.width * 0.6f;
                if (GUI.Button(new Rect(panel.center.x - cw / 2f, y, cw, 50 * s), DailyText.CopyButton, new GUIStyle(button) { fontSize = Mathf.RoundToInt(22 * s) }))
                {
                    GUIUtility.systemCopyBuffer = daily.ShareText();
                    daily.OnShareTapped();
                    copiedUntil = Time.unscaledTime + 1.8f;
                }
                y += 54 * s;
                if (Time.unscaledTime < copiedUntil)
                {
                    var ok = new GUIStyle(note);
                    ok.normal.textColor = new Color(0.6f, 1f, 0.6f);
                    Shadowed(new Rect(panel.x, y, panel.width, 22 * s), DailyText.Copied, ok);
                }
                y += 24 * s;
            }
            else
            {
                Shadowed(new Rect(panel.x, y, panel.width, 30 * s), d.status == 1 ? Loc.T("daily.officialStatus", DailyText.Status(d)) : Loc.T("daily.officialUntried"), line);
                y += 34 * s;
                Shadowed(new Rect(panel.x + 14 * s, y, panel.width - 28 * s, 44 * s), Loc.T("daily.onlyFirst"), note);
                y += 112 * s;
            }

            if (afterRun && session.Start.Mode == Telemetry.ModeDailyPractice)
            {
                Shadowed(new Rect(panel.x, y, panel.width, 26 * s), Loc.T("daily.practiceAfter", DailyText.Practice, HudText.Num(game.Score), HudText.Num(d.bestPracticeScore)), note);
            }
            else if (d.bestPracticeScore > 0)
                Shadowed(new Rect(panel.x, y, panel.width, 26 * s), Loc.T("daily.practiceToday", DailyText.Practice, HudText.Num(d.bestPracticeScore)), note);
            y += 30 * s;

            // The streak lives here and on the menu only (never on the endless results).
            if (daily.LaunchMessage != null)
            {
                var msg = new GUIStyle(note);
                msg.normal.textColor = new Color(0.6f, 0.9f, 1f);
                Shadowed(new Rect(panel.x, y, panel.width, 24 * s), daily.LaunchMessage, msg);
                y += 26 * s;
            }
            Shadowed(new Rect(panel.x + 10 * s, y, panel.width - 20 * s, 24 * s), StreakLine(), note);
            y += 28 * s;
            Shadowed(new Rect(panel.x, y, panel.width, 24 * s), Loc.T("daily.nextDaily", DailyText.UntilMidnight(System.DateTime.Now)), note);
            y += 34 * s;

            float bw = panel.width * 0.7f;
            bool ready = PickReady || pickTask == null || pickTask.IsCompleted;
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && ready;
            string main = !ready ? Loc.T("daily.preparing") : finished ? DailyText.Practice : d.status == 1 ? DailyText.ResumeOfficial : Loc.T("daily.startOfficial");
            if (GUI.Button(new Rect(panel.center.x - bw / 2f, y, bw, 84 * s), main, new GUIStyle(button) { fontSize = Mathf.RoundToInt(26 * s), wordWrap = true }))
            {
                GUI.enabled = wasEnabled;
                StartDailyRun(!finished);
                return;
            }
            GUI.enabled = wasEnabled;
            y += 94 * s;
            var secondary = new GUIStyle(button) { fontSize = Mathf.RoundToInt(22 * s) };
            if (afterRun)
            {
                // Back to the endless game. No interstitial after a daily run (the gate sees isDailyRanked).
                if (GUI.Button(new Rect(panel.center.x - bw / 2f, y, bw, 60 * s), Loc.T("daily.normalGame"), secondary))
                {
                    LeaveResults(RunStart.Fresh(NewSeed()));
                    return;
                }
            }
            else if (GUI.Button(new Rect(panel.center.x - bw / 2f, y, bw, 60 * s), Loc.T("common.back"), secondary))
            {
                screen = dailyReturn == UiScreen.Play && game != null ? UiScreen.Play : UiScreen.Menu;
            }
        }

        /// <summary>
        /// During a daily run: before the first tap of the official attempt, the intro line and "Înapoi" (the attempt is
        /// not used yet); once it started, "Închei" with the confirmation; in practice, "Ieși".
        /// </summary>
        void DrawDailyRunControls(float w, float s)
        {
            float top = Screen.height - Screen.safeArea.yMax;
            var small2 = new GUIStyle(button) { fontSize = Mathf.RoundToInt(18 * s) };
            var corner = new Rect(Screen.safeArea.xMin + 10 * s, top + 12 * s, 96 * s, 44 * s);
            bool practice = session.Start.Mode == Telemetry.ModeDailyPractice;

            if (quitConfirm)
            {
                GUI.color = new Color(0, 0, 0, 0.6f);
                GUI.DrawTexture(new Rect(0, 0, w, Screen.height), white);
                GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
                var card = new Rect(w * 0.08f, Screen.height * 0.32f, w * 0.84f, 230 * s);
                GUI.DrawTexture(card, white);
                GUI.color = Color.white;
                Shadowed(new Rect(card.x, card.y + 16 * s, card.width, 40 * s), DailyText.QuitConfirm, new GUIStyle(center) { fontSize = Mathf.RoundToInt(26 * s) });
                var note = new GUIStyle(small) { fontSize = Mathf.RoundToInt(16 * s), wordWrap = true, fontStyle = FontStyle.Normal };
                Shadowed(new Rect(card.x + 12 * s, card.y + 60 * s, card.width - 24 * s, 44 * s), Loc.T("daily.quitNote"), note);
                float bw = card.width * 0.42f;
                if (GUI.Button(new Rect(card.x + card.width * 0.05f, card.y + 130 * s, bw, 70 * s), Loc.T("daily.quitYes"), small2))
                {
                    if (daily.QuitOfficial()) SaveProfile();
                    LeaveDailyRun();
                    return;
                }
                if (GUI.Button(new Rect(card.xMax - card.width * 0.05f - bw, card.y + 130 * s, bw, 70 * s), Loc.T("daily.quitNo"), small2)) quitConfirm = false;
                return;
            }

            if (!practice && daily.OfficialRunUntouched)
            {
                // Before the first tap: what the attempt is, and a way out that does not use it. Above the wall, off the tray.
                var box = new Rect(w * 0.07f, Screen.height * 0.40f, w * 0.86f, 112 * s);
                GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.82f);
                GUI.DrawTexture(box, white);
                GUI.color = Color.white;
                var note = new GUIStyle(small) { fontSize = Mathf.RoundToInt(17 * s), wordWrap = true, fontStyle = FontStyle.Normal };
                Shadowed(new Rect(box.x + 10 * s, box.y + 6 * s, box.width - 20 * s, 50 * s), DailyText.Intro, note);
                var hint = new GUIStyle(note);
                hint.normal.textColor = HintYellow;
                Shadowed(new Rect(box.x + 10 * s, box.y + 58 * s, box.width - 20 * s, 48 * s), Loc.T("daily.startsAtTap"), hint);
                if (GUI.Button(corner, Loc.T("common.back"), small2)) LeaveDailyRun();
                return;
            }
            if (practice)
            {
                if (GUI.Button(corner, Loc.T("daily.exit"), small2)) LeaveDailyRun();
                return;
            }
            if (GUI.Button(corner, Loc.T("daily.end"), small2)) quitConfirm = true;
        }
    }
}
