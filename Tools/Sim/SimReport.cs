using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Titan.Core;

namespace Titan.Sim
{
    public sealed class TitanLevelSummary
    {
        public int Level;
        public TitanRole Role;
        public int Count;
        public double ClearP10, ClearP50, ClearP90, ApproachMean, MarginP10, MarginP50;
    }

    /// <summary>The metrics of DESIGN.md 12.1 for one bot. Shares are fractions (0..1).</summary>
    public sealed class BotSummary
    {
        public BotKind Bot;
        public int Games;

        public double DefeatedMean, DefeatedP10, DefeatedP50, DefeatedP90, DefeatedMax;
        public double RunMean, RunP10, RunP50, RunP90;
        public double Under20, Under30, Under60, Over180;
        public double ScoreP50;

        /// <summary>Share of runs per lose reason. LoseReason.None = the run hit the 30-min cap.</summary>
        public readonly Dictionary<LoseReason, double> LoseShare = new Dictionary<LoseReason, double>();
        /// <summary>Share of runs lost on titan k.</summary>
        public readonly SortedDictionary<int, double> LostOnTitan = new SortedDictionary<int, double>();
        public double TitanOneLoss;
        /// <summary>Share of losses at the wall (WallReached) among runs that ended with a loss.</summary>
        public double WallShareOfLosses;
        /// <summary>Median margin (fraction of the start distance left) over every defeated titan; share defeated "la mustață".</summary>
        public double MarginP50All, NearMissShare;
        /// <summary>Worst per-titan median clear time by role over titans 1-16 (DESIGN.md 12.1: 10-40 s, sprint &lt;= 16 s, boss &lt;= 40 s).</summary>
        public double ClearP50Min, ClearP50Max, SprintClearP50Max, BossClearP50Max;
        public double TimeoutShare;
        /// <summary>Share of SlotsFull losses that came from the bot's own tap (a 5th idle cannon).</summary>
        public double SelfTapShareOfSlotsFull;

        public readonly List<TitanLevelSummary> PerTitan = new List<TitanLevelSummary>();

        public double DeadAirShare, LineFullShare, WaitChoiceShare, Danger75Share, DeadParkedShare;
        public double MaxStuckP50, MaxStuckP90, MaxStuckMax;
        public double TapsPerMinute, ActShare, WaitShare, BlockedShare;

        public double ZeroChoiceShare, MeaningfulChoiceShare, NoVisibleOptionShare, OnlyDeadTrayShare;

        public int PieceCount;
        public double PiecesPerTitan, JointPieceShare, PieceSizeP50, PieceSizeP90, FirstPieceTimeP50;
        public double StrandedPerGame, DeadOnLineAtSlotsFullLoss;
        /// <summary>CORE-5: clean pieces per titan (same denominator as PiecesPerTitan), clean share of pieces, and the
        /// star rates over defeated titans (s2 decapitation, s3 clean line).</summary>
        public double CleanCutsPerTitan, CleanShare, StarDecapitationRate, StarCleanLineRate;
        /// <summary>ADS-2 ship check: share of titans n ≥ 2 where the "+1 Loc" condition held ≥ 1.0 s.</summary>
        public double ExtraSlotTriggerShare;
        public int ExtraSlotTitans;

        public static BotSummary From(BotKind bot, IList<GameStats> gs)
        {
            var s = new BotSummary { Bot = bot, Games = gs.Count };
            int n = gs.Count;
            if (n == 0) return s;

            var def = gs.Select(g => (double)g.Defeated).ToArray();
            s.DefeatedMean = def.Average();
            s.DefeatedP10 = Pct(def, .1);
            s.DefeatedP50 = Pct(def, .5);
            s.DefeatedP90 = Pct(def, .9);
            s.DefeatedMax = def.Max();

            var rt = gs.Select(g => g.RunTime).ToArray();
            s.RunMean = rt.Average();
            s.RunP10 = Pct(rt, .1);
            s.RunP50 = Pct(rt, .5);
            s.RunP90 = Pct(rt, .9);
            s.Under20 = Share(gs, g => g.RunTime < 20);
            s.Under30 = Share(gs, g => g.RunTime < 30);
            s.Under60 = Share(gs, g => g.RunTime < 60);
            s.Over180 = Share(gs, g => g.RunTime > 180);
            s.ScoreP50 = Pct(gs.Select(g => (double)g.Score), .5);

            foreach (LoseReason r in Enum.GetValues(typeof(LoseReason)))
                s.LoseShare[r] = Share(gs, g => g.Reason == r);
            s.TimeoutShare = s.LoseShare[LoseReason.None];
            foreach (var grp in gs.Where(g => g.Reason != LoseReason.None).GroupBy(g => g.LoseLevel))
                s.LostOnTitan[grp.Key] = (double)grp.Count() / n;
            s.TitanOneLoss = s.LostOnTitan.TryGetValue(1, out var t1) ? t1 : 0;
            int losses = gs.Count(g => g.Reason != LoseReason.None);
            s.WallShareOfLosses = losses == 0 ? 0 : (double)gs.Count(g => g.Reason == LoseReason.WallReached) / losses;
            var allTitans = gs.SelectMany(g => g.Titans).ToList();
            s.MarginP50All = Pct(allTitans.Select(x => x.Margin), .5);
            s.NearMissShare = allTitans.Count == 0 ? 0 : (double)allTitans.Count(x => x.NearMiss) / allTitans.Count;
            int slotsFull = gs.Count(g => g.Reason == LoseReason.SlotsFull);
            s.SelfTapShareOfSlotsFull = slotsFull == 0 ? 0 : (double)gs.Count(g => g.Reason == LoseReason.SlotsFull && g.LostOnTap) / slotsFull;
            s.DeadOnLineAtSlotsFullLoss = slotsFull == 0 ? 0 : gs.Where(g => g.Reason == LoseReason.SlotsFull).Average(g => (double)g.DeadOnLineAtLoss);

            foreach (var grp in gs.SelectMany(g => g.Titans).GroupBy(x => x.Level).OrderBy(x => x.Key))
            {
                var clear = grp.Select(x => x.ClearTime).ToArray();
                var margin = grp.Select(x => x.Margin).ToArray();
                s.PerTitan.Add(new TitanLevelSummary
                {
                    Level = grp.Key,
                    Role = grp.First().Role,
                    Count = clear.Length,
                    ClearP10 = Pct(clear, .1),
                    ClearP50 = Pct(clear, .5),
                    ClearP90 = Pct(clear, .9),
                    ApproachMean = grp.Average(x => x.Approach),
                    MarginP10 = Pct(margin, .1),
                    MarginP50 = Pct(margin, .5),
                });
            }

            // Medians on fewer than 10 defeats are noise; they do not count toward the clear-time range.
            var ranged = s.PerTitan.Where(t => t.Level <= 16 && t.Count >= 10).ToList();
            s.ClearP50Min = ranged.Count == 0 ? double.NaN : ranged.Min(t => t.ClearP50);
            s.ClearP50Max = ranged.Count == 0 ? double.NaN : ranged.Max(t => t.ClearP50);
            s.SprintClearP50Max = ranged.Where(t => t.Role == TitanRole.Sprint).Select(t => t.ClearP50).DefaultIfEmpty(double.NaN).Max();
            s.BossClearP50Max = ranged.Where(t => t.Role == TitanRole.Boss).Select(t => t.ClearP50).DefaultIfEmpty(double.NaN).Max();

            double tot = Math.Max(1e-9, gs.Sum(g => g.RunTime));
            s.DeadAirShare = gs.Sum(g => g.DeadAir) / tot;
            s.LineFullShare = gs.Sum(g => g.LineFull) / tot;
            s.WaitChoiceShare = gs.Sum(g => g.WaitChoice) / tot;
            s.Danger75Share = gs.Sum(g => g.Danger75Time) / tot;
            s.DeadParkedShare = gs.Sum(g => g.DeadParkedTime) / tot;
            var stuck = gs.Select(g => g.MaxStuck).ToArray();
            s.MaxStuckP50 = Pct(stuck, .5);
            s.MaxStuckP90 = Pct(stuck, .9);
            s.MaxStuckMax = stuck.Max();

            double opp = Math.Max(1, gs.Sum(g => g.Opportunities));
            s.TapsPerMinute = gs.Sum(g => g.Taps) / (tot / 60);
            s.ActShare = gs.Sum(g => g.Taps) / opp;
            s.WaitShare = gs.Sum(g => g.Waits) / opp;
            s.BlockedShare = gs.Sum(g => g.BlockedOpportunities) / opp;

            double dec = Math.Max(1, gs.Sum(g => g.Decisions));
            s.ZeroChoiceShare = gs.Sum(g => g.ZeroChoice) / dec;
            s.MeaningfulChoiceShare = gs.Sum(g => g.MeaningfulChoice) / dec;
            s.NoVisibleOptionShare = gs.Sum(g => g.NoVisibleOption) / dec;
            s.OnlyDeadTrayShare = gs.Sum(g => g.OnlyDeadTray) / dec;

            var pieces = gs.SelectMany(g => g.Pieces).ToList();
            s.PieceCount = pieces.Count;
            int titansSeen = gs.Sum(g => g.Defeated + 1);
            s.PiecesPerTitan = (double)pieces.Count / titansSeen;
            s.JointPieceShare = pieces.Count == 0 ? 0 : (double)pieces.Count(p => p.FromJointShot) / pieces.Count;
            s.PieceSizeP50 = Pct(pieces.Select(p => (double)p.Size), .5);
            s.PieceSizeP90 = Pct(pieces.Select(p => (double)p.Size), .9);
            s.FirstPieceTimeP50 = Pct(pieces.Select(p => p.TimeIntoTitan), .5);
            s.StrandedPerGame = gs.Average(g => (double)g.StrandedByPiece);
            s.CleanCutsPerTitan = (double)gs.Sum(g => g.CleanCuts) / titansSeen;
            s.CleanShare = pieces.Count == 0 ? 0 : (double)gs.Sum(g => g.CleanCuts) / pieces.Count;
            int defeated = gs.Sum(g => g.Defeated);
            s.StarDecapitationRate = defeated == 0 ? 0 : (double)gs.Sum(g => g.StarDecapitation) / defeated;
            s.StarCleanLineRate = defeated == 0 ? 0 : (double)gs.Sum(g => g.StarCleanLine) / defeated;
            s.ExtraSlotTitans = gs.Sum(g => g.ExtraSlotTitans);
            s.ExtraSlotTriggerShare = s.ExtraSlotTitans == 0 ? 0 : (double)gs.Sum(g => g.ExtraSlotTriggered) / s.ExtraSlotTitans;
            return s;
        }

        static double Share(IList<GameStats> gs, Func<GameStats, bool> pred) => (double)gs.Count(pred) / gs.Count;

        /// <summary>Linear-interpolated percentile (p in 0..1); NaN for an empty set.</summary>
        public static double Pct(IEnumerable<double> xs, double p)
        {
            var a = xs.OrderBy(x => x).ToArray();
            if (a.Length == 0) return double.NaN;
            double idx = p * (a.Length - 1);
            int lo = (int)Math.Floor(idx), hi = (int)Math.Ceiling(idx);
            return a[lo] + (a[hi] - a[lo]) * (idx - lo);
        }

        static string P(double share) => (100 * share).ToString("F0", CultureInfo.InvariantCulture) + "%";
        static string F(double v, int digits = 2) => v.ToString("F" + digits, CultureInfo.InvariantCulture);

        public void Print(TextWriter w)
        {
            w.WriteLine();
            w.WriteLine("=================== " + Bot + " (" + Games + " games) ===================");
            w.WriteLine("titans defeated: mean " + F(DefeatedMean) + "  p10 " + F(DefeatedP10, 0) + " p50 " + F(DefeatedP50, 0) + " p90 " + F(DefeatedP90, 0) + " max " + F(DefeatedMax, 0));
            w.WriteLine("run length (s): mean " + F(RunMean, 0) + " p10 " + F(RunP10, 0) + " p50 " + F(RunP50, 0) + " p90 " + F(RunP90, 0)
                + "; runs ending <20s " + P(Under20) + "  <30s " + P(Under30) + "  <60s " + P(Under60) + "  >180s " + P(Over180) + "; score p50 " + F(ScoreP50, 0));
            var reasons = LoseShare.Where(kv => kv.Key != LoseReason.None).Select(kv => kv.Key + " " + P(kv.Value));
            w.WriteLine("lose reasons: " + string.Join("  ", reasons) + "  timeout(30 min cap) " + P(TimeoutShare)
                + "; slotsFull from own tap: " + P(SelfTapShareOfSlotsFull));
            w.WriteLine("wall share of losses " + P(WallShareOfLosses) + "; margin at defeat p50 " + F(MarginP50All) + "; defeated 'la mustață' " + P(NearMissShare));
            w.WriteLine("lost on titan k: " + string.Join("  ", LostOnTitan.Take(12).Select(kv => "T" + kv.Key + ":" + P(kv.Value))) + "   (titan-1 loss " + P(TitanOneLoss) + ")");
            w.WriteLine("per-titan: clear time p50 (p10-p90) / approach / margin left p50 (p10) when defeated:");
            foreach (var t in PerTitan.Take(16))
                w.WriteLine("   titan " + t.Level.ToString().PadLeft(2) + (t.Role == TitanRole.Sprint ? " S" : t.Role == TitanRole.Boss ? " B" : "  ") + ": n=" + t.Count.ToString().PadLeft(4) + "  clear " + F(t.ClearP50, 0).PadLeft(4) + "s (" + F(t.ClearP10, 0) + "-" + F(t.ClearP90, 0)
                    + ")  approach " + F(t.ApproachMean, 0).PadLeft(4) + "s  margin " + F(t.MarginP50) + " (" + F(t.MarginP10) + ")");
            w.WriteLine("   median clear over titans 1-16 (n>=10): " + F(ClearP50Min, 0) + "-" + F(ClearP50Max, 0) + " s; sprint max " + F(SprintClearP50Max, 0) + " s; boss max " + F(BossClearP50Max, 0) + " s");
            w.WriteLine("time shares: dead air (nothing firing) " + P(DeadAirShare) + "  line full " + P(LineFullShare) + "  waiting by choice " + P(WaitChoiceShare)
                + "  danger>=75% " + P(Danger75Share) + "  dead cannon parked " + P(DeadParkedShare));
            w.WriteLine("max continuous stuck time per run (s): p50 " + F(MaxStuckP50) + " p90 " + F(MaxStuckP90) + " max " + F(MaxStuckMax));
            w.WriteLine("taps/min " + F(TapsPerMinute, 1) + "  opportunities: act " + P(ActShare) + " wait " + P(WaitShare) + " line full " + P(BlockedShare));
            w.WriteLine("decision points (free slot): zero-choice " + P(ZeroChoiceShare) + "  >=2 visible colors " + P(MeaningfulChoiceShare)
                + "  no visible option " + P(NoVisibleOptionShare) + "  every tray cannon dead " + F(100 * OnlyDeadTrayShare, 1) + "%");
            w.WriteLine("pieces: " + PieceCount + " => " + F(PiecesPerTitan) + "/titan; from joint shots " + P(JointPieceShare) + "; size p50 " + F(PieceSizeP50, 0) + " p90 " + F(PieceSizeP90, 0)
                + "; t-into-titan p50 " + F(FirstPieceTimeP50, 0) + "s");
            w.WriteLine("stranded line cannons (color vanished after a fall): " + F(StrandedPerGame) + "/game; dead cannons on the line at a slotsFull loss " + F(DeadOnLineAtSlotsFullLoss) + "/5");
            w.WriteLine("clean cuts: " + F(CleanCutsPerTitan) + "/titan (" + P(CleanShare) + " of pieces); stars over defeated titans: s2 decapitation " + P(StarDecapitationRate)
                + "  s3 clean line " + P(StarCleanLineRate));
            w.WriteLine("'+1 Loc' trigger (>=4 taken, >=3 idle for 1.0 s) on " + F(100 * ExtraSlotTriggerShare, 1) + "% of " + ExtraSlotTitans + " titans n>=2 (ship check: >= 5%)");
        }

        /// <summary>ADS-2 revive mode: what one revive at the first loss is worth.</summary>
        public static void PrintRevive(TextWriter w, BotKind bot, float pushBack, IList<GameStats> gs)
        {
            var revived = gs.Where(g => g.Revived).ToList();
            int firstLosses = gs.Count(g => g.FirstLossReason != LoseReason.None);
            double gain1 = revived.Count == 0 ? 0 : (double)revived.Count(g => g.DefeatedAfterRevive >= 1) / revived.Count;
            w.WriteLine(bot + " revive push-back " + F(pushBack) + ": offered at " + P(firstLosses == 0 ? 0 : (double)gs.Count(g => g.ReviveOffered) / firstLosses) + " of first losses ("
                + revived.Count + " revives); >= 1 more titan in " + P(gain1) + "; titans after p50 " + F(Pct(revived.Select(g => (double)g.DefeatedAfterRevive), .5), 1)
                + " mean " + F(revived.Count == 0 ? 0 : revived.Average(g => (double)g.DefeatedAfterRevive)) + "; seconds after p50 " + F(Pct(revived.Select(g => g.SecondsAfterRevive), .5), 0)
                + "; by loss: " + string.Join(" ", revived.GroupBy(g => g.FirstLossReason).Select(x => x.Key + " " + x.Count())));
        }
    }
}
