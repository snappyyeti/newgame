using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Titan.Core;

sealed class XRng : IRandom
{
    ulong s;
    public XRng(ulong seed) { s = seed * 0x9E3779B97F4A7C15UL + 0x1234567UL; if (s == 0) s = 1; }
    ulong Next() { s ^= s << 13; s ^= s >> 7; s ^= s << 17; return s; }
    public int Range(int min, int maxExclusive) => min + (int)(Next() % (ulong)(maxExclusive - min));
    public float Value() => (float)((Next() >> 11) * (1.0 / (1UL << 53)));
    public double D() => (Next() >> 11) * (1.0 / (1UL << 53));
}

static class Cloner
{
    public static T Clone<T>(T obj) => (T)Copy(obj, new Dictionary<object, object>(ReferenceEqualityComparer.Instance));

    static readonly Dictionary<Type, FieldInfo[]> fieldCache = new();

    static FieldInfo[] Fields(Type t)
    {
        lock (fieldCache)
        {
            if (fieldCache.TryGetValue(t, out var f)) return f;
            var list = new List<FieldInfo>();
            for (var tt = t; tt != null && tt != typeof(object); tt = tt.BaseType)
                list.AddRange(tt.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
            f = list.ToArray();
            fieldCache[t] = f;
            return f;
        }
    }

    static object Copy(object o, Dictionary<object, object> memo)
    {
        if (o == null) return null;
        var t = o.GetType();
        if (t.IsPrimitive || t.IsEnum || o is string || o is TitanConfig || o is Delegate || o is Type) return o;
        if (t.Name.Contains("Comparer")) return o;
        if (memo.TryGetValue(o, out var done)) return done;
        if (t.IsArray)
        {
            var a = (Array)o;
            var et = t.GetElementType();
            var na = (Array)a.Clone();
            memo[o] = na;
            if (!(et.IsPrimitive || et.IsEnum))
            {
                bool needs = !et.IsValueType || Fields(et).Any(f => !f.FieldType.IsPrimitive && !f.FieldType.IsEnum && !f.FieldType.IsValueType);
                if (needs)
                    for (int i = 0; i < a.Length; i++) na.SetValue(Copy(a.GetValue(i), memo), i);
            }
            return na;
        }
        if (t.IsValueType) return o; // our structs hold only primitives
        var n = RuntimeHelpers.GetUninitializedObject(t);
        memo[o] = n;
        foreach (var f in Fields(t))
        {
            var v = f.GetValue(o);
            f.SetValue(n, Copy(v, memo));
        }
        return n;
    }
}

static class Info
{
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

    /// <summary>Joint cubes: not anchored, with an anchored face neighbour (shoulders and neck).</summary>
    public static List<Int3> Joints(Monster m)
    {
        var list = new List<Int3>();
        foreach (var kv in m.Cubes)
        {
            if (m.IsAnchored(kv.Key)) continue;
            foreach (var d in Int3.Neighbours)
                if (m.Has(kv.Key + d) && m.IsAnchored(kv.Key + d)) { list.Add(kv.Key); break; }
        }
        return list;
    }
}

enum BotKind { Random, Greedy, Smart, Expert }

sealed class GameStats
{
    public BotKind Bot;
    public int Seed; public int Score; public bool LostOnTap; public int IdleBeforeLoss;
    public int Defeated;
    public double RunTime;
    public LoseReason Reason;
    public int LoseLevel;
    public List<(int level, double time, double approach)> TitanTimes = new();
    public List<(int level, int size, double tIntoTitan)> Pieces = new();
    public double DeadAir, Blocked, WaitChoice, ActTime, DeadParkedTime, DeadSlotSeconds;
    public double TFirstDead = -1; public int LevelFirstDead = -1;
    public double Danger75Time;
    public int Opportunities, Taps, Waits, BlockedOpp;
    public int DecisionsWithFreeSlot, ZeroChoice, SingleOption, AllSameColor, NoVisibleOption, OneVisibleDominant, MeaningfulChoice;
    public int DeadDrawn, Drawn, DeadDeployed, SurplusAmmoFromPieces;
    public double TFirstIdle3 = -1, TFirstDanger75 = -1, TFirstNoVisibleForced = -1;
    public int LevelFirstIdle3 = -1, LevelFirstDanger75 = -1;
    public int DeadOnLineAtLoss, IdleOnLineAtLoss;
    public double MarginAtDefeatSum; public int MarginCount; // fraction of approach left when titan defeated
    public List<(int level, double margin)> Margins = new();
    public double TimeLevel1to2;
    public List<double> LongestNoShotGap = new();
    public int ShotsTotal; public int PiecesFromJointShot, PiecesFromOther, StrandedByPiece, OnlyDeadTrayDecisions, ForcedDeadLast;
}

static class Exp
{
    static string E(string k) => Environment.GetEnvironmentVariable(k);
    public static TitanConfig Cfg()
    {
        var c = new TitanConfig();
        c.Salvage = E("SALVAGE") == "1"; c.FarthestFirst = E("FAR") == "1"; c.PerSlotRefill = E("SLOTREFILL") == "1"; c.Knock = E("KNOCK") == "1"; c.NoTriple = E("NOTRIPLE") == "1"; c.Cycle = E("CYCLE") == "1";
        if (E("ABASE") != null) c.ApproachBaseSeconds = float.Parse(E("ABASE"));
        if (E("APC") != null) c.ApproachSecondsPerCube = float.Parse(E("APC"));
        if (E("ASPD") != null) c.ApproachSpeedup = float.Parse(E("ASPD"));
        if (E("AMIN") != null) c.MinApproachSeconds = float.Parse(E("AMIN"));
        if (E("BIAS") != null) c.VisibleColorBias = float.Parse(E("BIAS"));
        if (E("SLOTS") != null) c.LineSlots = int.Parse(E("SLOTS"));
        if (E("KBPC") != null) c.KbPerCube = float.Parse(E("KBPC"));
        if (E("KBMAX") != null) c.KbMax = float.Parse(E("KBMAX"));
        if (E("STUNB") != null) c.StunBase = float.Parse(E("STUNB"));
        if (E("STUNMAX") != null) c.StunMax = float.Parse(E("STUNMAX"));
        return c;
    }
}

static class Runner
{
    const double Dt = 0.05;
    const double MaxTime = 1200;

    public static int Free(TitanGame g) => g.Line.Count(c => c == null);

    public static int Firing(TitanGame g)
    {
        int n = 0;
        foreach (var c in g.Line) if (c != null && g.Monster.HasVisibleOfColor(c.Color)) n++;
        return n;
    }

    public static bool Prune;
    public static GameStats Play(BotKind bot, int seed, int startLevel = 1)
    {
        var rng = new XRng((ulong)seed * 7 + 3);
        var botRng = new XRng((ulong)seed * 131 + 17);
        var cfg = Exp.Cfg();
        var g = new TitanGame(cfg, rng, startLevel);
        var st = new GameStats { Bot = bot, Seed = seed };
        double t = 0, titanStart = 0;
        double next = 0.8 + botRng.D() * 0.8; // first look at the tray
        double lastShot = 0, longestGap = 0;
        int level = g.Level;
        var joints = new HashSet<Int3>(Info.Joints(g.Monster));
        g.Events.Clear();
        int lastDecisionKind = -1; Int3 lastShotCube = default; bool piecesThisTick = false; // 0 act, 1 wait, 2 blocked
        CountDraw(g, st);

        while (!g.IsOver && t < MaxTime)
        {
            if (t >= next)
            {
                st.Opportunities++;
                double delay;
                if (Free(g) == 0)
                {
                    st.BlockedOpp++;
                    lastDecisionKind = 2;
                    delay = 0.3 + botRng.D() * 0.3;
                }
                else
                {
                    ClassifyDecision(g, st, t);
                    int pick = Choose(bot, g, botRng, joints);
                    if (pick >= 0)
                    {
                        var c = g.Tray[pick];
                        if (g.Monster.CountOfColor(c.Color) == 0) st.DeadDeployed++;
                        bool refill = g.Tray.Count(x => x != null) == 1;
                        g.Deploy(pick);
                        if (g.IsOver) st.LostOnTap = true;
                        st.Taps++;
                        lastDecisionKind = 0;
                        delay = 0.6 + botRng.D() * 1.0;
                        if (refill) CountDraw(g, st);
                    }
                    else
                    {
                        st.Waits++;
                        lastDecisionKind = 1;
                        delay = 0.3 + botRng.D() * 0.3;
                    }
                }
                next = t + delay;
            }

            // time accounting
            int firing = Firing(g);
            int free = Free(g);
            if (firing == 0) st.DeadAir += Dt;
            if (free == 0) st.Blocked += Dt;
            if (lastDecisionKind == 1) st.WaitChoice += Dt;
            int idle = g.Line.Count(c => c != null) - firing;
            int deadOnLine = g.Line.Count(c => c != null && g.Monster.CountOfColor(c.Color) == 0);
            if (deadOnLine > 0) { st.DeadParkedTime += Dt; st.DeadSlotSeconds += deadOnLine * Dt; if (st.TFirstDead < 0) { st.TFirstDead = t; st.LevelFirstDead = g.Level; } }
            if (idle >= 3 && st.TFirstIdle3 < 0) { st.TFirstIdle3 = t; st.LevelFirstIdle3 = g.Level; }
            double danger = 1.0 - g.Distance / cfg.StartDistance;
            if (danger >= 0.75)
            {
                st.Danger75Time += Dt;
                if (st.TFirstDanger75 < 0) { st.TFirstDanger75 = t; st.LevelFirstDanger75 = g.Level; }
            }

            double distBefore = g.Distance;
            float approach = g.ApproachSeconds;
            g.Tick((float)Dt);
            if (Prune && !g.IsOver) PruneSurplus(g);
            t += Dt;

            foreach (var e in g.Events)
            {
                switch (e.Kind)
                {
                    case EventKind.Shot:
                        lastShotCube = e.Cube;
                        st.ShotsTotal++;
                        longestGap = Math.Max(longestGap, t - lastShot);
                        lastShot = t;
                        break;
                    case EventKind.PieceFell:
                        st.Pieces.Add((level, e.Piece.Count, t - titanStart));
                        if (joints.Contains(lastShotCube)) st.PiecesFromJointShot++; else st.PiecesFromOther++;
                        piecesThisTick = true;
                        st.SurplusAmmoFromPieces += e.Piece.Count;
                        break;
                    case EventKind.TitanDefeated:
                        st.Defeated++;
                        st.TitanTimes.Add((level, t - titanStart, approach));
                        st.Margins.Add((level, distBefore / cfg.StartDistance));
                        st.LongestNoShotGap.Add(longestGap);
                        break;
                    case EventKind.TitanSpawned:
                        titanStart = t;
                        level = e.Level;
                        longestGap = 0;
                        lastShot = t;
                        joints = new HashSet<Int3>(Info.Joints(g.Monster));
                        CountDraw(g, st);
                        break;
                    case EventKind.GameOver:
                        st.Reason = e.Reason;
                        st.LoseLevel = e.Level;
                        st.DeadOnLineAtLoss = g.Line.Count(c => c != null && g.Monster.CountOfColor(c.Color) == 0);
                        st.IdleOnLineAtLoss = g.Line.Count(c => c != null && !g.Monster.HasVisibleOfColor(c.Color));
                        break;
                }
            }
            if (piecesThisTick && !g.IsOver)
                st.StrandedByPiece += g.Line.Count(c => c != null && c.Ammo > 0 && g.Monster.CountOfColor(c.Color) == 0);
            piecesThisTick = false;
            g.Events.Clear();
        }
        st.RunTime = t; st.Score = g.Score;
        return st;
    }

    static readonly FieldInfo deckField = typeof(TitanGame).GetField("deck", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly MethodInfo refill = typeof(TitanGame).GetMethod("RefillTray", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>Counterfactual fix: ammo in play never exceeds the cubes left (falling pieces take their ammo with them).</summary>
    static void PruneSurplus(TitanGame g)
    {
        var deck = (List<Cannon>)deckField.GetValue(g);
        var cnt = Info.Counts(g.Monster);
        for (int s = 0; s < g.Line.Length; s++)
            if (g.Line[s] != null && cnt[g.Line[s].Color] == 0) g.Line[s] = null;
        for (int color = 0; color < MonsterBuilder.MaxColors; color++)
        {
            int surplus = g.AmmoAvailable(color) - cnt[color];
            for (int i = deck.Count - 1; i >= 0 && surplus > 0; i--)
            {
                if (deck[i].Color != color) continue;
                int take = Math.Min(surplus, deck[i].Ammo);
                deck[i].Ammo -= take; surplus -= take;
                if (deck[i].Ammo == 0) deck.RemoveAt(i);
            }
            for (int i = 0; i < g.Tray.Length && surplus > 0; i++)
            {
                var c = g.Tray[i];
                if (c == null || c.Color != color) continue;
                int take = Math.Min(surplus, c.Ammo);
                c.Ammo -= take; surplus -= take;
                if (c.Ammo == 0) g.Tray[i] = null;
            }
        }
        if (g.Tray.All(c => c == null)) refill.Invoke(g, null);
    }

    static void CountDraw(TitanGame g, GameStats st)
    {
        foreach (var c in g.Tray)
        {
            if (c == null) continue;
            st.Drawn++;
            if (g.Monster.CountOfColor(c.Color) == 0) st.DeadDrawn++;
        }
    }

    static void ClassifyDecision(TitanGame g, GameStats st, double t)
    {
        var opts = g.Tray.Where(c => c != null).ToList();
        if (opts.Count == 0) return;
        st.DecisionsWithFreeSlot++;
        int distinct = opts.Select(c => c.Color).Distinct().Count();
        var vis = Info.VisibleCounts(g.Monster);
        var visColors = opts.Where(c => vis[c.Color] > 0).Select(c => c.Color).Distinct().Count();
        bool zero = opts.Count == 1 || distinct == 1;
        if (opts.Count == 1) st.SingleOption++;
        else if (distinct == 1) st.AllSameColor++;
        if (zero) st.ZeroChoice++;
        var cntc = Info.Counts(g.Monster);
        if (opts.All(c => cntc[c.Color] == 0)) { st.OnlyDeadTrayDecisions++; if (opts.Count == 1) st.ForcedDeadLast++; }
        if (visColors == 0)
        {
            st.NoVisibleOption++;
            if (st.TFirstNoVisibleForced < 0 && Firing(g) == 0) st.TFirstNoVisibleForced = t;
        }
        else if (visColors == 1 && !zero) st.OneVisibleDominant++;
        if (visColors >= 2) st.MeaningfulChoice++;
    }

    public static int Choose(BotKind bot, TitanGame g, XRng r, HashSet<Int3> joints)
    {
        var idx = Enumerable.Range(0, g.Tray.Length).Where(i => g.Tray[i] != null).ToList();
        if (idx.Count == 0 || Free(g) == 0) return -1;
        switch (bot)
        {
            case BotKind.Random:
                return idx[r.Range(0, idx.Count)];
            case BotKind.Greedy:
                return Greedy(g, r, idx);
            case BotKind.Smart:
                return Smart(g, idx, joints);
            case BotKind.Expert:
                return Expert(g, r, idx, joints);
        }
        return -1;
    }

    public static int Greedy(TitanGame g, XRng r, List<int> idx)
    {
        int best = -1;
        foreach (var i in idx)
            if (g.Monster.HasVisibleOfColor(g.Tray[i].Color) && (best < 0 || g.Tray[i].Ammo > g.Tray[best].Ammo)) best = i;
        if (best >= 0) return best;
        if (Firing(g) == 0 || Free(g) >= 4) return idx[r.Range(0, idx.Count)];
        return -1;
    }

    static int Smart(TitanGame g, List<int> idx, HashSet<Int3> joints)
    {
        var m = g.Monster;
        var vis = Info.VisibleCounts(m);
        var cnt = Info.Counts(m);
        var committed = new int[MonsterBuilder.MaxColors];
        foreach (var c in g.Line) if (c != null) committed[c.Color] += c.Ammo;
        var jointVisible = new bool[MonsterBuilder.MaxColors];
        foreach (var j in joints) if (m.Has(j) && m.IsVisible(j)) jointVisible[m.ColorAt(j)] = true;

        int best = -1; double bestScore = double.MinValue;
        foreach (var i in idx)
        {
            var c = g.Tray[i];
            double s;
            int left = cnt[c.Color] - committed[c.Color];
            if (left <= 0) s = -1000 + c.Ammo * -1; // surplus or dead: nothing left for it
            else if (vis[c.Color] > 0)
            {
                s = 100 + Math.Min(vis[c.Color], c.Ammo) - (committed[c.Color] > 0 ? 25 : 0) + (jointVisible[c.Color] ? 80 : 0) + 0.3 * c.Ammo;
            }
            else s = -50 + Math.Min(left, c.Ammo) * 0.1;
            if (s > bestScore) { bestScore = s; best = i; }
        }
        if (bestScore >= 0) return best;
        int free = Free(g), firing = Firing(g);
        if (bestScore > -1000)
            return (firing == 0 || free >= 3) ? best : -1;
        // only dead/surplus cannons left in the tray: must park them to get a refill
        return (firing == 0 || free >= 2) ? best : -1;
    }

    // ------------------------------------------------------------ expert: rollouts
    static int Expert(TitanGame g, XRng r, List<int> idx, HashSet<Int3> joints)
    {
        // candidates: each distinct (color, ammo) in the tray, plus waiting
        var cands = new List<int> { -1 };
        var seen = new HashSet<(int, int)>();
        foreach (var i in idx) if (seen.Add((g.Tray[i].Color, g.Tray[i].Ammo))) cands.Add(i);
        if (cands.Count == 2 && Free(g) >= 1)
        {
            // single option: still compare deploy vs wait
        }
        double best = double.MinValue; int bestI = -1;
        foreach (var cand in cands)
        {
            double total = 0;
            const int samples = 2;
            for (int k = 0; k < samples; k++)
            {
                var clone = Cloner.Clone(g);
                ReseedClone(clone, (ulong)(k * 977 + 1) ^ (ulong)r.Range(0, int.MaxValue));
                clone.Events.Clear();
                if (cand >= 0) clone.Deploy(cand);
                total += Rollout(clone, 12.0, joints, new XRng((ulong)k + 5));
            }
            double v = total / samples + (cand == -1 ? -1 : 0);
            if (v > best) { best = v; bestI = cand; }
        }
        return bestI;
    }

    static readonly FieldInfo rngField = typeof(TitanGame).GetField("rng", BindingFlags.Instance | BindingFlags.NonPublic);

    static void ReseedClone(TitanGame clone, ulong seed)
    {
        // the expert cannot see the future draws: give the clone an independent random stream
        rngField.SetValue(clone, new XRng(seed));
    }

    static double Rollout(TitanGame g, double horizon, HashSet<Int3> joints, XRng r)
    {
        int startLevel = g.Level;
        int startScore = g.Score;
        double t = 0, next = 0.9;
        const double dt = 0.1;
        while (t < horizon && !g.IsOver)
        {
            if (t >= next)
            {
                var idx = Enumerable.Range(0, g.Tray.Length).Where(i => g.Tray[i] != null).ToList();
                int pick = (idx.Count > 0 && Free(g) > 0) ? Smart(g, idx, joints) : -1;
                if (pick >= 0) g.Deploy(pick);
                next = t + (pick >= 0 ? 1.1 : 0.45);
            }
            g.Tick((float)dt);
            g.Events.Clear();
            t += dt;
        }
        if (g.IsOver) return -1e6 + t * 1000;
        double v = (g.Level - startLevel) * 100000 + (g.Score - startScore);
        int idle = g.Line.Count(c => c != null && !g.Monster.HasVisibleOfColor(c.Color));
        int dead = g.Line.Count(c => c != null && g.Monster.CountOfColor(c.Color) == 0);
        v -= idle * 60 + dead * 400;
        v += g.Distance * 20;
        return v;
    }
}

static class Program
{
    static double Pct(IEnumerable<double> xs, double p)
    {
        var a = xs.OrderBy(x => x).ToArray();
        if (a.Length == 0) return double.NaN;
        double idx = p * (a.Length - 1);
        int lo = (int)Math.Floor(idx), hi = (int)Math.Ceiling(idx);
        return a[lo] + (a[hi] - a[lo]) * (idx - lo);
    }

    static void Static()
    {
        Console.WriteLine("=== STATIC: titan composition per level (200 seeds each) ===");
        Console.WriteLine("lvl cubes colors approach(s) req.shots/s minClear(s) limbs% visible%@spawn hiddenOnlyColors joints armPiece headPiece cannons ");
        var cfg = Exp.Cfg();
        for (int lvl = 1; lvl <= 12; lvl++)
        {
            double cubes = 0, colors = 0, appr = 0, limbs = 0, visible = 0, hiddenOnly = 0, jointsN = 0, arm = 0, head = 0, cannons = 0;
            int N = 200;
            for (int s = 0; s < N; s++)
            {
                var g = new TitanGame(cfg, new XRng((ulong)(s * 31 + lvl)), lvl);
                var m = g.Monster;
                cubes += m.Count;
                var cnt = Info.Counts(m);
                var vis = Info.VisibleCounts(m);
                colors += cnt.Count(x => x > 0);
                hiddenOnly += Enumerable.Range(0, 6).Count(c => cnt[c] > 0 && vis[c] == 0);
                appr += g.ApproachSeconds;
                limbs += m.Cubes.Count(kv => !m.IsAnchored(kv.Key));
                visible += vis.Sum();
                var joints = Info.Joints(m);
                jointsN += joints.Count;
                cannons += g.DeckCount + 3;
                // piece sizes: remove joints of one side, measure
                var clone = Cloner.Clone(m);
                var pieces = new List<FallenPiece>();
                foreach (var j in joints.Where(j => j.X > 1 && !(m.Has(j + new Int3(0, -1, 0)) && m.IsAnchored(j + new Int3(0, -1, 0))))) pieces.AddRange(clone.Remove(j));
                arm += pieces.Sum(p => p.Count);
                var clone2 = Cloner.Clone(m);
                var p2 = new List<FallenPiece>();
                foreach (var j in joints.Where(j => m.Has(j + new Int3(0, -1, 0)) && m.IsAnchored(j + new Int3(0, -1, 0)))) p2.AddRange(clone2.Remove(j));
                head += p2.Sum(p => p.Count);
            }
            Console.WriteLine($"{lvl,3} {cubes / N,5:F0} {colors / N,6:F1} {appr / N,10:F0} {cubes / appr,11:F2} {cubes / N / (5 / cfg.FireInterval),11:F1} {100 * limbs / cubes,6:F0} {100 * visible / cubes,16:F0} {hiddenOnly / N,16:F2} {jointsN / N,6:F1} {arm / N,8:F0} {head / N,9:F0} {cannons / N,7:F1}");
        }
    }

    static void Main(string[] args)
    {
        Static();
        Runner.Prune = args.Length > 1 && args[1] == "prune";
        Console.WriteLine("PRUNE (counterfactual fix) = " + Runner.Prune);
        int nb = args.Length > 2 ? int.Parse(args[2]) : 600;
        var cfgN = new Dictionary<BotKind, int>();
        if (nb > 0) { cfgN[BotKind.Random] = nb; cfgN[BotKind.Greedy] = nb; cfgN[BotKind.Smart] = nb; }
        int ex = args.Length > 0 ? int.Parse(args[0]) : 80;
        if (ex > 0) cfgN[BotKind.Expert] = ex;
        var all = new Dictionary<BotKind, List<GameStats>>();
        foreach (var bot in cfgN.Keys)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var arr = new GameStats[cfgN[bot]];
            Parallel.For(0, arr.Length, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i => arr[i] = Runner.Play(bot, 1000 + i));
            all[bot] = arr.ToList();
            Console.Error.WriteLine($"{bot}: {sw.Elapsed.TotalSeconds:F0}s");
        }
        Report(all);
    }

    static void Report(Dictionary<BotKind, List<GameStats>> all)
    {
        foreach (var (bot, gs) in all)
        {
            int n = gs.Count;
            Console.WriteLine();
            Console.WriteLine($"=================== {bot} ({n} games) ===================");
            var def = gs.Select(g => (double)g.Defeated);
            Console.WriteLine($"titans defeated: mean {def.Average():F2}  p10 {Pct(def, .1)} p50 {Pct(def, .5)} p90 {Pct(def, .9)} max {def.Max()}");
            var hist = gs.GroupBy(g => g.Defeated).OrderBy(x => x.Key).Select(x => $"{x.Key}:{100.0 * x.Count() / n:F0}%");
            Console.WriteLine("  distribution: " + string.Join("  ", hist));
            var rt = gs.Select(g => g.RunTime);
            Console.WriteLine($"run length (s): mean {rt.Average():F0} p10 {Pct(rt, .1):F0} p50 {Pct(rt, .5):F0} p90 {Pct(rt, .9):F0}");
            Console.WriteLine($"runs ending <20s {100.0 * gs.Count(g => g.RunTime < 20) / n:F0}%  <30s {100.0 * gs.Count(g => g.RunTime < 30) / n:F0}%  <60s {100.0 * gs.Count(g => g.RunTime < 60) / n:F0}%  >180s {100.0 * gs.Count(g => g.RunTime > 180) / n:F0}%; score p50 {Pct(gs.Select(g => (double)g.Score), .5):F0}");
            Console.WriteLine($"slotsFull losses triggered by the player's own tap (5th cannon idle): {100.0 * gs.Count(g => g.LostOnTap) / Math.Max(1, gs.Count(g => g.Reason == LoseReason.SlotsFull)):F0}%");
            Console.WriteLine($"lose reasons: wall {100.0 * gs.Count(g => g.Reason == LoseReason.WallReached) / n:F0}%  slotsFull {100.0 * gs.Count(g => g.Reason == LoseReason.SlotsFull) / n:F0}%  timeout {100.0 * gs.Count(g => g.Reason == LoseReason.None) / n:F0}%");
            foreach (var grp in gs.GroupBy(g => g.LoseLevel).OrderBy(x => x.Key).Take(8))
                Console.WriteLine($"   lost on titan {grp.Key}: {100.0 * grp.Count() / n:F0}%  (wall {grp.Count(g => g.Reason == LoseReason.WallReached)}, slots {grp.Count(g => g.Reason == LoseReason.SlotsFull)})");
            var sf = gs.Where(g => g.Reason == LoseReason.SlotsFull).ToList();
            if (sf.Count > 0)
                Console.WriteLine($"   slotsFull losses: mean dead cannons (color gone) on line {sf.Average(g => g.DeadOnLineAtLoss):F2}/5; with >=1 dead: {100.0 * sf.Count(g => g.DeadOnLineAtLoss > 0) / sf.Count:F0}%");
            var survival = Enumerable.Range(1, 8).Select(k => $"T{k}:{100.0 * gs.Count(g => g.Defeated >= k) / n:F0}%");
            Console.WriteLine("survival (defeated >= k): " + string.Join(" ", survival));

            Console.WriteLine("per-titan clear time (s) / approach (s) / margin left (fraction of distance) when defeated:");
            foreach (var grp in gs.SelectMany(g => g.TitanTimes.Zip(g.Margins)).GroupBy(x => x.First.level).OrderBy(x => x.Key).Take(16))
                Console.WriteLine($"   titan {grp.Key}: n={grp.Count(),4} clear p50 {Pct(grp.Select(x => x.First.time), .5),5:F0}s (p10 {Pct(grp.Select(x => x.First.time), .1):F0} p90 {Pct(grp.Select(x => x.First.time), .9):F0})  approach {grp.Average(x => x.First.approach):F0}s  margin p50 {Pct(grp.Select(x => x.Second.margin), .5):F2} p10 {Pct(grp.Select(x => x.Second.margin), .1):F2}");
            double tot = gs.Sum(g => g.RunTime);
            Console.WriteLine($"time shares: no cannon firing (dead air) {100 * gs.Sum(g => g.DeadAir) / tot:F0}%  line full/can't act {100 * gs.Sum(g => g.Blocked) / tot:F0}%  waiting by choice {100 * gs.Sum(g => g.WaitChoice) / tot:F0}%  danger>=75% {100 * gs.Sum(g => g.Danger75Time) / tot:F0}%");
            var gaps = gs.SelectMany(g => g.LongestNoShotGap);
            if (gaps.Any()) Console.WriteLine($"longest no-shot gap per defeated titan: p50 {Pct(gaps, .5):F1}s p90 {Pct(gaps, .9):F1}s");
            Console.WriteLine($"taps/min {gs.Sum(g => g.Taps) / (tot / 60):F1}   opportunities: act {100.0 * gs.Sum(g => g.Taps) / gs.Sum(g => g.Opportunities):F0}% wait {100.0 * gs.Sum(g => g.Waits) / gs.Sum(g => g.Opportunities):F0}% blocked {100.0 * gs.Sum(g => g.BlockedOpp) / gs.Sum(g => g.Opportunities):F0}%");
            double dec = gs.Sum(g => g.DecisionsWithFreeSlot);
            Console.WriteLine($"decision points (free slot): zero-choice {100 * gs.Sum(g => g.ZeroChoice) / dec:F0}% (single cannon {100 * gs.Sum(g => g.SingleOption) / dec:F0}%, all same color {100 * gs.Sum(g => g.AllSameColor) / dec:F0}%)  no visible option {100 * gs.Sum(g => g.NoVisibleOption) / dec:F0}%  one dominant visible {100 * gs.Sum(g => g.OneVisibleDominant) / dec:F0}%  >=2 visible colors (real choice) {100 * gs.Sum(g => g.MeaningfulChoice) / dec:F0}%");
            Console.WriteLine($"dead cannons (color already gone) drawn: {100.0 * gs.Sum(g => g.DeadDrawn) / gs.Sum(g => g.Drawn):F1}% of draws; deployed dead per game {gs.Average(g => g.DeadDeployed):F2}; surplus ammo created by falling pieces per game {gs.Average(g => g.SurplusAmmoFromPieces):F0}");
            var pieces = gs.SelectMany(g => g.Pieces).ToList();
            int titansSeen = gs.Sum(g => g.Defeated + 1);
            Console.WriteLine($"combos: {pieces.Count} pieces in {titansSeen} titans => {(double)pieces.Count / titansSeen:F2}/titan; games with >=1 combo {100.0 * gs.Count(g => g.Pieces.Count > 0) / n:F0}%; size p50 {Pct(pieces.Select(p => (double)p.size), .5):F0} p90 {Pct(pieces.Select(p => (double)p.size), .9):F0}; first combo t-into-titan p50 {Pct(pieces.Select(p => p.tIntoTitan), .5):F0}s");
            foreach (var grp in pieces.GroupBy(p => p.level).OrderBy(x => x.Key).Take(6))
            {
                int titansAtLevel = gs.Count(g => g.Defeated + 1 >= grp.Key);
                Console.WriteLine($"   titan {grp.Key}: {(double)grp.Count() / titansAtLevel:F2} pieces per titan reached, mean size {grp.Average(p => p.size):F0}");
            }
            Console.WriteLine($"dead cannons parked on line: {100 * gs.Sum(g => g.DeadParkedTime) / tot:F0}% of play time has >=1; avg dead slots {gs.Sum(g => g.DeadSlotSeconds) / tot:F2}/5; first dead parked p50 at {Pct(gs.Where(g => g.TFirstDead >= 0).Select(g => g.TFirstDead), .5):F0}s in {100.0 * gs.Count(g => g.TFirstDead >= 0) / n:F0}% of games");
            Console.WriteLine($"pieces triggered by a shot on a joint (shoulder/neck) {100.0 * gs.Sum(g => g.PiecesFromJointShot) / Math.Max(1, gs.Sum(g => g.PiecesFromJointShot + g.PiecesFromOther)):F0}% vs other cube (e.g. arm root) {100.0 * gs.Sum(g => g.PiecesFromOther) / Math.Max(1, gs.Sum(g => g.PiecesFromJointShot + g.PiecesFromOther)):F0}%; line cannons stranded (color vanished) by a falling piece: {gs.Average(g => g.StrandedByPiece):F2}/game; decisions where every tray cannon is dead {100.0 * gs.Sum(g => g.OnlyDeadTrayDecisions) / dec:F1}% (single forced dead {100.0 * gs.Sum(g => g.ForcedDeadLast) / dec:F1}%)");
            var i3 = gs.Where(g => g.TFirstIdle3 >= 0).Select(g => g.TFirstIdle3);
            var d75 = gs.Where(g => g.TFirstDanger75 >= 0).Select(g => g.TFirstDanger75);
            var nv = gs.Where(g => g.TFirstNoVisibleForced >= 0).Select(g => g.TFirstNoVisibleForced);
            Console.WriteLine($"first hard moment: >=3 idle cannons in {100.0 * i3.Count() / n:F0}% of games, p50 at {Pct(i3, .5):F0}s (titan p50 {Pct(gs.Where(g => g.LevelFirstIdle3 > 0).Select(g => (double)g.LevelFirstIdle3), .5)}); titan at 75% of the way in {100.0 * d75.Count() / n:F0}%, p50 at {Pct(d75, .5):F0}s (titan p50 {Pct(gs.Where(g => g.LevelFirstDanger75 > 0).Select(g => (double)g.LevelFirstDanger75), .5)}); stalled with nothing firing & no visible tray option p50 at {Pct(nv, .5):F0}s ({100.0 * nv.Count() / n:F0}% of games)");
            var sc = gs.Select(g => (double)g.ShotsTotal);
        }
    }
}
