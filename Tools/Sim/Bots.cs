using System;
using System.Collections.Generic;
using Titan.Core;

namespace Titan.Sim
{
    public enum BotKind
    {
        /// <summary>Taps any deployable tray cannon.</summary>
        Random,
        /// <summary>Prefers cannons whose color is visible (the biggest one).</summary>
        Greedy,
        /// <summary>Joint-aware: prefers the cut, avoids parking idle cannons on a crowded line.</summary>
        Smart,
    }

    /// <summary>
    /// The three simulator bots. A bot only sees a <see cref="BotView"/> (plain numbers, no game objects)
    /// and answers with a tray index to tap, or -1 to wait. The runner performs the tap.
    ///
    /// ETHICS LOCK: bots never use ad helpers (revive, extra slot) or anything ad-related. They cannot reach the
    /// game object at all, and BalanceTests greps this file for helper and ad identifiers. Keep it that way:
    /// helper measurement modes (for example ADS-2 revive value) belong in the runner, never here.
    /// </summary>
    public static class Bots
    {
        public static BotKind Parse(string name)
        {
            switch (name.Trim().ToLowerInvariant())
            {
                case "random": return BotKind.Random;
                case "greedy": return BotKind.Greedy;
                case "smart": return BotKind.Smart;
            }
            throw new ArgumentException("unknown bot '" + name + "' (random, greedy, smart)");
        }

        /// <summary>Tray index to tap now, or -1 to wait.</summary>
        public static int Choose(BotKind bot, BotView v, IRandom r)
        {
            var idx = new List<int>();
            for (int i = 0; i < v.TraySize; i++) if (v.HasTray(i) && v.Tappable(i)) idx.Add(i); // a refused tap is never chosen
            if (idx.Count == 0 || v.FreeSlots == 0) return -1;
            switch (bot)
            {
                case BotKind.Random: return idx[r.Range(0, idx.Count)];
                case BotKind.Greedy: return Greedy(v, r, idx);
                case BotKind.Smart: return Smart(v, idx);
            }
            return -1;
        }

        /// <summary>The biggest cannon with a visible color; otherwise a random one if nothing fires or the line is roomy.</summary>
        static int Greedy(BotView v, IRandom r, List<int> idx)
        {
            var vis = v.VisibleCounts();
            int best = -1;
            foreach (var i in idx)
                if (vis[v.TrayColor(i)] > 0 && (best < 0 || v.TrayAmmo(i) > v.TrayAmmo(best))) best = i;
            if (best >= 0) return best;
            if (v.FiringCount == 0 || v.FreeSlots >= 4) return idx[r.Range(0, idx.Count)];
            return -1;
        }

        /// <summary>
        /// Scores every tray cannon: visible colors first, a visible joint (the cut) gets a big bonus and a "TAIE!" tag
        /// (the player-visible cut prediction) another one, a color already
        /// committed on the line a malus, cannons with nothing left to hit go last. An idle cannon is parked only when
        /// nothing fires (waiting cannot help) or the line still has room, so it does not fill the last free slot
        /// while something else is still shooting.
        /// </summary>
        static int Smart(BotView v, List<int> idx)
        {
            var vis = v.VisibleCounts();
            var cnt = v.CubeCounts();
            var committed = new int[v.ColorCount];
            for (int s = 0; s < v.LineSlots; s++)
                if (v.LineColor(s) >= 0) committed[v.LineColor(s)] += v.LineAmmo(s);

            int best = -1;
            double bestScore = double.MinValue;
            foreach (var i in idx)
            {
                int color = v.TrayColor(i), ammo = v.TrayAmmo(i);
                int left = cnt[color] - committed[color];
                double s;
                if (left <= 0) s = -1000 - ammo; // surplus or dead: nothing left for it
                else if (vis[color] > 0)
                    s = 100 + Math.Min(vis[color], ammo) - (committed[color] > 0 ? 25 : 0) + (v.JointVisible(color) ? 80 : 0) + 0.3 * ammo + (v.TrayCutShot(i) >= 0 ? 60 : 0);
                else s = -50 + Math.Min(left, ammo) * 0.1;
                if (s > bestScore)
                {
                    bestScore = s;
                    best = i;
                }
            }
            if (bestScore >= 0) return best;
            int free = v.FreeSlots, firing = v.FiringCount;
            if (bestScore > -1000) return (firing == 0 || free >= 3) ? best : -1;
            // Only dead or surplus cannons left in the tray: they must be parked to get a refill.
            return (firing == 0 || free >= 2) ? best : -1;
        }
    }
}
