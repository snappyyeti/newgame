using System.Collections;
using System.Collections.Generic;
using HumanCannon.Core;
using UnityEngine;

namespace HumanCannon.Game
{
    /// <summary>
    /// Entry point. Builds the whole prototype at runtime, so it works in any scene (including an empty one),
    /// drives rounds, input and the end-of-round flow, and draws the HUD.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        enum State { Playing, Animating, ReviveOffer, WaitingForAd, GameOver }

        const float ReviveOfferSeconds = 5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<GameController>() == null)
                new GameObject("HumanCannon").AddComponent<GameController>();
        }

        GameConfig cfg;
        PlayerProfile profile;
        SimulatedAdService ads;
        ArenaView view;
        Camera cam;
        IRandom random;

        Round round;
        Director director;
        State state;
        int tierColors;
        float reviveTimer;
        bool newRecord;
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
            Screen.orientation = ScreenOrientation.Portrait;
            Application.targetFrameRate = 60;
            random = new SystemRandom();

            cfg = new GameConfig();
            // MVP step 3: feed the Remote Config key/value map here, e.g. cfg.ApplyOverrides(remoteValues).

            profile = ProfileStore.Load();
            ads = gameObject.AddComponent<SimulatedAdService>();
            ads.Profile = profile;

            foreach (var existing in FindObjectsByType<Camera>(FindObjectsSortMode.None)) existing.gameObject.SetActive(false);
            cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.08f, 0.18f);
            cam.fieldOfView = 50f;

            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0);

            view = new GameObject("Arena").AddComponent<ArenaView>();
            view.Build(cfg, cam);
            view.Popup += (pos, text) => popups.Add(new PopupText { World = pos, Text = text, Born = Time.time });
            screenSize = new Vector2Int(Screen.width, Screen.height);

            Analytics.Log("session_start",
                "country", CountryCode(),
                "device", SystemInfo.deviceModel,
                "ram_mb", SystemInfo.systemMemorySize,
                "vibration", Settings.Vibration);

            StartRound();
        }

        static string CountryCode()
        {
            try { return System.Globalization.RegionInfo.CurrentRegion.TwoLetterISORegionName; }
            catch (System.Exception) { return "??"; }
        }

        void StartRound()
        {
            profile.RunsStarted++;
            ProfileStore.Save(profile);

            director = new Director(cfg, new HiddenRules(cfg, profile), random);
            round = new Round(cfg, director, random, profile.Record, profile.RunsStarted);
            tierColors = round.Tier.Colors;
            newRecord = false;

            view.ClearAll();
            view.SyncBoard(round.Board);
            view.RebuildHand(round, true);
            state = State.Playing;

            Analytics.Log("run_start", "run", round.RunNumber, "record", round.RecordBefore, "rule", director.LastDecision.Rule);
        }

        void Update()
        {
            if (Screen.width != screenSize.x || Screen.height != screenSize.y)
            {
                screenSize = new Vector2Int(Screen.width, Screen.height);
                view.FitCamera();
            }

            view.UpdateSignals(round, state == State.Playing && round.CanSwap);

            switch (state)
            {
                case State.Playing:
                    if (!ads.IsShowing && TryGetTap(out var tap)) HandleTap(tap);
                    break;
                case State.ReviveOffer:
                    reviveTimer -= Time.deltaTime;
                    if (reviveTimer <= 0f) DeclineRevive();
                    break;
            }
        }

        void HandleTap(Vector2 screen)
        {
            if (view.IsReserveAtScreen(screen))
            {
                if (!round.CanSwap) return;
                round.Swap();
                view.RebuildHand(round, true);
                if (round.IsOver) StartCoroutine(EndOfRound());
                return;
            }

            int p = view.PedestalAtScreen(screen);
            if (p >= 0 && round.CanFire(p)) StartCoroutine(Fire(p));
        }

        IEnumerator Fire(int pedestal)
        {
            state = State.Animating;
            var group = round.Cannon;
            var result = round.Fire(pedestal);

            Analytics.Log("group_fired", "pedestal", pedestal + 1, "size", group.Size, "h", group.DirectorH.ToString("0.00"), "mode", group.DirectorMode);
            foreach (var e in result.Events)
                if (e.Kind == BoardEventKind.Clear)
                    Analytics.Log("clear", "length", e.Count, "step", e.Step, "points", e.Points);

            yield return view.PlayFire(round, result);

            if (round.Tier.Colors > tierColors)
            {
                tierColors = round.Tier.Colors;
                ShowBanner("Un artist nou intră în arenă!");
            }

            if (round.IsOver) yield return EndOfRound();
            else state = State.Playing;
        }

        IEnumerator EndOfRound()
        {
            state = State.Animating;
            Haptics.Heavy();
            yield return view.PlayEndShake();

            if (round.CanRevive && ads.RewardedReady)
            {
                state = State.ReviveOffer;
                reviveTimer = ReviveOfferSeconds;
                Analytics.Log("revive_show", "score", round.Score, "record", round.RecordBefore);
            }
            else
            {
                FinishRound();
            }
        }

        void AcceptRevive()
        {
            state = State.WaitingForAd;
            ads.ShowRewarded(earned =>
            {
                if (!earned)
                {
                    FinishRound();
                    return;
                }
                Analytics.Log("reward_over");
                var events = round.Revive(out int bonus);
                Analytics.Log("revive_success", "bonus", bonus);
                if (bonus > 0) ShowBanner("Bonus de revive +" + bonus);
                StartCoroutine(AfterRevive(events));
            });
        }

        IEnumerator AfterRevive(List<BoardEvent> events)
        {
            state = State.Animating;
            yield return view.PlayKnockdown(events, round.Board);
            view.RebuildHand(round, false);
            if (round.IsOver) yield return EndOfRound();
            else state = State.Playing;
        }

        void DeclineRevive()
        {
            Analytics.Log("revive_close");
            FinishRound();
        }

        void FinishRound()
        {
            newRecord = round.Score > round.RecordBefore;
            profile.AddRun(new RunRecord { Score = round.Score, RecordBefore = round.RecordBefore, UsedRevive = round.UsedRevive });
            ProfileStore.Save(profile);

            Analytics.Log("run_end",
                "score", round.Score,
                "record", round.RecordBefore,
                "ratio", round.RecordBefore > 0 ? (round.Score / (float)round.RecordBefore).ToString("0.00") : "-",
                "cause", "natural",
                "revive", round.UsedRevive);
            state = State.GameOver;
        }

        void PlayAgain()
        {
            state = State.WaitingForAd;
            ads.ShowInterstitialIfAllowed(profile.RunsStarted + 1, StartRound);
        }

        void ShowBanner(string text)
        {
            banner = text;
            bannerUntil = Time.time + 2f;
        }

        // ---------------------------------------------------------------- input

        static bool TryGetTap(out Vector2 position)
        {
#if HC_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var touch = UnityEngine.InputSystem.Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            {
                position = touch.primaryTouch.position.ReadValue();
                return true;
            }
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                position = mouse.position.ReadValue();
                return true;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
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
#endif
            position = default;
            return false;
        }

        // ---------------------------------------------------------------- HUD

        GUIStyle big, small, center, button, overlay;
        float scale;

        void EnsureStyles()
        {
            float s = Screen.height / 1000f;
            if (big != null && Mathf.Approximately(s, scale)) return;
            scale = s;
            big = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(56 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            big.normal.textColor = Color.white;
            small = new GUIStyle(big) { fontSize = Mathf.RoundToInt(22 * s), fontStyle = FontStyle.Normal };
            center = new GUIStyle(big) { fontSize = Mathf.RoundToInt(30 * s), wordWrap = true };
            button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(32 * s), fontStyle = FontStyle.Bold };
            overlay = new GUIStyle(GUI.skin.box);
        }

        void OnGUI()
        {
            if (round == null) return;
            EnsureStyles();
            var safe = Screen.safeArea;
            float top = Screen.height - safe.yMax;
            float w = Screen.width;
            float s = scale;

            GUI.Label(new Rect(0, top + 20 * s, w, 70 * s), round.Score.ToString("N0"), big);
            GUI.Label(new Rect(0, top + 90 * s, w, 30 * s), "Record: " + Mathf.Max(profile.Record, round.RecordBefore).ToString("N0"), small);

            if (banner != null && Time.time < bannerUntil)
                GUI.Label(new Rect(0, top + 130 * s, w, 50 * s), banner, center);

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                float age = Time.time - p.Born;
                if (age > 1f)
                {
                    popups.RemoveAt(i);
                    continue;
                }
                var sp = cam.WorldToScreenPoint(p.World);
                if (sp.z <= 0) continue;
                GUI.Label(new Rect(sp.x - 200 * s, Screen.height - sp.y - 40 * s - age * 80 * s, 400 * s, 40 * s), p.Text, center);
            }

            if (ads.BannerVisible)
                GUI.Box(new Rect(0, Screen.height - safe.y - 60 * s, w, 60 * s), "Banner (LevelPlay, MVP pasul 4)");

            if (Debug.isDebugBuild)
            {
                var d = director.LastDecision;
                GUI.Label(new Rect(10 * s, top + 130 * s, w, 24 * s), $"regizor: {d.Rule} {d.Mode} D={d.D:0.00}", new GUIStyle(small) { alignment = TextAnchor.UpperLeft, fontSize = Mathf.RoundToInt(14 * s) });
            }

            if (state == State.ReviveOffer) DrawReviveOffer(w, s);
            else if (state == State.GameOver) DrawGameOver(w, s);
        }

        void DrawReviveOffer(float w, float s)
        {
            var panel = new Rect(w * 0.08f, Screen.height * 0.3f, w * 0.84f, 380 * s);
            GUI.Box(panel, "", overlay);
            GUI.Label(new Rect(panel.x, panel.y + 20 * s, panel.width, 60 * s), "Continui?", big);
            GUI.Label(new Rect(panel.x, panel.y + 90 * s, panel.width, 80 * s), "Elefantul dărâmă cele mai înalte 3 piedestale.\n" + Mathf.CeilToInt(reviveTimer), center);
            if (GUI.Button(new Rect(panel.x + 30 * s, panel.y + 190 * s, panel.width - 60 * s, 80 * s), "Revive (reclamă)", button))
                AcceptRevive();
            if (GUI.Button(new Rect(panel.x + 30 * s, panel.y + 285 * s, panel.width - 60 * s, 60 * s), "Nu, mulțumesc", button))
                DeclineRevive();
        }

        void DrawGameOver(float w, float s)
        {
            var panel = new Rect(w * 0.08f, Screen.height * 0.25f, w * 0.84f, 470 * s);
            GUI.Box(panel, "", overlay);
            GUI.Label(new Rect(panel.x, panel.y + 20 * s, panel.width, 60 * s), newRecord ? "RECORD NOU!" : "Final", big);
            GUI.Label(new Rect(panel.x, panel.y + 90 * s, panel.width, 50 * s), "Scor: " + round.Score.ToString("N0"), center);
            GUI.Label(new Rect(panel.x, panel.y + 140 * s, panel.width, 40 * s), "Record: " + profile.Record.ToString("N0"), small);

            int gap = round.RecordBefore - round.Score;
            if (!newRecord && gap > 0 && gap < cfg.NearRecordRatio * round.RecordBefore)
                GUI.Label(new Rect(panel.x, panel.y + 185 * s, panel.width, 50 * s), $"Erai la {gap} puncte de record!", center);

            if (GUI.Button(new Rect(panel.x + 30 * s, panel.y + 250 * s, panel.width - 60 * s, 100 * s), "Joacă din nou", button))
                PlayAgain();

            bool vib = GUI.Toggle(new Rect(panel.x + 30 * s, panel.y + 380 * s, panel.width - 60 * s, 50 * s), Settings.Vibration, " Vibrații");
            if (vib != Settings.Vibration) Settings.Vibration = vib;
        }
    }
}
