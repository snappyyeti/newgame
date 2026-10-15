using System;
using System.Collections.Generic;

namespace Titan.Core
{
    /// <summary>
    /// The juice and performance budget values (FEEL-1, DESIGN.md 7). Presentation only: nothing here changes a game
    /// rule (the simulation never reads it), so it is not part of TitanConfig.
    /// </summary>
    public sealed class FeelConfig
    {
        /// <summary>Hitstop hard cap: total freeze ≤ this share of any rolling <see cref="HitStopWindow"/>.</summary>
        public float HitStopMaxFraction = 0.10f;
        public float HitStopWindow = 1.0f;
        /// <summary>A time scale at or below this counts as "frozen" (hitstops run at 0.05).</summary>
        public float FreezeScale = 0.1f;
        public float HapticMinInterval = 0.6f;
        /// <summary>A falling piece with more cubes than this is combined into one mesh per color.</summary>
        public int PieceMeshCombineThreshold = 60;
        public int PerfTargetFps = 60;
        public float PerfP95FloorMs = 33f;
        /// <summary>No slow-mo of any kind (hitstop included) from this danger on: the last 15% of the approach.</summary>
        public float NoSlowMoDanger = 0.85f;
        public float SlowMoMaxSeconds = 0.15f;

        // Triggers (DESIGN.md 7).
        public int BigPieceCubes = 12;
        public float BigPieceStop = 0.08f;
        public int HugePieceCubes = 40;
        public float HugePieceStop = 0.06f;
        public float SlowMoScale = 0.35f;
        public float DefeatStop = 0.12f;
        public float StopScale = 0.05f;
        /// <summary>FOV punch: −3%, back over 0.25 s (unscaled).</summary>
        public float FovPunch = 0.03f;
        public float FovReturnSeconds = 0.25f;

        // Hit sound streak.
        public float HitStreakResetSeconds = 0.5f;
        public float HitStreakPitchStep = 0.04f;
        public float HitStreakPitchMax = 1.6f;
    }

    /// <summary>What the view should do when a piece starts to fall (pure: see <see cref="Juice.ForPiece"/>).</summary>
    public struct PieceJuice
    {
        public float StopSeconds;
        public bool FovPunch;
        public float SlowMoSeconds;
        public bool Haptic;
    }

    /// <summary>The FEEL-1 trigger table as pure functions (DESIGN.md 7).</summary>
    public static class Juice
    {
        /// <summary>
        /// A piece of <paramref name="cubes"/> cubes starts to fall. ≥ 12 cubes: a 0.08 s hitstop and the FOV punch.
        /// A head piece or a clean cut of ≥ 40 cubes: a 0.06 s hitstop (the larger 0.08 wins: calls never stack) then
        /// slow-mo at 0.35 for at most 0.15 s. ≥ 40 cubes: a haptic. The danger guard and the caps are applied by
        /// <see cref="TimeWarp"/>, the haptic interval by <see cref="HapticGate"/>.
        /// </summary>
        public static PieceJuice ForPiece(FeelConfig c, int cubes, int part, bool clean)
        {
            var j = new PieceJuice();
            if (cubes >= c.BigPieceCubes)
            {
                j.StopSeconds = c.BigPieceStop;
                j.FovPunch = true;
            }
            if (cubes >= c.HugePieceCubes && (part == Monster.Head || clean))
            {
                j.StopSeconds = Math.Max(j.StopSeconds, c.HugePieceStop);
                j.SlowMoSeconds = c.SlowMoMaxSeconds;
            }
            j.Haptic = cubes >= c.HugePieceCubes;
            return j;
        }

        /// <summary>A titan fell: always the defeat hitstop; a haptic for a boss or a near-miss win.</summary>
        public static bool DefeatHaptic(TitanRole role, bool nearMiss) => role == TitanRole.Boss || nearMiss;

        /// <summary>The hit sound's pitch for a slot streak: min(1.6, 1 + 0.04 × streak).</summary>
        public static float HitPitch(FeelConfig c, int streak) => Math.Min(c.HitStreakPitchMax, 1f + c.HitStreakPitchStep * Math.Max(0, streak));
    }

    /// <summary>
    /// Hitstop and slow-mo on one unscaled clock (FEEL-1). HitStop calls never stack (the larger remaining end and the
    /// lower scale win), the total freeze is capped at <see cref="FeelConfig.HitStopMaxFraction"/> of any rolling
    /// second, and nothing is granted under "Efecte reduse" or from <see cref="FeelConfig.NoSlowMoDanger"/> on.
    /// <see cref="Cancel"/> (pause, game over) returns the scale to 1 at once.
    /// </summary>
    public sealed class TimeWarp
    {
        readonly FeelConfig cfg;
        /// <summary>Granted freeze intervals [start, end) on the unscaled clock (the last one may be running).</summary>
        readonly List<float[]> freezes = new List<float[]>();
        float stopEnd = -1f, stopScale = 1f;
        float slowStart = -1f, slowEnd = -1f, slowScale = 1f;

        public TimeWarp(FeelConfig config)
        {
            cfg = config;
        }

        /// <summary>
        /// Requests a hitstop of <paramref name="seconds"/> (unscaled) at <paramref name="scale"/>. Returns the
        /// seconds actually granted from now (0 when refused or capped to nothing).
        /// </summary>
        public float HitStop(float now, float seconds, float scale, bool reducedFx, float danger)
        {
            if (reducedFx || danger >= cfg.NoSlowMoDanger || seconds <= 0f) return 0f;
            bool running = stopEnd > now;
            float currentEnd = running ? stopEnd : now;
            float wanted = Math.Max(currentEnd, now + seconds);
            float end = CappedEnd(now, currentEnd, wanted);
            if (end <= currentEnd) return Math.Max(0f, currentEnd - now);
            if (running && freezes.Count > 0) freezes[freezes.Count - 1][1] = end; // extend the running one
            else freezes.Add(new[] { now, end });
            stopEnd = end;
            stopScale = running ? Math.Min(stopScale, scale) : scale;
            Trim(now);
            return end - now;
        }

        /// <summary>
        /// Slow-mo at <paramref name="scale"/> for at most <see cref="FeelConfig.SlowMoMaxSeconds"/> (unscaled), starting
        /// when the current hitstop ends. Refused under "Efecte reduse" and from the danger limit on.
        /// </summary>
        public bool SlowMo(float now, float seconds, float scale, bool reducedFx, float danger)
        {
            if (reducedFx || danger >= cfg.NoSlowMoDanger || seconds <= 0f) return false;
            float start = stopEnd > now ? stopEnd : now;
            slowStart = start;
            slowEnd = start + Math.Min(seconds, cfg.SlowMoMaxSeconds);
            slowScale = scale;
            return true;
        }

        /// <summary>The time scale now: the hitstop's, else the slow-mo's, else 1.</summary>
        public float Scale(float now)
        {
            if (now < stopEnd) return stopScale;
            if (now >= slowStart && now < slowEnd) return slowScale;
            return 1f;
        }

        public bool SlowMoActive(float now) => now >= stopEnd && now >= slowStart && now < slowEnd;

        /// <summary>Ends any hitstop or slow-mo now (pause, game over, a new run).</summary>
        public void Cancel(float now)
        {
            if (stopEnd > now && freezes.Count > 0) freezes[freezes.Count - 1][1] = Math.Max(freezes[freezes.Count - 1][0], now);
            stopEnd = -1f;
            stopScale = 1f;
            slowStart = slowEnd = -1f;
            slowScale = 1f;
        }

        /// <summary>Ends only the slow-mo (the danger guard while it runs).</summary>
        public void CancelSlowMo()
        {
            slowStart = slowEnd = -1f;
            slowScale = 1f;
        }

        /// <summary>Granted freeze seconds inside [from, to].</summary>
        public float FrozenIn(float from, float to)
        {
            float total = 0f;
            foreach (var f in freezes)
            {
                float a = Math.Max(from, f[0]), b = Math.Min(to, f[1]);
                if (b > a) total += b - a;
            }
            return total;
        }

        /// <summary>
        /// The latest end ≤ <paramref name="wanted"/> such that the window ending there freezes at most the cap. The
        /// freeze is contiguous from now, so the worst window is the one ending at the new end (sliding it forward adds
        /// new freeze at least as fast as it drops old freeze).
        /// </summary>
        float CappedEnd(float now, float currentEnd, float wanted)
        {
            float cap = cfg.HitStopMaxFraction * cfg.HitStopWindow;
            Func<float, float> frozenUpTo = end => FrozenIn(end - cfg.HitStopWindow, now) + (end - now);
            if (frozenUpTo(wanted) <= cap + 1e-6f) return wanted;
            float lo = currentEnd, hi = wanted;
            if (frozenUpTo(lo) > cap + 1e-6f) return currentEnd;
            for (int i = 0; i < 30; i++)
            {
                float mid = (lo + hi) / 2f;
                if (frozenUpTo(mid) <= cap + 1e-6f) lo = mid;
                else hi = mid;
            }
            return lo;
        }

        void Trim(float now)
        {
            float keepFrom = now - 2f * cfg.HitStopWindow;
            while (freezes.Count > 1 && freezes[0][1] < keepFrom) freezes.RemoveAt(0);
        }
    }

    /// <summary>Per line slot, the run of hits; it resets after 0.5 s without a hit (FEEL-1 hit sound pitch).</summary>
    public sealed class HitStreak
    {
        readonly FeelConfig cfg;
        readonly int[] streak;
        readonly float[] last;

        public HitStreak(FeelConfig config, int slots)
        {
            cfg = config;
            streak = new int[slots];
            last = new float[slots];
            for (int i = 0; i < slots; i++) last[i] = float.NegativeInfinity;
        }

        /// <summary>A hit from <paramref name="slot"/> at <paramref name="now"/>: returns the pitch to play it at.</summary>
        public float Hit(int slot, float now)
        {
            if (slot < 0 || slot >= streak.Length) return 1f;
            streak[slot] = now - last[slot] > cfg.HitStreakResetSeconds ? 0 : streak[slot] + 1;
            last[slot] = now;
            return Juice.HitPitch(cfg, streak[slot]);
        }

        public int StreakOf(int slot) => streak[slot];

        public void Reset()
        {
            for (int i = 0; i < streak.Length; i++)
            {
                streak[i] = 0;
                last[i] = float.NegativeInfinity;
            }
        }
    }

    /// <summary>Haptics: only when the setting is on, and at most once per <see cref="FeelConfig.HapticMinInterval"/>.</summary>
    public sealed class HapticGate
    {
        readonly FeelConfig cfg;
        float lastFired = float.NegativeInfinity;

        public HapticGate(FeelConfig config)
        {
            cfg = config;
        }

        public int Fired { get; private set; }

        /// <summary>True when the vibration may fire now (and records it).</summary>
        public bool TryFire(float now, bool enabled)
        {
            if (!enabled || now - lastFired < cfg.HapticMinInterval) return false;
            lastFired = now;
            Fired++;
            return true;
        }
    }

    /// <summary>The last N frame times (the dev overlay: fps and p95 over the last 300 frames).</summary>
    public sealed class FrameStats
    {
        readonly float[] ms;
        readonly float[] scratch;
        int count, next;

        public FrameStats(int frames = 300)
        {
            ms = new float[frames];
            scratch = new float[frames];
        }

        public int Count => count;

        public void Add(float frameMs)
        {
            ms[next] = frameMs;
            next = (next + 1) % ms.Length;
            if (count < ms.Length) count++;
        }

        /// <summary>Mean fps over the frames held.</summary>
        public float Fps
        {
            get
            {
                if (count == 0) return 0f;
                double total = 0;
                for (int i = 0; i < count; i++) total += ms[i];
                return total <= 0 ? 0f : (float)(1000.0 * count / total);
            }
        }

        /// <summary>The 95th percentile frame time (nearest rank), in ms.</summary>
        public float P95
        {
            get
            {
                if (count == 0) return 0f;
                Array.Copy(ms, scratch, count);
                Array.Sort(scratch, 0, count);
                int rank = (int)Math.Ceiling(0.95 * count) - 1;
                return scratch[Math.Max(0, Math.Min(count - 1, rank))];
            }
        }
    }
}
