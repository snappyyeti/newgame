using System.Collections;
using System.Collections.Generic;
using SplashSort.Core;
using UnityEngine;

namespace SplashSort.Game
{
    /// <summary>
    /// Entry point of the Splash Sort test prototype. Builds everything at runtime, so Play works from any scene.
    /// Drag a ring from the tray onto an empty pool; merges, SPLASH and cascades happen on their own.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        enum State { Playing, Animating, GameOver }

        const string BestKey = "splash_best";

        static GameController instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance == null) new GameObject("SplashSort").AddComponent<GameController>();
        }

        SplashConfig cfg;
        IRandom random;
        Camera cam;
        Feedback fx;
        BoardView view;

        Round round;
        State state;
        int dragSlot = -1;
        int shownScore;
        int best;
        bool newBest;
        int colorsInPlay;
        float scorePunch;
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
            random = new SystemRandom();
            cfg = new SplashConfig();
            best = PlayerPrefs.GetInt(BestKey, 0);

            foreach (var existing in Camera.allCameras) existing.gameObject.SetActive(false);
            cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.gameObject.AddComponent<AudioListener>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.17f, 0.20f);
            cam.fieldOfView = 45f;

            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(55f, -25f, 0);

            fx = gameObject.AddComponent<Feedback>();
            fx.Init(cam);

            view = new GameObject("Board").AddComponent<BoardView>();
            view.Build(cfg, cam, fx);
            view.Popup += (pos, text) => popups.Add(new PopupText { World = pos, Text = text, Born = Time.time });
            screenSize = new Vector2Int(Screen.width, Screen.height);

            NewGame();
        }

        void NewGame()
        {
            round = new Round(cfg, random);
            shownScore = 0;
            newBest = false;
            colorsInPlay = round.ColorsInPlay;
            dragSlot = -1;
            popups.Clear();
            view.ClearBoard();
            view.SyncBoard(round.Board);
            view.RebuildTray(round.Tray, true);
            view.Highlight(-1, false);
            state = State.Playing;
        }

        void Update()
        {
            if (Screen.width != screenSize.x || Screen.height != screenSize.y)
            {
                screenSize = new Vector2Int(Screen.width, Screen.height);
                view.FitCamera();
            }

            scorePunch = Mathf.MoveTowards(scorePunch, 0f, Time.deltaTime * 3f);

            if (state != State.Playing || !GetPointer(out bool down, out bool held, out bool up, out Vector2 pos)) return;

            if (dragSlot < 0 && down)
            {
                dragSlot = view.TraySlotAtScreen(pos);
                if (dragSlot >= 0) view.DragTo(dragSlot, pos);
            }

            if (dragSlot < 0) return;

            if (held && !up)
            {
                view.DragTo(dragSlot, pos);
                int hover = view.PoolAtScreen(pos);
                view.Highlight(hover, hover >= 0 && round.CanPlace(dragSlot, hover));
            }

            if (up)
            {
                int pool = view.PoolAtScreen(pos);
                view.Highlight(-1, false);
                int slot = dragSlot;
                dragSlot = -1;
                if (pool >= 0 && round.CanPlace(slot, pool)) StartCoroutine(Place(slot, pool));
                else view.ReturnToTray(slot);
            }
        }

        IEnumerator Place(int slot, int pool)
        {
            state = State.Animating;
            int ringsLeftBefore = 0;
            foreach (var r in round.Tray) if (r != null) ringsLeftBefore++;

            var result = round.Place(slot, pool);
            yield return view.PlayPlace(slot, pool, result, round.Board, points =>
            {
                shownScore += points;
                scorePunch = 1f;
            });
            shownScore = round.Score;

            // The last of the three rings was used, so a fresh set arrived.
            if (ringsLeftBefore == 1) view.RebuildTray(round.Tray, true);

            if (round.ColorsInPlay > colorsInPlay)
            {
                colorsInPlay = round.ColorsInPlay;
                ShowBanner("Culoare nouă! (" + colorsInPlay + " culori)");
            }

            if (round.IsOver)
            {
                newBest = round.Score > best;
                if (newBest)
                {
                    best = round.Score;
                    PlayerPrefs.SetInt(BestKey, best);
                    PlayerPrefs.Save();
                }
                fx.Shake(0.15f, 0.4f);
                state = State.GameOver;
            }
            else
            {
                state = State.Playing;
            }
        }

        void ShowBanner(string text)
        {
            banner = text;
            bannerUntil = Time.time + 2f;
        }

        // ---------------------------------------------------------------- input

#if !ENABLE_LEGACY_INPUT_MANAGER
        static bool warnedNoInput;
#endif

        /// <summary>Pointer for drag and drop: first touch on phones, left mouse button on desktop (classic Input Manager).</summary>
        static bool GetPointer(out bool down, out bool held, out bool up, out Vector2 position)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                down = t.phase == TouchPhase.Began;
                up = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
                held = !up;
                position = t.position;
                return true;
            }
            down = Input.GetMouseButtonDown(0);
            held = Input.GetMouseButton(0);
            up = Input.GetMouseButtonUp(0);
            position = Input.mousePosition;
            return true;
#else
            if (!warnedNoInput)
            {
                warnedNoInput = true;
                Debug.LogError("[Splash Sort] Input Manager este oprit. Edit > Project Settings > Player > Active Input Handling: alege \"Input Manager (Old)\" sau \"Both\" și repornește Unity.");
            }
            down = held = up = false;
            position = default;
            return false;
#endif
        }

        // ---------------------------------------------------------------- HUD

        GUIStyle scoreStyle, small, center, button;
        float styleScale;

        void EnsureStyles()
        {
            float s = Screen.height / 1000f;
            if (scoreStyle != null && Mathf.Approximately(s, styleScale)) return;
            styleScale = s;
            scoreStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(60 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            scoreStyle.normal.textColor = Color.white;
            small = new GUIStyle(scoreStyle) { fontSize = Mathf.RoundToInt(22 * s), fontStyle = FontStyle.Normal };
            center = new GUIStyle(scoreStyle) { fontSize = Mathf.RoundToInt(30 * s), wordWrap = true };
            button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(34 * s), fontStyle = FontStyle.Bold };
        }

        void OnGUI()
        {
            if (round == null) return;
            EnsureStyles();
            float s = styleScale;
            float w = Screen.width;
            float top = Screen.height - Screen.safeArea.yMax;

            // The score jumps when points land.
            int baseSize = Mathf.RoundToInt(60 * s);
            scoreStyle.fontSize = Mathf.RoundToInt(baseSize * (1f + 0.45f * scorePunch));
            GUI.Label(new Rect(0, top + 15 * s, w, 90 * s), shownScore.ToString("N0"), scoreStyle);
            GUI.Label(new Rect(0, top + 100 * s, w, 30 * s), "Record: " + Mathf.Max(best, shownScore).ToString("N0"), small);

            if (banner != null && Time.time < bannerUntil)
                GUI.Label(new Rect(0, top + 135 * s, w, 50 * s), banner, center);

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                float age = Time.time - p.Born;
                if (age > 1.1f)
                {
                    popups.RemoveAt(i);
                    continue;
                }
                var sp = cam.WorldToScreenPoint(p.World);
                if (sp.z <= 0) continue;
                // Bounce up, then drift.
                float rise = age < 0.15f ? Mathf.Lerp(0f, 50f, age / 0.15f) : 50f + (age - 0.15f) * 40f;
                GUI.Label(new Rect(sp.x - 200 * s, Screen.height - sp.y - 50 * s - rise * s, 400 * s, 90 * s), p.Text, center);
            }

            if (state == State.GameOver) DrawGameOver(w, s);
        }

        void DrawGameOver(float w, float s)
        {
            var panel = new Rect(w * 0.08f, Screen.height * 0.3f, w * 0.84f, 360 * s);
            GUI.Box(panel, "");
            GUI.Label(new Rect(panel.x, panel.y + 20 * s, panel.width, 60 * s), newBest ? "RECORD NOU!" : "Nu mai e loc!", center);
            GUI.Label(new Rect(panel.x, panel.y + 90 * s, panel.width, 50 * s), "Scor: " + round.Score.ToString("N0"), center);
            GUI.Label(new Rect(panel.x, panel.y + 140 * s, panel.width, 40 * s), "Record: " + best.ToString("N0"), small);
            if (GUI.Button(new Rect(panel.x + 30 * s, panel.y + 220 * s, panel.width - 60 * s, 100 * s), "Joacă din nou", button))
                NewGame();
        }
    }
}
