using System;
using System.Collections.Generic;

namespace HumanCannon.Core
{
    /// <summary>Completely random groups that respect the current tier. MVP step 1 (no director).</summary>
    public sealed class RandomGroupSource : IGroupSource
    {
        readonly IRandom random;
        public RandomGroupSource(IRandom random) { this.random = random; }
        public Group Next(Round round) => GroupGenerator.Generate(round.Tier, random);
        public void OnRevive(Round round) { }
    }

    public static class GroupGenerator
    {
        /// <summary>A random group for the tier: size, number of distinct colors, colors laid out in contiguous blocks.</summary>
        public static Group Generate(ScoreTier tier, IRandom random)
        {
            int size = random.Range(tier.MinSize, tier.MaxSize + 1);
            int maxDistinct = Math.Min(Math.Min(tier.MaxDistinct, size), tier.Colors);
            int minDistinct = Math.Min(tier.MinDistinct, maxDistinct);
            int distinct = random.Range(minDistinct, maxDistinct + 1);

            // Pick `distinct` different colors.
            var palette = new List<int>();
            for (int c = 0; c < tier.Colors; c++) palette.Add(c);
            var chosen = new int[distinct];
            for (int i = 0; i < distinct; i++)
            {
                int j = random.Range(0, palette.Count);
                chosen[i] = palette[j];
                palette.RemoveAt(j);
            }

            // Split `size` into `distinct` positive block lengths.
            var lengths = new int[distinct];
            for (int i = 0; i < distinct; i++) lengths[i] = 1;
            for (int extra = size - distinct; extra > 0; extra--) lengths[random.Range(0, distinct)]++;

            var colors = new int[size];
            int k = 0;
            for (int i = 0; i < distinct; i++)
                for (int n = 0; n < lengths[i]; n++)
                    colors[k++] = chosen[i];
            return new Group(colors);
        }
    }

    /// <summary>
    /// The hidden group director: generates candidates, scores them on the predicted board and picks the one
    /// whose helpfulness percentile H is closest to 1 − D.
    /// </summary>
    public sealed class Director : IGroupSource
    {
        readonly GameConfig cfg;
        readonly HiddenRules rules;
        readonly IRandom random;

        public DirectorDecision LastDecision { get; private set; }

        public Director(GameConfig cfg, HiddenRules rules, IRandom random)
        {
            this.cfg = cfg;
            this.rules = rules;
            this.random = random;
        }

        public void OnRevive(Round round) => rules.OnRevive();

        public Group Next(Round round)
        {
            var decision = rules.Decide(round);
            float d = decision.D;
            if (decision.Mode == DirectorMode.Normal && cfg.DNoise > 0f)
                d += (random.Value() * 2f - 1f) * cfg.DNoise;
            d = Math.Max(0f, Math.Min(1f, d));
            decision.D = d;
            LastDecision = decision;

            var tier = round.Tier;
            var predicted = PredictBoard(round);

            int n = Math.Max(1, cfg.DirectorCandidates);
            var candidates = new List<Candidate>(n);
            for (int i = 0; i < n; i++)
            {
                var g = GroupGenerator.Generate(tier, random);
                var eval = EvaluateBest(predicted, g);
                candidates.Add(new Candidate { Group = g, Fits = eval.Fits, Score = eval.Score, Chain = eval.MaxStep >= 2 });
            }
            AssignPercentiles(candidates);

            var pool = candidates.FindAll(c => c.Fits);
            if (pool.Count == 0) pool = candidates;
            if (decision.Mode == DirectorMode.Assist)
            {
                var chains = pool.FindAll(c => c.Chain);
                if (chains.Count > 0) pool = chains;
            }

            float target = 1f - d;
            pool.Sort((a, b) => Math.Abs(a.H - target).CompareTo(Math.Abs(b.H - target)));
            int pick = random.Range(0, Math.Min(cfg.DirectorPickPool, pool.Count));
            var chosen = pool[pick];
            chosen.Group.DirectorH = chosen.H;
            chosen.Group.DirectorMode = decision.Mode;
            return chosen.Group;
        }

        /// <summary>
        /// The board the new group will land on: the cannon group and the queued groups are each placed
        /// on their best pedestal of the current board, in order.
        /// </summary>
        public Board PredictBoard(Round round)
        {
            var board = round.Board.Clone();
            if (round.Cannon != null) PlaceBest(board, round.Cannon);
            foreach (var g in round.Queue) PlaceBest(board, g);
            return board;
        }

        void PlaceBest(Board board, Group g)
        {
            var eval = EvaluateBest(board, g);
            if (eval.Fits) Resolver.Place(board, eval.Pedestal, g, cfg);
        }

        public struct Evaluation
        {
            public bool Fits;
            public int Pedestal;
            public float Score;
            public int MaxStep;
        }

        public Evaluation EvaluateBest(Board board, Group g)
        {
            var best = new Evaluation { Fits = false, Pedestal = -1, Score = float.NegativeInfinity };
            for (int p = 0; p < board.Count; p++)
            {
                if (!board.CanPlace(p, g.Size)) continue;
                var e = Evaluate(board, p, g);
                if (!best.Fits || e.Score > best.Score) best = e;
            }
            return best;
        }

        /// <summary>
        /// Points of clears and chains + 0.5 × growth of a same-colored top run
        /// − penalty for different colors buried − penalty for height ≥ danger.
        /// </summary>
        public Evaluation Evaluate(Board board, int p, Group g)
        {
            board.TopRun(p, out int topColor, out _);
            var sim = board.Clone();
            var placement = Resolver.Place(sim, p, g, cfg);

            float growth = 0f;
            if (topColor == g.Bottom)
                for (int i = 0; i < g.Size && g.Colors[i] == topColor; i++) growth++;

            int buried = g.InternalBoundaries + (topColor >= 0 && topColor != g.Bottom ? 1 : 0);
            if (placement.PeopleCleared > 0) buried = 0;

            int over = sim.Height(p) - (cfg.DangerHeight - 1);
            float heightPenalty = over > 0 ? over * cfg.WeightHeight : 0f;

            float score = placement.Points / (float)cfg.PointsPerPerson
                          + cfg.WeightSameColorGrowth * growth
                          - cfg.WeightBuriedColor * buried
                          - heightPenalty;
            return new Evaluation { Fits = true, Pedestal = p, Score = score, MaxStep = placement.MaxStep };
        }

        sealed class Candidate
        {
            public Group Group;
            public bool Fits;
            public float Score;
            public bool Chain;
            public float H;
        }

        /// <summary>Scores become percentiles H in [0, 1] (ties share the average rank). Candidates that fit nowhere get H = 0.</summary>
        static void AssignPercentiles(List<Candidate> candidates)
        {
            int n = candidates.Count;
            if (n == 1)
            {
                candidates[0].H = candidates[0].Fits ? 1f : 0f;
                return;
            }
            var sorted = new List<Candidate>(candidates);
            sorted.Sort((a, b) => a.Score.CompareTo(b.Score));
            int i = 0;
            while (i < n)
            {
                int j = i;
                while (j + 1 < n && sorted[j + 1].Score == sorted[i].Score) j++;
                float h = (i + j) / 2f / (n - 1);
                for (int k = i; k <= j; k++) sorted[k].H = sorted[k].Fits ? h : 0f;
                i = j + 1;
            }
        }
    }
}
