using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Titan.Core;

namespace Titan.Sim
{
    /// <summary>
    /// What a bot may see: plain numbers about the tray, the line and the titan. It exposes no game object,
    /// no way to tap and no helper of any kind (ethics lock: bots never use ad helpers).
    /// </summary>
    public sealed class BotView
    {
        readonly TitanGame game;

        internal BotView(TitanGame game)
        {
            this.game = game;
        }

        public int ColorCount => MonsterBuilder.MaxColors;
        public int TraySize => game.Tray.Length;
        public bool HasTray(int i) => game.Tray[i] != null;
        /// <summary>False when a tap on tray cannon i would be refused (line full, or the last-slot guard: the player sees the shake).</summary>
        public bool Tappable(int i) => game.CanDeploy(i);
        public int TrayColor(int i) => game.Tray[i] != null ? game.Tray[i].Color : -1;
        public int TrayAmmo(int i) => game.Tray[i] != null ? game.Tray[i].Ammo : 0;
        public int LineSlots => game.ActiveSlots;
        public int LineColor(int s) => game.Line[s] != null ? game.Line[s].Color : -1;
        public int LineAmmo(int s) => game.Line[s] != null ? game.Line[s].Ammo : 0;
        public int FreeSlots => SimRunner.Free(game);
        public int FiringCount => SimRunner.Firing(game);
        public int[] VisibleCounts() => SimRunner.VisibleCounts(game.Monster);
        public int[] CubeCounts() => SimRunner.Counts(game.Monster);

        /// <summary>True when a joint (shoulder or neck, Monster.IsJoint) of this color is visible.</summary>
        public bool JointVisible(int color) => game.Monster.HasVisibleJointOfColor(color);

        /// <summary>The "TAIE!" tag the player sees: 0-based shot of tray cannon i that cuts a piece, or -1.</summary>
        public int TrayCutShot(int i) => game.Tray[i] != null ? game.ShotsToSever(game.Tray[i]) : -1;
    }

    public struct TitanRecord
    {
        public int Level;
        public TitanRole Role;
        public bool NearMiss;
        public double ClearTime;
        public double Approach;
        /// <summary>Fraction of the start distance still left when the titan died.</summary>
        public double Margin;
    }

    public struct PieceRecord
    {
        public int Level;
        public int Size;
        public double TimeIntoTitan;
        public bool FromJointShot;
        public bool Clean;
    }

    /// <summary>
    /// Measurement modes of the runner. Balance runs use the defaults: every helper OFF (ethics lock, DESIGN.md 11.2).
    /// The revive mode exists ONLY to measure what "A doua suflare" is worth (ADS-2): the run is revived once, at its
    /// first loss, when the revive would be offered (≥ 1 titan defeated or ≥ 15% of the current one destroyed).
    /// The bot never decides it and never sees it.
    /// </summary>
    public sealed class SimOptions
    {
        public bool Revive;
        public float RevivePushBack = 0.25f;
        public float ReviveGraceSeconds = 2.0f;
        public float ReviveMinProgress = 0.15f;
    }

    /// <summary>Everything measured in one simulated run.</summary>
    public sealed class GameStats
    {
        public BotKind Bot;
        public ulong Seed;
        public int Score;
        public int Defeated;
        public double RunTime;
        /// <summary>None means the run hit the time cap.</summary>
        public LoseReason Reason;
        public int LoseLevel;
        /// <summary>The run ended on the bot's own tap (a 5th idle cannon).</summary>
        public bool LostOnTap;
        public readonly List<TitanRecord> Titans = new List<TitanRecord>();
        public readonly List<PieceRecord> Pieces = new List<PieceRecord>();

        public double DeadAir, LineFull, WaitChoice, Danger75Time;
        /// <summary>Longest stretch with nothing firing and no useful tap (0 free slots, or 1 free and no visible tray color).</summary>
        public double MaxStuck;
        public int Opportunities, Taps, Waits, BlockedOpportunities;
        public int Decisions, ZeroChoice, MeaningfulChoice, NoVisibleOption, OnlyDeadTray;
        /// <summary>Line cannons whose color vanished because a piece fell (counted on every tick after a fall).</summary>
        public int StrandedByPiece;
        public int DeadOnLineAtLoss;
        public double DeadParkedTime;

        /// <summary>CORE-5: clean pieces, defeated titans with the decapitation star (s2) and the clean-line star (s3).</summary>
        public int CleanCuts, StarDecapitation, StarCleanLine;

        /// <summary>ADS-2 "+1 Loc" ship check: titans n ≥ 2 played, and those where ≥ 4 slots taken with ≥ 3 idle held ≥ 1.0 s.</summary>
        public int ExtraSlotTitans, ExtraSlotTriggered;

        /// <summary>ADS-2 revive mode: the first loss was revivable (offered), was revived, and what came after.</summary>
        public bool ReviveOffered, Revived;
        public LoseReason FirstLossReason;
        public int DefeatedAtRevive;
        public double TimeAtRevive;
        public int DefeatedAfterRevive => Revived ? Defeated - DefeatedAtRevive : 0;
        public double SecondsAfterRevive => Revived ? RunTime - TimeAtRevive : 0;
    }

    /// <summary>Plays the real core with a bot at a fixed dt and records the metrics of DESIGN.md 12.1.</summary>
    public static class SimRunner
    {
        public const double Dt = 0.05;
        /// <summary>A run is cut at 30 minutes (DESIGN.md 12.1: Smart reaches the cap in &lt; 2% of games).</summary>
        public const double MaxTime = 1800;

        /// <summary>Free firing slots among the active ones (Line has a closed 6th entry for "+1 Loc").</summary>
        public static int Free(TitanGame g) => g.FreeSlots;

        public static int Firing(TitanGame g)
        {
            int n = 0;
            for (int s = 0; s < g.ActiveSlots; s++) if (g.Line[s] != null && g.Monster.HasVisibleOfColor(g.Line[s].Color)) n++;
            return n;
        }

        public static int[] VisibleCounts(Monster m)
        {
            var v = new int[MonsterBuilder.MaxColors];
            foreach (var kv in m.Cubes) if (m.IsVisible(kv.Key)) v[kv.Value]++;
            return v;
        }

        public static int[] Counts(Monster m)
        {
            var v = new int[MonsterBuilder.MaxColors];
            foreach (var kv in m.Cubes) v[kv.Value]++;
            return v;
        }

        /// <summary>Seed of game i of a batch. The same i gives the same titans and draws stream for every bot.</summary>
        public static ulong GameSeed(ulong masterSeed, int index) => SeededRandom.Derive(masterSeed, (ulong)index);

        /// <summary>Plays <paramref name="games"/> runs in parallel. Deterministic: results depend only on the seeds.</summary>
        public static GameStats[] RunMany(BotKind bot, TitanConfig cfg, int games, ulong masterSeed, SimOptions options = null)
        {
            var result = new GameStats[games];
            Parallel.For(0, games, i => result[i] = Play(bot, cfg, GameSeed(masterSeed, i), options));
            return result;
        }

        /// <summary>The ≥ 4 occupied / ≥ 3 idle condition of the "+1 Loc" wall button (AdConfig defaults).</summary>
        public static bool ExtraSlotCondition(TitanGame g)
        {
            int occupied = 0, idle = 0;
            for (int s = 0; s < g.ActiveSlots; s++)
            {
                var c = g.Line[s];
                if (c == null) continue;
                occupied++;
                if (!g.Monster.HasVisibleOfColor(c.Color)) idle++;
            }
            return occupied >= 4 && idle >= 3;
        }

        public static GameStats Play(BotKind bot, TitanConfig cfg, ulong seed, SimOptions options = null)
        {
            options = options ?? new SimOptions();
            var rng = new SeededRandom(SeededRandom.Hash(seed, 1));
            var botRng = new SeededRandom(SeededRandom.Hash(seed, 2));
            var g = new TitanGame(cfg, rng);
            var view = new BotView(g);
            var st = new GameStats { Bot = bot, Seed = seed };
            double t = 0, titanStart = 0, stuck = 0;
            double next = 0.8 + botRng.NextDouble() * 0.8; // first look at the tray
            int level = g.Level;
            g.Events.Clear();
            int lastDecision = -1; // 0 tap, 1 wait, 2 line full
            bool lastShotJoint = false;
            var titan = g.Monster; // the titan the events refer to (g.Monster may already be the next one)
            double extraHeld = 0;
            bool extraTriggered = false;
            int extraTitan = g.Level;

            // ADS-2 revive mode only (never in balance runs): revive once at the first loss, if it would be offered.
            bool TryRevive()
            {
                if (!options.Revive || st.ReviveOffered || st.Revived || !g.IsOver) return false;
                st.FirstLossReason = g.Lost;
                if (!g.CanRevive || (st.Defeated < 1 && g.ProgressFraction < options.ReviveMinProgress)) return false;
                st.ReviveOffered = true;
                st.DefeatedAtRevive = st.Defeated;
                st.TimeAtRevive = t;
                st.Revived = g.Revive(options.RevivePushBack, options.ReviveGraceSeconds);
                g.Events.Clear();
                stuck = 0;
                return st.Revived;
            }

            while (t < MaxTime && (!g.IsOver || TryRevive()))
            {
                if (t >= next)
                {
                    st.Opportunities++;
                    double delay;
                    if (Free(g) == 0)
                    {
                        st.BlockedOpportunities++;
                        lastDecision = 2;
                        delay = 0.3 + botRng.NextDouble() * 0.3;
                    }
                    else
                    {
                        ClassifyDecision(g, st);
                        int pick = Bots.Choose(bot, view, botRng);
                        if (pick >= 0 && g.Deploy(pick))
                        {
                            if (g.IsOver) st.LostOnTap = true;
                            st.Taps++;
                            lastDecision = 0;
                            delay = 0.6 + botRng.NextDouble() * 1.0;
                        }
                        else
                        {
                            st.Waits++;
                            lastDecision = 1;
                            delay = 0.3 + botRng.NextDouble() * 0.3;
                        }
                    }
                    next = t + delay;
                }
                if (g.IsOver)
                {
                    ReadEvents(g, st, ref level, ref titanStart, ref lastShotJoint, ref titan, t, g.Distance, g.ApproachSeconds, cfg);
                    if (TryRevive()) continue;
                    break;
                }

                // "+1 Loc" ship check: did the wall-button condition hold for 1.0 s during this titan (n >= 2)?
                if (g.Level != extraTitan)
                {
                    if (extraTitan >= 2)
                    {
                        st.ExtraSlotTitans++;
                        if (extraTriggered) st.ExtraSlotTriggered++;
                    }
                    extraTitan = g.Level;
                    extraHeld = 0;
                    extraTriggered = false;
                }
                extraHeld = ExtraSlotCondition(g) ? extraHeld + Dt : 0;
                if (extraHeld >= 1.0 - 1e-6) extraTriggered = true;

                // Time accounting, before the tick (the state the player is looking at).
                int firing = Firing(g);
                int free = Free(g);
                if (firing == 0) st.DeadAir += Dt;
                if (free == 0) st.LineFull += Dt;
                if (lastDecision == 1) st.WaitChoice += Dt;
                bool deadParked = false;
                var counts = Counts(g.Monster);
                foreach (var c in g.Line) if (c != null && counts[c.Color] == 0) deadParked = true;
                if (deadParked) st.DeadParkedTime += Dt;
                if (1.0 - g.Distance / cfg.StartDistance >= 0.75) st.Danger75Time += Dt;
                bool noUsefulTap = free == 0 || (free == 1 && !TrayHasVisible(g));
                stuck = firing == 0 && noUsefulTap ? stuck + Dt : 0;
                if (stuck > st.MaxStuck) st.MaxStuck = stuck;

                double distBefore = g.Distance;
                float approach = g.ApproachSeconds;
                g.Tick((float)Dt);
                t += Dt;
                ReadEvents(g, st, ref level, ref titanStart, ref lastShotJoint, ref titan, t, distBefore, approach, cfg);
            }
            if (extraTitan >= 2)
            {
                st.ExtraSlotTitans++;
                if (extraTriggered) st.ExtraSlotTriggered++;
            }
            st.RunTime = t;
            st.Score = g.Score;
            return st;
        }

        static void ReadEvents(TitanGame g, GameStats st, ref int level, ref double titanStart,
            ref bool lastShotJoint, ref Monster titan, double t, double distBefore, double approach, TitanConfig cfg)
        {
            bool pieceFell = false;
            foreach (var e in g.Events)
            {
                switch (e.Kind)
                {
                    case EventKind.Shot:
                        lastShotJoint = titan.IsJoint(e.Cube);
                        break;
                    case EventKind.PieceFell:
                        st.Pieces.Add(new PieceRecord { Level = level, Size = e.Piece.Count, TimeIntoTitan = t - titanStart, FromJointShot = lastShotJoint, Clean = e.Clean });
                        if (e.Clean) st.CleanCuts++;
                        pieceFell = true;
                        break;
                    case EventKind.TitanDefeated:
                        st.Defeated++;
                        if ((e.Stars & Stars.Decapitation) != 0) st.StarDecapitation++;
                        if ((e.Stars & Stars.CleanLine) != 0) st.StarCleanLine++;
                        st.Titans.Add(new TitanRecord { Level = level, Role = e.Role, NearMiss = e.NearMiss, ClearTime = t - titanStart, Approach = approach, Margin = distBefore / cfg.StartDistance });
                        break;
                    case EventKind.TitanSpawned:
                        titanStart = t;
                        level = e.Level;
                        titan = g.Monster;
                        break;
                    case EventKind.GameOver:
                        st.Reason = e.Reason;
                        st.LoseLevel = e.Level;
                        var counts = Counts(g.Monster);
                        foreach (var c in g.Line) if (c != null && counts[c.Color] == 0) st.DeadOnLineAtLoss++;
                        break;
                }
            }
            if (pieceFell && !g.IsOver)
            {
                var counts = Counts(g.Monster);
                foreach (var c in g.Line) if (c != null && c.Ammo > 0 && counts[c.Color] == 0) st.StrandedByPiece++;
            }
            g.Events.Clear();
        }

        static bool TrayHasVisible(TitanGame g)
        {
            foreach (var c in g.Tray) if (c != null && g.Monster.HasVisibleOfColor(c.Color)) return true;
            return false;
        }

        static void ClassifyDecision(TitanGame g, GameStats st)
        {
            var colors = new List<int>();
            int options = 0;
            foreach (var c in g.Tray)
            {
                if (c == null) continue;
                options++;
                if (!colors.Contains(c.Color)) colors.Add(c.Color);
            }
            if (options == 0) return;
            st.Decisions++;
            if (options == 1 || colors.Count == 1) st.ZeroChoice++;
            var vis = VisibleCounts(g.Monster);
            var cnt = Counts(g.Monster);
            int visibleColors = 0;
            bool allDead = true;
            foreach (var color in colors)
            {
                if (vis[color] > 0) visibleColors++;
                if (cnt[color] > 0) allDead = false;
            }
            if (visibleColors == 0) st.NoVisibleOption++;
            if (visibleColors >= 2) st.MeaningfulChoice++;
            if (allDead) st.OnlyDeadTray++;
        }
    }
}
