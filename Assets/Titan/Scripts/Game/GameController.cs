using System.Collections.Generic;
using Titan.Core;
using UnityEngine;

namespace Titan.Game
{
    /// <summary>
    /// Entry point of Titanul din Cuburi. Builds everything at runtime, so Play works from any scene.
    /// Tap a cannon at the bottom to send it to the firing line on the wall; it shoots its color on its own.
    /// </summary>
    public sealed partial class GameController : MonoBehaviour
    {
        static GameController instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance == null) new GameObject("Titan").AddComponent<GameController>();
        }

        TitanConfig cfg;
        ArenaView view;
        Fx fx;
        Camera cam;

        // Profile, telemetry and the current run (RET-1). The rules live in Titan.Core (RunSession, ProfileStore).
        Profile profile;
        Titan.Core.ProfileStore store;
        Telemetry telemetry;
        RunSession session;
        TitanGame game => session?.Game;
        /// <summary>App launch with a checkpoint after 3+ runs: "Continuă (titanul #n)" / "Joc nou" before any run.</summary>
        bool launchOverlay;
        int seedCounter;

        float shownScore;
        float scorePunch;
        int lastScore;
        Vector2Int screenSize;

        string banner;
        Color bannerColor = Color.white;
        float bannerUntil, bannerStart;
        /// <summary>The defeat banner's stars (−1: a banner without stars) and the line peak behind a missing third star.</summary>
        int bannerStars = -1, bannerMaxLine, starTicksPlayed;
        /// <summary>Banners from game events wait their turn (a defeat banner and the next titan's banner come in one frame).</summary>
        readonly Queue<BannerItem> bannerQueue = new Queue<BannerItem>();
        readonly List<PopupText> popups = new List<PopupText>();
        /// <summary>"Punct de salvare: titanul #n" (1.2 s).</summary>
        string toast;
        float toastUntil;

        /// <summary>Per-frame HUD cache: colors with cubes but none visible (the "? culori ascunse" chip).</summary>
        int hiddenColors;

        // Fast restart and performance telemetry.
        bool restartPending;
        float restartFrom;
        int restartFrame;
        /// <summary>Frame times of the current run in 1 ms buckets (the last bucket holds everything slower).</summary>
        readonly int[] frameMs = new int[200];

        struct BannerItem
        {
            public string Text;
            public Color Color;
            public float Seconds;
            public int Stars;
            public int MaxLine;
        }

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

            // The profile: file, then the PlayerPrefs mirror, then a new one (which migrates the old best and settings).
            store = UnityProfileStore.Open();
            var corrupt = new List<int>();
            profile = store.Load(corrupt.Add);
            telemetry = new Telemetry(() => profile);
            foreach (int source in corrupt) telemetry.Track(Telemetry.SaveCorrupt, source);
            int today = DayClock.StartSession(profile, System.DateTime.Now, out int daysSinceLast, out _);
            telemetry.Track(Telemetry.SessionStart, today, daysSinceLast);
            Settings.Bind(profile.settings, SaveProfile);
            SaveProfile();
            InitAds();

            view = new GameObject("Arena").AddComponent<ArenaView>();
            view.Build(cfg, cam, fx);
            view.Popup += (pos, text) => popups.Add(new PopupText { World = pos, Text = text, Born = Time.time });
            view.PiecePopup += OnPiecePopup;
            view.Banner += (text, color, seconds) => QueueBanner(text, color, seconds);
            screenSize = new Vector2Int(Screen.width, Screen.height);

            if (RunSession.ShowLaunchOverlay(profile)) launchOverlay = true;
            else StartRun(RunStart.Fresh(NewSeed()));
        }

        /// <summary>A new run seed: the clock and a counter, mixed (never shown, never needed again unless a checkpoint keeps it).</summary>
        long NewSeed() => unchecked((long)SeededRandom.Hash((ulong)System.DateTime.UtcNow.Ticks, (ulong)(++seedCounter)));

        /// <summary>
        /// Starts a run on the same arena (RET-2 fast restart): the view resets instead of being destroyed and rebuilt.
        /// For a restart from the game-over panel, the time from the tap to the first rendered frame of the new run is
        /// logged as restart_ms.
        /// </summary>
        void StartRun(RunStart start, bool restart = false)
        {
            restartFrom = Time.realtimeSinceStartup;
            restartFrame = Time.frameCount;
            restartPending = restart;
            launchOverlay = false;

            session = new RunSession(profile, telemetry, cfg, start);
            ResetRunAds();
            fx.StopRunEffects();
            view.Reset(session.Game);
            shownScore = start.Score;
            lastScore = start.Score;
            scorePunch = 0f;
            popups.Clear();
            bigPopup = queuedPopup = null;
            bannerQueue.Clear();
            banner = null;
            bannerStars = -1;
            toast = null;
            confetti.Clear();
            System.Array.Clear(frameMs, 0, frameMs.Length);
            PlayEvents();
        }

        void SaveProfile()
        {
            if (profile != null) store.Save(profile);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) SaveProfile();
            OnAdsAppPause(paused);
        }

        void OnApplicationQuit() => SaveProfile();

        void Update()
        {
            if (Screen.width != screenSize.x || Screen.height != screenSize.y)
            {
                screenSize = new Vector2Int(Screen.width, Screen.height);
                if (game != null) view.Refit();
            }
            if (restartPending && Time.frameCount > restartFrame + 1)
            {
                // The new run has been rendered once (it was built during frame restartFrame, drawn in the next one).
                restartPending = false;
                telemetry.Track(Telemetry.RestartMs, Mathf.RoundToInt((Time.realtimeSinceStartup - restartFrom) * 1000f));
            }
            CheckLogExport();
            if (game == null) return;

            // Ad cards and ads first: while one is up (the pause contract) the run does not tick and the tray takes no taps.
            UpdateAds(Time.unscaledDeltaTime);
            bool frozen = PlayFrozen;
            if (!game.IsOver && !frozen)
            {
                if (TryGetTap(out var tap)) HandleTap(tap);
                float dt = Mathf.Min(Time.deltaTime, 0.1f);
                game.Tick(dt);
                governor.AddPlaySeconds(dt);
                frameMs[Mathf.Clamp(Mathf.RoundToInt(Time.unscaledDeltaTime * 1000f), 0, frameMs.Length - 1)]++;
            }
            if (game.Events.Count > 0) PlayEvents(); // also the Revived event of a revive granted from a card
            if ((session.Update(frozen ? 0f : Time.unscaledDeltaTime) & SessionSignal.NewRecord) != 0)
            {
                ShowBanner("RECORD NOU!", ArenaView.BannerGold, 1.5f);
                fx.RecordFanfare();
                Confetti();
            }
            fx.Heartbeat(game.IsOver ? -1f : game.Danger);
            hiddenColors = game.IsOver ? 0 : game.HiddenOnlyColorCount();
            if ((banner == null || Time.time >= bannerUntil) && bannerQueue.Count > 0)
            {
                var next = bannerQueue.Dequeue();
                ShowBanner(next.Text, next.Color, next.Seconds, next.Stars, next.MaxLine);
            }
            StarTicks();

            if (game.Score != lastScore)
            {
                lastScore = game.Score;
                scorePunch = 1f;
            }
            shownScore = Mathf.MoveTowards(shownScore, game.Score, Mathf.Max(30f, (game.Score - shownScore) * 6f) * Time.deltaTime);
            scorePunch = Mathf.MoveTowards(scorePunch, 0f, Time.deltaTime * 3f);
            UpdateConfetti(Time.unscaledDeltaTime);
        }

        void HandleTap(Vector2 screen)
        {
            int i = view.TrayAtScreen(screen);
            if (i < 0) return;
            // A refused tap only shakes the cannon and says why. It NEVER opens anything else (DESIGN.md 4.2.8, 11.4):
            // the tray-tap path is TrayTap.Handle, which cannot reach a card (EthicsLockTests).
            switch (TrayTap.Handle(game, i))
            {
                case DeployBlockReason.WouldJam:
                    view.ShakeTray(i);
                    ShowBanner("Ar bloca ultimul loc! Alege o culoare vizibilă.", ArenaView.BannerGold, 1.6f);
                    break;
                case DeployBlockReason.LineFull:
                    view.ShakeTray(i);
                    ShowBanner("Toate cele " + game.ActiveSlots + " locuri sunt ocupate!", Color.white, 2f);
                    break;
            }
        }

        void PlayEvents()
        {
            var signals = SessionSignal.None;
            foreach (var e in game.Events)
            {
                view.Play(e);
                TrackAdEvent(e);
                if (e.Kind == EventKind.TitanDefeated) DefeatBanner(e);
                // A revivable loss waits for the "A doua suflare" card: the session hears about it only if the run really ends.
                if (e.Kind == EventKind.GameOver && OfferSecondWind(e)) continue;
                var signal = session.Process(e);
                if ((signal & SessionSignal.NewFurthestTitan) != 0) QueueBanner("TITAN NOU: #" + e.Level, ArenaView.BannerGold, 1.4f);
                if ((signal & SessionSignal.CheckpointSaved) != 0)
                {
                    toast = "Punct de salvare: titanul #" + e.Level;
                    toastUntil = Time.time + 1.2f;
                }
                signals |= signal;
            }
            game.Events.Clear();
            if ((signals & SessionSignal.RunEnded) != 0) RunReallyEnded();
            if ((signals & SessionSignal.Save) != 0) SaveProfile();
        }

        /// <summary>"TITAN DOBORÂT! +N" (or "LA MUSTAȚĂ!") with the titan's 3 stars popping in under it.</summary>
        void DefeatBanner(GameEvent e)
        {
            if (e.NearMiss)
                QueueBanner("LA MUSTAȚĂ!  x" + cfg.NearMissBonusMult.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "  +" + HudText.Num(e.Points),
                    ArenaView.BannerGold, 2.2f, e.Stars, e.MaxLine);
            else QueueBanner("TITAN DOBORÂT!  +" + HudText.Num(e.Points), ArenaView.BannerWhite, 2.2f, e.Stars, e.MaxLine);
        }

        int FrameP95()
        {
            int total = 0;
            foreach (int n in frameMs) total += n;
            if (total == 0) return 0;
            int seen = 0;
            for (int ms = 0; ms < frameMs.Length; ms++)
            {
                seen += frameMs[ms];
                if (seen >= 0.95f * total) return ms;
            }
            return frameMs.Length - 1;
        }

        /// <summary>Shows a banner now, replacing the current one (tap feedback, the record).</summary>
        void ShowBanner(string text, Color color, float seconds, int stars = -1, int maxLine = 0)
        {
            banner = text;
            bannerColor = color;
            bannerStart = Time.time;
            bannerUntil = Time.time + seconds;
            bannerStars = stars;
            bannerMaxLine = maxLine;
            starTicksPlayed = 0;
        }

        /// <summary>Game-event banners: shown now if the screen is free, otherwise after the current one (at most 3 waiting).</summary>
        void QueueBanner(string text, Color color, float seconds, int stars = -1, int maxLine = 0)
        {
            if (banner == null || Time.time >= bannerUntil)
            {
                ShowBanner(text, color, seconds, stars, maxLine);
                return;
            }
            if (bannerQueue.Count < 3) bannerQueue.Enqueue(new BannerItem { Text = text, Color = color, Seconds = seconds, Stars = stars, MaxLine = maxLine });
        }

        const float StarFirst = 0.2f, StarGap = 0.15f;

        /// <summary>One rising tick per star as it pops in (0.15 s apart).</summary>
        void StarTicks()
        {
            if (bannerStars < 0 || banner == null || Time.time >= bannerUntil) return;
            while (starTicksPlayed < 3 && Time.time - bannerStart >= StarFirst + StarGap * starTicksPlayed)
            {
                fx.StarTick(starTicksPlayed, (bannerStars & (1 << starTicksPlayed)) != 0);
                starTicksPlayed++;
            }
        }

        /// <summary>Debug export of the log ring: F9 in the editor, or a 3-finger tap on the score. Copies JSON, says "Jurnal copiat".</summary>
        void CheckLogExport()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            bool export = Application.isEditor && Input.GetKeyDown(KeyCode.F9);
            if (!export && Input.touchCount == 3)
            {
                float s = Screen.height / 1000f;
                float top = Screen.height - Screen.safeArea.yMax;
                var scoreRect = new Rect(Screen.width * 0.2f, top, Screen.width * 0.6f, 100 * s);
                for (int i = 0; i < 3; i++)
                {
                    var t = Input.GetTouch(i);
                    if (t.phase == TouchPhase.Began && scoreRect.Contains(new Vector2(t.position.x, Screen.height - t.position.y))) export = true;
                }
            }
            if (!export) return;
            GUIUtility.systemCopyBuffer = UnityProfileStore.LogJson(profile);
            ShowBanner("Jurnal copiat", Color.white, 1.2f);
#endif
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
            EnsureStyles();
            float s = styleScale;
            float w = Screen.width;
            if (launchOverlay)
            {
                DrawLaunchOverlay(w, s);
                return;
            }
            if (game == null) return;
            float top = Screen.height - Screen.safeArea.yMax;

            float danger = game.Danger;
            if (!game.IsOver) DangerBorder(danger);

            // Score (it pulses at 1 Hz while the record is in reach), record chase, cycle and titan number.
            bool chase = !game.IsOver && HudText.ShowPbMarker(game.Score, session.BestAtStart);
            float pulse1Hz = chase && !Settings.ReducedFx ? 0.06f * Mathf.Sin(Time.time * 2f * Mathf.PI) : 0f;
            big.fontSize = Mathf.RoundToInt(54 * s * (1f + 0.3f * scorePunch + pulse1Hz));
            var scoreRect = new Rect(0, top + 10 * s, w, 70 * s);
            if (chase) big.normal.textColor = Color.Lerp(Color.white, ArenaView.BannerGold, 0.35f);
            Shadowed(scoreRect, HudText.Num(Mathf.RoundToInt(shownScore)), big);
            big.normal.textColor = Color.white;
            var under = new GUIStyle(small) { fontSize = Mathf.RoundToInt(16 * s) };
            if (chase)
            {
                under.normal.textColor = ArenaView.BannerGold;
                Shadowed(new Rect(0, top + 76 * s, w, 22 * s), HudText.PbMarker(game.Score, session.BestAtStart), under);
            }
            else if (session.BestAtStart > 0 || game.Score > 0)
                Shadowed(new Rect(0, top + 76 * s, w, 22 * s), "Record " + HudText.Num(Mathf.Max(session.BestAtStart, game.Score)), under);
            else if (profile.legacyBest > 0)
                Shadowed(new Rect(0, top + 76 * s, w, 22 * s), "Record vechi (alt punctaj): " + HudText.Num(profile.legacyBest), under);
            ChainArc(new Vector2(w / 2f + 150 * s, scoreRect.center.y), s);
            int cycle = TitanConfig.CycleIndex(game.Level);
            Shadowed(new Rect(0, top + 98 * s, w, 26 * s), "Ciclul " + (cycle + 1) + " · Titanul #" + game.Level, small);
            CycleDots(new Vector2(w / 2f, top + 136 * s), TitanConfig.CyclePosition(game.Level), s);

            // How close the titan is to the wall.
            var bar = new Rect(w * 0.12f, top + 154 * s, w * 0.76f, 16 * s);
            GUI.color = new Color(0, 0, 0, 0.45f);
            GUI.DrawTexture(bar, white);
            float pulse = danger > 0.75f && !Settings.ReducedFx ? 0.5f + 0.5f * Mathf.Sin(Time.time * 10f) : 1f;
            GUI.color = Color.Lerp(new Color(0.3f, 0.85f, 0.35f), new Color(1f, 0.2f, 0.15f), danger) * new Color(1, 1, 1, pulse);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * danger, bar.height), white);
            GUI.color = Color.white;
            Shadowed(new Rect(bar.xMax - 80 * s, bar.yMax, 80 * s, 22 * s), "ZIDUL", new GUIStyle(small) { alignment = TextAnchor.UpperRight, fontSize = Mathf.RoundToInt(14 * s) });
            if (!game.IsOver) LiveStars(new Vector2(bar.x, bar.yMax + 4 * s), s);
            if (hiddenColors > 0) HiddenChip(new Vector2(w / 2f, bar.yMax + 6 * s), hiddenColors, s);

            if (!game.IsOver) SlotPips(s);

            // Ammo on every cannon. Tray labels count down visibly when salvage takes dead ammo away.
            for (int i = 0; i < game.Tray.Length; i++)
            {
                var c = game.Tray[i];
                var empty = view.EmptyTrayLabel(i);
                if (empty.HasValue && !game.IsOver)
                {
                    var r = new Rect(empty.Value.x - 50 * s, empty.Value.y - 14 * s, 100 * s, 28 * s);
                    GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.8f);
                    GUI.DrawTexture(r, white);
                    GUI.color = Color.white;
                    Shadowed(r, "pachet gol", new GUIStyle(small) { fontSize = Mathf.RoundToInt(15 * s) });
                }
                var pos = view.TrayLabel(i);
                if (c == null || !pos.HasValue) continue;
                AmmoTag(pos.Value, c.Color, view.TrayAmmoShown(i, c.Ammo), TagState.Ready, s);
                if (!game.IsOver) CutTag(pos.Value, c, i, s);
            }
            view.ForEachDyingTrayTag((pos, color, ammo) => AmmoTag(pos, color, ammo, TagState.Ready, s));
            for (int i = 0; i < game.ActiveSlots; i++)
            {
                var c = game.Line[i];
                var pos = view.LineLabel(i);
                if (c == null || !pos.HasValue) continue;
                // "zzz" only for a color that still exists (hidden for now). With salvage a vanished color never stays on the wall.
                var state = game.Monster.HasVisibleOfColor(c.Color) ? TagState.Ready
                    : game.Monster.CountOfColor(c.Color) > 0 ? TagState.Waiting : TagState.Dead;
                AmmoTag(pos.Value, c.Color, c.Ammo, state, s);
            }
            view.ForEachFlyLabel((pos, color, text, alpha) =>
            {
                var r = new Rect(pos.x - 40 * s, pos.y - 34 * s, 80 * s, 30 * s);
                GUI.color = new Color(0, 0, 0, 0.55f * alpha);
                GUI.DrawTexture(r, white);
                var style = new GUIStyle(label);
                style.normal.textColor = Color.Lerp(ArenaView.Palette[color % ArenaView.Palette.Length], Color.white, 0.25f) * new Color(1, 1, 1, alpha);
                GUI.color = Color.white;
                GUI.Label(r, text, style);
            });

            if (banner != null && Time.time < bannerUntil)
            {
                var old = center.normal.textColor;
                center.normal.textColor = bannerColor;
                var bannerRect = new Rect(0, Screen.height * 0.36f, w, 60 * s);
                Shadowed(bannerRect, banner, center);
                center.normal.textColor = old;
                if (bannerStars >= 0) BannerStars(new Vector2(w / 2f, bannerRect.yMax + 6 * s), s);
            }
            if (toast != null && Time.time < toastUntil) Toast(toast, Screen.height * 0.30f, s);

            // Small popups (salvage); piece popups are the one big popup below.
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
                Shadowed(new Rect(sp.x - 150 * s, Screen.height - sp.y - 30 * s - rise * s, 300 * s, 60 * s), p.Text, label);
            }
            DrawBigPopup(s);
            DrawConfetti(s);

            // A held game over shows the revive card instead of the results.
            if (game.IsOver && pendingGameOver == null) DrawGameOver(w, s);
            DrawAdLayer(s);
        }

        enum TagState { Ready, Waiting, Dead }

        void AmmoTag(Vector2 at, int color, int ammo, TagState state, float s)
        {
            var r = new Rect(at.x - 32 * s, at.y - 34 * s, 64 * s, 30 * s);
            GUI.color = state == TagState.Ready ? ArenaView.Palette[color % ArenaView.Palette.Length]
                : state == TagState.Waiting ? new Color(0.25f, 0.25f, 0.28f, 0.85f) : new Color(0.12f, 0.12f, 0.14f, 0.85f);
            GUI.DrawTexture(r, white);
            GUI.color = Color.white;
            Shadowed(r, state == TagState.Waiting ? ammo + " zzz" : ammo.ToString(), label);
        }

        GUIStyle cutStyle;

        /// <summary>
        /// "TAIE!" above a tray cannon whose own ammo cuts a limb when deployed now (Core simulates the targeting on a
        /// copy, so the tag never lies). Otherwise, when its color owns a visible joint and another cannon of that
        /// color is in the tray or on the wall, a hollow "taie în N" (N = shots the cut needs). Otherwise nothing.
        /// </summary>
        void CutTag(Vector2 at, Cannon c, int traySlot, float s)
        {
            var r = new Rect(at.x - 32 * s, at.y - 34 * s - 4 * s - 22 * s, 64 * s, 22 * s);
            if (cutStyle == null || cutStyle.fontSize != Mathf.RoundToInt(18 * s))
                cutStyle = new GUIStyle(label) { fontSize = Mathf.RoundToInt(18 * s), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
            var yellow = new Color(1f, 0.86f, 0.1f);
            if (game.ShotsToSever(c) >= 0)
            {
                float alpha = 0.85f + 0.15f * Mathf.Sin(Time.time * 2f * Mathf.PI * 3f); // 0.7-1.0 at 3 Hz
                GUI.color = new Color(yellow.r, yellow.g, yellow.b, alpha);
                GUI.DrawTexture(r, white);
                GUI.color = Color.white;
                cutStyle.normal.textColor = new Color(0.1f, 0.08f, 0f, alpha);
                GUI.Label(r, "TAIE!", cutStyle);
                return;
            }
            if (!game.Monster.HasVisibleJointOfColor(c.Color)) return;
            int others = 0;
            for (int i = 0; i < game.Tray.Length; i++)
                if (i != traySlot && game.Tray[i] != null && game.Tray[i].Color == c.Color) others += game.Tray[i].Ammo;
            for (int sl = 0; sl < game.ActiveSlots; sl++)
                if (game.Line[sl] != null && game.Line[sl].Color == c.Color) others += game.Line[sl].Ammo;
            if (others == 0) return;
            int shots = game.ShotsToCut(c.Color, game.DeploySlot, c.Ammo + others);
            if (shots <= 0) return;
            float t = Mathf.Max(1f, 2f * s);
            GUI.color = yellow;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), white);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), white);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), white);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), white);
            GUI.color = Color.white;
            cutStyle.normal.textColor = yellow;
            GUI.Label(r, "taie în " + shots, cutStyle);
        }

        /// <summary>
        /// Danger &gt;= 75%: a red border of 4 rects (6% of the screen width), alpha 0.12 + 0.13 sin(8t) (about 1.3 Hz,
        /// under the 3 Hz limit). Static at 0.2 under "Efecte reduse".
        /// </summary>
        void DangerBorder(float danger)
        {
            if (danger < 0.75f) return;
            float a = Settings.ReducedFx ? 0.2f : 0.12f + 0.13f * Mathf.Sin(Time.time * 8f);
            float b = Screen.width * 0.06f;
            GUI.color = new Color(1f, 0.1f, 0.08f, Mathf.Max(0f, a));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, b), white);
            GUI.DrawTexture(new Rect(0, Screen.height - b, Screen.width, b), white);
            GUI.DrawTexture(new Rect(0, b, b, Screen.height - 2 * b), white);
            GUI.DrawTexture(new Rect(Screen.width - b, b, b, Screen.height - 2 * b), white);
            GUI.color = Color.white;
        }

        /// <summary>5 cycle dots: filled for the titans of this cycle already defeated, the 5th is a crown (the boss).</summary>
        void CycleDots(Vector2 center, int position, float s)
        {
            float d = 16 * s, gap = 10 * s;
            float x0 = center.x - (5 * d + 4 * gap) / 2f;
            for (int i = 0; i < 5; i++)
            {
                var r = new Rect(x0 + i * (d + gap), center.y - d / 2f, d, d);
                bool cleared = i < position;
                bool current = i == position;
                if (i == 4)
                {
                    var crownRect = new Rect(r.x - 3 * s, r.y - 4 * s, d + 6 * s, d + 6 * s);
                    GUI.color = new Color(0, 0, 0, 0.5f);
                    GUI.DrawTexture(new Rect(crownRect.x + 2, crownRect.y + 2, crownRect.width, crownRect.height), Glyphs.Crown);
                    GUI.color = cleared ? new Color(1f, 0.82f, 0.2f) : current ? new Color(1f, 0.55f, 0.45f) : new Color(1f, 1f, 1f, 0.45f);
                    GUI.DrawTexture(crownRect, Glyphs.Crown);
                }
                else
                {
                    GUI.color = new Color(0, 0, 0, 0.5f);
                    GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, r.width, r.height), cleared ? Glyphs.Dot : Glyphs.Ring);
                    GUI.color = current ? new Color(1f, 0.9f, 0.4f) : Color.white;
                    GUI.DrawTexture(r, cleared ? Glyphs.Dot : Glyphs.Ring);
                }
            }
            GUI.color = Color.white;
        }

        /// <summary>"{k} culori ascunse ?": colors that exist only inside the titan right now.</summary>
        void HiddenChip(Vector2 topCenter, int k, float s)
        {
            string text = k == 1 ? "1 culoare ascunsă ?" : k + " culori ascunse ?";
            var style = new GUIStyle(small) { fontSize = Mathf.RoundToInt(15 * s) };
            float width = style.CalcSize(new GUIContent(text)).x + 20 * s;
            var r = new Rect(topCenter.x - width / 2f, topCenter.y, width, 24 * s);
            GUI.color = new Color(0.1f, 0.1f, 0.14f, 0.75f);
            GUI.DrawTexture(r, white);
            GUI.color = Color.white;
            Shadowed(r, text, style);
        }

        /// <summary>
        /// Slot pips on the wall's face, one per firing slot (28×28, at least 8 apart): empty = outline, firing = the
        /// cannon's color with its symbol, idle = grey with "zzz". Shape and fill carry the state, not color alone.
        /// The border tells how many wait: white (0-2), amber (3), red pulsing at 4 Hz (4+; static with reduced effects).
        /// </summary>
        void SlotPips(float s)
        {
            int idle = 0;
            var firing = new bool[game.ActiveSlots];
            for (int i = 0; i < game.ActiveSlots; i++)
            {
                var c = game.Line[i];
                if (c == null) continue;
                firing[i] = game.Monster.HasVisibleOfColor(c.Color);
                if (!firing[i]) idle++;
            }
            Color border = idle >= 4 ? new Color(1f, 0.2f, 0.15f) : idle == 3 ? new Color(1f, 0.7f, 0.1f) : Color.white;
            if (idle >= 4 && !Settings.ReducedFx) border.a = 0.55f + 0.45f * Mathf.Sin(Time.time * Mathf.PI * 2f * 4f);
            float size = 28 * s, edge = Mathf.Max(1f, 3 * s);
            var zzz = new GUIStyle(small) { fontSize = Mathf.RoundToInt(11 * s) };
            for (int i = 0; i < game.ActiveSlots; i++)
            {
                var at = view.SlotPip(i);
                if (!at.HasValue) continue;
                var r = new Rect(at.Value.x - size / 2f, at.Value.y - size / 2f, size, size);
                var c = game.Line[i];
                if (c != null)
                {
                    GUI.color = firing[i] ? ArenaView.Palette[c.Color % ArenaView.Palette.Length] : new Color(0.35f, 0.35f, 0.35f);
                    GUI.DrawTexture(r, white);
                    if (firing[i])
                    {
                        var inner = new Rect(r.x + size * 0.18f, r.y + size * 0.18f, size * 0.64f, size * 0.64f);
                        GUI.color = new Color(0f, 0f, 0f, 0.75f);
                        GUI.DrawTexture(inner, Glyphs.Symbol(c.Color));
                    }
                    else
                    {
                        GUI.color = Color.white;
                        GUI.Label(r, "zzz", zzz);
                    }
                }
                GUI.color = border;
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, edge), white);
                GUI.DrawTexture(new Rect(r.x, r.yMax - edge, r.width, edge), white);
                GUI.DrawTexture(new Rect(r.x, r.y, edge, r.height), white);
                GUI.DrawTexture(new Rect(r.xMax - edge, r.y, edge, r.height), white);
            }
            GUI.color = Color.white;
        }
    }
}
