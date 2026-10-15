using System;
using System.Collections.Generic;

namespace SplashSort.Core
{
    public enum BoardEventKind
    {
        /// <summary>A ring was dropped on pool From (= To).</summary>
        Place,
        /// <summary>Count swimmers of Color hopped from pool From to pool To.</summary>
        Merge,
        /// <summary>The top run of pool From (Count swimmers of Color) splashed out. Step is the cascade step.</summary>
        Splash,
    }

    public struct BoardEvent
    {
        public BoardEventKind Kind;
        public int From;
        public int To;
        public int Color;
        public int Count;
        /// <summary>Cascade step: 1 for the first splash wave, 2 for the next, and so on (0 for merges before any splash).</summary>
        public int Step;
        public int Points;

        public override string ToString() => $"{Kind}({From}->{To} c{Color} x{Count} s{Step} +{Points})";
    }

    public sealed class PlaceResult
    {
        public readonly List<BoardEvent> Events = new List<BoardEvent>();
        public int Points;
        public int SwimmersRemoved;
        /// <summary>Number of splash waves (0 = no splash, 2+ = cascade).</summary>
        public int CascadeSteps;
    }

    /// <summary>The automatic merge, SPLASH and cascade. Pure and deterministic.</summary>
    public static class Resolver
    {
        public static PlaceResult Place(Board board, int pool, Ring ring, SplashConfig cfg)
        {
            if (!board.IsEmpty(pool)) throw new InvalidOperationException($"Pool {pool} is not empty.");

            var result = new PlaceResult();
            foreach (var c in ring.Colors) board.Push(pool, c);
            result.Events.Add(new BoardEvent { Kind = BoardEventKind.Place, From = pool, To = pool, Color = ring.Colors[ring.Size - 1], Count = ring.Size });

            int focus = pool;
            int step = 0;
            while (true)
            {
                // 1–3: merge neighbouring pools with the same top color until no pair is left.
                // Every merge joins two runs into one, so the number of runs on the board shrinks and this always ends.
                while (FindMerge(board, pool, focus, out int from, out int to))
                {
                    board.TopRun(from, out int color, out int count);
                    board.PopTop(from, count);
                    for (int i = 0; i < count; i++) board.Push(to, color);
                    result.Events.Add(new BoardEvent { Kind = BoardEventKind.Merge, From = from, To = to, Color = color, Count = count, Step = step });
                    focus = to;
                }

                // 4: every stack with 10+ of one color on top splashes out.
                bool splashed = false;
                int wave = step + 1;
                for (int p = 0; p < board.Count; p++)
                {
                    board.TopRun(p, out int color, out int count);
                    if (count < cfg.SplashRun) continue;
                    int points = cfg.PointsPerSwimmer * count * wave;
                    board.PopTop(p, count);
                    result.Points += points;
                    result.SwimmersRemoved += count;
                    result.Events.Add(new BoardEvent { Kind = BoardEventKind.Splash, From = p, To = p, Color = color, Count = count, Step = wave, Points = points });
                    if (!splashed) focus = p;
                    splashed = true;
                }
                if (!splashed) break;

                // 5: new colors on top after a splash -> back to step 1. That is the cascade.
                step = wave;
                result.CascadeSteps = step;
            }

            return result;
        }

        /// <summary>
        /// Picks the next pair of neighbours with the same top color, and which way the run goes.
        /// The shorter run moves onto the longer one; on a tie it moves onto the pool where the ring was just placed,
        /// then onto the most recently changed pool (the focus), then onto the lower index.
        /// Pairs are tried in this order: moves into the focus first (so the focus keeps gathering), then other pairs
        /// touching the focus, then the rest; inside each group the longest combined run goes first, then the lower index.
        /// </summary>
        static bool FindMerge(Board board, int placed, int focus, out int from, out int to)
        {
            from = to = -1;
            int bestRank = -1, bestCombined = -1;

            for (int a = 0; a < board.Count; a++)
            {
                board.TopRun(a, out int ca, out int ka);
                if (ka == 0) continue;
                foreach (int b in board.Neighbors(a))
                {
                    if (b < a) continue;
                    board.TopRun(b, out int cb, out int kb);
                    if (kb == 0 || cb != ca) continue;

                    Direction(a, ka, b, kb, placed, focus, out int f, out int t);
                    int rank = t == focus ? 2 : (a == focus || b == focus) ? 1 : 0;
                    int combined = ka + kb;
                    if (rank > bestRank || (rank == bestRank && combined > bestCombined))
                    {
                        bestRank = rank;
                        bestCombined = combined;
                        from = f;
                        to = t;
                    }
                }
            }
            return from >= 0;
        }

        static void Direction(int a, int ka, int b, int kb, int placed, int focus, out int from, out int to)
        {
            if (ka != kb)
            {
                from = ka < kb ? a : b;
                to = ka < kb ? b : a;
                return;
            }
            to = a == placed || b == placed ? placed
                : a == focus || b == focus ? focus
                : Math.Min(a, b);
            from = to == a ? b : a;
        }
    }

    /// <summary>One game: the board, the tray of 3 rings, the score and the loss check.</summary>
    public sealed class Round
    {
        public readonly SplashConfig Config;
        public readonly Board Board;
        public readonly Ring[] Tray;
        readonly IRandom random;

        public int Score { get; private set; }
        public bool IsOver { get; private set; }
        public int RingsPlaced { get; private set; }

        public Round(SplashConfig config, IRandom random)
        {
            Config = config;
            this.random = random;
            Board = new Board(config.Columns, config.Rows);
            Tray = new Ring[config.TraySize];
            RefillTray();
        }

        public int ColorsInPlay => Config.ColorsFor(Score);

        public bool CanPlace(int slot, int pool) =>
            !IsOver && slot >= 0 && slot < Tray.Length && Tray[slot] != null && pool >= 0 && pool < Board.Count && Board.IsEmpty(pool);

        public PlaceResult Place(int slot, int pool)
        {
            if (!CanPlace(slot, pool)) throw new InvalidOperationException($"Cannot place slot {slot} on pool {pool}.");
            var ring = Tray[slot];
            Tray[slot] = null;
            var result = Resolver.Place(Board, pool, ring, Config);
            Score += result.Points;
            RingsPlaced++;

            bool trayEmpty = true;
            foreach (var r in Tray) if (r != null) trayEmpty = false;
            if (trayEmpty) RefillTray();

            // There is always a ring left to place, so the game ends as soon as no pool is empty.
            IsOver = !Board.HasEmptyPool;
            return result;
        }

        /// <summary>Test hook: puts specific rings in the tray.</summary>
        public void SetTray(params Ring[] rings)
        {
            for (int i = 0; i < Tray.Length; i++) Tray[i] = i < rings.Length ? rings[i] : null;
        }

        void RefillTray()
        {
            int colors = ColorsInPlay;
            for (int i = 0; i < Tray.Length; i++) Tray[i] = Ring.Random(Config, colors, random);
        }
    }
}
