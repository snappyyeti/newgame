using System;
using System.Collections;
using System.Collections.Generic;
using Titan.Core;
using UnityEngine;

namespace Titan.Game
{
    /// <summary>
    /// Everything 3D: the arena, the voxel titan, the cannons, projectiles and pieces that break off and fall with physics.
    /// The simulation in <see cref="TitanGame"/> is already resolved; the view plays its events.
    /// </summary>
    public sealed class ArenaView : MonoBehaviour
    {
        const float CubeScale = 0.94f;
        const float ProjectileTime = 0.18f;
        const float StepTime = 0.4f;
        const float WallTop = 1.4f;
        const float TrayZ = -4.2f;
        const float TraySpacing = 3.3f;
        const float TrayScale = 1.35f;
        /// <summary>Salvage timing: the "-N" label flies for 0.3 s, then the ammo label counts down for 0.3 s.</summary>
        const float SalvageFlyTime = 0.3f;
        const float SalvageCountTime = 0.3f;

        public static readonly Color[] Palette =
        {
            new Color(0.93f, 0.23f, 0.21f), // red
            new Color(0.18f, 0.47f, 0.95f), // blue
            new Color(1.00f, 0.82f, 0.12f), // yellow
            new Color(0.24f, 0.78f, 0.30f), // green
            new Color(0.62f, 0.30f, 0.88f), // purple
            new Color(1.00f, 0.52f, 0.10f), // orange
        };

        TitanGame game;
        TitanConfig config;
        Camera cam;
        Fx fx;
        /// <summary>Everything that belongs to one run (titans, cannons, falling pieces). Reset destroys it whole.</summary>
        Transform runRoot;
        bool firstSpawnOfRun;

        /// <summary>Projectiles are pooled; the ones in flight are returned on Reset.</summary>
        readonly Stack<Transform> projectilePool = new Stack<Transform>();
        readonly List<Transform> projectilesInFlight = new List<Transform>();

        /// <summary>Joint stitches and the limb each one holds (gold rim while the limb would still fall clean, CORE-5).</summary>
        readonly List<Stitch> stitches = new List<Stitch>();
        int stitchEpoch = -1;
        Material stitchGoldMat;

        struct Stitch
        {
            public Renderer Band;
            public int Part;
        }

        Material[][] cubeMats;
        Material[] colorMats;
        Material darkMat, metalMat, stitchMat;

        Transform monsterRoot;
        readonly Dictionary<Int3, Transform> cubes = new Dictionary<Int3, Transform>();
        int stepCount;

        // Titan pose: one tween drives the current titan's Z, bob and lean (steps and staggers never fight).
        float riseStart, riseDepth;
        float zShown, zFrom, zTo, zStart, zDuration;
        bool zStagger;
        float sway;
        bool poseFrozen;

        Vector3[] slotPos;
        Vector3[] trayPos;
        CannonView[] line;
        CannonView[] tray;

        Vector3 cameraTarget;
        Vector3 cameraBase;
        bool cameraReady;

        public event Action<Vector3, string> Popup;
        /// <summary>A piece started to fall: where (world, above its center) and its PieceFell event (points, clean, chain).</summary>
        public event Action<Vector3, GameEvent> PiecePopup;
        /// <summary>Text, color, seconds. The HUD queues these behind the banner on screen.</summary>
        public event Action<string, Color, float> Banner;

        public static readonly Color BannerWhite = Color.white;
        public static readonly Color BannerGold = new Color(1f, 0.82f, 0.2f);
        public static readonly Color BannerGreen = new Color(0.45f, 1f, 0.45f);
        public static readonly Color BannerRed = new Color(1f, 0.35f, 0.3f);

        /// <summary>The "pachet gol" ring shown on a tray slot the empty deck could not refill.</summary>
        Transform[] emptyRings;
        /// <summary>The 6th firing slot ("+1 Loc", ADS-2): a pad with a gold trim at x = 7.2, open until the titan falls.</summary>
        Transform extraPad;
        float extraPadScale = 1f;

        sealed class CannonView
        {
            public Transform Root;
            public Transform Pivot;
            public int Color;
            /// <summary>Ammo label: shows LabelFrom until LabelStart, then counts to LabelTo over SalvageCountTime.</summary>
            public int LabelFrom, LabelTo;
            public float LabelStart;
            /// <summary>Hidden (no label, no taps) until this time, while it pops in behind a salvaged cannon.</summary>
            public float ShowFrom;

            public int ShownAmmo
            {
                get
                {
                    float k = Mathf.Clamp01((Time.time - LabelStart) / SalvageCountTime);
                    return Mathf.RoundToInt(Mathf.Lerp(LabelFrom, LabelTo, k));
                }
            }
        }

        /// <summary>A "-N" that flies from a fallen piece to the tray cannon losing that ammo.</summary>
        sealed class FlyLabel
        {
            public Vector3 From;
            public CannonView Target;
            public int Color;
            public string Text;
            public float Start;
        }

        readonly List<FlyLabel> flyLabels = new List<FlyLabel>();
        /// <summary>Tray cannons emptied by salvage: still drawn (label counting to 0) until they shrink away.</summary>
        readonly List<CannonView> dyingTray = new List<CannonView>();
        Vector3 lastPieceCenter;
        float lastPieceTime = -10f;

        // ---------------------------------------------------------------- building

        /// <summary>Builds the arena once per app run: ground, wall, slot and tray pads, materials. Runs come and go through <see cref="Reset"/>.</summary>
        public void Build(TitanConfig cfg, Camera camera, Fx effects)
        {
            config = cfg;
            cam = camera;
            fx = effects;

            cubeMats = new Material[Palette.Length][];
            colorMats = new Material[Palette.Length];
            for (int c = 0; c < Palette.Length; c++)
            {
                colorMats[c] = MakeMaterial(Palette[c]);
                // Three shades per color give the voxels a hand-made look.
                cubeMats[c] = new[]
                {
                    MakeMaterial(Palette[c] * 0.92f),
                    colorMats[c],
                    MakeMaterial(Color.Lerp(Palette[c], Color.white, 0.12f)),
                };
            }
            darkMat = MakeMaterial(new Color(0.20f, 0.21f, 0.24f));
            stitchMat = MakeMaterial(Color.white);
            stitchGoldMat = MakeMaterial(new Color(1f, 0.78f, 0.15f));
            metalMat = MakeMaterial(new Color(0.55f, 0.57f, 0.62f));

            BuildArena();
            // Until the first titan fits the camera (the launch overlay shows over an empty arena).
            cam.transform.SetPositionAndRotation(new Vector3(0f, 12f, -22f), Quaternion.Euler(20f, 0f, 0f));

            // LineSlots pads plus the "+1 Loc" one at SlotX(LineSlots) = 7.2 (hidden until granted; nobody moves).
            int slots = config.LineSlots + 1;
            slotPos = new Vector3[slots];
            line = new CannonView[slots];
            for (int s = 0; s < slots; s++)
            {
                slotPos[s] = new Vector3(config.SlotX(s), WallTop, -0.5f);
                if (s == config.LineSlots)
                {
                    extraPad = BuildExtraPad(slotPos[s]);
                    continue;
                }
                var pad = Primitive(PrimitiveType.Cylinder, "SlotPad", transform, metalMat, false);
                pad.position = slotPos[s] + Vector3.up * 0.02f;
                pad.localScale = new Vector3(1.6f, 0.03f, 1.6f);
            }

            trayPos = new Vector3[config.TraySize];
            tray = new CannonView[trayPos.Length];
            emptyRings = new Transform[trayPos.Length];
            var ringMat = MakeMaterial(new Color(0.12f, 0.12f, 0.14f));
            for (int i = 0; i < trayPos.Length; i++)
            {
                trayPos[i] = new Vector3((i - (trayPos.Length - 1) / 2f) * TraySpacing, 0f, TrayZ);
                var pad = Primitive(PrimitiveType.Cylinder, "TrayPad", transform, MakeMaterial(new Color(0.42f, 0.36f, 0.30f)), false);
                pad.position = trayPos[i] + Vector3.up * 0.03f;
                pad.localScale = new Vector3(2.4f, 0.05f, 2.4f);
                emptyRings[i] = BuildRing(trayPos[i], ringMat);
            }
        }

        /// <summary>
        /// Fast restart (RET-2): stops every coroutine of the old run, returns projectiles in flight to the pool and
        /// destroys the old run's titans, cannons and falling pieces in one go. The camera, wall, pads, rings and
        /// materials stay. The new game's first events (TitanSpawned, TrayRefilled) then build the titan and the tray.
        /// </summary>
        public void Reset(TitanGame titanGame)
        {
            StopAllCoroutines();
            foreach (var p in projectilesInFlight) ReturnProjectile(p);
            projectilesInFlight.Clear();
            if (runRoot != null) Destroy(runRoot.gameObject);
            runRoot = new GameObject("Run").transform;
            runRoot.SetParent(transform, false);

            game = titanGame;
            monsterRoot = null;
            cubes.Clear();
            stitches.Clear();
            stitchEpoch = -1;
            for (int s = 0; s < line.Length; s++) line[s] = null;
            for (int i = 0; i < tray.Length; i++) tray[i] = null;
            flyLabels.Clear();
            dyingTray.Clear();
            lastPieceTime = -10f;
            poseFrozen = false;
            firstSpawnOfRun = true;
            foreach (var ring in emptyRings) ring.gameObject.SetActive(false);
            extraPad.gameObject.SetActive(false);
            extraPad.localScale = Vector3.one * extraPadScale;
        }

        /// <summary>The 6th pad at x = 7.2 (on the wall, which spans x = -12..12): a slot pad on a gold trim ring.</summary>
        Transform BuildExtraPad(Vector3 at)
        {
            var root = new GameObject("ExtraSlotPad").transform;
            root.SetParent(transform, false);
            root.position = at;
            var gold = MakeMaterial(new Color(1f, 0.78f, 0.15f));
            var trim = Primitive(PrimitiveType.Cylinder, "GoldTrim", root, gold, false);
            trim.localPosition = Vector3.up * 0.015f;
            trim.localScale = new Vector3(1.9f, 0.025f, 1.9f);
            var pad = Primitive(PrimitiveType.Cylinder, "SlotPad", root, metalMat, false);
            pad.localPosition = Vector3.up * 0.03f;
            pad.localScale = new Vector3(1.6f, 0.03f, 1.6f);
            root.gameObject.SetActive(false);
            return root;
        }

        /// <summary>A dark ring of 16 blocks lying on a tray pad.</summary>
        Transform BuildRing(Vector3 center, Material mat)
        {
            var root = new GameObject("EmptyDeckRing").transform;
            root.SetParent(transform, false);
            root.position = center + Vector3.up * 0.1f;
            const int n = 16;
            for (int k = 0; k < n; k++)
            {
                float a = k * Mathf.PI * 2f / n;
                var b = Primitive(PrimitiveType.Cube, "RingBlock", root, mat, false);
                b.localPosition = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.8f;
                b.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
                b.localScale = new Vector3(0.18f, 0.08f, 0.34f);
            }
            root.gameObject.SetActive(false);
            return root;
        }

        void BuildArena()
        {
            var ground = Primitive(PrimitiveType.Cube, "Ground", transform, MakeMaterial(new Color(0.36f, 0.52f, 0.30f)), true);
            ground.position = new Vector3(0, -0.5f, 35f);
            ground.localScale = new Vector3(120f, 1f, 140f);

            var path = Primitive(PrimitiveType.Cube, "Path", transform, MakeMaterial(new Color(0.62f, 0.53f, 0.40f)), false);
            path.position = new Vector3(0, 0.005f, 30f);
            path.localScale = new Vector3(30f, 0.01f, 60f);

            var stoneA = MakeMaterial(new Color(0.58f, 0.58f, 0.60f));
            var stoneB = MakeMaterial(new Color(0.50f, 0.50f, 0.53f));
            for (int i = -6; i <= 6; i++)
            {
                var block = Primitive(PrimitiveType.Cube, "Wall", transform, (i & 1) == 0 ? stoneA : stoneB, true);
                block.position = new Vector3(i * 2f, WallTop / 2f, -0.5f);
                block.localScale = new Vector3(1.96f, WallTop, 1f);
            }
        }

        // ---------------------------------------------------------------- camera

        /// <summary>Fixed camera behind the wall, pulled back until the titan fits both at spawn and at the wall.</summary>
        void FitCamera(bool instant)
        {
            var rot = Quaternion.Euler(20f, 0, 0);
            cam.transform.rotation = rot;
            var m = game.Monster;
            float halfW = Mathf.Max(Mathf.Abs(m.MinX), m.MaxX) + 0.5f;
            float top = m.MaxY + 1f;
            float far = config.StartDistance + m.MaxZ + 1f;

            var points = new List<Vector3>
            {
                trayPos[0] + new Vector3(-1.3f, 0, -1.3f), trayPos[trayPos.Length - 1] + new Vector3(1.3f, 0, -1.3f),
                new Vector3(-halfW, top, far), new Vector3(halfW, top, far),
                new Vector3(-halfW, top, 0.5f), new Vector3(halfW, top, 0.5f),
                // The right edge is the "+1 Loc" slot at x = 7.2 (+ margin), even while it is closed, so the camera
                // never moves when it opens (ADS-2).
                slotPos[0] + Vector3.left * 1.2f, slotPos[slotPos.Length - 1] + Vector3.right * 1.2f,
            };
            var target = new Vector3(0, top * 0.35f, far * 0.3f);

            float lo = 2f, hi = 200f;
            for (int i = 0; i < 32; i++)
            {
                float mid = (lo + hi) / 2f;
                cam.transform.position = target - rot * Vector3.forward * mid;
                if (AllVisible(points)) hi = mid;
                else lo = mid;
            }
            cameraTarget = target - rot * Vector3.forward * hi;
            if (instant || !cameraReady)
            {
                cameraBase = cameraTarget;
                cameraReady = true;
            }
            cam.transform.position = cameraBase;
            fx.SetCameraBase(cameraBase);
        }

        bool AllVisible(List<Vector3> points)
        {
            foreach (var w in points)
            {
                var v = cam.WorldToViewportPoint(w);
                if (v.z <= 0 || v.x < 0.02f || v.x > 0.98f || v.y < 0.03f || v.y > 0.84f) return false;
            }
            return true;
        }

        public void Refit() => FitCamera(true);

        void Update()
        {
            if (game == null) return;
            UpdateTitanPose();
            UpdateStitches();
            for (int i = 0; i < emptyRings.Length; i++)
            {
                bool empty = game.Tray[i] == null && tray[i] == null && !DyingAt(i);
                if (emptyRings[i].gameObject.activeSelf != empty) emptyRings[i].gameObject.SetActive(empty);
            }
            if (!cameraReady) return;
            cameraBase = Vector3.Lerp(cameraBase, cameraTarget, 1f - Mathf.Exp(-Time.deltaTime * 2f));
            fx.SetCameraBase(cameraBase);
        }

        /// <summary>Gold rim on a joint's stitch while its limb would still fall clean (≥ 80% left); white after.</summary>
        void UpdateStitches()
        {
            if (game.Monster.Epoch == stitchEpoch) return;
            stitchEpoch = game.Monster.Epoch;
            foreach (var st in stitches)
            {
                if (st.Band == null) continue;
                var mat = game.IsCleanEligible(st.Part) ? stitchGoldMat : stitchMat;
                if (st.Band.sharedMaterial != mat) st.Band.sharedMaterial = mat;
            }
        }

        bool DyingAt(int i)
        {
            foreach (var d in dyingTray)
                if (d.Root != null && (d.Root.position - trayPos[i]).sqrMagnitude < 0.01f) return true;
            return false;
        }

        /// <summary>
        /// The current titan's pose: it rises out of the ground for 1 s after spawning, then each step is a 0.4 s
        /// smoothstep with a bob and a sideways sway, and each stagger a 0.3 s ease-out back with a 10° back-lean.
        /// </summary>
        void UpdateTitanPose()
        {
            if (monsterRoot == null || poseFrozen) return;
            float now = Time.time;
            float y = 0f;
            float r = Mathf.Clamp01((now - riseStart) / 1.0f);
            if (r < 1f) y = Mathf.Lerp(-riseDepth, 0f, 1f - (1f - r) * (1f - r));
            float k = zDuration > 0f ? Mathf.Clamp01((now - zStart) / zDuration) : 1f;
            float ease = zStagger ? 1f - (1f - k) * (1f - k) : k * k * (3f - 2f * k);
            zShown = Mathf.Lerp(zFrom, zTo, ease);
            var rot = Quaternion.identity;
            if (k < 1f)
            {
                float arc = Mathf.Sin(k * Mathf.PI);
                if (zStagger) rot = Quaternion.Euler(10f * arc, 0f, 0f); // leans back, away from the wall
                else
                {
                    y += arc * 0.35f;
                    rot = Quaternion.Euler(0f, 0f, arc * sway);
                }
            }
            monsterRoot.position = new Vector3(0f, y, zShown);
            monsterRoot.rotation = rot;
        }

        void MoveTitanTo(float distance, float duration, bool stagger)
        {
            zFrom = zShown;
            zTo = distance;
            zStart = Time.time;
            zDuration = duration;
            zStagger = stagger;
        }

        /// <summary>Screen position (GUI coordinates) of slot s's pip: centered on the wall's front face under the slot.</summary>
        public Vector2? SlotPip(int s)
        {
            var p = cam.WorldToScreenPoint(new Vector3(slotPos[s].x, WallTop * 0.5f, -1.0f));
            if (p.z <= 0) return null;
            return new Vector2(p.x, Screen.height - p.y);
        }

        /// <summary>Screen position of the "pachet gol" label of tray slot i, or null when the slot holds a cannon.</summary>
        public Vector2? EmptyTrayLabel(int i)
        {
            if (!emptyRings[i].gameObject.activeSelf) return null;
            var p = cam.WorldToScreenPoint(trayPos[i] + Vector3.up * 0.9f);
            if (p.z <= 0) return null;
            return new Vector2(p.x, Screen.height - p.y);
        }

        // ---------------------------------------------------------------- input and labels

        /// <summary>GUI-space (y down) tray hit circles: the same centers and radius as <see cref="TrayAtScreen"/>.</summary>
        bool CircleHitsRect(Vector2 guiCenter, float radius, Rect r)
        {
            float cx = Mathf.Clamp(guiCenter.x, r.xMin, r.xMax), cy = Mathf.Clamp(guiCenter.y, r.yMin, r.yMax);
            return (new Vector2(cx, cy) - guiCenter).sqrMagnitude < radius * radius;
        }

        /// <summary>True when a GUI rect overlaps any tray hit area (the "+1 LOC" button must never do that).</summary>
        public bool OverlapsTray(Rect guiRect)
        {
            float radius = Screen.height * 0.09f;
            for (int i = 0; i < trayPos.Length; i++)
            {
                var p = cam.WorldToScreenPoint(trayPos[i] + Vector3.up * 0.7f);
                if (p.z > 0 && CircleHitsRect(new Vector2(p.x, Screen.height - p.y), radius, guiRect)) return true;
            }
            return false;
        }

        /// <summary>
        /// Where the gold "+1 LOC" button goes (GUI rect, y down): on the wall, right of slot 5, above the wall line
        /// (the half of the wall area away from the tray), clamped inside the safe area; moved up if it would touch a
        /// tray hit area. Needs an editor check at 9:19.5 and 9:16 (DESIGN.md 10.2 B).
        /// </summary>
        public Rect WallButtonRect(float size)
        {
            var safe = Screen.safeArea;
            var slot = cam.WorldToScreenPoint(slotPos[config.LineSlots]);
            var wallTop = cam.WorldToScreenPoint(new Vector3(slotPos[config.LineSlots].x, WallTop, -0.5f));
            float x = slot.x + size * 0.15f;
            float y = (Screen.height - wallTop.y) - size * 1.9f; // GUI y of the wall line, then up
            var r = new Rect(x, y, size, size);
            r.x = Mathf.Clamp(r.x, safe.xMin + 4f, safe.xMax - size - 4f);
            r.y = Mathf.Clamp(r.y, Screen.height - safe.yMax + 4f, Screen.height - safe.yMin - size - 4f);
            for (int k = 0; k < 20 && OverlapsTray(r); k++) r.y -= size * 0.25f;
            return r;
        }

        public int TrayAtScreen(Vector2 screen)
        {
            int best = -1;
            float bestD = Screen.height * 0.09f;
            for (int i = 0; i < tray.Length; i++)
            {
                if (tray[i] == null || Time.time < tray[i].ShowFrom) continue;
                var p = cam.WorldToScreenPoint(trayPos[i] + Vector3.up * 0.7f);
                float d = Vector2.Distance(screen, p);
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>GUI position (y down) of tray slot i's hit circle center: the same circle <see cref="TrayAtScreen"/> uses.</summary>
        public Vector2? TrayHitCenter(int i)
        {
            if (trayPos == null || i < 0 || i >= trayPos.Length) return null;
            var p = cam.WorldToScreenPoint(trayPos[i] + Vector3.up * 0.7f);
            if (p.z <= 0) return null;
            return new Vector2(p.x, Screen.height - p.y);
        }

        /// <summary>Radius (pixels) of the tray hit circles.</summary>
        public float TrayHitRadius => Screen.height * 0.09f;

        /// <summary>GUI y of the top of the tray area (the highest hit circle's top): hints stay above it.</summary>
        public float TrayTopGui()
        {
            float top = Screen.height;
            for (int i = 0; i < trayPos.Length; i++)
            {
                var c = TrayHitCenter(i);
                if (c.HasValue) top = Mathf.Min(top, c.Value.y - TrayHitRadius);
            }
            return top;
        }

        /// <summary>GUI position of firing slot s (the cannon's middle on the wall), or null.</summary>
        public Vector2? LineSlotCenter(int s)
        {
            if (slotPos == null || s < 0 || s >= slotPos.Length) return null;
            var p = cam.WorldToScreenPoint(slotPos[s] + Vector3.up * 0.6f);
            if (p.z <= 0) return null;
            return new Vector2(p.x, Screen.height - p.y);
        }

        /// <summary>Screen position (GUI coordinates, y down) above a tray cannon or a line cannon, or null.</summary>
        public Vector2? TrayLabel(int i) => Label(tray[i]);
        public Vector2? LineLabel(int s) => Label(line[s]);

        /// <summary>The ammo the tray label shows now (it counts down after a salvage), or the game's value.</summary>
        public int TrayAmmoShown(int i, int actual) => tray[i] != null ? tray[i].ShownAmmo : actual;

        /// <summary>Calls <paramref name="draw"/>(label position, color, shown ammo) for tray cannons emptied by salvage.</summary>
        public void ForEachDyingTrayTag(Action<Vector2, int, int> draw)
        {
            foreach (var c in dyingTray)
            {
                var p = Label(c);
                if (p.HasValue) draw(p.Value, c.Color, c.ShownAmmo);
            }
        }

        /// <summary>Calls <paramref name="draw"/>(screen position, color, text, alpha) for every flying "-N" label.</summary>
        public void ForEachFlyLabel(Action<Vector2, int, string, float> draw)
        {
            for (int i = flyLabels.Count - 1; i >= 0; i--)
            {
                var f = flyLabels[i];
                float k = (Time.time - f.Start) / SalvageFlyTime;
                if (k > 1.15f || f.Target.Root == null)
                {
                    flyLabels.RemoveAt(i);
                    continue;
                }
                if (k < 0f) continue;
                var to = Label(f.Target);
                var from = cam.WorldToScreenPoint(f.From);
                if (!to.HasValue || from.z <= 0) continue;
                float e = Mathf.Clamp01(k);
                e = 1f - (1f - e) * (1f - e);
                var start = new Vector2(from.x, Screen.height - from.y);
                var pos = Vector2.Lerp(start, to.Value, e) + Vector2.up * (-40f * Screen.height / 1000f * 4f * e * (1f - e));
                draw(pos, f.Color, f.Text, k > 1f ? 1f - (k - 1f) / 0.15f : 1f);
            }
        }

        Vector2? Label(CannonView c)
        {
            if (c == null || c.Root == null || Time.time < c.ShowFrom) return null;
            var p = cam.WorldToScreenPoint(c.Root.position + Vector3.up * (1.45f * c.Root.localScale.y));
            if (p.z <= 0) return null;
            return new Vector2(p.x, Screen.height - p.y);
        }

        public void ShakeTray(int i)
        {
            if (tray[i] != null) StartCoroutine(Wobble(tray[i].Root));
        }

        // ---------------------------------------------------------------- events

        public void Play(GameEvent e)
        {
            switch (e.Kind)
            {
                case EventKind.TitanSpawned: SpawnTitan(e.Level, e.Role); break;
                case EventKind.TrayRefilled:
                    if (e.TraySlot >= 0) RefillSlot(e.TraySlot);
                    else RebuildTray();
                    break;
                case EventKind.Staggered: Stagger(e); break;
                case EventKind.Deployed: Deploy(e.TraySlot, e.Slot); break;
                case EventKind.Shot: Shoot(e); break;
                case EventKind.PieceFell:
                    RememberPiece(e.Piece);
                    StartCoroutine(Fall(e));
                    break;
                case EventKind.Salvaged:
                    if (e.Slot >= 0) SalvageLine(e);
                    else if (e.TraySlot >= 0) SalvageTray(e);
                    break;
                case EventKind.CannonEmpty: StartCoroutine(RetireCannon(e.Slot)); break;
                case EventKind.Step: StepTo(e.Distance); break;
                case EventKind.SlotAdded: StartCoroutine(UnfoldExtraPad()); break;
                case EventKind.Revived: Revived(e); break;
                case EventKind.TitanDefeated: Defeated(e); break;
                case EventKind.GameOver: StartCoroutine(GameOver(e.Reason)); break;
            }
        }

        /// <summary>The 6th pad unfolds from the wall in 0.3 s (scale 0 → 1.15 → 1).</summary>
        IEnumerator UnfoldExtraPad()
        {
            extraPad.gameObject.SetActive(true);
            extraPad.localScale = Vector3.zero;
            yield return StartCoroutine(PopIn(extraPad, extraPadScale, 0.3f));
        }

        /// <summary>The 6th pad folds away when the next titan comes (its cannon leaves with the others).</summary>
        IEnumerator FoldExtraPad()
        {
            float time = 0f;
            while (time < 0.3f && extraPad.gameObject.activeSelf)
            {
                time += Time.deltaTime;
                extraPad.localScale = Vector3.one * extraPadScale * (1f - Mathf.Clamp01(time / 0.3f));
                yield return null;
            }
            extraPad.gameObject.SetActive(false);
            extraPad.localScale = Vector3.one * extraPadScale;
        }

        /// <summary>
        /// "A doua suflare": the titan stumbles back (0.5 s ease-out with the back-lean) and the sleeping cannons fly
        /// from the wall to the tray and vanish into the deck.
        /// </summary>
        void Revived(GameEvent e)
        {
            poseFrozen = false;
            MoveTitanTo(e.Distance, 0.5f, true);
            fx.Roar();
            fx.Shake(0.2f, 0.4f);
            for (int s = 0; s < line.Length; s++)
            {
                if (line[s] == null || game.Line[s] != null) continue;
                StartCoroutine(FlyIntoDeck(line[s].Root, 0.15f * s));
                line[s] = null;
            }
        }

        IEnumerator FlyIntoDeck(Transform t, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (t == null) yield break;
            yield return StartCoroutine(Move(t, new Vector3(0f, 0.6f, TrayZ - 1.5f), 0.45f, 3f, TrayScale));
            if (t != null) yield return StartCoroutine(ShrinkAway(t, 0.2f));
        }

        void SpawnTitan(int level, TitanRole role)
        {
            if (monsterRoot != null) Destroy(monsterRoot.gameObject, 1.5f);
            if (extraPad.gameObject.activeSelf) StartCoroutine(FoldExtraPad());
            cubes.Clear();
            for (int s = 0; s < line.Length; s++)
            {
                if (line[s] != null) StartCoroutine(ShrinkAway(line[s].Root, 0.3f));
                line[s] = null;
            }

            monsterRoot = new GameObject("Titan" + level).transform;
            monsterRoot.SetParent(runRoot, false);
            zShown = zFrom = zTo = game.Distance;
            zDuration = 0f;
            poseFrozen = false;
            riseStart = Time.time;
            riseDepth = game.Monster.MaxY + 2f;
            monsterRoot.position = new Vector3(0, -riseDepth, zShown);
            var rng = new System.Random(level * 7919);
            foreach (var kv in game.Monster.Cubes)
            {
                var c = kv.Key;
                var mat = cubeMats[kv.Value][rng.Next(3)];
                var t = Primitive(PrimitiveType.Cube, "Cube", monsterRoot, mat, false);
                t.localPosition = new Vector3(c.X, c.Y + 0.5f, c.Z + 0.5f);
                t.localScale = Vector3.one * CubeScale;
                if (game.Monster.IsJoint(c)) AddStitch(t, LimbOf(c));
                else if (game.Monster.IsGlove(c)) MakeGlove(t, mat);
                cubes[c] = t;
            }
            stepCount = 0;
            stitchEpoch = -1;
            FitCamera(firstSpawnOfRun);
            firstSpawnOfRun = false;
            switch (role)
            {
                case TitanRole.Sprint:
                    fx.SprintCue();
                    Banner?.Invoke("TITAN RAPID!", BannerGreen, 1.2f);
                    break;
                case TitanRole.Boss:
                    fx.Roar();
                    fx.Shake(0.3f, 0.6f);
                    Banner?.Invoke("TITAN GIGANT!", BannerRed, 1.8f);
                    break;
                default:
                    fx.Roar();
                    Banner?.Invoke("TITANUL #" + level, BannerWhite, 1.4f);
                    break;
            }
        }

        /// <summary>A cut pushed the titan back: it staggers to its new Z with a back-lean; a big piece makes it roar.</summary>
        void Stagger(GameEvent e)
        {
            MoveTitanTo(e.Distance, 0.3f, true);
            if (e.Piece != null && e.Piece.Count >= 20) fx.Roar();
            fx.Shake(0.15f, 0.25f);
        }

        /// <summary>The "cusătură" (stitch): a thin band around a joint cube (white, gold while the limb is clean-eligible). The cube keeps its color.</summary>
        void AddStitch(Transform cube, int limb)
        {
            var band = Primitive(PrimitiveType.Cube, "Stitch", cube, stitchMat, false);
            band.localPosition = Vector3.zero;
            band.localScale = new Vector3(1.04f, 0.14f, 1.04f);
            if (limb > Monster.Body) stitches.Add(new Stitch { Band = band.GetComponent<Renderer>(), Part = limb });
        }

        /// <summary>The limb a joint holds: the part of its first non-body neighbour (joints themselves are tagged body).</summary>
        int LimbOf(Int3 joint)
        {
            foreach (var d in Int3.Neighbours)
            {
                var n = joint + d;
                if (!game.Monster.Has(n)) continue;
                int part = game.Monster.PartOf(n);
                if (part > Monster.Body) return part;
            }
            return Monster.Body;
        }

        /// <summary>A hand cube: an inset cube of the same material inside a dark edge frame. A shape cue, same luminance.</summary>
        void MakeGlove(Transform cube, Material mat)
        {
            cube.GetComponent<Renderer>().enabled = false;
            var inset = Primitive(PrimitiveType.Cube, "Glove", cube, mat, false);
            inset.localScale = Vector3.one * 0.9f;
            const float edge = 0.07f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                // Front face frame (facing the wall) and the four edges running back along Z.
                var vertical = Primitive(PrimitiveType.Cube, "Edge", cube, darkMat, false);
                vertical.localPosition = new Vector3(sx * 0.48f, 0f, -0.48f);
                vertical.localScale = new Vector3(edge, 1f, edge);
                var horizontal = Primitive(PrimitiveType.Cube, "Edge", cube, darkMat, false);
                horizontal.localPosition = new Vector3(0f, sx * 0.48f, -0.48f);
                horizontal.localScale = new Vector3(1f, edge, edge);
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    var depth = Primitive(PrimitiveType.Cube, "Edge", cube, darkMat, false);
                    depth.localPosition = new Vector3(sx * 0.48f, sy * 0.48f, 0f);
                    depth.localScale = new Vector3(edge, edge, 1f);
                }
            }
        }

        void RebuildTray()
        {
            for (int i = 0; i < tray.Length; i++)
            {
                if (tray[i] != null) Destroy(tray[i].Root.gameObject);
                tray[i] = null;
                var data = game.Tray[i];
                if (data == null) continue;
                tray[i] = MakeCannon(data.Color, trayPos[i], TrayScale);
                SetLabel(tray[i], data.Ammo);
                // Behind a cannon that salvage is emptying, the new one waits until the old one has shrunk away.
                float wait = 0f;
                foreach (var d in dyingTray)
                    if (d.Root != null && (d.Root.position - trayPos[i]).sqrMagnitude < 0.01f)
                        wait = Mathf.Max(wait, d.LabelStart + SalvageCountTime + 0.25f - Time.time);
                tray[i].ShowFrom = Time.time + wait;
                StartCoroutine(PopInLater(tray[i].Root, TrayScale, 0.2f + 0.06f * i, wait));
            }
        }

        /// <summary>Conveyor refill of one slot: the new cannon rises from 2.5 units below its pad in 0.18 s.</summary>
        void RefillSlot(int i)
        {
            if (tray[i] != null && tray[i].Root != null) Destroy(tray[i].Root.gameObject);
            tray[i] = null;
            var data = game.Tray[i];
            if (data == null) return; // the empty deck: the ring and "pachet gol" show instead
            var c = MakeCannon(data.Color, trayPos[i] + Vector3.down * 2.5f, TrayScale);
            tray[i] = c;
            SetLabel(c, data.Ammo);
            float wait = 0f;
            foreach (var d in dyingTray)
                if (d.Root != null && (d.Root.position - trayPos[i]).sqrMagnitude < 0.01f)
                    wait = Mathf.Max(wait, d.LabelStart + SalvageCountTime + 0.25f - Time.time);
            c.ShowFrom = Time.time + wait;
            StartCoroutine(RiseInto(c.Root, trayPos[i], 0.18f, wait));
        }

        IEnumerator RiseInto(Transform t, Vector3 target, float duration, float delay)
        {
            if (delay > 0f)
            {
                t.localScale = Vector3.zero;
                yield return new WaitForSeconds(delay);
                if (t == null) yield break;
                t.localScale = Vector3.one * TrayScale;
            }
            fx.TrayRise();
            var start = t.position;
            float time = 0f;
            while (time < duration && t != null)
            {
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / duration);
                t.position = Vector3.Lerp(start, target, 1f - (1f - k) * (1f - k));
                yield return null;
            }
            if (t != null) t.position = target;
        }

        static void SetLabel(CannonView c, int ammo)
        {
            c.LabelFrom = c.LabelTo = ammo;
            c.LabelStart = 0f;
        }

        // ---------------------------------------------------------------- salvage

        void RememberPiece(FallenPiece piece)
        {
            var center = Vector3.zero;
            int n = 0;
            foreach (var c in piece.Cubes)
                if (cubes.TryGetValue(c, out var t) && t != null)
                {
                    center += t.position;
                    n++;
                }
            if (n == 0) return;
            lastPieceCenter = center / n;
            lastPieceTime = Time.time;
        }

        /// <summary>A line cannon whose color is gone: spins once and flies up off the wall, "RECUPERAT +N".</summary>
        void SalvageLine(GameEvent e)
        {
            var c = line[e.Slot];
            line[e.Slot] = null;
            fx.Salvage();
            Popup?.Invoke(slotPos[e.Slot] + Vector3.up * 2.2f, "RECUPERAT +" + e.Points);
            if (c != null) StartCoroutine(SpinAndFly(c.Root, 0.35f, 6f));
        }

        /// <summary>A tray cannon loses dead ammo: "-N" flies from the fallen piece to it, then its label counts down.</summary>
        void SalvageTray(GameEvent e)
        {
            var c = tray[e.TraySlot];
            if (c == null) return;
            fx.Salvage();
            // The piece starts to fall one projectile flight after the cut; the label leaves with it.
            float start = Time.time + (Time.time - lastPieceTime < 0.05f ? ProjectileTime : 0f);
            var from = Time.time - lastPieceTime < 0.05f ? lastPieceCenter
                : (monsterRoot != null ? monsterRoot.position + Vector3.up * game.Monster.MaxY * 0.6f : Vector3.up * 5f);
            flyLabels.Add(new FlyLabel { From = from, Target = c, Color = e.Color, Text = "-" + e.Ammo, Start = start });
            c.LabelFrom = c.ShownAmmo;
            c.LabelTo = Mathf.Max(0, c.LabelTo - e.Ammo);
            c.LabelStart = start + SalvageFlyTime;
            if (c.LabelTo > 0) return;
            // Emptied: it stays until its label reaches 0, then shrinks away (the slot may refill right behind it).
            tray[e.TraySlot] = null;
            dyingTray.Add(c);
            StartCoroutine(RetireDying(c, c.LabelStart + SalvageCountTime - Time.time));
        }

        IEnumerator RetireDying(CannonView c, float wait)
        {
            if (wait > 0f) yield return new WaitForSeconds(wait);
            yield return StartCoroutine(ShrinkAway(c.Root, 0.25f));
            dyingTray.Remove(c);
        }

        static IEnumerator SpinAndFly(Transform t, float duration, float height)
        {
            if (t == null) yield break;
            var start = t.position;
            var rot = t.rotation;
            float time = 0f;
            while (time < duration && t != null)
            {
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / duration);
                t.position = start + Vector3.up * height * k * k;
                t.rotation = rot * Quaternion.Euler(0f, 360f * k, 0f);
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        void Deploy(int traySlot, int slot)
        {
            var c = tray[traySlot];
            tray[traySlot] = null;
            if (c == null) c = MakeCannon(game.Line[slot] != null ? game.Line[slot].Color : 0, trayPos[traySlot], TrayScale);
            line[slot] = c;
            c.ShowFrom = 0f;
            StartCoroutine(Move(c.Root, slotPos[slot], 0.25f, 2.5f, 1f));
            fx.Deploy();
        }

        void Shoot(GameEvent e)
        {
            if (!cubes.TryGetValue(e.Cube, out var cube)) return;
            cubes.Remove(e.Cube);
            var cannon = line[e.Slot];
            var from = cannon != null ? cannon.Pivot.position + cannon.Pivot.up * 0.9f : slotPos[e.Slot] + Vector3.up;
            if (cannon != null)
            {
                cannon.Pivot.rotation = Quaternion.FromToRotation(Vector3.up, (cube.position - cannon.Pivot.position).normalized);
                StartCoroutine(Recoil(cannon.Pivot));
            }
            fx.Shot();
            StartCoroutine(Projectile(from, cube, e.Color));
        }

        IEnumerator Projectile(Vector3 from, Transform cube, int color)
        {
            var bullet = TakeProjectile(colorMats[color]);
            bullet.position = from;
            float t = 0f;
            while (t < ProjectileTime && cube != null)
            {
                t += Time.deltaTime;
                bullet.position = Vector3.Lerp(from, cube.position, Mathf.Clamp01(t / ProjectileTime));
                yield return null;
            }
            projectilesInFlight.Remove(bullet);
            ReturnProjectile(bullet);
            if (cube == null) yield break;
            fx.Burst(cube.position, colorMats[color], 6, 6f);
            fx.Hit();
            StartCoroutine(PopAndDestroy(cube));
        }

        Transform TakeProjectile(Material mat)
        {
            Transform bullet;
            if (projectilePool.Count > 0)
            {
                bullet = projectilePool.Pop();
                bullet.gameObject.SetActive(true);
            }
            else
            {
                bullet = Primitive(PrimitiveType.Sphere, "Shot", transform, mat, false);
                bullet.localScale = Vector3.one * 0.35f;
            }
            bullet.GetComponent<Renderer>().sharedMaterial = mat;
            projectilesInFlight.Add(bullet);
            return bullet;
        }

        void ReturnProjectile(Transform bullet)
        {
            if (bullet == null) return;
            bullet.gameObject.SetActive(false);
            projectilePool.Push(bullet);
        }

        IEnumerator Fall(GameEvent e)
        {
            var piece = e.Piece;
            var parts = new List<Transform>();
            foreach (var c in piece.Cubes)
            {
                if (!cubes.TryGetValue(c, out var t)) continue;
                cubes.Remove(c);
                parts.Add(t);
            }
            yield return new WaitForSeconds(ProjectileTime);
            if (parts.Count == 0) yield break;

            var center = Vector3.zero;
            foreach (var p in parts) center += p.position;
            center /= parts.Count;

            // The piece becomes one rigid body made of its cubes and tumbles down.
            var root = new GameObject("Piece").transform;
            root.SetParent(runRoot, false);
            root.position = center;
            foreach (var p in parts)
            {
                if (p == null) continue;
                p.SetParent(root, true);
                p.gameObject.AddComponent<BoxCollider>();
            }
            var body = root.gameObject.AddComponent<Rigidbody>();
            body.mass = Mathf.Max(1f, parts.Count * 0.5f);
            float side = Mathf.Sign(center.x);
            body.AddForce(new Vector3(side * UnityEngine.Random.Range(1f, 3f), UnityEngine.Random.Range(2f, 4f), -UnityEngine.Random.Range(2f, 4f)), ForceMode.VelocityChange);
            body.AddTorque(UnityEngine.Random.insideUnitSphere * 3f, ForceMode.VelocityChange);

            fx.Crash(parts.Count);
            PiecePopup?.Invoke(center + Vector3.up * 1.5f, e);

            yield return new WaitForSeconds(2.6f);
            if (root == null) yield break;
            foreach (Transform p in root) StartCoroutine(ShrinkAway(p, 0.5f));
            Destroy(root.gameObject, 0.6f);
        }

        IEnumerator RetireCannon(int slot)
        {
            var c = line[slot];
            line[slot] = null;
            if (c == null) yield break;
            yield return new WaitForSeconds(ProjectileTime);
            if (c.Root != null) StartCoroutine(ShrinkAway(c.Root, 0.25f));
        }

        void StepTo(float distance)
        {
            if (monsterRoot == null) return;
            stepCount++;
            sway = (stepCount & 1) == 0 ? 2.5f : -2.5f;
            fx.Step(1f - distance / config.StartDistance);
            MoveTitanTo(distance, StepTime, false);
        }

        /// <summary>The defeat banner (with the titan's stars) is the HUD's; the view plays the fanfare.</summary>
        void Defeated(GameEvent e)
        {
            if (e.NearMiss) fx.NearMissFanfare();
            else fx.Fanfare();
        }

        IEnumerator GameOver(LoseReason reason)
        {
            if (reason == LoseReason.Completed)
            {
                fx.RecordFanfare(); // Titanul Zilei won: the last titan fell, nothing smashes the wall
                yield break;
            }
            fx.Lose();
            if (reason != LoseReason.WallReached || monsterRoot == null) yield break;
            // The titan smashes into the wall.
            poseFrozen = true;
            var root = monsterRoot;
            float t = 0f;
            var start = root.position;
            while (t < 0.35f && root != null)
            {
                t += Time.deltaTime;
                root.position = start + new Vector3(0, 0, -1.2f * Mathf.Sin(Mathf.Clamp01(t / 0.35f) * Mathf.PI));
                yield return null;
            }
        }

        // ---------------------------------------------------------------- cannons

        CannonView MakeCannon(int color, Vector3 position, float scale)
        {
            var root = new GameObject("Cannon").transform;
            root.SetParent(runRoot, false);
            root.position = position;
            root.localScale = Vector3.one * scale;

            var basePart = Primitive(PrimitiveType.Cube, "Base", root, darkMat, false);
            basePart.localPosition = new Vector3(0, 0.25f, 0);
            basePart.localScale = new Vector3(0.95f, 0.5f, 0.95f);

            var band = Primitive(PrimitiveType.Cube, "Band", root, colorMats[color], false);
            band.localPosition = new Vector3(0, 0.52f, 0);
            band.localScale = new Vector3(1.0f, 0.08f, 1.0f);

            var pivot = new GameObject("Pivot").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(0, 0.6f, 0);
            pivot.localRotation = Quaternion.Euler(55f, 0, 0);

            var barrel = Primitive(PrimitiveType.Cylinder, "Barrel", pivot, colorMats[color], false);
            barrel.localPosition = new Vector3(0, 0.45f, 0);
            barrel.localScale = new Vector3(0.42f, 0.45f, 0.42f);

            var muzzle = Primitive(PrimitiveType.Cylinder, "Muzzle", pivot, darkMat, false);
            muzzle.localPosition = new Vector3(0, 0.9f, 0);
            muzzle.localScale = new Vector3(0.48f, 0.06f, 0.48f);

            return new CannonView { Root = root, Pivot = pivot, Color = color };
        }

        // ---------------------------------------------------------------- tweens

        static IEnumerator Move(Transform t, Vector3 target, float duration, float arc, float endScale)
        {
            var start = t.position;
            var startScale = t.localScale;
            float time = 0f;
            while (time < duration && t != null)
            {
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / duration);
                t.position = Vector3.Lerp(start, target, k) + Vector3.up * arc * 4f * k * (1f - k);
                t.localScale = Vector3.Lerp(startScale, Vector3.one * endScale, k);
                yield return null;
            }
            if (t == null) yield break;
            t.position = target;
            t.localScale = Vector3.one * endScale;
        }

        static IEnumerator Recoil(Transform pivot)
        {
            var rest = pivot.localPosition;
            float time = 0f;
            while (time < 0.1f && pivot != null)
            {
                time += Time.deltaTime;
                pivot.localPosition = rest - pivot.parent.InverseTransformDirection(pivot.up) * 0.12f * Mathf.Sin(Mathf.Clamp01(time / 0.1f) * Mathf.PI);
                yield return null;
            }
            if (pivot != null) pivot.localPosition = rest;
        }

        static IEnumerator PopIn(Transform t, float scale, float duration)
        {
            float time = 0f;
            while (time < duration && t != null)
            {
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / duration);
                t.localScale = Vector3.one * scale * (k < 0.75f ? k / 0.75f * 1.15f : Mathf.Lerp(1.15f, 1f, (k - 0.75f) / 0.25f));
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one * scale;
        }

        IEnumerator PopInLater(Transform t, float scale, float duration, float delay)
        {
            if (delay > 0f)
            {
                t.localScale = Vector3.zero;
                yield return new WaitForSeconds(delay);
            }
            if (t != null) yield return StartCoroutine(PopIn(t, scale, duration));
        }

        static IEnumerator PopAndDestroy(Transform t)
        {
            float time = 0f;
            var s = t.localScale;
            while (time < 0.08f && t != null)
            {
                time += Time.deltaTime;
                t.localScale = s * (1f + 0.4f * (time / 0.08f));
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        static IEnumerator ShrinkAway(Transform t, float duration)
        {
            if (t == null) yield break;
            var s = t.localScale;
            float time = 0f;
            while (time < duration && t != null)
            {
                time += Time.deltaTime;
                t.localScale = s * (1f - Mathf.Clamp01(time / duration));
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        static IEnumerator Wobble(Transform t)
        {
            var rest = t.position;
            float time = 0f;
            while (time < 0.25f && t != null)
            {
                time += Time.deltaTime;
                t.position = rest + Vector3.right * Mathf.Sin(time * 60f) * 0.15f * (1f - time / 0.25f);
                yield return null;
            }
            if (t != null) t.position = rest;
        }

        // ---------------------------------------------------------------- primitives

        static Material defaultMaterial;

        /// <summary>Copies the render pipeline's default material, so this works with both the built-in renderer and URP.</summary>
        static Material MakeMaterial(Color color)
        {
            if (defaultMaterial == null)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                defaultMaterial = probe.GetComponent<Renderer>().sharedMaterial;
                Destroy(probe);
            }
            color.a = 1f;
            return new Material(defaultMaterial) { color = color };
        }

        static Transform Primitive(PrimitiveType type, string name, Transform parent, Material material, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) DestroyImmediate(col);
            }
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
