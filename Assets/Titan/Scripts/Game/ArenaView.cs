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
        Camera cam;
        Fx fx;

        Material[][] cubeMats;
        Material[] colorMats;
        Material darkMat, metalMat, stitchMat;

        Transform monsterRoot;
        readonly Dictionary<Int3, Transform> cubes = new Dictionary<Int3, Transform>();
        float monsterZ;
        int stepCount;

        Vector3[] slotPos;
        Vector3[] trayPos;
        CannonView[] line;
        CannonView[] tray;

        Vector3 cameraTarget;
        Vector3 cameraBase;
        bool cameraReady;

        public event Action<Vector3, string> Popup;
        public event Action<string> Banner;

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

        public void Build(TitanGame titanGame, Camera camera, Fx effects)
        {
            game = titanGame;
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
            metalMat = MakeMaterial(new Color(0.55f, 0.57f, 0.62f));

            BuildArena();

            int slots = game.Config.LineSlots;
            slotPos = new Vector3[slots];
            line = new CannonView[slots];
            for (int s = 0; s < slots; s++)
            {
                slotPos[s] = new Vector3(game.SlotX(s), WallTop, -0.5f);
                var pad = Primitive(PrimitiveType.Cylinder, "SlotPad", transform, metalMat, false);
                pad.position = slotPos[s] + Vector3.up * 0.02f;
                pad.localScale = new Vector3(1.6f, 0.03f, 1.6f);
            }

            trayPos = new Vector3[game.Config.TraySize];
            tray = new CannonView[trayPos.Length];
            for (int i = 0; i < trayPos.Length; i++)
            {
                trayPos[i] = new Vector3((i - (trayPos.Length - 1) / 2f) * TraySpacing, 0f, TrayZ);
                var pad = Primitive(PrimitiveType.Cylinder, "TrayPad", transform, MakeMaterial(new Color(0.42f, 0.36f, 0.30f)), false);
                pad.position = trayPos[i] + Vector3.up * 0.03f;
                pad.localScale = new Vector3(2.4f, 0.05f, 2.4f);
            }
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
            float far = game.Config.StartDistance + m.MaxZ + 1f;

            var points = new List<Vector3>
            {
                trayPos[0] + new Vector3(-1.3f, 0, -1.3f), trayPos[trayPos.Length - 1] + new Vector3(1.3f, 0, -1.3f),
                new Vector3(-halfW, top, far), new Vector3(halfW, top, far),
                new Vector3(-halfW, top, 0.5f), new Vector3(halfW, top, 0.5f),
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
            if (!cameraReady) return;
            cameraBase = Vector3.Lerp(cameraBase, cameraTarget, 1f - Mathf.Exp(-Time.deltaTime * 2f));
            fx.SetCameraBase(cameraBase);
        }

        // ---------------------------------------------------------------- input and labels

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
                case EventKind.TitanSpawned: SpawnTitan(e.Level); break;
                case EventKind.TrayRefilled: RebuildTray(); break;
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
                case EventKind.Step: StartCoroutine(StepTo(e.Distance)); break;
                case EventKind.TitanDefeated: Defeated(e); break;
                case EventKind.GameOver: StartCoroutine(GameOver(e.Reason)); break;
            }
        }

        void SpawnTitan(int level)
        {
            if (monsterRoot != null) Destroy(monsterRoot.gameObject, 1.5f);
            cubes.Clear();
            for (int s = 0; s < line.Length; s++)
            {
                if (line[s] != null) StartCoroutine(ShrinkAway(line[s].Root, 0.3f));
                line[s] = null;
            }

            monsterRoot = new GameObject("Titan" + level).transform;
            monsterRoot.SetParent(transform, false);
            monsterZ = game.Distance;
            monsterRoot.position = new Vector3(0, 0, monsterZ);
            var rng = new System.Random(level * 7919);
            foreach (var kv in game.Monster.Cubes)
            {
                var c = kv.Key;
                var mat = cubeMats[kv.Value][rng.Next(3)];
                var t = Primitive(PrimitiveType.Cube, "Cube", monsterRoot, mat, false);
                t.localPosition = new Vector3(c.X, c.Y + 0.5f, c.Z + 0.5f);
                t.localScale = Vector3.one * CubeScale;
                if (game.Monster.IsJoint(c)) AddStitch(t);
                else if (game.Monster.IsGlove(c)) MakeGlove(t, mat);
                cubes[c] = t;
            }
            stepCount = 0;
            FitCamera(level == 1);
            StartCoroutine(Rise(monsterRoot, game.Monster.MaxY + 2f));
            fx.Roar();
            Banner?.Invoke("TITANUL #" + level);
        }

        /// <summary>The "cusătură" (stitch): a thin white band around a joint cube. The cube keeps its color.</summary>
        void AddStitch(Transform cube)
        {
            var band = Primitive(PrimitiveType.Cube, "Stitch", cube, stitchMat, false);
            band.localPosition = Vector3.zero;
            band.localScale = new Vector3(1.04f, 0.14f, 1.04f);
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
            var bullet = Primitive(PrimitiveType.Sphere, "Shot", transform, colorMats[color], false);
            bullet.localScale = Vector3.one * 0.35f;
            float t = 0f;
            while (t < ProjectileTime && cube != null)
            {
                t += Time.deltaTime;
                bullet.position = Vector3.Lerp(from, cube.position, Mathf.Clamp01(t / ProjectileTime));
                yield return null;
            }
            Destroy(bullet.gameObject);
            if (cube == null) yield break;
            fx.Burst(cube.position, colorMats[color], 6, 6f);
            fx.Hit();
            StartCoroutine(PopAndDestroy(cube));
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
            root.SetParent(transform, false);
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
            Popup?.Invoke(center + Vector3.up * 1.5f, "BUCATĂ RUPTĂ!\n+" + e.Points);

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

        IEnumerator StepTo(float distance)
        {
            if (monsterRoot == null) yield break;
            var root = monsterRoot;
            float from = monsterZ;
            monsterZ = distance;
            stepCount++;
            float sway = (stepCount & 1) == 0 ? 2.5f : -2.5f;
            float danger = 1f - distance / game.Config.StartDistance;
            fx.Step(danger);
            float t = 0f;
            while (t < StepTime && root != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / StepTime);
                float ease = k * k * (3f - 2f * k);
                root.position = new Vector3(0, Mathf.Sin(k * Mathf.PI) * 0.35f, Mathf.Lerp(from, distance, ease));
                root.rotation = Quaternion.Euler(0, 0, Mathf.Sin(k * Mathf.PI) * sway);
                yield return null;
            }
            if (root != null)
            {
                root.position = new Vector3(0, 0, distance);
                root.rotation = Quaternion.identity;
            }
        }

        void Defeated(GameEvent e)
        {
            fx.Fanfare();
            Banner?.Invoke("TITAN DOBORÂT!  +" + e.Points);
        }

        IEnumerator GameOver(LoseReason reason)
        {
            fx.Lose();
            if (reason != LoseReason.WallReached || monsterRoot == null) yield break;
            // The titan smashes into the wall.
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
            root.SetParent(transform, false);
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

        static IEnumerator Rise(Transform root, float depth)
        {
            float time = 0f;
            const float duration = 1.0f;
            var end = root.position;
            while (time < duration && root != null)
            {
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / duration);
                float ease = 1f - (1f - k) * (1f - k);
                root.position = new Vector3(end.x, Mathf.Lerp(-depth, 0f, ease), root.position.z);
                yield return null;
            }
            if (root != null) root.position = new Vector3(end.x, 0f, root.position.z);
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
