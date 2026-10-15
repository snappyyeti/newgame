using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Titan.Core;

namespace Titan.Sim
{
    /// <summary>
    /// dotnet run -c Release --project Tools/Sim -- --games N --bots random,greedy,smart --preset current [--set Key=Value ...]
    /// Ad helpers are always OFF: there is no flag to turn them on for balance runs (ethics lock).
    /// </summary>
    static class Program
    {
        const string Usage =
@"Titan balance simulator (real core from Assets/Titan/Scripts/Core, bots tap every 0.6-1.6 s, dt 0.05)

  dotnet run -c Release --project Tools/Sim -- [options]

  --games N          games per bot (default 200)
  --bots LIST        comma list of random,greedy,smart (default all three)
  --preset NAME      current | v2 (default current); see Tools/Sim/Presets.cs
  --set Key=Value    override any public TitanConfig field, repeatable (e.g. --set ApproachSecondsPerCube=0.08)
  --seed S           master seed, unsigned 64-bit (default 1); game i uses SeededRandom.Derive(S, i)
  --static           also print the titan composition table per level
  --revive [LIST]    ADS-2 measurement ONLY (not balance): revive once at the first loss with each push-back in
                     LIST (default 0.25,0.40,0.60) and report what the revive is worth; helpers stay OFF otherwise
  --help             this text";

        static int Main(string[] args)
        {
            int games = 200;
            ulong seed = 1;
            string preset = "current";
            var bots = new List<BotKind> { BotKind.Random, BotKind.Greedy, BotKind.Smart };
            var sets = new List<string>();
            bool showStatic = false;
            List<float> revivePushes = null;

            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i];
                    switch (a)
                    {
                        case "--games": games = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                        case "--seed": seed = ulong.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                        case "--preset": preset = Next(args, ref i); break;
                        case "--set": sets.Add(Next(args, ref i)); break;
                        case "--bots": bots = Next(args, ref i).Split(',').Select(Bots.Parse).Distinct().ToList(); break;
                        case "--static": showStatic = true; break;
                        case "--revive":
                            revivePushes = new List<float> { 0.25f, 0.40f, 0.60f };
                            if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                                revivePushes = args[++i].Split(',').Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToList();
                            break;
                        case "--help":
                        case "-h":
                            Console.WriteLine(Usage);
                            return 0;
                        default: throw new ArgumentException("unknown option '" + a + "'");
                    }
                }
                if (games <= 0) throw new ArgumentException("--games must be > 0");
            }
            catch (Exception e) when (e is ArgumentException || e is FormatException || e is OverflowException)
            {
                Console.Error.WriteLine("error: " + e.Message);
                Console.Error.WriteLine(Usage);
                return 2;
            }

            var warnings = new List<string>();
            TitanConfig cfg;
            try
            {
                cfg = Presets.Build(preset, sets, warnings);
            }
            catch (ArgumentException e)
            {
                Console.Error.WriteLine("error: " + e.Message);
                return 2;
            }

            Console.WriteLine("preset " + preset + (sets.Count > 0 ? " + " + string.Join(" ", sets) : "") + "; " + games + " games/bot; master seed " + seed + "; ad helpers OFF");
            if (warnings.Count > 0)
            {
                Console.WriteLine("WARNING: preset '" + preset + "' is PARTIAL, " + warnings.Count + " setting(s) not in TitanConfig yet:");
                foreach (var w in warnings) Console.WriteLine("   - " + w);
            }
            if (showStatic) Static(cfg);

            if (revivePushes != null)
            {
                Console.WriteLine("ADS-2 revive measurement (NOT a balance run): one revive at the first loss when it would be offered");
                foreach (var bot in bots)
                    foreach (float push in revivePushes)
                    {
                        var opts = new SimOptions { Revive = true, RevivePushBack = push };
                        BotSummary.PrintRevive(Console.Out, bot, push, SimRunner.RunMany(bot, cfg, games, seed, opts));
                    }
                return 0;
            }

            var summaries = new Dictionary<BotKind, BotSummary>();
            foreach (var bot in bots)
            {
                var sw = Stopwatch.StartNew();
                var stats = SimRunner.RunMany(bot, cfg, games, seed);
                Console.Error.WriteLine(bot + ": " + sw.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + " s");
                var s = BotSummary.From(bot, stats);
                summaries[bot] = s;
                s.Print(Console.Out);
            }

            Console.WriteLine();
            if (summaries.TryGetValue(BotKind.Random, out var r) && summaries.TryGetValue(BotKind.Smart, out var sm) && r.DefeatedMean > 0)
                Console.WriteLine("Smart:Random mean titans defeated = " + (sm.DefeatedMean / r.DefeatedMean).ToString("F2", CultureInfo.InvariantCulture)
                    + "x  (" + sm.DefeatedMean.ToString("F2", CultureInfo.InvariantCulture) + " / " + r.DefeatedMean.ToString("F2", CultureInfo.InvariantCulture) + "; target 1.7-2.3x)");
            if (summaries.TryGetValue(BotKind.Random, out var r2) && summaries.TryGetValue(BotKind.Smart, out var sm2) && r2.CleanCutsPerTitan > 0)
                Console.WriteLine("Smart:Random clean cuts per titan = " + (sm2.CleanCutsPerTitan / r2.CleanCutsPerTitan).ToString("F2", CultureInfo.InvariantCulture)
                    + "x  (" + sm2.CleanCutsPerTitan.ToString("F2", CultureInfo.InvariantCulture) + " / " + r2.CleanCutsPerTitan.ToString("F2", CultureInfo.InvariantCulture)
                    + "; target >= 1.5x); Smart s3 'linie curata' " + (100 * sm2.StarCleanLineRate).ToString("F0", CultureInfo.InvariantCulture) + "% (target 25-40%)");
            return 0;
        }

        static string Next(string[] args, ref int i)
        {
            if (i + 1 >= args.Length) throw new ArgumentException(args[i] + " needs a value");
            return args[++i];
        }

        /// <summary>Titan composition per titan number n (built at the cycle level when UseCycles), averaged over 200 seeds.</summary>
        static void Static(TitanConfig cfg)
        {
            Console.WriteLine("=== titan composition per titan number n (200 seeds each) ===");
            Console.WriteLine("  n role   L cubes colors approach(s) shots/s-needed limbs% visible%@spawn hiddenOnlyColors joints cannons");
            const int N = 200;
            for (int lvl = 1; lvl <= 25; lvl++)
            {
                double cubes = 0, colors = 0, appr = 0, limbs = 0, visible = 0, hiddenOnly = 0, joints = 0, cannons = 0;
                for (int s = 0; s < N; s++)
                {
                    var g = new TitanGame(cfg, new SeededRandom(SeededRandom.Hash((ulong)lvl, (ulong)s)), lvl);
                    var m = g.Monster;
                    var cnt = SimRunner.Counts(m);
                    var vis = SimRunner.VisibleCounts(m);
                    cubes += m.Count;
                    colors += cnt.Count(x => x > 0);
                    for (int c = 0; c < cnt.Length; c++) if (cnt[c] > 0 && vis[c] == 0) hiddenOnly++;
                    appr += g.ApproachSeconds;
                    limbs += m.Cubes.Count(kv => !m.IsAnchored(kv.Key));
                    visible += vis.Sum();
                    joints += m.Joints.Count();
                    cannons += g.DeckCount + g.Tray.Count(c => c != null);
                }
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0,3} {10,-6} {11,1} {1,5:F0} {2,6:F1} {3,11:F0} {4,14:F2} {5,6:F0} {6,16:F0} {7,16:F2} {8,6:F1} {9,7:F1}",
                    lvl, cubes / N, colors / N, appr / N, cubes / appr, 100 * limbs / cubes, 100 * visible / cubes, hiddenOnly / N, joints / N, cannons / N, cfg.RoleOf(lvl), cfg.BuildLevel(lvl)));
            }
        }
    }
}
