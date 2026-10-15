using Titan.Core;
using UnityEngine;

namespace Titan.Game
{
    /// <summary>
    /// Pause (UX-1, DESIGN.md 7): the top-right pause button (48×48 s), the automatic pause when the app goes to the
    /// background or loses focus, and the pause menu. While paused nothing ticks, the tray takes no taps,
    /// Time.timeScale = 0 and AudioListener.pause = true; only "Continuă" resumes (no countdown).
    ///
    /// Menu: Continuă · Sunet · Vibrații · Efecte reduse · Simboluri culori · Limba RO/EN · the "Fără reclame 30 min"
    /// row (ADS-1 fills it, from GameController.Ads.cs; never in a Titanul Zilei run) · Joc nou (with a confirmation;
    /// endless runs only, a daily run has its own "Închei"/"Ieși").
    /// </summary>
    public sealed partial class GameController
    {
        /// <summary>The pause menu is up: the run is frozen.</summary>
        bool paused;
        /// <summary>"Joc nou" asked for its confirmation.</summary>
        bool pauseConfirmNewGame;

        /// <summary>A run is on screen and playing: the pause button shows and the app lifecycle may pause it.</summary>
        bool CanPause => game != null && screen == UiScreen.Play && !game.IsOver && !paused && !quitConfirm && !AdPause.Active;

        void PauseRun()
        {
            if (!CanPause) return;
            paused = true;
            pauseConfirmNewGame = false;
            warp.Cancel(Time.unscaledTime); // the time scale is 1 again when the run resumes
            ApplyTimeScale();
        }

        void ResumeRun()
        {
            paused = false;
            pauseConfirmNewGame = false;
            ApplyTimeScale();
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus) PauseRun();
        }

        /// <summary>The pause button's rect: top-right inside the safe area, 48×48 s.</summary>
        Rect PauseButtonRect(float s)
        {
            float size = 48 * s;
            var safe = Screen.safeArea;
            float top = Screen.height - safe.yMax;
            return new Rect(safe.xMax - size - 6 * s, top + 8 * s, size, size);
        }

        void DrawPauseButton(float s)
        {
            if (!CanPause) return;
            var r = PauseButtonRect(s);
            if (GUI.Button(r, GUIContent.none, button)) PauseRun();
            float icon = r.width * 0.7f;
            GUI.DrawTexture(new Rect(r.center.x - icon / 2f, r.center.y - icon / 2f, icon, icon), Glyphs.Pause);
        }

        GUIStyle pauseRow, pauseTitle, pauseNote;

        void DrawPauseMenu(float w, float s)
        {
            if (!paused) return;
            if (pauseRow == null || pauseRow.fontSize != Mathf.RoundToInt(22 * s))
            {
                pauseRow = new GUIStyle(button) { fontSize = Mathf.RoundToInt(22 * s), wordWrap = true };
                pauseTitle = new GUIStyle(big) { fontSize = Mathf.RoundToInt(40 * s) };
                pauseNote = new GUIStyle(small) { fontSize = Mathf.RoundToInt(18 * s), wordWrap = true, fontStyle = FontStyle.Normal };
            }
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, w, Screen.height), white);
            GUI.color = Color.white;

            // While an offer card from this menu is open, the menu stays visible but takes no input.
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && !AdPause.Active;
            if (pauseConfirmNewGame) DrawNewGameConfirm(w, s);
            else DrawPauseItems(w, s);
            GUI.enabled = wasEnabled;
        }

        void DrawPauseItems(float w, float s)
        {
            bool endless = !session.IsDaily;
            float rowH = 58 * s, gap = 8 * s;
            float height = 70 * s + 96 * s + gap + 5 * (rowH + gap) + (endless ? 2 * (rowH + gap) : 0f) + 24 * s;
            var panel = new Rect(w * 0.06f, Mathf.Max(Screen.height - Screen.safeArea.yMax + 70 * s, (Screen.height - height) / 2f), w * 0.88f, height);
            GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
            GUI.DrawTexture(panel, white);
            GUI.color = Color.white;

            float y = panel.y + 10 * s;
            Shadowed(new Rect(panel.x, y, panel.width, 56 * s), Loc.T("pause.title"), pauseTitle);
            y += 60 * s;
            float bw = panel.width * 0.84f, x = panel.center.x - bw / 2f;
            if (GUI.Button(new Rect(x, y, bw, 96 * s), Loc.T("pause.continue"), button))
            {
                ResumeRun();
                return;
            }
            y += 96 * s + gap;
            if (GUI.Button(new Rect(x, y, bw, rowH), Loc.T("pause.sound", OnOff(Settings.Sfx)), pauseRow)) Settings.Sfx = !Settings.Sfx;
            y += rowH + gap;
            if (GUI.Button(new Rect(x, y, bw, rowH), Loc.T("pause.haptics", OnOff(Settings.Haptics)), pauseRow)) Settings.Haptics = !Settings.Haptics;
            y += rowH + gap;
            if (GUI.Button(new Rect(x, y, bw, rowH), Loc.T("pause.reducedFx", OnOff(Settings.ReducedFx)), pauseRow)) Settings.ReducedFx = !Settings.ReducedFx;
            y += rowH + gap;
            if (GUI.Button(new Rect(x, y, bw, rowH), Loc.T("pause.symbols", OnOff(Settings.ColorSymbols)), pauseRow)) Settings.ColorSymbols = !Settings.ColorSymbols;
            y += rowH + gap;
            if (GUI.Button(new Rect(x, y, bw, rowH), Loc.T("pause.language"), pauseRow)) Language.Toggle();
            y += rowH + gap;
            if (!endless) return;
            // The slot ADS-1 fills: "Fără reclame 30 min" (opt-in card), or its non-interactive countdown, or nothing.
            if (PauseMenuAdBreakRow(new Rect(x, y, bw, rowH), s)) y += rowH + gap;
            if (GUI.Button(new Rect(x, y, bw, rowH), Loc.T("menu.newGame"), pauseRow)) pauseConfirmNewGame = true;
        }

        static string OnOff(bool on) => on ? Loc.T("common.on") : Loc.T("common.off");

        void DrawNewGameConfirm(float w, float s)
        {
            var card = new Rect(w * 0.08f, Screen.height * 0.32f, w * 0.84f, 260 * s);
            GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.97f);
            GUI.DrawTexture(card, white);
            GUI.color = Color.white;
            Shadowed(new Rect(card.x + 12 * s, card.y + 14 * s, card.width - 24 * s, 70 * s), Loc.T("pause.newGameAsk"), new GUIStyle(center) { fontSize = Mathf.RoundToInt(26 * s) });
            Shadowed(new Rect(card.x + 12 * s, card.y + 86 * s, card.width - 24 * s, 56 * s), Loc.T("pause.newGameNote"), pauseNote);
            float bw = card.width * 0.42f;
            if (GUI.Button(new Rect(card.x + card.width * 0.05f, card.y + 160 * s, bw, 76 * s), Loc.T("pause.newGameYes"), pauseRow))
            {
                NewGameFromPause();
                return;
            }
            if (GUI.Button(new Rect(card.xMax - card.width * 0.05f - bw, card.y + 160 * s, bw, 76 * s), Loc.T("common.no"), pauseRow)) pauseConfirmNewGame = false;
        }

        /// <summary>
        /// "Joc nou" from the pause menu: the run ends here (it counts as a run and its score counts toward the record,
        /// <see cref="RunSession.Abandon"/>), the checkpoint stays, and a new run starts at titan 1 at once. No ad is
        /// shown on this path: the between-runs interstitial belongs to the results panel only.
        /// </summary>
        void NewGameFromPause()
        {
            telemetry.Track(Telemetry.Perf, FrameP95(), game.Level);
            var signal = session.Abandon();
            if ((signal & SessionSignal.Save) != 0) SaveProfile();
            paused = false;
            pauseConfirmNewGame = false;
            StartRun(RunStart.Fresh(NewSeed()), true);
        }
    }
}
