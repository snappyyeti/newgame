using System;
using System.Text;

namespace Titan.Core
{
    /// <summary>
    /// SplitMix64 random stream: the ONLY seeded RNG for checkpoints, FTUE, the daily mode and the simulator.
    /// Same seed, same numbers on every platform: seeds and integers use ulong math only (wrap-around multiply,
    /// shifts, xor), doubles are (next >> 11) * 2^-53 and floats are (next >> 40) * 2^-24, both exact,
    /// so no floating-point rounding can differ between machines.
    /// </summary>
    public sealed class SeededRandom : IRandom
    {
        /// <summary>2^64 / golden ratio, the SplitMix64 increment.</summary>
        public const ulong Golden = 0x9E3779B97F4A7C15UL;

        const ulong FnvOffset = 0xCBF29CE484222325UL;
        const ulong FnvPrime = 0x100000001B3UL;

        ulong state;

        public SeededRandom(ulong seed)
        {
            state = seed;
        }

        /// <summary>The current internal state (for saving a stream and resuming it later).</summary>
        public ulong State => state;

        /// <summary>Next raw 64-bit value.</summary>
        public ulong NextULong()
        {
            unchecked
            {
                state += Golden;
                return Mix(state);
            }
        }

        /// <summary>Integer in [min, maxExclusive), without modulo bias. Returns min when the range is empty.</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            unchecked
            {
                ulong range = (ulong)((long)maxExclusive - min);
                // Reject the few lowest values so every result is equally likely.
                ulong threshold = (0UL - range) % range;
                ulong r;
                do r = NextULong();
                while (r < threshold);
                return (int)((long)min + (long)(r % range));
            }
        }

        /// <summary>Float in [0, 1), 24 random bits (exactly representable).</summary>
        public float Value() => (NextULong() >> 40) * (1.0f / 16777216f);

        /// <summary>Double in [0, 1), 53 random bits (exactly representable).</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);

        /// <summary>The SplitMix64 output function applied to x + Golden: one step of a stream seeded with x.</summary>
        public static ulong SplitMix64(ulong x)
        {
            unchecked { return Mix(x + Golden); }
        }

        /// <summary>
        /// Seed of the n-th sub-stream of a run (titan n, daily titan n, ...):
        /// SplitMix64(runSeed ^ (n * Golden)). Neighbouring n give unrelated seeds.
        /// </summary>
        public static ulong Derive(ulong runSeed, ulong n)
        {
            unchecked { return SplitMix64(runSeed ^ (n * Golden)); }
        }

        /// <summary>Combines two values into one well-mixed seed (order matters).</summary>
        public static ulong Hash(ulong a, ulong b)
        {
            unchecked { return SplitMix64(SplitMix64(a) ^ b); }
        }

        /// <summary>FNV-1a 64 over the UTF-8 bytes of the text (for example "titan-daily-v1:" + dayNumber).</summary>
        public static ulong FromString(string text)
        {
            ulong h = FnvOffset;
            if (string.IsNullOrEmpty(text)) return h;
            unchecked
            {
                foreach (byte b in Encoding.UTF8.GetBytes(text))
                {
                    h ^= b;
                    h *= FnvPrime;
                }
            }
            return h;
        }

        static ulong Mix(ulong z)
        {
            unchecked
            {
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
