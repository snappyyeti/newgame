using Titan.Core;
using UnityEngine;

namespace Titan.Game
{
    /// <summary>
    /// The run menus (RET-1, RET-2): the game-over panel with the honest record line and the next goal, the free
    /// checkpoint buttons, the launch overlay and the checkpoint toast. Checkpoints are free: nothing in this file may
    /// touch an ad API (RetentionTests greps it).
    /// </summary>
    public sealed partial class GameController
    {
        /// <summary>
        /// Game over, top to bottom: reason, score, record line ("RECORD NOU!" / "Ai fost la X% din record!" from 85% /
        /// "Record: N"), "Titani · Stele · Bucăți rupte", best cut and max combo, one next-goal line, then the buttons
        /// "Joacă din nou" (primary, 60% wide), "Reia ciclul (titanul #n)" (only with a checkpoint) and "Joc nou".
        /// </summary>
        void DrawGameOver(float w, float s)
        {
            bool hasCheckpoint = profile.HasCheckpoint;
            float height = (hasCheckpoint ? 690 : 610) * s;
            var panel = new Rect(w * 0.05f, Mathf.Max(Screen.height * 0.16f, (Screen.height - height) / 2f), w * 0.9f, height);
            GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.88f);
            GUI.DrawTexture(panel, white);
            GUI.color = Color.white;

            float y = panel.y + 16 * s;
            string reason = game.Lost == LoseReason.WallReached ? "Titanul a spart zidul!"
                : game.Lost == LoseReason.Deadlock ? "BLOCAT! Niciun tun nu mai\nare în ce trage."
                : "Cele 5 locuri sunt pline\nși niciun tun nu are în ce trage!";
            var reasonStyle = new GUIStyle(center) { fontSize = Mathf.RoundToInt(26 * s) };
            Shadowed(new Rect(panel.x, y, panel.width, 66 * s), reason, reasonStyle);
            y += 70 * s;

            var scoreStyle = new GUIStyle(big) { fontSize = Mathf.RoundToInt(46 * s) };
            Shadowed(new Rect(panel.x, y, panel.width, 56 * s), HudText.Num(game.Score), scoreStyle);
            y += 58 * s;

            var recordStyle = new GUIStyle(center) { fontSize = Mathf.RoundToInt(26 * s) };
            bool newBest = session.NewBest;
            recordStyle.normal.textColor = newBest ? ArenaView.BannerGold : HudText.NearMissText(game.Score, session.BestAtStart) != null ? new Color(1f, 0.9f, 0.6f) : Color.white;
            Shadowed(new Rect(panel.x, y, panel.width, 36 * s), HudText.RecordLine(game.Score, session.BestAtStart, newBest), recordStyle);
            y += 40 * s;

            var line = new GUIStyle(small) { fontSize = Mathf.RoundToInt(19 * s), wordWrap = true };
            Shadowed(new Rect(panel.x + 10 * s, y, panel.width - 20 * s, 28 * s), HudText.RunStats(game.TitansDefeated, game.StarsEarned, game.PiecesCut), line);
            y += 30 * s;
            Shadowed(new Rect(panel.x + 10 * s, y, panel.width - 20 * s, 28 * s), "Cea mai bună tăietură: +" + HudText.Num(game.MaxPiecePoints), line);
            y += 28 * s;
            Shadowed(new Rect(panel.x + 10 * s, y, panel.width - 20 * s, 28 * s), "Combo maxim: x" + game.MaxChain, line);
            y += 36 * s;

            var goal = new GUIStyle(line);
            goal.normal.textColor = new Color(0.75f, 0.9f, 1f);
            Shadowed(new Rect(panel.x + 10 * s, y, panel.width - 20 * s, 28 * s), HudText.NextGoal(game.Level, profile.bestTitanN, false), goal);
            y += 44 * s;

            float bw = panel.width * 0.6f;
            if (GUI.Button(new Rect(panel.center.x - bw / 2f, y, bw, 100 * s), "Joacă din nou", button))
            {
                StartRun(session.PlayAgainStart(NewSeed()), true);
                return;
            }
            y += 110 * s;
            var secondary = new GUIStyle(button) { fontSize = Mathf.RoundToInt(24 * s) };
            if (hasCheckpoint)
            {
                if (GUI.Button(new Rect(panel.center.x - bw / 2f, y, bw, 66 * s), "Reia ciclul (titanul #" + profile.checkpoint.n + ")", secondary))
                {
                    StartRun(RunSession.ResumeStart(profile).Value, true);
                    return;
                }
                y += 76 * s;
            }
            if (GUI.Button(new Rect(panel.center.x - bw / 2f, y, bw, 66 * s), "Joc nou", secondary))
            {
                var start = RunSession.NewGameStart(profile, NewSeed());
                SaveProfile(); // the checkpoint is gone
                StartRun(start, true);
            }
        }

        /// <summary>App launch after 3+ runs with a checkpoint: "Continuă (titanul #n)" (primary, focused) or "Joc nou".</summary>
        void DrawLaunchOverlay(float w, float s)
        {
            GUI.color = new Color(0.05f, 0.06f, 0.1f, 0.9f);
            GUI.DrawTexture(new Rect(0, 0, w, Screen.height), white);
            GUI.color = Color.white;
            var cp = profile.checkpoint;
            float y = Screen.height * 0.28f;
            Shadowed(new Rect(0, y, w, 60 * s), "Titanul din Cuburi", new GUIStyle(big) { fontSize = Mathf.RoundToInt(44 * s) });
            y += 80 * s;
            var note = new GUIStyle(small) { fontSize = Mathf.RoundToInt(19 * s), wordWrap = true };
            note.normal.textColor = new Color(0.8f, 0.85f, 0.95f);
            Shadowed(new Rect(w * 0.1f, y, w * 0.8f, 52 * s), "Punct de salvare: același titan, cu scorul de atunci (" + HudText.Num(cp.score) + ").", note);
            y += 70 * s;
            float bw = w * 0.7f;
            GUI.SetNextControlName("continue");
            bool go = GUI.Button(new Rect((w - bw) / 2f, y, bw, 100 * s), "Continuă (titanul #" + cp.n + ")", button);
            GUI.FocusControl("continue");
            var e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Space))
            {
                go = true;
                e.Use();
            }
            if (go)
            {
                StartRun(RunStart.FromCheckpoint(cp));
                return;
            }
            y += 116 * s;
            if (GUI.Button(new Rect((w - bw) / 2f, y, bw, 70 * s), "Joc nou", new GUIStyle(button) { fontSize = Mathf.RoundToInt(26 * s) }))
            {
                var start = RunSession.NewGameStart(profile, NewSeed());
                SaveProfile();
                StartRun(start);
            }
        }

        /// <summary>A small dark chip with one line (the checkpoint toast). It never covers the tray.</summary>
        void Toast(string text, float y, float s)
        {
            var style = new GUIStyle(small) { fontSize = Mathf.RoundToInt(18 * s) };
            float width = style.CalcSize(new GUIContent(text)).x + 30 * s;
            var r = new Rect((Screen.width - width) / 2f, y, width, 34 * s);
            GUI.color = new Color(0.1f, 0.12f, 0.2f, 0.85f);
            GUI.DrawTexture(r, white);
            GUI.color = new Color(0.6f, 0.9f, 1f);
            GUI.DrawTexture(new Rect(r.x, r.y, 4 * s, r.height), white);
            GUI.color = Color.white;
            Shadowed(r, text, style);
        }
    }
}
