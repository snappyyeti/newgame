using Titan.Core.Ads;
using UnityEngine;

namespace Titan.Game
{
    /// <summary>
    /// The pause contract (DESIGN.md 10.1): while an offer card or an ad is on screen the game does not advance, the
    /// tray takes no taps, Time.timeScale = 0, AudioListener.pause = true, and the arena is darkened 70%. Everything
    /// is restored when the last card or ad closes (from the ad callback, and checked again every frame).
    /// </summary>
    public static class AdPause
    {
        static bool active;
        static float savedTimeScale = 1f;
        static bool savedAudioPause;

        public static bool Active => active;

        /// <summary>Enters or leaves the pause. Idempotent: call it every frame and from every callback.</summary>
        public static void Apply(bool want)
        {
            if (want == active) return;
            active = want;
            if (want)
            {
                savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                savedAudioPause = AudioListener.pause;
                Time.timeScale = 0f;
                AudioListener.pause = true;
            }
            else
            {
                Time.timeScale = savedTimeScale;
                AudioListener.pause = savedAudioPause;
            }
        }
    }

    /// <summary>PlayerPrefs behind the ad state ("ad_*" keys), apart from the profile.</summary>
    public sealed class PlayerPrefsAdStore : IStore
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public string GetString(string key) => PlayerPrefs.GetString(key, null);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void Flush() => PlayerPrefs.Save();
    }

    /// <summary>
    /// The shared offer card (DESIGN.md 10.1): a small "Reclamă" tag top-left, a title, one line that says exactly what
    /// you get, a drawn play icon, and two buttons of IDENTICAL size and style side by side (accept left, decline
    /// right). No timers, no pre-checked boxes, no X that means "accept". Returns 1 for accept, -1 for decline, 0.
    /// </summary>
    public static class AdOfferCard
    {
        static Texture2D white, play;
        static GUIStyle tag, title, value, button, footer, status;
        static float styleScale;

        static void Ensure(float s)
        {
            if (white == null)
            {
                white = new Texture2D(1, 1);
                white.SetPixel(0, 0, Color.white);
                white.Apply();
            }
            if (play == null) play = PlayIcon();
            if (title != null && Mathf.Approximately(s, styleScale)) return;
            styleScale = s;
            tag = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(14 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            tag.normal.textColor = new Color(0.1f, 0.1f, 0.12f);
            title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(34 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            title.normal.textColor = new Color(1f, 0.85f, 0.3f);
            value = new GUIStyle(title) { fontSize = Mathf.RoundToInt(21 * s), fontStyle = FontStyle.Normal };
            value.normal.textColor = Color.white;
            footer = new GUIStyle(value) { fontSize = Mathf.RoundToInt(16 * s) };
            footer.normal.textColor = new Color(0.75f, 0.8f, 0.9f);
            status = new GUIStyle(value) { fontSize = Mathf.RoundToInt(18 * s), fontStyle = FontStyle.Bold };
            status.normal.textColor = new Color(1f, 0.7f, 0.5f);
            button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(22 * s), fontStyle = FontStyle.Bold, wordWrap = true };
        }

        /// <summary>A right-pointing triangle in a ring: the "play an ad" icon, drawn procedurally.</summary>
        static Texture2D PlayIcon()
        {
            const int n = 48;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "play-icon", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n - 0.5f;
                    float d = Mathf.Sqrt(u * u + v * v);
                    bool ring = d < 0.48f && d > 0.40f;
                    bool tri = u > -0.15f && u < 0.22f && Mathf.Abs(v) < (0.22f - u) * 0.62f;
                    px[y * n + x] = new Color(1f, 1f, 1f, ring || tri ? 1f : 0f);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        /// <summary>The 70% dark overlay of the pause contract (also the base of every card).</summary>
        public static void Dim(float alpha = 0.7f)
        {
            Ensure(Screen.height / 1000f);
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
            GUI.color = Color.white;
        }

        /// <param name="statusLine">Under the value line: a spinner text or "Nicio reclamă disponibilă acum" (null: none).</param>
        /// <param name="stakeText">Optional line under the title: what you keep ("Titanul #5 · 62% distrus · Scor 31.400").</param>
        public static int Draw(string titleText, string valueText, string acceptText, string declineText, string footerText,
            bool acceptEnabled, bool declineEnabled, string statusLine, bool showsAd = true, string stakeText = null)
        {
            float s = Screen.height / 1000f;
            Ensure(s);
            Dim();
            float w = Screen.width;
            float height = (stakeText != null ? 440 : 410) * s;
            var panel = new Rect(w * 0.06f, Mathf.Max(Screen.height * 0.22f, (Screen.height - height) / 2f), w * 0.88f, height);
            GUI.color = new Color(0.10f, 0.11f, 0.17f, 0.97f);
            GUI.DrawTexture(panel, white);
            GUI.color = new Color(1f, 0.82f, 0.2f);
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 4 * s), white);

            // The small "Reclamă" tag, top-left (also on a free first use: the card belongs to an ad placement).
            var tagRect = new Rect(panel.x + 10 * s, panel.y + 12 * s, 80 * s, 22 * s);
            GUI.color = new Color(0.85f, 0.85f, 0.88f);
            GUI.DrawTexture(tagRect, white);
            GUI.color = Color.white;
            GUI.Label(tagRect, AdText.Tag, tag);

            float y = panel.y + 40 * s;
            GUI.Label(new Rect(panel.x + 10 * s, y, panel.width - 20 * s, 50 * s), titleText, title);
            y += 56 * s;
            if (stakeText != null)
            {
                var stake = new GUIStyle(value) { fontStyle = FontStyle.Bold };
                GUI.Label(new Rect(panel.x + 10 * s, y, panel.width - 20 * s, 28 * s), stakeText, stake);
                y += 32 * s;
            }
            GUI.Label(new Rect(panel.x + 20 * s, y, panel.width - 40 * s, 92 * s), valueText, value);
            y += 96 * s;
            if (!string.IsNullOrEmpty(statusLine))
                GUI.Label(new Rect(panel.x + 10 * s, y, panel.width - 20 * s, 30 * s), statusLine, status);
            y += 36 * s;

            // Two buttons of identical size and style. The play icon sits inside the accept button when it shows an ad.
            OfferCardLayout.Buttons(panel.x + 16 * s, y, panel.width - 32 * s, 96 * s, 14 * s, out var a, out var d);
            var accept = new Rect(a.X, a.Y, a.W, a.H);
            var decline = new Rect(d.X, d.Y, d.W, d.H);
            Debug.Assert(accept.size == decline.size, "AdOfferCard: accept and decline must have identical sizes");
            int result = 0;
            bool old = GUI.enabled;
            GUI.enabled = old && acceptEnabled;
            if (GUI.Button(accept, showsAd ? "     " + acceptText : acceptText, button)) result = 1;
            if (showsAd)
            {
                GUI.color = GUI.enabled ? Color.white : new Color(1, 1, 1, 0.4f);
                float icon = 28 * s;
                GUI.DrawTexture(new Rect(accept.x + 10 * s, accept.center.y - icon / 2f, icon, icon), play);
                GUI.color = Color.white;
            }
            GUI.enabled = old && declineEnabled;
            if (GUI.Button(decline, declineText, button)) result = -1;
            GUI.enabled = old;
            y += 104 * s;
            if (!string.IsNullOrEmpty(footerText))
                GUI.Label(new Rect(panel.x + 10 * s, y, panel.width - 20 * s, 26 * s), footerText, footer);
            return result;
        }

        /// <summary>The 0.8 s card before an interstitial: "Reclamă — Mulțumim că susții jocul gratuit."</summary>
        public static void DrawInterCard()
        {
            float s = Screen.height / 1000f;
            Ensure(s);
            Dim(0.85f);
            GUI.Label(new Rect(Screen.width * 0.08f, Screen.height * 0.42f, Screen.width * 0.84f, 120 * s), AdText.InterCard, value);
        }
    }
}
