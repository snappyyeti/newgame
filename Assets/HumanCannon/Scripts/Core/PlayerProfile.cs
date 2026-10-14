using System;
using System.Collections.Generic;

namespace HumanCannon.Core
{
    [Serializable]
    public sealed class RunRecord
    {
        public int Score;
        public int RecordBefore;
        public bool UsedRevive;
    }

    /// <summary>Everything the hidden rules need to know about the player across runs. Saved locally.</summary>
    [Serializable]
    public sealed class PlayerProfile
    {
        public const int HistoryLimit = 10;

        public int Record;
        public int RunsStarted;
        public long FirstLaunchUnixSeconds;
        public List<RunRecord> History = new List<RunRecord>();

        public void AddRun(RunRecord run)
        {
            History.Add(run);
            if (run.Score > Record) Record = run.Score;
            while (History.Count > HistoryLimit) History.RemoveAt(0);
        }

        /// <summary>The most recent runs, newest last. Returns fewer when there is not enough history.</summary>
        public List<RunRecord> LastRuns(int count)
        {
            int start = Math.Max(0, History.Count - count);
            return History.GetRange(start, History.Count - start);
        }
    }

    public interface IRandom
    {
        /// <summary>Integer in [min, maxExclusive).</summary>
        int Range(int min, int maxExclusive);
        /// <summary>Float in [0, 1).</summary>
        float Value();
    }

    public sealed class SystemRandom : IRandom
    {
        readonly Random random;
        public SystemRandom(int seed) { random = new Random(seed); }
        public SystemRandom() { random = new Random(); }
        public int Range(int min, int maxExclusive) => random.Next(min, maxExclusive);
        public float Value() => (float)random.NextDouble();
    }
}
