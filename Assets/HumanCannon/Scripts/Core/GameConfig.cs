using System;
using System.Collections.Generic;
using System.Globalization;

namespace HumanCannon.Core
{
    /// <summary>One visible difficulty tier (design doc: "Pragurile vizibile").</summary>
    [Serializable]
    public sealed class ScoreTier
    {
        public int MinScore;
        public int Colors;
        public int MinSize;
        public int MaxSize;
        public int MinDistinct;
        public int MaxDistinct;

        public ScoreTier(int minScore, int colors, int minSize, int maxSize, int minDistinct, int maxDistinct)
        {
            MinScore = minScore;
            Colors = colors;
            MinSize = minSize;
            MaxSize = maxSize;
            MinDistinct = minDistinct;
            MaxDistinct = maxDistinct;
        }
    }

    /// <summary>
    /// Every tunable number of the game. Defaults are the launch values from the design doc;
    /// <see cref="ApplyOverrides"/> takes the flat key/value map a Remote Config provider returns.
    /// </summary>
    public sealed class GameConfig
    {
        // Board and rules
        public int PedestalCount = 7;
        public int PedestalCapacity = 12;
        public int ClearRun = 5;
        public int QueueLength = 2;
        public int DangerHeight = 10;
        public int PointsPerPerson = 10;
        public int ArenaClearBonus = 500;
        public List<ScoreTier> ScoreTiers = DefaultTiers();

        // Group director
        public int DirectorCandidates = 40;
        public int DirectorPickPool = 3;
        public float DStart = 0.25f;
        public float DAtCurveScore = 0.60f;
        public int DCurveScore = 5000;
        public float DCap = 0.70f;
        public float DNoise = 0.05f;
        public float DAssist = 0.05f;
        public float WeightSameColorGrowth = 0.5f;
        public float WeightBuriedColor = 1f;
        public float WeightHeight = 2f;

        // Hidden rules (traits) that only ever help the player
        public bool TraitReviveClearWave = true;
        public int ReviveClearWaveGroups = 4;
        public bool TraitNewUser = true;
        public int NewUserRuns = 3;
        public int NewUserAssistUntilScore = 300;
        public float NewUserDOffset = 0.15f;
        public bool TraitLossMercy = true;
        public int LossMercyRuns = 2;
        public float LossMercyRatio = 0.5f;
        public int LossMercyGroups = 10;

        // Revive
        public int MaxRevivesPerRun = 1;
        public int RevivePedestalsCleared = 3;
        public float ReviveBonusMax = 0.10f;
        public float NearRecordRatio = 0.10f;

        public static List<ScoreTier> DefaultTiers()
        {
            return new List<ScoreTier>
            {
                new ScoreTier(0, 4, 2, 3, 1, 2),
                new ScoreTier(1000, 5, 2, 3, 1, 2),
                new ScoreTier(2500, 5, 2, 4, 1, 3),
                new ScoreTier(5000, 6, 2, 4, 1, 3),
            };
        }

        public ScoreTier TierFor(int score)
        {
            ScoreTier best = ScoreTiers[0];
            foreach (var tier in ScoreTiers)
                if (score >= tier.MinScore && tier.MinScore >= best.MinScore)
                    best = tier;
            return best;
        }

        /// <summary>Normal-mode base curve: linear from DStart at 0 to DAtCurveScore at DCurveScore, capped at DCap.</summary>
        public float BaseDifficulty(int score)
        {
            float d = DStart + (DAtCurveScore - DStart) * score / (float)DCurveScore;
            return Math.Min(DCap, d);
        }

        /// <summary>
        /// Applies Remote Config values. Unknown keys are ignored and malformed values keep the default,
        /// so a bad push from the dashboard can never break the game.
        /// score_tiers uses "minScore,colors,minSize,maxSize,minDistinct,maxDistinct" entries separated by '|'.
        /// </summary>
        public void ApplyOverrides(IDictionary<string, string> values)
        {
            if (values == null) return;
            Int(values, "pedestal_count", ref PedestalCount);
            Int(values, "pedestal_capacity", ref PedestalCapacity);
            Int(values, "clear_run", ref ClearRun);
            Int(values, "arena_clear_bonus", ref ArenaClearBonus);
            Int(values, "director_candidates", ref DirectorCandidates);
            Float(values, "d_start", ref DStart);
            Float(values, "d_at_curve_score", ref DAtCurveScore);
            Int(values, "d_curve_score", ref DCurveScore);
            Float(values, "d_cap", ref DCap);
            Float(values, "d_noise", ref DNoise);
            Float(values, "d_assist", ref DAssist);
            Bool(values, "trait_revive_clear_wave", ref TraitReviveClearWave);
            Int(values, "trait_revive_clear_wave_groups", ref ReviveClearWaveGroups);
            Bool(values, "trait_new_user", ref TraitNewUser);
            Int(values, "trait_new_user_runs", ref NewUserRuns);
            Int(values, "trait_new_user_assist_until", ref NewUserAssistUntilScore);
            Float(values, "trait_new_user_d_offset", ref NewUserDOffset);
            Bool(values, "trait_loss_mercy", ref TraitLossMercy);
            Int(values, "trait_loss_mercy_runs", ref LossMercyRuns);
            Float(values, "trait_loss_mercy_ratio", ref LossMercyRatio);
            Int(values, "trait_loss_mercy_groups", ref LossMercyGroups);
            Float(values, "revive_bonus_max", ref ReviveBonusMax);
            Int(values, "revive_pedestals_cleared", ref RevivePedestalsCleared);

            if (values.TryGetValue("score_tiers", out var raw) && TryParseTiers(raw, out var tiers))
                ScoreTiers = tiers;
        }

        static bool TryParseTiers(string raw, out List<ScoreTier> tiers)
        {
            tiers = new List<ScoreTier>();
            if (string.IsNullOrWhiteSpace(raw)) return false;
            foreach (var entry in raw.Split('|'))
            {
                var p = entry.Split(',');
                if (p.Length != 6) return false;
                var n = new int[6];
                for (int i = 0; i < 6; i++)
                    if (!int.TryParse(p[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n[i])) return false;
                tiers.Add(new ScoreTier(n[0], n[1], n[2], n[3], n[4], n[5]));
            }
            tiers.Sort((a, b) => a.MinScore.CompareTo(b.MinScore));
            return tiers.Count > 0 && tiers[0].MinScore == 0;
        }

        static void Int(IDictionary<string, string> v, string key, ref int field)
        {
            if (v.TryGetValue(key, out var s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)) field = x;
        }

        static void Float(IDictionary<string, string> v, string key, ref float field)
        {
            if (v.TryGetValue(key, out var s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) field = x;
        }

        static void Bool(IDictionary<string, string> v, string key, ref bool field)
        {
            if (v.TryGetValue(key, out var s) && bool.TryParse(s, out var x)) field = x;
        }
    }
}
