using System;
using System.Collections;
using System.Collections.Generic;
using SplashSort.Core;
using UnityEngine;

namespace SplashSort.Game
{
    /// <summary>Marks a spawned swimmer with its color so the view can be checked against the board.</summary>
    public sealed class Swimmer : MonoBehaviour
    {
        public int Color;
    }

    /// <summary>
    /// Everything 3D, built from grey primitives: the 3×4 pools, the stacks of swimmers (colored discs),
    /// the tray of 3 rings and the animations. The logic is already resolved when an animation plays.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public const float DiscHeight = 0.13f;
        public const float DiscRadius = 0.42f;
        const float Spacing = 1.55f;
        const float PoolTop = 0.05f;
        const float TrayScale = 0.7f;
        const float DragHeight = 0.9f;

        public const float HopTime = 0.15f;
        const float HopStagger = 0.04f;
        const float DropTime = 0.12f;
        const float SplashTime = 0.40f;

        static readonly Color[] Palette =
        {
            new Color(0.92f, 0.22f, 0.22f), // red
            new Color(0.20f, 0.48f, 0.96f), // blue
            new Color(0.98f, 0.84f, 0.15f), // yellow
            new Color(0.25f, 0.80f, 0.32f), // green
            new Color(0.64f, 0.32f, 0.88f), // purple
            new Color(1.00f, 0.55f, 0.12f), // orange
        };

        SplashConfig cfg;
        Camera cam;
        Feedback fx;

        Vector3[] poolPos;
        Material[] rimMats;
        Material[] colorMats;
        List<Transform>[] stacks;

        Vector3[] trayPos;
        List<Transform>[] trayRings;

        Color rimIdle = new Color(0.62f, 0.64f, 0.68f);
        Color rimValid = new Color(0.25f, 0.9f, 0.35f);

        /// <summary>World position and text of a floating label (score, cascade).</summary>
        public event Action<Vector3, string> Popup;

        public void Build(SplashConfig config, Camera camera, Feedback feedback)
        {
            cfg = config;
            cam = camera;
            fx = feedback;

            colorMats = new Material[Palette.Length];
            for (int i = 0; i < Palette.Length; i++) colorMats[i] = MakeMaterial(Palette[i]);

            var floorMat = MakeMaterial(new Color(0.30f, 0.32f, 0.36f));
            var floor = Primitive(PrimitiveType.Cube, "Floor", transform, floorMat);
            floor.localPosition = new Vector3(0, -0.06f, -0.9f);
            floor.localScale = new Vector3(cfg.Columns * Spacing + 1f, 0.1f, cfg.Rows * Spacing + 4.2f);

            int n = cfg.Columns * cfg.Rows;
            poolPos = new Vector3[n];
            rimMats = new Material[n];
            stacks = new List<Transform>[n];
            var waterMat = MakeMaterial(new Color(0.45f, 0.47f, 0.52f));
            for (int i = 0; i < n; i++)
            {
                int r = i / cfg.Columns, c = i % cfg.Columns;
                poolPos[i] = new Vector3((c - (cfg.Columns - 1) / 2f) * Spacing, 0, ((cfg.Rows - 1) / 2f - r) * Spacing);
                stacks[i] = new List<Transform>();

                rimMats[i] = MakeMaterial(rimIdle);
                var rim = Primitive(PrimitiveType.Cylinder, "Pool" + i, transform, rimMats[i]);
                rim.localPosition = poolPos[i] + Vector3.up * 0.01f;
                rim.localScale = new Vector3(1.3f, 0.03f, 1.3f);

                var water = Primitive(PrimitiveType.Cylinder, "Water", transform, waterMat);
                water.localPosition = poolPos[i] + Vector3.up * 0.03f;
                water.localScale = new Vector3(1.1f, 0.02f, 1.1f);
            }

            float trayZ = poolPos[n - 1].z - Spacing * 1.35f;
            trayPos = new Vector3[cfg.TraySize];
            trayRings = new List<Transform>[cfg.TraySize];
            var padMat = MakeMaterial(new Color(0.40f, 0.42f, 0.46f));
            for (int s = 0; s < cfg.TraySize; s++)
            {
                trayPos[s] = new Vector3((s - (cfg.TraySize - 1) / 2f) * Spacing * 1.15f, 0, trayZ);
                trayRings[s] = new List<Transform>();
                var pad = Primitive(PrimitiveType.Cylinder, "TrayPad", transform, padMat);
                pad.localPosition = trayPos[s] + Vector3.up * 0.01f;
                pad.localScale = new Vector3(1.0f, 0.02f, 1.0f);
            }

            FitCamera();
        }

        // ---------------------------------------------------------------- camera and picking

        public void FitCamera()
        {
            var rot = Quaternion.Euler(58f, 0, 0);
            cam.transform.rotation = rot;
            var target = new Vector3(0, 0.6f, -0.6f);

            var points = new List<Vector3>();
            foreach (var p in poolPos)
            {
                points.Add(p + new Vector3(-0.7f, 0, -0.7f));
                points.Add(p + new Vector3(0.7f, 0, 0.7f));
                points.Add(p + Vector3.up * (DiscHeight * 12f));
            }
            foreach (var t in trayPos)
            {
                points.Add(t + new Vector3(-0.6f, 0, -0.6f));
                points.Add(t + new Vector3(0.6f, 0, 0.6f));
            }

            float lo = 2f, hi = 80f;
            for (int i = 0; i < 30; i++)
            {
                float mid = (lo + hi) / 2f;
                cam.transform.position = target - rot * Vector3.forward * mid;
                if (AllVisible(points)) hi = mid;
                else lo = mid;
            }
            cam.transform.position = target - rot * Vector3.forward * hi;
            fx.SetCameraBase(cam.transform.position);
        }

        bool AllVisible(List<Vector3> points)
        {
            foreach (var w in points)
            {
                var v = cam.WorldToViewportPoint(w);
                // Room for the score at the top.
                if (v.z <= 0 || v.x < 0.03f || v.x > 0.97f || v.y < 0.03f || v.y > 0.84f) return false;
            }
            return true;
        }

        float PickRadiusPixels()
        {
            var a = cam.WorldToScreenPoint(poolPos[0]);
            var b = cam.WorldToScreenPoint(poolPos[1]);
            return Vector2.Distance(a, b) * 0.55f;
        }

        /// <summary>The pool under the pointer, or -1.</summary>
        public int PoolAtScreen(Vector2 screen)
        {
            int best = -1;
            float bestD = PickRadiusPixels();
            for (int i = 0; i < poolPos.Length; i++)
            {
                float d = Vector2.Distance(screen, cam.WorldToScreenPoint(poolPos[i] + Vector3.up * 0.2f));
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>The tray slot under the pointer that still has a ring, or -1.</summary>
        public int TraySlotAtScreen(Vector2 screen)
        {
            int best = -1;
            float bestD = PickRadiusPixels() * 1.1f;
            for (int s = 0; s < trayPos.Length; s++)
            {
                if (trayRings[s].Count == 0) continue;
                float d = Vector2.Distance(screen, cam.WorldToScreenPoint(trayPos[s] + Vector3.up * 0.3f));
                if (d < bestD)
                {
                    bestD = d;
                    best = s;
                }
            }
            return best;
        }

        public void Highlight(int pool, bool valid)
        {
            for (int i = 0; i < rimMats.Length; i++)
                rimMats[i].color = i == pool && valid ? rimValid : rimIdle;
        }

        // ---------------------------------------------------------------- tray and dragging

        public void RebuildTray(Ring[] tray, bool pop)
        {
            for (int s = 0; s < trayRings.Length; s++)
            {
                foreach (var t in trayRings[s]) if (t != null) Destroy(t.gameObject);
                trayRings[s].Clear();
                if (tray[s] == null) continue;
                for (int i = 0; i < tray[s].Size; i++)
                {
                    var t = SpawnSwimmer(tray[s].Colors[i]);
                    t.localPosition = trayPos[s] + Vector3.up * (PoolTop + DiscHeight * TrayScale * (i + 0.5f));
                    t.localScale = DiscScale() * TrayScale;
                    trayRings[s].Add(t);
                    if (pop) StartCoroutine(PopIn(t, TrayScale, 0.15f + 0.05f * s));
                }
            }
        }

        /// <summary>Moves the dragged ring under the pointer, a bit above the finger so it stays visible.</summary>
        public void DragTo(int slot, Vector2 screen)
        {
            var ray = cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0, DragHeight, 0));
            if (!plane.Raycast(ray, out float enter)) return;
            var basePos = ray.GetPoint(enter) + Vector3.forward * 0.6f + Vector3.down * DragHeight;
            var list = trayRings[slot];
            for (int i = 0; i < list.Count; i++)
            {
                var target = basePos + Vector3.up * (DragHeight + DiscHeight * (i + 0.5f));
                list[i].localPosition = Vector3.Lerp(list[i].localPosition, target, 0.6f);
                list[i].localScale = DiscScale();
            }
        }

        public void ReturnToTray(int slot)
        {
            var list = trayRings[slot];
            for (int i = 0; i < list.Count; i++)
            {
                var target = trayPos[slot] + Vector3.up * (PoolTop + DiscHeight * TrayScale * (i + 0.5f));
                StartCoroutine(Move(list[i], target, 0.12f, 0f, 0.2f));
                list[i].localScale = DiscScale() * TrayScale;
            }
        }

        // ---------------------------------------------------------------- board

        Vector3 SlotPos(int pool, int index) => poolPos[pool] + Vector3.up * (PoolTop + DiscHeight * (index + 0.5f));

        public void ClearBoard()
        {
            foreach (var s in stacks)
            {
                foreach (var t in s) if (t != null) Destroy(t.gameObject);
                s.Clear();
            }
        }

        /// <summary>Makes the pools match the board exactly. Runs after every animation as a safety net.</summary>
        public void SyncBoard(Board board)
        {
            for (int p = 0; p < board.Count; p++)
            {
                var s = stacks[p];
                bool same = s.Count == board.Height(p);
                for (int i = 0; same && i < s.Count; i++)
                    same = s[i] != null && s[i].GetComponent<Swimmer>().Color == board.Stack(p)[i];
                if (!same)
                {
                    foreach (var t in s) if (t != null) Destroy(t.gameObject);
                    s.Clear();
                    for (int i = 0; i < board.Height(p); i++) s.Add(SpawnSwimmer(board.Stack(p)[i]));
                }
                for (int i = 0; i < s.Count; i++)
                {
                    s[i].localPosition = SlotPos(p, i);
                    s[i].localScale = DiscScale();
                }
            }
        }

        /// <summary>Drop the ring, then play every merge hop and every SPLASH wave in order.</summary>
        public IEnumerator PlayPlace(int slot, int pool, PlaceResult result, Board finalBoard, Action<int> onPoints)
        {
            var ring = trayRings[slot];
            trayRings[slot] = new List<Transform>();
            for (int i = 0; i < ring.Count; i++)
            {
                stacks[pool].Add(ring[i]);
                ring[i].localScale = DiscScale();
                StartCoroutine(Move(ring[i], SlotPos(pool, i), DropTime, 0f, 0f));
            }
            yield return new WaitForSeconds(DropTime);
            fx.Land();

            var events = result.Events;
            int e = 1; // event 0 is the placement
            while (e < events.Count)
            {
                var ev = events[e];
                if (ev.Kind == BoardEventKind.Merge)
                {
                    yield return PlayMerge(ev);
                    e++;
                    continue;
                }

                // All splashes of one cascade wave go off together.
                int step = ev.Step;
                int wavePoints = 0;
                while (e < events.Count && events[e].Kind == BoardEventKind.Splash && events[e].Step == step)
                {
                    PlaySplash(events[e]);
                    wavePoints += events[e].Points;
                    e++;
                }
                fx.Splash(step);
                onPoints?.Invoke(wavePoints);
                yield return new WaitForSeconds(SplashTime);
            }

            SyncBoard(finalBoard);
        }

        IEnumerator PlayMerge(BoardEvent ev)
        {
            var from = stacks[ev.From];
            var to = stacks[ev.To];
            int start = to.Count;
            // Swimmers leave from the top, so the top one hops first and lands lowest in its new stack.
            for (int i = 0; i < ev.Count; i++)
            {
                var t = from[from.Count - 1];
                from.RemoveAt(from.Count - 1);
                to.Add(t);
                StartCoroutine(Move(t, SlotPos(ev.To, start + i), HopTime, i * HopStagger, 0.6f));
                StartCoroutine(HopSound(i * HopStagger));
            }
            yield return new WaitForSeconds(HopTime + HopStagger * (ev.Count - 1));
        }

        IEnumerator HopSound(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            fx.Hop();
        }

        void PlaySplash(BoardEvent ev)
        {
            var s = stacks[ev.From];
            var run = s.GetRange(s.Count - ev.Count, ev.Count);
            s.RemoveRange(s.Count - ev.Count, ev.Count);

            var top = SlotPos(ev.From, s.Count + ev.Count);
            fx.Burst(top, colorMats[ev.Color % colorMats.Length], 26);
            for (int i = 0; i < run.Count; i++)
            {
                var t = run[i];
                var jump = new Vector3(UnityEngine.Random.Range(-1.2f, 1.2f), UnityEngine.Random.Range(1.2f, 2.4f), UnityEngine.Random.Range(-0.6f, 1.2f));
                StartCoroutine(Move(t, t.localPosition + jump, SplashTime, i * 0.01f, 0.8f));
                StartCoroutine(ShrinkAndDestroy(t, SplashTime));
            }

            string label = "+" + ev.Points;
            if (ev.Step > 1) label = "CASCADĂ x" + ev.Step + "\n" + label;
            Popup?.Invoke(top + Vector3.up * 0.8f, label);
        }

        // ---------------------------------------------------------------- tweens

        static IEnumerator Move(Transform t, Vector3 target, float duration, float delay, float arc)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (t == null) yield break;
            var start = t.localPosition;
            float time = 0f;
            while (time < duration && t != null)
            {
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / duration);
                t.localPosition = Vector3.Lerp(start, target, k) + Vector3.up * arc * 4f * k * (1f - k);
                yield return null;
            }
            if (t != null) t.localPosition = target;
        }

        static IEnumerator PopIn(Transform t, float scale, float duration)
        {
            var full = t.localScale;
            float time = 0f;
            while (time < duration && t != null)
            {
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / duration);
                t.localScale = full * (k < 0.8f ? k / 0.8f * 1.1f : Mathf.Lerp(1.1f, 1f, (k - 0.8f) / 0.2f));
                yield return null;
            }
            if (t != null) t.localScale = full;
        }

        static IEnumerator ShrinkAndDestroy(Transform t, float duration)
        {
            var start = t != null ? t.localScale : Vector3.one;
            float time = 0f;
            while (time < duration && t != null)
            {
                time += Time.deltaTime;
                t.localScale = start * (1f - Mathf.Clamp01(time / duration));
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        // ---------------------------------------------------------------- primitives

        static Vector3 DiscScale() => new Vector3(DiscRadius * 2f, DiscHeight * 0.45f, DiscRadius * 2f);

        Transform SpawnSwimmer(int color)
        {
            var t = Primitive(PrimitiveType.Cylinder, "Swimmer" + color, transform, colorMats[color % colorMats.Length]);
            t.gameObject.AddComponent<Swimmer>().Color = color;
            t.localScale = DiscScale();
            return t;
        }

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
            return new Material(defaultMaterial) { color = color };
        }

        static Transform Primitive(PrimitiveType type, string name, Transform parent, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
