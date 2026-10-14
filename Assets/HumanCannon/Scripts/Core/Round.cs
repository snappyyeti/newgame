using System;
using System.Collections.Generic;

namespace HumanCannon.Core
{
    /// <summary>Decides every new group that joins the end of the queue.</summary>
    public interface IGroupSource
    {
        Group Next(Round round);
        void OnRevive(Round round);
    }

    public sealed class FireResult
    {
        public int Pedestal;
        public Group Group;
        public PlacementResult Placement;
        public int Points;
        public bool ArenaCleared;
        public List<BoardEvent> Events => Placement.Events;
    }

    /// <summary>One round of Human Cannon: rules 1–7, scoring and the revive. No Unity types.</summary>
    public sealed class Round
    {
        public readonly GameConfig Config;
        public readonly Board Board;
        public readonly List<Group> Queue = new List<Group>();
        public readonly int RecordBefore;
        /// <summary>1-based run number over the life of the account.</summary>
        public readonly int RunNumber;

        public Group Cannon { get; private set; }
        public Group Reserve { get; private set; }
        public bool SwapUsed { get; private set; }
        public int Score { get; private set; }
        public int GroupsFired { get; private set; }
        public int GroupsGenerated { get; private set; }
        public int RevivesUsed { get; private set; }
        public bool IsOver { get; private set; }

        readonly IGroupSource source;
        readonly IRandom random;

        public Round(GameConfig config, IGroupSource source, IRandom random, int recordBefore, int runNumber)
        {
            Config = config;
            this.source = source;
            this.random = random;
            RecordBefore = recordBefore;
            RunNumber = runNumber;
            Board = new Board(config.PedestalCount, config.PedestalCapacity);

            Cannon = Generate();
            while (Queue.Count < config.QueueLength) Queue.Add(Generate());
        }

        public ScoreTier Tier => Config.TierFor(Score);
        public bool UsedRevive => RevivesUsed > 0;

        public bool CanFire(int pedestal) => !IsOver && Board.CanPlace(pedestal, Cannon.Size);

        /// <summary>
        /// Rule 5 allows one swap between two shots. If the cannon group fits nowhere the swap is allowed again,
        /// otherwise a player who swapped in a group that does not fit would be stuck with no legal move.
        /// </summary>
        public bool CanSwap => !IsOver && (!SwapUsed || !Board.FitsAnywhere(Cannon.Size));

        public FireResult Fire(int pedestal)
        {
            if (!CanFire(pedestal)) throw new InvalidOperationException($"Cannot fire at pedestal {pedestal}.");

            var group = Cannon;
            var placement = Resolver.Place(Board, pedestal, group, Config);
            var result = new FireResult { Pedestal = pedestal, Group = group, Placement = placement, Points = placement.Points };

            if (Board.IsEmpty)
            {
                result.ArenaCleared = true;
                result.Points += Config.ArenaClearBonus;
                placement.Events.Add(new BoardEvent { Kind = BoardEventKind.ArenaClear, Pedestal = -1, Target = -1, Points = Config.ArenaClearBonus });
            }

            Score += result.Points;
            GroupsFired++;
            SwapUsed = false;
            AdvanceQueueIntoCannon();
            UpdateOver();
            return result;
        }

        public void Swap()
        {
            if (!CanSwap) throw new InvalidOperationException("Swap not allowed now.");
            if (Reserve == null)
            {
                Reserve = Cannon;
                AdvanceQueueIntoCannon();
            }
            else
            {
                var tmp = Reserve;
                Reserve = Cannon;
                Cannon = tmp;
            }
            SwapUsed = true;
            UpdateOver();
        }

        public bool CanRevive => IsOver && RevivesUsed < Config.MaxRevivesPerRun;

        /// <summary>
        /// The elephant knocks over the tallest pedestals (no points) and a random bonus of 0–10% of the record is added.
        /// Returns the knockdown events and the bonus.
        /// </summary>
        public List<BoardEvent> Revive(out int bonus)
        {
            if (!CanRevive) throw new InvalidOperationException("Revive not available.");

            var order = new List<int>();
            for (int p = 0; p < Board.Count; p++) order.Add(p);
            // Tallest first; on equal height the left one goes first.
            order.Sort((a, b) => Board.Height(a) != Board.Height(b) ? Board.Height(b).CompareTo(Board.Height(a)) : a.CompareTo(b));

            var events = new List<BoardEvent>();
            for (int i = 0; i < Math.Min(Config.RevivePedestalsCleared, order.Count); i++)
            {
                int p = order[i];
                if (Board.Height(p) == 0) continue;
                events.Add(new BoardEvent { Kind = BoardEventKind.Knockdown, Pedestal = p, Target = p, Count = Board.Height(p) });
                Board.ClearPedestal(p);
            }

            int maxBonus = (int)Math.Floor(Config.ReviveBonusMax * RecordBefore);
            bonus = maxBonus > 0 ? random.Range(0, maxBonus + 1) : 0;
            Score += bonus;

            RevivesUsed++;
            SwapUsed = false;
            IsOver = false;
            source.OnRevive(this);
            UpdateOver();
            return events;
        }

        void AdvanceQueueIntoCannon()
        {
            Cannon = Queue[0];
            Queue.RemoveAt(0);
            Queue.Add(Generate());
        }

        Group Generate()
        {
            var g = source.Next(this);
            GroupsGenerated++;
            return g;
        }

        /// <summary>Rule 7: the round ends when neither the cannon group nor the reserve group fits anywhere.</summary>
        void UpdateOver()
        {
            bool cannonFits = Board.FitsAnywhere(Cannon.Size);
            // With an empty reserve the player can still park the cannon group and draw the next one.
            bool reserveFits = Reserve == null || Board.FitsAnywhere(Reserve.Size);
            IsOver = !cannonFits && !reserveFits;
        }
    }
}
