using System;
using System.Collections.Generic;

namespace HumanCannon.Core
{
    public enum BoardEventKind
    {
        /// <summary>A group landed on Pedestal. Count = group size.</summary>
        Land,
        /// <summary>The top run of Pedestal formed a pyramid and vanished.</summary>
        Clear,
        /// <summary>Count people jumped from Pedestal to Target (always followed by a Clear on Target).</summary>
        Jump,
        /// <summary>All pedestals are empty after a shot.</summary>
        ArenaClear,
        /// <summary>The revive elephant knocked Pedestal over. Count = people removed, no points.</summary>
        Knockdown,
    }

    public struct BoardEvent
    {
        public BoardEventKind Kind;
        public int Pedestal;
        public int Target;
        public int Color;
        public int Count;
        /// <summary>Chain step: 1 for the first clear, 2 after the first jump, and so on.</summary>
        public int Step;
        public int Points;

        public override string ToString() => $"{Kind}(p{Pedestal}->{Target} c{Color} x{Count} s{Step} +{Points})";
    }

    public sealed class PlacementResult
    {
        public readonly List<BoardEvent> Events = new List<BoardEvent>();
        public int Points;
        public int PeopleCleared;
        /// <summary>Highest chain step reached (0 = nothing cleared, 1 = one clear, 2+ = chain).</summary>
        public int MaxStep;
    }

    /// <summary>Rules 2–4: placing a group, local clears and the neighbour chain. Pure and deterministic.</summary>
    public static class Resolver
    {
        public static int ClearPoints(GameConfig cfg, int runLength, int step)
        {
            int normal = Math.Min(runLength, cfg.ClearRun);
            int extra = Math.Max(0, runLength - cfg.ClearRun);
            return cfg.PointsPerPerson * step * (normal + 2 * extra);
        }

        /// <summary>Places the whole group on P and resolves clears and chain jumps. Caller must check CanPlace first.</summary>
        public static PlacementResult Place(Board board, int p, Group group, GameConfig cfg)
        {
            if (!board.CanPlace(p, group.Size))
                throw new InvalidOperationException($"Group of {group.Size} does not fit on pedestal {p}.");

            var result = new PlacementResult();
            foreach (var c in group.Colors) board.Push(p, c);
            result.Events.Add(new BoardEvent { Kind = BoardEventKind.Land, Pedestal = p, Target = p, Color = group.Colors[group.Size - 1], Count = group.Size });

            board.TopRun(p, out int color, out int length);
            if (length < cfg.ClearRun) return result;

            int step = 1;
            int current = p;
            while (true)
            {
                // Rule 3: the whole top run vanishes; what is underneath stays.
                int pts = ClearPoints(cfg, length, step);
                board.PopTop(current, length);
                result.Points += pts;
                result.PeopleCleared += length;
                result.MaxStep = step;
                result.Events.Add(new BoardEvent { Kind = BoardEventKind.Clear, Pedestal = current, Target = current, Color = color, Count = length, Step = step, Points = pts });

                // Rule 4: does the newly exposed run jump onto a neighbour and clear there?
                board.TopRun(current, out int x, out int k);
                if (k == 0) break;

                int target = -1;
                int targetRun = 0;
                for (int n = current - 1; n <= current + 1; n += 2)
                {
                    if (n < 0 || n >= board.Count) continue;
                    board.TopRun(n, out int nc, out int nk);
                    if (nc != x || nk == 0) continue;
                    if (k + nk < cfg.ClearRun) continue;
                    if (board.Height(n) + k > board.Capacity) continue;
                    // Longer top run wins; on a tie the left one (checked first) keeps it.
                    if (nk > targetRun)
                    {
                        target = n;
                        targetRun = nk;
                    }
                }
                if (target < 0) break;

                board.PopTop(current, k);
                for (int i = 0; i < k; i++) board.Push(target, x);
                step++;
                result.Events.Add(new BoardEvent { Kind = BoardEventKind.Jump, Pedestal = current, Target = target, Color = x, Count = k, Step = step });

                current = target;
                color = x;
                length = k + targetRun;
            }

            return result;
        }
    }
}
