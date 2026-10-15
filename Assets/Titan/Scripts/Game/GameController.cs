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
        Color bannerColor = Color.white;
        float bannerUntil;
        /// <summary>Banners from game events wait their turn (a defeat banner and the next titan's banner come in one frame).</summary>
        readonly Queue<BannerItem> bannerQueue = new Queue<BannerItem>();
        readonly List<PopupText> popups = new List<PopupText>();

        /// <summary>Per-frame HUD cache: colors with cubes but none visible (the "? culori ascunse" chip).</summary>
        int hiddenColors;

        struct BannerItem
        {
            public string Text;
            public Color Color;
            public float Seconds;
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
            view.Banner += QueueBanner;
            screenSize = new Vector2Int(Screen.width, Screen.height);
            shownScore = 0;
            lastScore = 0;
            newBest = false;
            popups.Clear();
            bannerQueue.Clear();
            banner = null;
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
            fx.Heartbeat(game.IsOver ? -1f : game.Danger);
            hiddenColors = game.IsOver ? 0 : game.HiddenOnlyColorCount();
            if ((banner == null || Time.time >= bannerUntil) && bannerQueue.Count > 0)
            {
                var next = bannerQueue.Dequeue();
                ShowBanner(next.Text, next.Color, next.Seconds);
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
            // A refused tap only shakes the cannon and says why. It NEVER opens anything else (no ad card, DESIGN.md 4.2.8).
            switch (game.DeployBlock(i))
            {
                case DeployBlockReason.None:
                    game.Deploy(i);
                    break;
                case DeployBlockReason.WouldJam:
                    view.ShakeTray(i);
                    ShowBanner("Ar bloca ultimul loc! Alege o culoare vizibilă.", ArenaView.BannerGold, 1.6f);
                    break;
                case DeployBlockReason.LineFull:
                    view.ShakeTray(i);
                    ShowBanner("Toate cele " + game.Line.Length + " locuri sunt ocupate!", Color.white, 2f);
                    break;
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

        /// <summary>Shows a banner now, replacing the current one (tap feedback).</summary>
        void ShowBanner(string text, Color color, float seconds)
        {
            banner = text;
            bannerColor = color;
            bannerUntil = Time.time + seconds;
        }

        /// <summary>Game-event banners: shown now if the screen is free, otherwise after the current one (at most 3 waiting).</summary>
        void QueueBanner(string text, Color color, float seconds)
        {
            if (banner == null || Time.time >= bannerUntil)
            {
                ShowBanner(text, color, seconds);
                return;
            }
            if (bannerQueue.Count < 3) bannerQueue.Enqueue(new BannerItem { Text = text, Color = color, Seconds = seconds });
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

            float danger = game.Danger;
            if (!game.IsOver) DangerBorder(danger);

            // Score, record, cycle and titan number.
            big.fontSize = Mathf.RoundToInt(54 * s * (1f + 0.3f * scorePunch));
            Shadowed(new Rect(0, top + 10 * s, w, 70 * s), Mathf.RoundToInt(shownScore).ToString("N0"), big);
            Shadowed(new Rect(0, top + 76 * s, w, 22 * s), "Record " + Mathf.Max(best, game.Score).ToString("N0"), new GUIStyle(small) { fontSize = Mathf.RoundToInt(16 * s) });
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
            for (int i = 0; i < game.Line.Length; i++)
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
                Shadowed(new Rect(0, Screen.height * 0.36f, w, 60 * s), banner, center);
                center.normal.textColor = old;
            }

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
            foreach (var l in game.Line)
                if (l != null && l.Color == c.Color) others += l.Ammo;
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
            var firing = new bool[game.Line.Length];
            for (int i = 0; i < game.Line.Length; i++)
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
            for (int i = 0; i < game.Line.Length; i++)
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

        void DrawGameOver(float w, float s)
        {
            var panel = new Rect(w * 0.08f, Screen.height * 0.3f, w * 0.84f, 380 * s);
            GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);
            GUI.DrawTexture(panel, white);
            GUI.color = Color.white;
            string reason = game.Lost == LoseReason.WallReached ? "Titanul a spart zidul!"
                : game.Lost == LoseReason.Deadlock ? "BLOCAT! Niciun tun nu mai\nare în ce trage."
                : "Cele 5 locuri sunt pline\nși niciun tun nu are în ce trage!";
            Shadowed(new Rect(panel.x, panel.y + 20 * s, panel.width, 80 * s), newBest ? "RECORD NOU!" : reason, center);
            Shadowed(new Rect(panel.x, panel.y + 105 * s, panel.width, 50 * s), "Scor: " + game.Score.ToString("N0"), center);
            Shadowed(new Rect(panel.x, panel.y + 155 * s, panel.width, 30 * s), "Titani doborâți: " + (game.Level - 1) + "   ·   Record: " + best.ToString("N0"), small);
            if (GUI.Button(new Rect(panel.x + 30 * s, panel.y + 240 * s, panel.width - 60 * s, 100 * s), "Joacă din nou", button))
                NewGame();
        }
    }
}
