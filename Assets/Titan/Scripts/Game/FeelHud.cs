using System.Collections.Generic;
using Titan.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Titan.Game
{
    /// <summary>
    /// The juice pass and the performance budget (FEEL-1, DESIGN.md 7): <see cref="HitStop"/> and slow-mo on one
    /// unscaled clock (Titan.Core.TimeWarp: calls never stack, ≤ 10% freeze in any rolling second, nothing under
    /// "Efecte reduse" or in the last 15% of the approach), the triggers, haptics, the one place Time.timeScale is set
    /// outside the ad pause contract, the editor freeze assertion, and the developer overlays (F8 or a 3-finger
    /// long-press: fps, p95, active objects and rigidbodies; F7 in the editor: the grey palette check).
    /// No gameplay change: the simulation never reads any of this.
    /// </summary>
    public sealed partial class GameController
    {
        readonly FeelConfig feel = new FeelConfig();
        TimeWarp warp;

        // Editor play-mode assertion: real freeze time over the last second.
        readonly Queue<Vector2> freezeFrames = new Queue<Vector2>();
        float freezeSum, freezeWarnedAt = -10f;

        // Developer overlay.
        bool perfOverlay;
        float threeFingerSince = -1f;
        readonly FrameStats frameStats = new FrameStats(300);
        int activeObjects, activeBodies;
        float objectsCountedAt = -10f;
        readonly List<GameObject> rootScratch = new List<GameObject>();
        readonly List<Transform> transformScratch = new List<Transform>();
        readonly List<Rigidbody> bodyScratch = new List<Rigidbody>();

        void InitFeel()
        {
            warp = new TimeWarp(feel);
        }

        float CurrentDanger => game == null || game.IsOver ? 0f : game.Danger;

        /// <summary>
        /// Freezes the game for <paramref name="seconds"/> of real time at <paramref name="scale"/>. Calls never stack
        /// (the longer remaining one wins), the total freeze is capped at 10% of any rolling second, and it returns at
        /// once under "Efecte reduse" and from 85% of the approach on.
        /// </summary>
        void HitStop(float seconds, float scale)
        {
            if (Settings.ReducedFx) return;
            warp.HitStop(Time.unscaledTime, seconds, scale, false, CurrentDanger);
        }

        /// <summary>A piece starts to fall on screen (the view's moment, one projectile flight after the cut).</summary>
        void PieceJuice(GameEvent e)
        {
            if (e.Piece == null || paused) return;
            var j = Juice.ForPiece(feel, e.Piece.Count, e.Part, e.Clean);
            if (j.StopSeconds > 0f) HitStop(j.StopSeconds, feel.StopScale);
            if (j.FovPunch) fx.PunchFov();
            if (j.SlowMoSeconds > 0f && !Settings.ReducedFx)
            {
                bool granted = warp.SlowMo(Time.unscaledTime, j.SlowMoSeconds, feel.SlowMoScale, false, CurrentDanger);
                Debug.Assert(!granted || CurrentDanger < feel.NoSlowMoDanger, "[Titan] slow-mo granted in the last 15% of the approach");
            }
            if (j.Haptic) fx.Haptic();
        }

        void DefeatJuice(GameEvent e)
        {
            HitStop(feel.DefeatStop, feel.StopScale);
            if (Juice.DefeatHaptic(e.Role, e.NearMiss)) fx.Haptic();
        }

        /// <summary>
        /// Once per frame: the time scale is 0 while paused, else the hitstop's or slow-mo's (1 otherwise); the ad
        /// pause contract owns the clock while a card or an ad is up. A slow-mo still running when the titan crosses
        /// 85% of its approach is cut short.
        /// </summary>
        void ApplyTimeScale()
        {
            if (AdPause.Active) return;
            float now = Time.unscaledTime;
            if (warp.SlowMoActive(now) && CurrentDanger >= feel.NoSlowMoDanger)
            {
                warp.CancelSlowMo();
                if (Application.isEditor) Debug.LogWarning("[Titan] slow-mo cut: the titan reached 85% of its approach.");
            }
            Time.timeScale = paused ? 0f : warp.Scale(now);
            AudioListener.pause = paused;
        }

        /// <summary>
        /// Editor play-mode assertion (FEEL-1): logs a warning when the frames spent frozen (time scale ≤ 0.1, not
        /// paused) add up to more than 10% of the last second, plus one frame of rounding.
        /// </summary>
        void MonitorFreeze()
        {
            if (!Application.isEditor) return;
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            bool frozen = !paused && !AdPause.Active && Time.timeScale <= feel.FreezeScale;
            freezeFrames.Enqueue(new Vector2(now, frozen ? dt : 0f));
            freezeSum += frozen ? dt : 0f;
            while (freezeFrames.Count > 0 && freezeFrames.Peek().x < now - feel.HitStopWindow) freezeSum -= freezeFrames.Dequeue().y;
            if (freezeSum > feel.HitStopMaxFraction * feel.HitStopWindow + dt + 1e-3f && now - freezeWarnedAt > 5f)
            {
                freezeWarnedAt = now;
                Debug.LogWarning("[Titan] HitStop froze " + Mathf.RoundToInt(freezeSum * 1000f) + " ms in the last second (cap 100 ms).");
            }
        }

        /// <summary>Developer keys and gestures, every frame: F8 / 3-finger long-press (overlay), F7 (grey palette, editor).</summary>
        void UpdateDevTools()
        {
            frameStats.Add(Time.unscaledDeltaTime * 1000f);
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Application.isEditor && Input.GetKeyDown(KeyCode.F8)) perfOverlay = !perfOverlay;
            if (Application.isEditor && Input.GetKeyDown(KeyCode.F7))
            {
                ColorStyle.SetDebugGreyscale(!ColorStyle.DebugGreyscale);
                ShowBanner(Loc.T("dev.greyscale", OnOff(ColorStyle.DebugGreyscale)), Color.white, 1.5f);
            }
            if (Input.touchCount == 3)
            {
                if (threeFingerSince < 0f) threeFingerSince = Time.unscaledTime;
                else if (Time.unscaledTime - threeFingerSince >= 1.0f)
                {
                    perfOverlay = !perfOverlay;
                    threeFingerSince = float.PositiveInfinity; // once per press
                }
            }
            else threeFingerSince = -1f;
#endif
            if (perfOverlay && Time.unscaledTime - objectsCountedAt >= 0.5f) CountObjects();
        }

        /// <summary>Active GameObjects and rigidbodies in the scene (walks the roots; no Find* API).</summary>
        void CountObjects()
        {
            objectsCountedAt = Time.unscaledTime;
            activeObjects = activeBodies = 0;
            SceneManager.GetActiveScene().GetRootGameObjects(rootScratch);
            foreach (var root in rootScratch) CountUnder(root);
            if (ads != null) CountUnder(ads.gameObject); // DontDestroyOnLoad
        }

        void CountUnder(GameObject root)
        {
            if (!root.activeInHierarchy) return;
            root.GetComponentsInChildren(false, transformScratch);
            activeObjects += transformScratch.Count;
            root.GetComponentsInChildren(false, bodyScratch);
            activeBodies += bodyScratch.Count;
        }

        void DrawPerfOverlay(float s)
        {
            if (!perfOverlay) return;
            var style = new GUIStyle(small) { fontSize = Mathf.RoundToInt(15 * s), alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Normal };
            var r = new Rect(Screen.safeArea.xMin + 8 * s, Screen.height * 0.47f, 330 * s, 52 * s);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(r, white);
            GUI.color = Color.white;
            float p95 = frameStats.P95;
            style.normal.textColor = p95 <= feel.PerfP95FloorMs ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.5f, 0.4f);
            GUI.Label(new Rect(r.x + 8 * s, r.y + 2 * s, r.width - 16 * s, 24 * s), Loc.T("dev.perf", Mathf.RoundToInt(frameStats.Fps), p95.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)), style);
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(r.x + 8 * s, r.y + 26 * s, r.width - 16 * s, 24 * s), Loc.T("dev.objects", activeObjects, activeBodies), style);
        }

        /// <summary>Sound, language and cube patterns follow the settings (the pause menu, Bind at startup).</summary>
        void OnSettingsChanged()
        {
            Fx.ApplySound();
            Language.Apply();
            ColorStyle.Refresh();
        }
    }
}
