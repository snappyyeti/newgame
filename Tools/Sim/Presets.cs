using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Titan.Core;

namespace Titan.Sim
{
    /// <summary>
    /// Named TitanConfig presets plus Key=Value overrides applied by reflection to any public TitanConfig field
    /// or settable property. Values: numbers (invariant culture, '.' decimals), true/false, enum names,
    /// and comma-separated lists for arrays (AmmoSizes=10,15,20).
    /// </summary>
    public static class Presets
    {
        /// <summary>
        /// "current": TitanConfig as it is in Assets (no overrides).
        /// "v2": DESIGN.md 4.3, column v2. Fields that do not exist yet in TitanConfig are skipped with a warning;
        /// they appear as the backlog items land (CORE-1..CORE-4), and the preset picks them up with no edit here.
        /// </summary>
        public static readonly Dictionary<string, string[]> All = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "current", new string[0] },
            {
                "v2", new[]
                {
                    "SalvageEnabled=true", "PointsPerSalvagedAmmo=5",                                   // CORE-1
                    "TargetOrder=FarthestFirst",                                                         // CORE-2
                    "TrayRefill=PerSlot", "NoTripleColor=true",                                          // CORE-3
                    "FireInterval=0.23", "AmmoSizes=8,12,16,20,24",                                      // fix pass (DESIGN 4.3.3)
                    "ApproachBaseSeconds=4", "ApproachSecondsPerCube=0.06", "ApproachSpeedup=0.992",
                    "MinApproachSeconds=12", "MinApproachFactor=0.6",
                    "TutorialTitans=3", "TutorialSecondsPerCube=0.11",
                    "KnockbackPerCube=0.03", "KnockbackMin=0.3", "KnockbackMax=1.5",
                    "StunBase=0.5", "StunPerCube=0.01", "StunMax=1.0",
                    "NearMissFraction=0.15", "NearMissBonusMult=1.5", "UseCycles=true",
                    "DeadlockSeconds=1.5",                                                              // CORE-4
                    "CleanCutThreshold=0.8", "CleanCutMult=2", "ChainWindowSeconds=4", "ChainMaxMult=5",
                    "CleanLineMax=4",                                                                    // CORE-5
                    "VisibleColorBias=0.65",                                                             // locked (ethics)
                }
            },
        };

        /// <summary>Builds a config from a preset and overrides. Missing preset fields go to <paramref name="warnings"/>; a bad --set throws.</summary>
        public static TitanConfig Build(string preset, IEnumerable<string> sets, List<string> warnings)
        {
            if (!All.TryGetValue(preset ?? "current", out var entries))
                throw new ArgumentException("unknown preset '" + preset + "' (" + string.Join(", ", All.Keys) + ")");
            var cfg = new TitanConfig();
            foreach (var kv in entries)
            {
                Split(kv, out var key, out var value);
                if (!TrySet(cfg, key, value, out var error))
                {
                    if (warnings == null) throw new ArgumentException(error);
                    warnings.Add(error);
                }
            }
            if (sets != null)
                foreach (var kv in sets)
                {
                    Split(kv, out var key, out var value);
                    if (!TrySet(cfg, key, value, out var error)) throw new ArgumentException("--set " + kv + ": " + error);
                }
            return cfg;
        }

        static void Split(string kv, out string key, out string value)
        {
            int eq = kv.IndexOf('=');
            if (eq <= 0) throw new ArgumentException("expected Key=Value, got '" + kv + "'");
            key = kv.Substring(0, eq).Trim();
            value = kv.Substring(eq + 1).Trim();
        }

        public static bool TrySet(TitanConfig cfg, string key, string value, out string error)
        {
            error = null;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
            var field = typeof(TitanConfig).GetField(key, flags);
            var prop = field == null ? typeof(TitanConfig).GetProperty(key, flags) : null;
            if (field == null && (prop == null || !prop.CanWrite))
            {
                error = "TitanConfig has no public field '" + key + "'";
                return false;
            }
            var type = field != null ? field.FieldType : prop.PropertyType;
            object parsed;
            try
            {
                parsed = Parse(type, value);
            }
            catch (Exception e)
            {
                error = "cannot read '" + value + "' as " + type.Name + " for " + key + ": " + e.Message;
                return false;
            }
            if (field != null) field.SetValue(cfg, parsed);
            else prop.SetValue(cfg, parsed);
            return true;
        }

        static object Parse(Type type, string value)
        {
            var inv = CultureInfo.InvariantCulture;
            if (type.IsArray)
            {
                var parts = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                var arr = Array.CreateInstance(type.GetElementType(), parts.Length);
                for (int i = 0; i < parts.Length; i++) arr.SetValue(Parse(type.GetElementType(), parts[i].Trim()), i);
                return arr;
            }
            if (type.IsEnum) return Enum.Parse(type, value, true);
            if (type == typeof(bool))
            {
                if (value == "1") return true;
                if (value == "0") return false;
                return bool.Parse(value);
            }
            if (type == typeof(int)) return int.Parse(value, inv);
            if (type == typeof(long)) return long.Parse(value, inv);
            if (type == typeof(float)) return float.Parse(value, inv);
            if (type == typeof(double)) return double.Parse(value, inv);
            if (type == typeof(string)) return value;
            return Convert.ChangeType(value, type, inv);
        }
    }
}
