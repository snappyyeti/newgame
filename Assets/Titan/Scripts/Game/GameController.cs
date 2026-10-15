using System.Collections.Generic;
using Titan.Core;
using UnityEngine;

namespace Titan.Game
{
    /// <summary>
    /// Entry point of Titanul din Cuburi. Builds everything at runtime, so Play works from any scene.
    /// Tap a cannon at the bottom to send it to the firing line on the wall; it shoots its color on its own.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        const string BestKey = "titan_best";

        static GameController instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance == null) new GameObject("Titan").AddComponent<GameController>();
        }

        TitanConfig cfg;
        TitanGame game;
        ArenaView view;
        Fx fx;
        Camera cam;

        int best;
        bool newBest;
        float shownScore;
        float scorePunch;
        int lastScore;
        Vector2Int screenSize;

        string banner;
        float bannerUntil;
        readonly List<PopupText> popups = new List<PopupText>();

        sealed class PopupText
        {
            public Vector3 World;
            public string Text;
            public float Born;
        }

        void Awake()
        {
            instance = this;
            Screen.orientation = ScreenOrientation.Portrait;
            Application.targetFrameRate = 60;
            cfg = new TitanConfig();
            best = PlayerPrefs.GetInt(BestKey, 0);

            foreach (var existing in Camera.allCameras) existing.gameObject.SetActive(false);
            cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.gameObject.AddComponent<AudioListener>();
            var sky = new Color(0.62f, 0.78f, 0.95f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = sky;
            cam.fieldOfView = 55f;
            cam.farClipPlane = 400f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = sky;
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 220f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.66f);
            QualitySettings.shadowDistance = 120f;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0);

            fx = gameObject.AddComponent<Fx>();
            fx.Init(cam);

            NewGame();
        }

        void NewGame()
        {
            if (view != null) Destroy(view.gameObject);
            game = new TitanGame(cfg, new SystemRandom());
            view = new GameObject("Arena").AddComponent<ArenaView>();
            view.Build(game, cam, fx);
            view.Popup += (pos, text) => popups.Add(new PopupText { World = pos, Text = text, Born = Time.time });
            view.Banner += ShowBanner;
            screenSize = new Vector2Int(Screen.width, Screen.height);
            shownScore = 0;
            lastScore = 0;
            newBest = false;
            popups.Clear();
            PlayEvents();
        }

        void Update()
        {
            if (Screen.width != screenSize.x || Screen.height != screenSize.y)
            {
                screenSize = new Vector2Int(Screen.width, Screen.height);
                view.Refit();
            }

            if (!game.IsOver)
            {
                if (TryGetTap(out var tap)) HandleTap(tap);
                game.Tick(Mathf.Min(Time.deltaTime, 0.1f));
                PlayEvents();
            }

            if (game.Score != lastScore)
            {
                lastScore = game.Score;
                scorePunch = 1f;
            }
            shownScore = Mathf.MoveTowards(shownScore, game.Score, Mathf.Max(30f, (game.Score - shownScore) * 6f) * Time.deltaTime);
            scorePunch = Mathf.MoveTowards(scorePunch, 0f, Time.deltaTime * 3f);
        }

        void HandleTap(Vector2 screen)
        {
            int i = view.TrayAtScreen(screen);
            if (i < 0) return;
            if (game.CanDeploy(i))
            {
                game.Deploy(i);
            }
            else
            {
                view.ShakeTray(i);
                ShowBanner("Toate cele 5 locuri sunt ocupate!");
            }
        }

        void PlayEvents()
        {
            foreach (var e in game.Events)
            {
                view.Play(e);
                if (e.Kind == EventKind.GameOver) OnGameOver();
            }
            game.Events.Clear();
        }

        void OnGameOver()
        {
            newBest = game.Score > best;
            if (newBest)
            {
                best = game.Score;
                PlayerPrefs.SetInt(BestKey, best);
                PlayerPrefs.Save();
            }
        }

        void ShowBanner(string text)
        {
            banner = text;
            bannerUntil = Time.time + 2f;
        }

        static bool TryGetTap(out Vector2 position)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began)
                {
                    position = t.position;
                    return true;
                }
            }
            if (Input.touchCount == 0 && Input.GetMouseButtonDown(0))
            {
                position = Input.mousePosition;
                return true;
            }
            position = default;
            return false;
#else
            if (!warnedNoInput)
            {
                warnedNoInput = true;
                Debug.LogError("[Titan] Input Manager este oprit. Edit > Project Settings > Player > Active Input Handling: alege \"Input Manager (Old)\" sau \"Both\" și repornește Unity.");
            }
            position = default;
            return false;
#endif
        }

#if !ENABLE_LEGACY_INPUT_MANAGER
        static bool warnedNoInput;
#endif

        // ---------------------------------------------------------------- HUD

        GUIStyle big, small, center, label, button;
        Texture2D white;
        float styleScale;

        void EnsureStyles()
        {
            float s = Screen.height / 1000f;
            if (big != null && Mathf.Approximately(s, styleScale)) return;
            styleScale = s;
            if (white == null)
            {
                white = new Texture2D(1, 1);
                white.SetPixel(0, 0, Color.white);
                white.Apply();
            }
            big = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(54 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            big.normal.textColor = Color.white;
            small = new GUIStyle(big) { fontSize = Mathf.RoundToInt(20 * s), fontStyle = FontStyle.Bold };
            center = new GUIStyle(big) { fontSize = Mathf.RoundToInt(30 * s), wordWrap = true };
            label = new GUIStyle(big) { fontSize = Mathf.RoundToInt(24 * s) };
            button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(34 * s), fontStyle = FontStyle.Bold };
        }

        void Shadowed(Rect r, string text, GUIStyle style)
        {
            var old = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, 0.6f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            style.normal.textColor = old;
            GUI.Label(r, text, style);
        }

        void OnGUI()
        {
            if (game == null) return;
            EnsureStyles();
            float s = styleScale;
            float w = Screen.width;
            float top = Screen.height - Screen.safeArea.yMax;

            // Score, record, titan number.
            big.fontSize = Mathf.RoundToInt(54 * s * (1f + 0.3f * scorePunch));
            Shadowed(new Rect(0, top + 10 * s, w, 70 * s), Mathf.RoundToInt(shownScore).ToString("N0"), big);
            Shadowed(new Rect(0, top + 78 * s, w, 26 * s), "Record " + Mathf.Max(best, game.Score).ToString("N0") + "   ·   Titanul #" + game.Level, small);

            // How close the titan is to the wall.
            float danger = 1f - game.Distance / cfg.StartDistance;
            var bar = new Rect(w * 0.12f, top + 112 * s, w * 0.76f, 16 * s);
            GUI.color = new Color(0, 0, 0, 0.45f);
            GUI.DrawTexture(bar, white);
            float pulse = danger > 0.75f ? 0.5f + 0.5f * Mathf.Sin(Time.time * 10f) : 1f;
            GUI.color = Color.Lerp(new Color(0.3f, 0.85f, 0.35f), new Color(1f, 0.2f, 0.15f), danger) * new Color(1, 1, 1, pulse);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * danger, bar.height), white);
            GUI.color = Color.white;
            Shadowed(new Rect(bar.xMax - 80 * s, bar.yMax, 80 * s, 22 * s), "ZIDUL", new GUIStyle(small) { alignment = TextAnchor.UpperRight, fontSize = Mathf.RoundToInt(14 * s) });

            // Ammo on every cannon.
            for (int i = 0; i < game.Tray.Length; i++)
            {
                var pos = view.TrayLabel(i);
                if (game.Tray[i] != null && pos.HasValue) AmmoTag(pos.Value, game.Tray[i], false, s);
            }
            for (int i = 0; i < game.Line.Length; i++)
            {
                var pos = view.LineLabel(i);
                if (game.Line[i] != null && pos.HasValue)
                    AmmoTag(pos.Value, game.Line[i], !game.Monster.HasVisibleOfColor(game.Line[i].Color), s);
            }

            if (banner != null && Time.time < bannerUntil)
                Shadowed(new Rect(0, Screen.height * 0.36f, w, 60 * s), banner, center);

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                float age = Time.time - p.Born;
                if (age > 1.3f)
                {
                    popups.RemoveAt(i);
                    continue;
                }
                var sp = cam.WorldToScreenPoint(p.World);
                if (sp.z <= 0) continue;
                float rise = age < 0.15f ? Mathf.Lerp(0f, 40f, age / 0.15f) : 40f + (age - 0.15f) * 35f;
                Shadowed(new Rect(sp.x - 200 * s, Screen.height - sp.y - 40 * s - rise * s, 400 * s, 80 * s), p.Text, center);
            }

            if (game.IsOver) DrawGameOver(w, s);
        }

        void AmmoTag(Vector2 at, Cannon c, bool waiting, float s)
        {
            var r = new Rect(at.x - 32 * s, at.y - 34 * s, 64 * s, 30 * s);
            GUI.color = waiting ? new Color(0.25f, 0.25f, 0.28f, 0.85f) : ArenaView.Palette[c.Color % ArenaView.Palette.Length];
            GUI.DrawTexture(r, white);
            GUI.color = Color.white;
            Shadowed(r, waiting ? c.Ammo + " zzz" : c.Ammo.ToString(), label);
        }

        void DrawGameOver(float w, float s)
        {
            var panel = new Rect(w * 0.08f, Screen.height * 0.3f, w * 0.84f, 380 * s);
            GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);
            GUI.DrawTexture(panel, white);
            GUI.color = Color.white;
            string reason = game.Lost == LoseReason.WallReached ? "Titanul a spart zidul!" : "Cele 5 locuri sunt pline\nși niciun tun nu are în ce trage!";
            Shadowed(new Rect(panel.x, panel.y + 20 * s, panel.width, 80 * s), newBest ? "RECORD NOU!" : reason, center);
            Shadowed(new Rect(panel.x, panel.y + 105 * s, panel.width, 50 * s), "Scor: " + game.Score.ToString("N0"), center);
            Shadowed(new Rect(panel.x, panel.y + 155 * s, panel.width, 30 * s), "Titani doborâți: " + (game.Level - 1) + "   ·   Record: " + best.ToString("N0"), small);
            if (GUI.Button(new Rect(panel.x + 30 * s, panel.y + 240 * s, panel.width - 60 * s, 100 * s), "Joacă din nou", button))
                NewGame();
        }
    }
}
