using System;
using System.Collections;
using System.Collections.Generic;
using HumanCannon.Core;
using UnityEngine;

namespace HumanCannon.Game
{
    /// <summary>Marks a spawned person with its color so the view can be checked against the board.</summary>
    public sealed class Person : MonoBehaviour
    {
        public int Color;
    }

    /// <summary>
    /// Everything 3D: the arena built from primitives (grey prototype, MVP step 1), the people and the animations.
    /// The logic is already resolved when an animation plays; the view only follows the events.
    /// </summary>
    public sealed class ArenaView : MonoBehaviour
    {
        public const float PersonHeight = 0.34f;
        public const float PersonWidth = 0.34f;
        const float BaseTop = 0.12f;
        const float ArcRadius = 4.2f;
        const float ArcHalfAngle = 60f;

        // Timings from the design doc.
        public const float FlightTime = 0.30f;
        public const float FlightStagger = 0.06f;
        public const float CannonRefillTime = 0.15f;
        public const float ClearTime = 0.40f;
        public const float ChainStepTime = 0.35f;
        public const float EndShakeTime = 0.50f;

        static readonly Color[] Palette =
        {
            new Color(0.90f, 0.20f, 0.20f), // red
            new Color(0.20f, 0.45f, 0.95f), // blue
            new Color(0.98f, 0.85f, 0.15f), // yellow
            new Color(0.25f, 0.80f, 0.30f), // green
            new Color(0.62f, 0.30f, 0.85f), // purple
            new Color(1.00f, 0.55f, 0.10f), // orange
        };

        public static Color ColorOf(int c) => Palette[c % Palette.Length];

        GameConfig cfg;
        Camera cam;
        Vector3[] pedestalPos;
        Vector3[] outward;
        Material[] ringMats;
        Material[] poleMats;
        Material[] colorMats;
        List<Transform>[] stacks;

        Transform barrel;
        Vector3 barrelRest;
        Vector3 cannonMouth;
        Vector3 reservePos;
        Vector3[] queuePos;
        Material reservePadMat;

        readonly List<Transform> cannonPeople = new List<Transform>();
        readonly List<Transform> reservePeople = new List<Transform>();
        readonly List<Transform>[] queuePeople = { new List<Transform>(), new List<Transform>(), new List<Transform>(), new List<Transform>() };

        public event Action<Vector3, string> Popup;

        public int PedestalCount => pedestalPos.Length;

        public void Build(GameConfig config, Camera camera)
        {
            cfg = config;
            cam = camera;
            int n = cfg.PedestalCount;

            colorMats = new Material[Palette.Length];
            for (int c = 0; c < Palette.Length; c++) colorMats[c] = MakeMaterial(Palette[c]);

            var floor = Primitive(PrimitiveType.Cylinder, "Ring floor", transform, MakeMaterial(new Color(0.45f, 0.12f, 0.14f)));
            floor.localPosition = new Vector3(0, -0.05f, 2.2f);
            floor.localScale = new Vector3(12f, 0.05f, 9f);

            pedestalPos = new Vector3[n];
            outward = new Vector3[n];
            ringMats = new Material[n];
            poleMats = new Material[n];
            stacks = new List<Transform>[n];
            var notchMat = MakeMaterial(new Color(0.85f, 0.85f, 0.85f));
            var baseMat = MakeMaterial(new Color(0.55f, 0.55f, 0.58f));
            for (int p = 0; p < n; p++)
            {
                float a = Mathf.Lerp(-ArcHalfAngle, ArcHalfAngle, n == 1 ? 0.5f : p / (float)(n - 1)) * Mathf.Deg2Rad;
                pedestalPos[p] = new Vector3(Mathf.Sin(a) * ArcRadius, 0, Mathf.Cos(a) * ArcRadius);
                outward[p] = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                stacks[p] = new List<Transform>();

                var root = new GameObject("P" + (p + 1)).transform;
                root.SetParent(transform, false);
                root.localPosition = pedestalPos[p];
                root.localRotation = Quaternion.LookRotation(outward[p]);

                ringMats[p] = MakeMaterial(Color.gray);
                var ring = Primitive(PrimitiveType.Cylinder, "Ring", root, ringMats[p]);
                ring.localPosition = new Vector3(0, 0.02f, 0);
                ring.localScale = new Vector3(0.84f, 0.02f, 0.84f);

                var pedestalBase = Primitive(PrimitiveType.Cylinder, "Base", root, baseMat);
                pedestalBase.localPosition = new Vector3(0, 0.08f, 0);
                pedestalBase.localScale = new Vector3(0.68f, 0.04f, 0.68f);

                // The pole behind the column with one notch per slot, so the height reads at a glance.
                poleMats[p] = MakeMaterial(new Color(0.35f, 0.35f, 0.38f));
                float poleHeight = cfg.PedestalCapacity * PersonHeight;
                var pole = Primitive(PrimitiveType.Cube, "Pole", root, poleMats[p]);
                pole.localPosition = new Vector3(0, BaseTop + poleHeight / 2f, 0.3f);
                pole.localScale = new Vector3(0.07f, poleHeight, 0.07f);
                for (int s = 1; s <= cfg.PedestalCapacity; s++)
                {
                    var notch = Primitive(PrimitiveType.Cube, "Notch", root, notchMat);
                    notch.localPosition = new Vector3(0, BaseTop + s * PersonHeight, 0.27f);
                    notch.localScale = new Vector3(s % 5 == 0 ? 0.24f : 0.16f, 0.02f, 0.04f);
                }
            }

            var cannon = new GameObject("Cannon").transform;
            cannon.SetParent(transform, false);
            var dark = MakeMaterial(new Color(0.18f, 0.18f, 0.2f));
            barrel = Primitive(PrimitiveType.Cylinder, "Barrel", cannon, dark);
            barrelRest = new Vector3(0, 0.35f, -0.25f);
            barrel.localPosition = barrelRest;
            barrel.localRotation = Quaternion.Euler(60f, 0, 0);
            barrel.localScale = new Vector3(0.55f, 0.4f, 0.55f);
            for (int side = -1; side <= 1; side += 2)
            {
                var wheel = Primitive(PrimitiveType.Cylinder, "Wheel", cannon, MakeMaterial(new Color(0.5f, 0.3f, 0.15f)));
                wheel.localPosition = new Vector3(side * 0.38f, 0.25f, -0.3f);
                wheel.localRotation = Quaternion.Euler(0, 0, 90f);
                wheel.localScale = new Vector3(0.5f, 0.05f, 0.5f);
            }
            cannonMouth = new Vector3(0, 0.62f, 0.05f);

            queuePos = new[] { new Vector3(-1.45f, 0.05f, -0.35f), new Vector3(-2.2f, 0.05f, -0.6f) };
            for (int i = 0; i < queuePos.Length; i++)
            {
                var pad = Primitive(PrimitiveType.Cylinder, "QueuePad", transform, baseMat);
                pad.localPosition = queuePos[i];
                pad.localScale = new Vector3(0.45f, 0.03f, 0.45f);
            }

            reservePos = new Vector3(1.55f, 0.05f, -0.35f);
            reservePadMat = MakeMaterial(new Color(0.55f, 0.55f, 0.58f));
            var reservePad = Primitive(PrimitiveType.Cylinder, "ReservePad", transform, reservePadMat);
            reservePad.localPosition = reservePos;
            reservePad.localScale = new Vector3(0.6f, 0.03f, 0.6f);

            FitCamera();
        }

        // ---------------------------------------------------------------- camera and input mapping

        /// <summary>Fixed camera behind the cannon tilted 35°, pulled back until the whole arena fits in portrait.</summary>
        public void FitCamera()
        {
            var rot = Quaternion.Euler(35f, 0, 0);
            var target = new Vector3(0, 1.4f, 2.0f);
            cam.transform.rotation = rot;

            var points = new List<Vector3> { cannonMouth, reservePos + Vector3.right * 0.35f, queuePos[1] + Vector3.left * 0.35f };
            foreach (var p in pedestalPos)
            {
                points.Add(p + Vector3.up * (BaseTop + cfg.PedestalCapacity * PersonHeight + 0.2f));
                points.Add(p);
            }

            float lo = 3f, hi = 80f;
            for (int i = 0; i < 30; i++)
            {
                float mid = (lo + hi) / 2f;
                cam.transform.position = target - rot * Vector3.forward * mid;
                if (AllVisible(points)) hi = mid;
                else lo = mid;
            }
            cam.transform.position = target - rot * Vector3.forward * hi;
        }

        bool AllVisible(List<Vector3> points)
        {
            foreach (var w in points)
            {
                var v = cam.WorldToViewportPoint(w);
                // Leave room for the score at the top and the banner at the bottom.
                if (v.z <= 0 || v.x < 0.04f || v.x > 0.96f || v.y < 0.13f || v.y > 0.82f) return false;
            }
            return true;
        }

        /// <summary>Which pedestal a tap points at, by screen column, or -1.</summary>
        public int PedestalAtScreen(Vector2 screen)
        {
            int best = -1;
            float bestDx = float.MaxValue;
            float halfGap = float.MaxValue;
            for (int p = 0; p < pedestalPos.Length; p++)
            {
                var bottom = cam.WorldToScreenPoint(pedestalPos[p]);
                var top = cam.WorldToScreenPoint(pedestalPos[p] + Vector3.up * (BaseTop + cfg.PedestalCapacity * PersonHeight));
                if (p > 0) halfGap = Mathf.Min(halfGap, Mathf.Abs(bottom.x - cam.WorldToScreenPoint(pedestalPos[p - 1]).x) / 2f);
                float margin = Screen.height * 0.04f;
                if (screen.y < bottom.y - margin || screen.y > top.y + margin) continue;
                float dx = Mathf.Abs(screen.x - (bottom.x + top.x) / 2f);
                if (dx < bestDx)
                {
                    bestDx = dx;
                    best = p;
                }
            }
            return bestDx <= halfGap ? best : -1;
        }

        public bool IsReserveAtScreen(Vector2 screen)
        {
            var s = cam.WorldToScreenPoint(reservePos + Vector3.up * 0.5f);
            return Vector2.Distance(screen, s) < Screen.height * 0.07f;
        }

        public Vector3 PedestalTop(int p) => SlotPos(p, stacks[p].Count) + Vector3.up * 0.3f;

        // ---------------------------------------------------------------- per-frame signals

        public void UpdateSignals(Round round, bool canSwap)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
            for (int p = 0; p < pedestalPos.Length; p++)
            {
                bool fits = round != null && !round.IsOver && round.Board.CanPlace(p, round.Cannon.Size);
                ringMats[p].color = fits ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.45f, 0.45f, 0.45f);
                bool danger = round != null && round.Board.Height(p) >= cfg.DangerHeight;
                poleMats[p].color = danger ? Color.Lerp(new Color(0.35f, 0.35f, 0.38f), new Color(1f, 0.15f, 0.1f), pulse) : new Color(0.35f, 0.35f, 0.38f);
            }
            reservePadMat.color = canSwap ? new Color(0.95f, 0.8f, 0.3f) : new Color(0.55f, 0.55f, 0.58f);
        }

        // ---------------------------------------------------------------- building from state

        /// <summary>Rebuilds cannon, queue and reserve from the round. The cannon group pops in over 0.15 s.</summary>
        public void RebuildHand(Round round, bool popCannon)
        {
            Replace(cannonPeople, round.Cannon, cannonMouth, 1f, popCannon);
            for (int i = 0; i < queuePos.Length; i++)
                Replace(queuePeople[i], i < round.Queue.Count ? round.Queue[i] : null, queuePos[i], 0.7f, false);
            Replace(reservePeople, round.Reserve, reservePos, 0.85f, false);
        }

        void Replace(List<Transform> list, Group group, Vector3 basePos, float scale, bool pop)
        {
            foreach (var t in list) if (t != null) Destroy(t.gameObject);
            list.Clear();
            if (group == null) return;
            for (int i = 0; i < group.Size; i++)
            {
                var t = SpawnPerson(group.Colors[i]);
                t.localPosition = basePos + Vector3.up * (PersonHeight * scale * (i + 0.5f));
                t.localScale = Vector3.one * scale;
                list.Add(t);
                if (pop) StartCoroutine(ScaleFromZero(t, scale, CannonRefillTime));
            }
        }

        /// <summary>Makes the pedestals match the board exactly. Called after every animation as a safety net.</summary>
        public void SyncBoard(Board board)
        {
            for (int p = 0; p < board.Count; p++)
            {
                var s = stacks[p];
                bool same = s.Count == board.Height(p);
                for (int i = 0; same && i < s.Count; i++)
                    same = s[i] != null && s[i].GetComponent<Person>().Color == board.Stack(p)[i];
                if (same)
                {
                    for (int i = 0; i < s.Count; i++)
                    {
                        s[i].localPosition = SlotPos(p, i);
                        s[i].localScale = Vector3.one;
                    }
                    continue;
                }
                foreach (var t in s) if (t != null) Destroy(t.gameObject);
                s.Clear();
                for (int i = 0; i < board.Height(p); i++)
                {
                    var t = SpawnPerson(board.Stack(p)[i]);
                    t.localPosition = SlotPos(p, i);
                    s.Add(t);
                }
            }
        }

        public void ClearAll()
        {
            foreach (var s in stacks)
            {
                foreach (var t in s) if (t != null) Destroy(t.gameObject);
                s.Clear();
            }
        }

        Vector3 SlotPos(int p, int index) => pedestalPos[p] + Vector3.up * (BaseTop + PersonHeight * (index + 0.5f));

        // ---------------------------------------------------------------- animations

        public IEnumerator PlayFire(Round round, FireResult result)
        {
            int p = result.Pedestal;
            var flyers = new List<Transform>(cannonPeople);
            cannonPeople.Clear();
            RebuildHand(round, true);
            StartCoroutine(Recoil());
            Haptics.Light();

            int start = stacks[p].Count;
            for (int i = 0; i < flyers.Count; i++)
            {
                stacks[p].Add(flyers[i]);
                StartCoroutine(Fly(flyers[i], SlotPos(p, start + i), FlightTime, i * FlightStagger, 1.6f, true));
            }
            yield return new WaitForSeconds(FlightTime + FlightStagger * (flyers.Count - 1));

            foreach (var e in result.Events)
            {
                switch (e.Kind)
                {
                    case BoardEventKind.Clear:
                        yield return PlayClear(e);
                        break;
                    case BoardEventKind.Jump:
                        yield return PlayJump(e);
                        break;
                    case BoardEventKind.ArenaClear:
                        Popup?.Invoke(new Vector3(0, 2.5f, 3f), "ARENĂ CURATĂ! +" + e.Points);
                        break;
                }
            }

            if (result.Placement.MaxStep >= 3) Haptics.Heavy();
            else if (result.Placement.MaxStep >= 1) Haptics.Medium();
            SyncBoard(round.Board);
        }

        IEnumerator PlayClear(BoardEvent e)
        {
            var s = stacks[e.Pedestal];
            var run = s.GetRange(s.Count - e.Count, e.Count);
            s.RemoveRange(s.Count - e.Count, e.Count);

            // Human pyramid for the first half, fireworks for the second.
            var origin = SlotPos(e.Pedestal, s.Count);
            var targets = PyramidPositions(origin, run.Count);
            float half = ClearTime / 2f;
            for (int i = 0; i < run.Count; i++) StartCoroutine(Fly(run[i], targets[i], half, 0f, 0.3f, false));
            yield return new WaitForSeconds(half);

            foreach (var t in run)
            {
                Burst(t.position, t.GetComponent<Person>().Color);
                StartCoroutine(ShrinkAndDestroy(t, half));
            }
            Popup?.Invoke(origin + Vector3.up * 1.5f, (e.Step > 1 ? "LANȚ x" + e.Step + "  " : "") + "+" + e.Points);
            yield return new WaitForSeconds(half);
        }

        IEnumerator PlayJump(BoardEvent e)
        {
            var from = stacks[e.Pedestal];
            var to = stacks[e.Target];
            var movers = from.GetRange(from.Count - e.Count, e.Count);
            from.RemoveRange(from.Count - e.Count, e.Count);
            int start = to.Count;
            // The top person lands first so the order on the new pedestal stays readable.
            for (int i = 0; i < movers.Count; i++)
            {
                to.Add(movers[i]);
                StartCoroutine(Fly(movers[i], SlotPos(e.Target, start + i), ChainStepTime * 0.8f, ChainStepTime * 0.2f * i / Mathf.Max(1, movers.Count), 1f, true));
            }
            yield return new WaitForSeconds(ChainStepTime);
        }

        public IEnumerator PlayKnockdown(List<BoardEvent> events, Board board)
        {
            // Slow motion elephant: the pedestals tip over and the people tumble off.
            foreach (var e in events)
            {
                var s = stacks[e.Pedestal];
                foreach (var t in s)
                {
                    var away = outward[e.Pedestal] * 2.5f + Vector3.right * UnityEngine.Random.Range(-1f, 1f) + Vector3.down * 1.5f;
                    StartCoroutine(Fly(t, t.localPosition + away, 1.2f, UnityEngine.Random.Range(0f, 0.3f), 1.2f, false));
                    StartCoroutine(ShrinkAndDestroy(t, 1.5f));
                }
                s.Clear();
            }
            yield return new WaitForSeconds(1.5f);
            SyncBoard(board);
        }

        public IEnumerator PlayEndShake()
        {
            float t = 0;
            var rest = transform.localPosition;
            while (t < EndShakeTime)
            {
                t += Time.deltaTime;
                transform.localPosition = rest + new Vector3(Mathf.Sin(t * 70f) * 0.04f, 0, 0);
                yield return null;
            }
            transform.localPosition = rest;
        }

        static List<Vector3> PyramidPositions(Vector3 origin, int count)
        {
            int width = 1;
            while (width * (width + 1) / 2 < count) width++;
            var list = new List<Vector3>();
            int row = 0;
            while (list.Count < count)
            {
                int inRow = Mathf.Min(width - row, count - list.Count);
                for (int i = 0; i < inRow; i++)
                {
                    float x = (i - (inRow - 1) / 2f) * PersonWidth;
                    list.Add(origin + new Vector3(x, row * PersonHeight + 0.2f, -0.2f));
                }
                row++;
            }
            return list;
        }

        IEnumerator Fly(Transform t, Vector3 target, float duration, float delay, float arc, bool squash)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            if (t == null) yield break;
            var start = t.localPosition;
            float time = 0;
            while (time < duration && t != null)
            {
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / duration);
                t.localPosition = Vector3.Lerp(start, target, k) + Vector3.up * arc * 4f * k * (1f - k);
                yield return null;
            }
            if (t == null) yield break;
            t.localPosition = target;
            if (squash) yield return Squash(t);
        }

        IEnumerator Squash(Transform t)
        {
            const float d = 0.12f;
            float time = 0;
            var baseScale = t.localScale;
            while (time < d && t != null)
            {
                time += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(time / d) * Mathf.PI);
                t.localScale = new Vector3(baseScale.x * (1 + 0.25f * k), baseScale.y * (1 - 0.25f * k), baseScale.z * (1 + 0.25f * k));
                yield return null;
            }
            if (t != null) t.localScale = baseScale;
        }

        IEnumerator Recoil()
        {
            float time = 0;
            const float d = 0.2f;
            while (time < d)
            {
                time += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(time / d) * Mathf.PI);
                barrel.localPosition = barrelRest + new Vector3(0, -0.05f, -0.15f) * k;
                yield return null;
            }
            barrel.localPosition = barrelRest;
        }

        static IEnumerator ScaleFromZero(Transform t, float scale, float duration)
        {
            float time = 0;
            while (time < duration && t != null)
            {
                time += Time.deltaTime;
                t.localScale = Vector3.one * scale * Mathf.Clamp01(time / duration);
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one * scale;
        }

        static IEnumerator ShrinkAndDestroy(Transform t, float duration)
        {
            float time = 0;
            var s = t != null ? t.localScale : Vector3.one;
            while (time < duration && t != null)
            {
                time += Time.deltaTime;
                t.localScale = s * (1f - Mathf.Clamp01(time / duration));
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        /// <summary>Grey-box fireworks: a handful of colored cubes thrown outwards.</summary>
        void Burst(Vector3 at, int color)
        {
            for (int i = 0; i < 6; i++)
            {
                var spark = Primitive(PrimitiveType.Cube, "Spark", transform, colorMats[color % colorMats.Length]);
                spark.position = at;
                spark.localScale = Vector3.one * 0.08f;
                var dir = UnityEngine.Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) + 0.5f;
                StartCoroutine(Fly(spark, spark.localPosition + dir * 0.9f, 0.5f, 0f, 0.4f, false));
                StartCoroutine(ShrinkAndDestroy(spark, 0.6f));
            }
        }

        // ---------------------------------------------------------------- primitives

        /// <summary>A low-poly stand-in for the circus person: colored body plus a hat whose shape differs per color.</summary>
        Transform SpawnPerson(int color)
        {
            var root = new GameObject("Person" + color).transform;
            root.SetParent(transform, false);
            root.gameObject.AddComponent<Person>().Color = color;
            var mat = colorMats[color % colorMats.Length];

            var body = Primitive(PrimitiveType.Capsule, "Body", root, mat);
            body.localScale = new Vector3(PersonWidth * 0.92f, PersonHeight * 0.47f, PersonWidth * 0.92f);
            body.localPosition = new Vector3(0, -0.02f, 0);

            var hatMat = MakeHatMaterial(color);
            Transform hat;
            switch (color % 6)
            {
                case 0: // cone stand-in
                    hat = Primitive(PrimitiveType.Cube, "Hat", root, hatMat);
                    hat.localScale = new Vector3(0.1f, 0.12f, 0.1f);
                    hat.localRotation = Quaternion.Euler(45, 0, 45);
                    break;
                case 1: // top hat
                    hat = Primitive(PrimitiveType.Cylinder, "Hat", root, hatMat);
                    hat.localScale = new Vector3(0.14f, 0.06f, 0.14f);
                    break;
                case 2: // ball
                    hat = Primitive(PrimitiveType.Sphere, "Hat", root, hatMat);
                    hat.localScale = Vector3.one * 0.12f;
                    break;
                case 3: // star stand-in
                    hat = Primitive(PrimitiveType.Cube, "Hat", root, hatMat);
                    hat.localScale = new Vector3(0.16f, 0.04f, 0.16f);
                    hat.localRotation = Quaternion.Euler(0, 45, 0);
                    break;
                case 4: // crown stand-in
                    hat = Primitive(PrimitiveType.Cylinder, "Hat", root, hatMat);
                    hat.localScale = new Vector3(0.22f, 0.02f, 0.22f);
                    break;
                default: // feather
                    hat = Primitive(PrimitiveType.Capsule, "Hat", root, hatMat);
                    hat.localScale = new Vector3(0.04f, 0.08f, 0.04f);
                    hat.localRotation = Quaternion.Euler(0, 0, 25);
                    break;
            }
            hat.localPosition = new Vector3(0, PersonHeight * 0.5f, 0);
            return root;
        }

        readonly Dictionary<int, Material> hatMats = new Dictionary<int, Material>();

        Material MakeHatMaterial(int color)
        {
            if (!hatMats.TryGetValue(color, out var m))
            {
                m = MakeMaterial(Color.Lerp(ColorOf(color), Color.white, 0.55f));
                hatMats[color] = m;
            }
            return m;
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
