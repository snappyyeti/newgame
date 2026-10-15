using System.Collections.Generic;
using Titan.Core;
using UnityEngine;

namespace Titan.Game
{
    /// <summary>
    /// The cut-scoring HUD (CORE-5, DESIGN.md 6.2-6.3): the one big piece popup, the draining chain arc next to the
    /// score, the live 3-dot star indicator, the stars on the defeat banner, and the record confetti (RET-2).
    /// </summary>
    public sealed partial class GameController
    {
        /// <summary>
        /// The piece popup: "DECAPITARE!" / "TĂIETURĂ CURATĂ!" / "BUCATĂ RUPTĂ!", then "COMBO xN" (chain ≥ 2), then
        /// "+points". Only one is on screen; a newer one waits at most 0.3 s (when the current one is about to end),
        /// otherwise it merges into the current one (best label, highest chain, summed points).
        /// </summary>
        sealed class BigPopup
        {
            public Vector3 World;
            /// <summary>0 not clean, 1 clean, 2 clean head (the label shown is the best one merged in).</summary>
            public int Rank;
            public int Chain, Points, Mult;
            public float Born;
        }

        const float BigPopupLife = 1.1f, BigPopupMaxWait = 0.3f;
        BigPopup bigPopup, queuedPopup;
        /// <summary>Popups are pooled (FEEL-1): at most a few exist, reused piece after piece.</summary>
        readonly Stack<BigPopup> popupPool = new Stack<BigPopup>();

        void RecyclePopup(BigPopup p)
        {
            if (p != null && !popupPool.Contains(p)) popupPool.Push(p);
        }

        void OnPiecePopup(Vector3 world, GameEvent e)
        {
            PieceJuice(e); // FEEL-1: hitstop, FOV punch, slow-mo, haptics at the moment the piece breaks off on screen
            var p = popupPool.Count > 0 ? popupPool.Pop() : new BigPopup();
            p.World = world;
            p.Rank = !e.Clean ? 0 : e.Part == Monster.Head ? 2 : 1;
            p.Chain = e.Chain;
            p.Points = e.Points;
            p.Mult = e.Mult;
            p.Born = Time.time;
            if (bigPopup == null || Time.time - bigPopup.Born >= BigPopupLife)
            {
                RecyclePopup(bigPopup);
                bigPopup = p;
                return;
            }
            float left = bigPopup.Born + BigPopupLife - Time.time;
            if (queuedPopup == null && left <= BigPopupMaxWait)
            {
                queuedPopup = p;
                return;
            }
            var into = queuedPopup ?? bigPopup;
            into.Rank = Mathf.Max(into.Rank, p.Rank);
            into.Chain = Mathf.Max(into.Chain, p.Chain);
            into.Mult = Mathf.Max(into.Mult, p.Mult);
            into.Points += p.Points;
            into.World = p.World;
            if (into == bigPopup) into.Born = Mathf.Max(into.Born, Time.time - 0.15f); // stays up, without rising again from the start
            RecyclePopup(p);
        }

        void DrawBigPopup(float s)
        {
            if (bigPopup != null && Time.time - bigPopup.Born >= BigPopupLife)
            {
                RecyclePopup(bigPopup);
                bigPopup = queuedPopup;
                queuedPopup = null;
                if (bigPopup != null) bigPopup.Born = Time.time;
            }
            if (bigPopup == null) return;
            var p = bigPopup;
            float age = Time.time - p.Born;
            var sp = cam.WorldToScreenPoint(p.World);
            if (sp.z <= 0) return;
            string text = HudText.PieceLabel(p.Rank > 0, p.Rank == 2 ? Monster.Head : Monster.LeftArm);
            if (p.Chain >= 2) text += "\n" + Loc.T("hud.combo", p.Chain);
            text += "\n+" + HudText.Num(p.Points);
            float scale = HudText.PopupScale(p.Chain);
            float pop = age < 0.12f ? Mathf.Lerp(0.6f, 1.1f, age / 0.12f) : age < 0.2f ? Mathf.Lerp(1.1f, 1f, (age - 0.12f) / 0.08f) : 1f;
            var style = new GUIStyle(center) { fontSize = Mathf.RoundToInt(30 * s * scale * pop), wordWrap = false };
            style.normal.textColor = p.Mult >= 4 ? ArenaView.BannerGold : Color.white;
            float alpha = age > BigPopupLife - 0.25f ? Mathf.Clamp01((BigPopupLife - age) / 0.25f) : 1f;
            style.normal.textColor *= new Color(1f, 1f, 1f, alpha);
            float rise = Mathf.Min(age, 0.6f) * 50f;
            float height = 3.4f * 30 * s * scale;
            // Never wider than the screen (a x4/x5 popup at scale 1.45-1.6 was): center on the piece, keep a margin,
            // and shrink the font when the widest line still does not fit.
            float margin = 8f * s;
            float width = Mathf.Min(440 * s * scale, Mathf.Max(1f, Screen.width - 2f * margin));
            var r = new Rect(sp.x - width / 2f, Screen.height - sp.y - height - rise * s, width, height);
            r.x = Mathf.Clamp(r.x, margin, Mathf.Max(margin, Screen.width - margin - width));
            float textWidth = style.CalcSize(new GUIContent(text)).x;
            if (textWidth > width) style.fontSize = Mathf.Max(1, Mathf.FloorToInt(style.fontSize * width / textWidth));
            Shadowed(r, text, style);
        }

        /// <summary>The chain window (4 s) as a ring of dots draining clockwise, with "xN" inside, next to the score.</summary>
        void ChainArc(Vector2 c, float s)
        {
            float left = game.ChainTimeLeft;
            if (game.IsOver || left <= 0f || game.Chain < 1) return;
            const int dots = 20;
            float radius = 24 * s, dot = 7 * s;
            int lit = Mathf.CeilToInt(dots * left / cfg.ChainWindowSeconds);
            for (int i = 0; i < dots; i++)
            {
                float a = Mathf.PI * 2f * i / dots;
                var p = c + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * radius;
                GUI.color = i < lit ? (game.Chain >= 4 ? ArenaView.BannerGold : Color.white) : new Color(1f, 1f, 1f, 0.15f);
                GUI.DrawTexture(new Rect(p.x - dot / 2f, p.y - dot / 2f, dot, dot), Glyphs.Dot);
            }
            GUI.color = Color.white;
            Shadowed(new Rect(c.x - radius, c.y - 12 * s, 2 * radius, 24 * s), Loc.T("hud.chainX", Mathf.Min(game.Chain, cfg.ChainMaxMult)), new GUIStyle(small) { fontSize = Mathf.RoundToInt(16 * s) });
        }

        /// <summary>
        /// Live grade of the current titan, under the danger bar: dot 1 = the defeat (open until it falls), dot 2 = gold
        /// once a clean head piece fell, dot 3 = gold ring while the wall never held more than CleanLineMax cannons,
        /// grey from the moment it did.
        /// </summary>
        void LiveStars(Vector2 topLeft, float s)
        {
            float d = 14 * s, gap = 6 * s;
            var gold = ArenaView.BannerGold;
            for (int i = 0; i < 3; i++)
            {
                var r = new Rect(topLeft.x + i * (d + gap), topLeft.y, d, d);
                Texture2D tex;
                Color color;
                if (i == 0)
                {
                    tex = Glyphs.Ring;
                    color = Color.white;
                }
                else if (i == 1)
                {
                    tex = game.DecapitatedThisTitan ? Glyphs.Dot : Glyphs.Ring;
                    color = game.DecapitatedThisTitan ? gold : Color.white;
                }
                else
                {
                    tex = game.CleanLineAlive ? Glyphs.Ring : Glyphs.Dot;
                    color = game.CleanLineAlive ? gold : new Color(0.45f, 0.45f, 0.45f);
                }
                GUI.color = new Color(0, 0, 0, 0.5f);
                GUI.DrawTexture(new Rect(r.x + 1, r.y + 1, r.width, r.height), tex);
                GUI.color = color;
                GUI.DrawTexture(r, tex);
            }
            GUI.color = Color.white;
        }

        /// <summary>The defeat banner's 3 stars, popping in 0.15 s apart; a missing star is grey with its one-line reason.</summary>
        void BannerStars(Vector2 topCenter, float s)
        {
            float size = 44 * s, gap = 14 * s;
            float x0 = topCenter.x - (3 * size + 2 * gap) / 2f;
            float age = Time.time - bannerStart;
            var reasons = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                int bit = 1 << i;
                bool earned = (bannerStars & bit) != 0;
                if (!earned)
                {
                    var why = HudText.MissingStarReason(bit, bannerMaxLine);
                    if (why != null) reasons.Add(why);
                }
                float k = (age - StarFirst - StarGap * i) / 0.15f;
                if (k <= 0f) continue;
                float scale = k < 1f ? Mathf.Lerp(0.2f, 1.2f, k) : Mathf.Lerp(1.2f, 1f, Mathf.Clamp01(k - 1f));
                var r = new Rect(x0 + i * (size + gap) + size * (1f - scale) / 2f, topCenter.y + size * (1f - scale) / 2f, size * scale, size * scale);
                GUI.color = new Color(0, 0, 0, 0.5f);
                GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, r.width, r.height), Glyphs.StarGlyph);
                GUI.color = earned ? ArenaView.BannerGold : new Color(0.5f, 0.5f, 0.5f, 0.9f);
                GUI.DrawTexture(r, Glyphs.StarGlyph);
            }
            GUI.color = Color.white;
            if (age < StarFirst + StarGap * 3f) return;
            var style = new GUIStyle(small) { fontSize = Mathf.RoundToInt(16 * s) };
            style.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            for (int i = 0; i < reasons.Count; i++)
                Shadowed(new Rect(0, topCenter.y + size + 6 * s + i * 22 * s, Screen.width, 22 * s), reasons[i], style);
        }

        // ---------------------------------------------------------------- record confetti (RET-2)

        struct Confetto
        {
            public Vector2 Pos, Vel;
            public Color Color;
            public float Life, Size, Spin;
        }

        readonly List<Confetto> confetti = new List<Confetto>();

        /// <summary>40 pieces of confetti burst from the score (screen space, IMGUI).</summary>
        void Confetti()
        {
            float s = Screen.height / 1000f;
            float top = Screen.height - Screen.safeArea.yMax;
            var origin = new Vector2(Screen.width / 2f, top + 45 * s);
            for (int i = 0; i < 40; i++)
            {
                float a = Random.Range(-Mathf.PI * 0.9f, -Mathf.PI * 0.1f); // upward fan (GUI y points down)
                float speed = Random.Range(250f, 650f) * s;
                confetti.Add(new Confetto
                {
                    Pos = origin,
                    Vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed,
                    Color = ArenaView.Palette[i % ArenaView.Palette.Length],
                    Life = Random.Range(1.0f, 1.6f),
                    Size = Random.Range(6f, 11f) * s,
                    Spin = Random.Range(0f, 1f),
                });
            }
        }

        void UpdateConfetti(float dt)
        {
            float g = 900f * Screen.height / 1000f;
            for (int i = confetti.Count - 1; i >= 0; i--)
            {
                var c = confetti[i];
                c.Life -= dt;
                if (c.Life <= 0f)
                {
                    confetti.RemoveAt(i);
                    continue;
                }
                c.Vel.y += g * dt;
                c.Vel *= 1f - 0.8f * dt;
                c.Pos += c.Vel * dt;
                c.Spin += dt * 3f;
                confetti[i] = c;
            }
        }

        void DrawConfetti(float s)
        {
            foreach (var c in confetti)
            {
                float flip = Mathf.Abs(Mathf.Sin(c.Spin * Mathf.PI));
                GUI.color = new Color(c.Color.r, c.Color.g, c.Color.b, Mathf.Clamp01(c.Life / 0.4f));
                GUI.DrawTexture(new Rect(c.Pos.x - c.Size / 2f, c.Pos.y - c.Size * flip / 2f, c.Size, Mathf.Max(1f, c.Size * flip)), white);
            }
            GUI.color = Color.white;
        }
    }
}
