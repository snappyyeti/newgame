using Titan.Core;
using UnityEngine;

namespace Titan.Game
{
    /// <summary>
    /// The 4 FTUE hints on screen (FTUE-1, DESIGN.md 8). The rules (when, once per install, one at a time) are in
    /// Titan.Core.FtueHints; this only draws. Hints are textures and labels, never buttons, so they cannot take a tap
    /// (taps are read from Input in Update), and their text never covers the tray.
    /// </summary>
    public sealed partial class GameController
    {
        static readonly Color HintYellow = new Color(1f, 0.86f, 0.1f);

        void DrawHints(float s)
        {
            if (hints.Active == 0 || hints.Active == Ftue.H4GameOver) return;
            float pulse = Settings.ReducedFx ? 1f : 1f + 0.08f * Mathf.Sin(Time.time * Mathf.PI * 2f * 1.5f);
            float trayTop = view.TrayTopGui();
            switch (hints.Active)
            {
                case Ftue.H1TapYellow:
                {
                    var c = view.TrayHitCenter(0);
                    if (c.HasValue) Ring(c.Value, view.TrayHitRadius * 1.1f * pulse, s);
                    // Above the tray, never on it.
                    HintBox(hints.Text, trayTop - 10 * s, s);
                    break;
                }
                case Ftue.H2Zzz:
                {
                    var c = view.LineSlotCenter(hints.Slot);
                    if (c.HasValue) Ring(c.Value, 58 * s * pulse, s);
                    HintBox(hints.Text, Mathf.Min(Screen.height * 0.56f, trayTop - 10 * s), s);
                    break;
                }
                case Ftue.H3DontFill:
                {
                    var pip = view.SlotPip(Mathf.Min(2, game.ActiveSlots - 1));
                    float boxBottom = Mathf.Min(Screen.height * 0.56f, trayTop - 10 * s);
                    if (pip.HasValue)
                    {
                        float size = 40 * s;
                        float bob = Settings.ReducedFx ? 0f : 6 * s * Mathf.Sin(Time.time * Mathf.PI * 2f * 1.5f);
                        var r = new Rect(pip.Value.x - size / 2f, pip.Value.y - 18 * s - size + bob, size, size);
                        GUI.color = new Color(0, 0, 0, 0.55f);
                        GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, r.width, r.height), Glyphs.ArrowDown);
                        GUI.color = HintYellow;
                        GUI.DrawTexture(r, Glyphs.ArrowDown);
                        GUI.color = Color.white;
                        boxBottom = Mathf.Min(boxBottom, r.y - 8 * s);
                    }
                    HintBox(hints.Text, boxBottom, s);
                    break;
                }
            }
        }

        /// <summary>A pulsing yellow ring (a texture: it never takes a tap).</summary>
        void Ring(Vector2 center, float radius, float s)
        {
            // Glyphs.Ring's outer edge is at 0.44 of the texture.
            float size = radius / 0.44f * 2f;
            var r = new Rect(center.x - size / 2f, center.y - size / 2f, size, size);
            GUI.color = new Color(0, 0, 0, 0.45f);
            GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, r.width, r.height), Glyphs.Ring);
            GUI.color = HintYellow;
            GUI.DrawTexture(r, Glyphs.Ring);
            GUI.color = Color.white;
        }

        GUIStyle hintStyle;

        /// <summary>A dark box with the hint text whose bottom edge is at <paramref name="bottom"/> (GUI y).</summary>
        void HintBox(string text, float bottom, float s)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (hintStyle == null || hintStyle.fontSize != Mathf.RoundToInt(21 * s))
                hintStyle = new GUIStyle(label) { fontSize = Mathf.RoundToInt(21 * s), wordWrap = true, alignment = TextAnchor.MiddleCenter };
            float w = Screen.width * 0.86f;
            float h = Mathf.Max(48 * s, hintStyle.CalcHeight(new GUIContent(text), w - 24 * s) + 16 * s);
            var r = new Rect((Screen.width - w) / 2f, bottom - h, w, h);
            GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.82f);
            GUI.DrawTexture(r, white);
            GUI.color = HintYellow;
            GUI.DrawTexture(new Rect(r.x, r.y, 4 * s, r.height), white);
            GUI.color = Color.white;
            Shadowed(new Rect(r.x + 12 * s, r.y, r.width - 24 * s, r.height), text, hintStyle);
        }

        /// <summary>H4 on the results panel: the cause of the first game over in one line and the tip.</summary>
        void DrawGameOverTip(Rect r, float s)
        {
            GUI.color = new Color(0.25f, 0.2f, 0.02f, 0.85f);
            GUI.DrawTexture(r, white);
            GUI.color = HintYellow;
            GUI.DrawTexture(new Rect(r.x, r.y, 4 * s, r.height), white);
            GUI.color = Color.white;
            var style = new GUIStyle(small) { fontSize = Mathf.RoundToInt(17 * s), wordWrap = true, fontStyle = FontStyle.Normal };
            Shadowed(new Rect(r.x + 10 * s, r.y + 2 * s, r.width - 16 * s, r.height / 2f), hints.CauseLine, style);
            var tip = new GUIStyle(style) { fontStyle = FontStyle.Bold };
            tip.normal.textColor = HintYellow;
            Shadowed(new Rect(r.x + 10 * s, r.y + r.height / 2f, r.width - 16 * s, r.height / 2f - 2 * s), FtueText.H4Tip, tip);
        }
    }
}
