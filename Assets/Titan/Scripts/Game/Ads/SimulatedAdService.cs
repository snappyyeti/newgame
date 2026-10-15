using System;
using Titan.Core.Ads;
using UnityEngine;

namespace Titan.Game
{
    /// <summary>
    /// PLACEHOLDER FOR LEVELPLAY (DESIGN.md 10.1, 15). A simulated ad network for playtests: a full-screen IMGUI panel
    /// that says "RECLAMĂ SIMULATĂ" and "Înlocuitor pentru LevelPlay". No SDK, no network, no package. The real
    /// LevelPlay adapter implements the same <see cref="IAdService"/> later, on a separate branch.
    ///
    /// Rewarded: 6 s, the close button visible from the start; closing early asks "Dacă închizi acum nu primești
    /// recompensa." [Continuă reclama] [Închide] (Închide = ClosedEarly). Interstitial: 4 s, close after 2 s.
    /// Fill rate 0.9 per load. <see cref="ForceFail"/> makes every load and show fail (to test that no flow sticks).
    /// Runs on unscaled time: the pause contract sets Time.timeScale = 0 while an ad is up.
    /// </summary>
    public sealed class SimulatedAdService : MonoBehaviour, IAdService
    {
        /// <summary>Every load fails and every show returns Failed (editor / QA switch).</summary>
        public static bool ForceFail;

        AdConfig cfg;
        System.Random rng;
        bool rewardedLoaded, interLoaded;
        float rewardedReadyAt, interReadyAt;
        const float LoadSeconds = 0.4f;

        enum Kind { None, Rewarded, Interstitial }
        Kind showing;
        float shownFor;
        bool confirmClose;
        /// <summary>The callback of the ad on screen.</summary>
        Action<AdResult> pending;
        /// <summary>Results waiting for the next Update (callbacks never run inside the caller's own call).</summary>
        readonly System.Collections.Generic.Queue<(Action<AdResult> cb, AdResult result)> results = new System.Collections.Generic.Queue<(Action<AdResult>, AdResult)>();
        bool personalized, childDirected = true;

        public static SimulatedAdService Create(AdConfig config)
        {
            var go = new GameObject("SimulatedAdService (placeholder for LevelPlay)");
            DontDestroyOnLoad(go);
            var s = go.AddComponent<SimulatedAdService>();
            s.cfg = config;
            s.rng = new System.Random(Environment.TickCount);
            s.Preload();
            return s;
        }

        /// <summary>An ad is on screen (the pause contract holds).</summary>
        public bool IsShowing => showing != Kind.None;
        public bool Personalized => personalized;
        public bool ChildDirected => childDirected;

        public bool IsRewardedReady => !ForceFail && showing == Kind.None && rewardedLoaded && Time.realtimeSinceStartup >= rewardedReadyAt;
        public bool IsInterstitialReady => !ForceFail && showing == Kind.None && interLoaded && Time.realtimeSinceStartup >= interReadyAt;

        public void Preload()
        {
            if (ForceFail) return;
            float now = Time.realtimeSinceStartup;
            if (!rewardedLoaded && rng.NextDouble() < cfg.sim_fill_rate)
            {
                rewardedLoaded = true;
                rewardedReadyAt = now + LoadSeconds;
            }
            if (!interLoaded && rng.NextDouble() < cfg.sim_fill_rate)
            {
                interLoaded = true;
                interReadyAt = now + LoadSeconds;
            }
        }

        public void ShowRewarded(Placement placement, Action<AdResult> onDone) => Show(Kind.Rewarded, onDone);

        public void ShowInterstitial(Placement placement, Action<AdResult> onDone) => Show(Kind.Interstitial, onDone);

        /// <summary>Until the age gate exists: SetPrivacy(false, true). The simulated network only remembers it.</summary>
        public void SetPrivacy(bool personalized, bool childDirected)
        {
            this.personalized = personalized;
            this.childDirected = childDirected;
        }

        void Show(Kind kind, Action<AdResult> onDone)
        {
            if (ForceFail)
            {
                results.Enqueue((onDone, AdResult.Failed));
                return;
            }
            bool ready = kind == Kind.Rewarded ? IsRewardedReady : IsInterstitialReady;
            if (!ready)
            {
                results.Enqueue((onDone, AdResult.NotReady));
                return;
            }
            if (kind == Kind.Rewarded) rewardedLoaded = false;
            else interLoaded = false;
            showing = kind;
            shownFor = 0f;
            confirmClose = false;
            pending = onDone;
        }

        /// <summary>The ad on screen ends; its callback runs in the next Update, on the main thread.</summary>
        void Finish(AdResult result)
        {
            results.Enqueue((pending, result));
            pending = null;
            showing = Kind.None;
            confirmClose = false;
        }

        void Update()
        {
            if (showing != Kind.None && !confirmClose)
            {
                shownFor += Time.unscaledDeltaTime;
                float length = showing == Kind.Rewarded ? cfg.sim_rewarded_seconds : cfg.sim_inter_seconds;
                if (shownFor >= length) Finish(AdResult.Completed);
            }
            if (results.Count == 0) return;
            var (cb, result) = results.Dequeue();
            Preload();
            cb?.Invoke(result);
        }

        GUIStyle title, note, button;
        Texture2D white;

        void OnGUI()
        {
            if (showing == Kind.None) return;
            GUI.depth = -1000; // above the game's HUD and cards
            float s = Screen.height / 1000f;
            if (white == null)
            {
                white = new Texture2D(1, 1);
                white.SetPixel(0, 0, Color.white);
                white.Apply();
            }
            if (title == null || title.fontSize != Mathf.RoundToInt(44 * s))
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(44 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                title.normal.textColor = Color.white;
                note = new GUIStyle(title) { fontSize = Mathf.RoundToInt(20 * s), fontStyle = FontStyle.Normal };
                note.normal.textColor = new Color(0.8f, 0.85f, 0.95f);
                button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(24 * s), fontStyle = FontStyle.Bold };
            }
            float w = Screen.width, h = Screen.height;
            GUI.color = new Color(0.06f, 0.07f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(0, 0, w, h), white);
            GUI.color = Color.white;

            GUI.Label(new Rect(w * 0.05f, h * 0.30f, w * 0.9f, 70 * s), "RECLAMĂ SIMULATĂ", title);
            GUI.Label(new Rect(w * 0.05f, h * 0.30f + 74 * s, w * 0.9f, 60 * s),
                "Înlocuitor pentru LevelPlay — nu e o reclamă reală.\n" + (showing == Kind.Rewarded ? "Reclamă cu recompensă" : "Reclamă între runde"), note);

            float length = showing == Kind.Rewarded ? cfg.sim_rewarded_seconds : cfg.sim_inter_seconds;
            var bar = new Rect(w * 0.15f, h * 0.30f + 150 * s, w * 0.7f, 10 * s);
            GUI.color = new Color(1, 1, 1, 0.2f);
            GUI.DrawTexture(bar, white);
            GUI.color = new Color(1f, 0.82f, 0.2f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(shownFor / length), bar.height), white);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, bar.yMax + 4 * s, w, 30 * s), Mathf.CeilToInt(Mathf.Max(0f, length - shownFor)) + " s", note);

            // The close button: rewarded from the start (with a confirmation), interstitial after 2 s.
            bool canClose = showing == Kind.Rewarded || shownFor >= cfg.sim_inter_close_after_s;
            var close = new Rect(w - 70 * s - 12 * s, Screen.height - Screen.safeArea.yMax + 12 * s, 70 * s, 70 * s);
            if (canClose && !confirmClose && GUI.Button(close, "X", button))
            {
                if (showing == Kind.Rewarded) confirmClose = true;
                else Finish(AdResult.ClosedEarly);
            }
            if (!confirmClose) return;

            var panel = new Rect(w * 0.08f, h * 0.55f, w * 0.84f, 200 * s);
            GUI.color = new Color(0.15f, 0.16f, 0.22f, 1f);
            GUI.DrawTexture(panel, white);
            GUI.color = Color.white;
            GUI.Label(new Rect(panel.x + 10 * s, panel.y + 10 * s, panel.width - 20 * s, 70 * s), "Dacă închizi acum nu primești recompensa.", note);
            OfferCardLayout.Buttons(panel.x + 16 * s, panel.y + 100 * s, panel.width - 32 * s, 80 * s, 12 * s, out var a, out var b);
            if (GUI.Button(new Rect(a.X, a.Y, a.W, a.H), "Continuă reclama", button)) confirmClose = false;
            else if (GUI.Button(new Rect(b.X, b.Y, b.W, b.H), "Închide", button)) Finish(AdResult.ClosedEarly);
        }
    }
}
