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
        Material darkMat, metalMat;

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
        }

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
                if (tray[i] == null) continue;
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

        Vector2? Label(CannonView c)
        {
            if (c == null || c.Root == null) return null;
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
                case EventKind.PieceFell: StartCoroutine(Fall(e)); break;
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
                var t = Primitive(PrimitiveType.Cube, "Cube", monsterRoot, cubeMats[kv.Value][rng.Next(3)], false);
                t.localPosition = new Vector3(c.X, c.Y + 0.5f, c.Z + 0.5f);
                t.localScale = Vector3.one * CubeScale;
                cubes[c] = t;
            }
            stepCount = 0;
            FitCamera(level == 1);
            StartCoroutine(Rise(monsterRoot, game.Monster.MaxY + 2f));
            fx.Roar();
            Banner?.Invoke("TITANUL #" + level);
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
                StartCoroutine(PopIn(tray[i].Root, TrayScale, 0.2f + 0.06f * i));
            }
        }

        void Deploy(int traySlot, int slot)
        {
            var c = tray[traySlot];
            tray[traySlot] = null;
            if (c == null) c = MakeCannon(game.Line[slot] != null ? game.Line[slot].Color : 0, trayPos[traySlot], TrayScale);
            line[slot] = c;
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
