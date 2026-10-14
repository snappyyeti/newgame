namespace HumanCannon.Core
{
    public enum DirectorMode
    {
        Assist,
        Normal,
    }

    public struct DirectorDecision
    {
        public DirectorMode Mode;
        public float D;
        public string Rule;
    }

    /// <summary>
    /// The hidden rules ("traits") that pick the director mode for each new group.
    /// They are checked in priority order and the first one that applies wins. Each can be switched off in Remote Config.
    /// </summary>
    public sealed class HiddenRules
    {
        readonly GameConfig cfg;
        readonly PlayerProfile profile;
        int reviveWaveRemaining;

        public HiddenRules(GameConfig cfg, PlayerProfile profile)
        {
            this.cfg = cfg;
            this.profile = profile;
        }

        public void OnRevive()
        {
            if (cfg.TraitReviveClearWave) reviveWaveRemaining = cfg.ReviveClearWaveGroups;
        }

        /// <summary>Called once per generated group; consumes counters such as the post-revive wave.</summary>
        public DirectorDecision Decide(Round round)
        {
            float baseD = cfg.BaseDifficulty(round.Score);

            // 1. ReviveClearWave: assist for the first groups after a revive.
            if (reviveWaveRemaining > 0)
            {
                reviveWaveRemaining--;
                return Assist("ReviveClearWave");
            }

            // 4. NewUserBoost: the first runs of an account start easy.
            if (cfg.TraitNewUser && round.RunNumber <= cfg.NewUserRuns)
            {
                if (round.Score < cfg.NewUserAssistUntilScore) return Assist("NewUserBoost");
                return new DirectorDecision { Mode = DirectorMode.Normal, D = Clamp01(baseD - cfg.NewUserDOffset), Rule = "NewUserBoost" };
            }

            // 5. LossStreakMercy: the last runs ended far below the record without a revive.
            if (cfg.TraitLossMercy && round.GroupsGenerated < cfg.LossMercyGroups && IsLossStreak())
                return Assist("LossStreakMercy");

            // 6. Base curve.
            return new DirectorDecision { Mode = DirectorMode.Normal, D = baseD, Rule = "Base" };
        }

        bool IsLossStreak()
        {
            var last = profile.LastRuns(cfg.LossMercyRuns);
            if (last.Count < cfg.LossMercyRuns) return false;
            foreach (var run in last)
            {
                if (run.UsedRevive || run.RecordBefore <= 0) return false;
                if (run.Score >= cfg.LossMercyRatio * run.RecordBefore) return false;
            }
            return true;
        }

        DirectorDecision Assist(string rule) => new DirectorDecision { Mode = DirectorMode.Assist, D = cfg.DAssist, Rule = rule };

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
