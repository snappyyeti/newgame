using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Titan.Core.Ads
{
    /// <summary>
    /// Every ad value (DESIGN.md 10, ADS-1/ADS-2). Public fields named like the JSON keys; the game may override them
    /// from Resources/ad_config.json (later: remote config) through <see cref="ApplyJson"/>. The hard floors below are
    /// code, not config: <see cref="CooldownSeconds"/>, <see cref="MinPlaySeconds"/> and <see cref="MaxPer10Min"/>
    /// never go past them whatever the JSON says.
    /// </summary>
    [Serializable]
    public sealed class AdConfig
    {
        // ------------------------------------------------------------ hard floors (code only)
        public const float FloorCooldownSeconds = 120f;
        public const float FloorMinPlaySeconds = 90f;
        public const float CeilingPer10Min = 3.5f;
        /// <summary>The FTUE gate of every ad (DESIGN 12.1): config may raise it, never lower it.</summary>
        public const int FloorLifetimeRunsAnyAd = 3;
        public const float FloorLifetimePlaySecondsAnyAd = 600f;
        /// <summary>Buckets 0..9 are the interstitial control group forever: config may widen it, never shrink it.</summary>
        public const int FloorHoldoutInterMaxBucket = 9;

        // ------------------------------------------------------------ interstitial (10.2 C)
        public bool inter_enabled = true;
        /// <summary>light (soft launch default) 240 / 210 / 8, standard 180 / 150 / 5, firm 150 / 120 / 4.</summary>
        public string inter_preset = "light";
        /// <summary>Optional explicit values; 0 = take them from the preset. Clamped to the floors either way.</summary>
        public float inter_cooldown_s;
        public float inter_min_play_s;
        public int inter_grace_runs;
        /// <summary>Interstitials per 10 minutes of play; clamped to at most 3.5.</summary>
        public float inter_max_per_10min = CeilingPer10Min;
        public float inter_min_run_s = 45f;
        public float inter_after_rewarded_s = 180f;
        public int inter_max_per_session = 5;
        public int inter_max_per_day = 15;
        public float inter_min_lifetime_play_s = 600f;
        public float inter_results_min_s = 1.5f;
        public float inter_ready_wait_s = 1.0f;
        /// <summary>The honest card before the ad: "Reclamă — Mulțumim că susții jocul gratuit." (seconds).</summary>
        public float inter_card_s = 0.8f;
        public float session_timeout_s = 1800f;

        // ------------------------------------------------------------ "Pauză de reclame" (10.2 D)
        public bool adbreak_enabled = true;
        public float adbreak_minutes = 30f;
        public float adbreak_max_stack_minutes = 60f;
        public int adbreak_max_per_day = 4;
        public int adbreak_proactive_after_inter = 2;

        // ------------------------------------------------------------ FTUE gate for any ad (10.2)
        public int min_lifetime_runs_any_ad = 3;
        public float min_lifetime_play_s_any_ad = 600f;

        // ------------------------------------------------------------ cohorts (10.4)
        /// <summary>Buckets 0..this: no interstitials, forever (the control group).</summary>
        public int holdout_inter_max_bucket = 9;
        /// <summary>Buckets in this range: no "+1 Loc".</summary>
        public int holdout_extra_slot_min_bucket = 10;
        public int holdout_extra_slot_max_bucket = 14;
        /// <summary>Remote experiment tags, appended to the experiments string on every ad event.</summary>
        public string experiments = "";

        public bool banner_enabled = false;

        // ------------------------------------------------------------ the simulated ad (placeholder for LevelPlay)
        public float sim_rewarded_seconds = 6f;
        public float sim_inter_seconds = 4f;
        public float sim_inter_close_after_s = 2f;
        public float sim_fill_rate = 0.9f;

        // ------------------------------------------------------------ "A doua suflare" (10.2 A, ADS-2)
        public bool revive_enabled = true;
        public int revive_max_per_run = 1;
        /// <summary>
        /// Chosen in Tools/Sim --revive from {0.25, 0.40, 0.60}: the smallest value where Smart gains ≥ 1 titan in
        /// ≥ 50% of revives. Measured (300 games, seed 1): 0.25 → 93%, 0.40 → 98%, 0.60 → 99% (DESIGN.md 10.2 A).
        /// </summary>
        public float revive_push_back = 0.25f;
        public float revive_grace_s = 2.0f;
        public float revive_min_progress = 0.15f;
        public float revive_freeze_s = 0.6f;
        public float revive_load_wait_s = 3f;
        public int fallback_grants_per_session = 1;
        public bool first_free_revive = true;

        // ------------------------------------------------------------ "+1 Loc" (10.2 B, ADS-2)
        public bool extra_slot_enabled = true;
        public int extra_slot_min_titan = 2;
        public int extra_slot_min_occupied = 4;
        public int extra_slot_min_idle = 3;
        public float extra_slot_debounce_s = 1.0f;
        public float extra_slot_hide_after_s = 2.0f;
        public float extra_slot_decline_cooldown_s = 20f;
        public int extra_slot_declines_rest_titan = 2;
        public int inrun_help_max_per_run = 2;
        public bool first_free_extra_slot = true;

        // ------------------------------------------------------------ effective values (floors applied)

        void Preset(out float cooldown, out float minPlay, out int grace)
        {
            switch ((inter_preset ?? "").Trim().ToLowerInvariant())
            {
                case "standard":
                    cooldown = 180f; minPlay = 150f; grace = 5;
                    break;
                case "firm":
                    cooldown = 150f; minPlay = 120f; grace = 4;
                    break;
                default: // "light" and anything unknown: the gentlest
                    cooldown = 240f; minPlay = 210f; grace = 8;
                    break;
            }
        }

        /// <summary>Seconds between two interstitials (rule 6), never under 120.</summary>
        public float CooldownSeconds
        {
            get
            {
                Preset(out float c, out _, out _);
                return Math.Max(FloorCooldownSeconds, inter_cooldown_s > 0f ? inter_cooldown_s : c);
            }
        }

        /// <summary>Seconds of play since the last interstitial (rule 7), never under 90.</summary>
        public float MinPlaySeconds
        {
            get
            {
                Preset(out _, out float m, out _);
                return Math.Max(FloorMinPlaySeconds, inter_min_play_s > 0f ? inter_min_play_s : m);
            }
        }

        /// <summary>Lifetime runs before the first interstitial (rule 3), never under the any-ad gate.</summary>
        public int GraceRuns
        {
            get
            {
                Preset(out _, out _, out int g);
                return Math.Max(MinLifetimeRunsAnyAd, inter_grace_runs > 0 ? inter_grace_runs : g);
            }
        }

        /// <summary>Lifetime runs before any ad (FTUE gate), never under 3.</summary>
        public int MinLifetimeRunsAnyAd => Math.Max(FloorLifetimeRunsAnyAd, min_lifetime_runs_any_ad);

        /// <summary>Lifetime play seconds before any ad (FTUE gate), never under 600 (NaN counts as the floor).</summary>
        public float MinLifetimePlaySecondsAnyAd =>
            float.IsNaN(min_lifetime_play_s_any_ad) ? FloorLifetimePlaySecondsAnyAd : Math.Max(FloorLifetimePlaySecondsAnyAd, min_lifetime_play_s_any_ad);

        /// <summary>Last bucket of the interstitial holdout (0..this see no interstitial), never under 9.</summary>
        public int HoldoutInterMaxBucket => Math.Max(FloorHoldoutInterMaxBucket, holdout_inter_max_bucket);

        /// <summary>At most 3.5 interstitials per 10 minutes of play (never more, whatever the config).</summary>
        public float MaxPer10Min => Math.Max(0f, Math.Min(CeilingPer10Min, inter_max_per_10min));

        /// <summary>The preset name actually used (an unknown name falls back to light).</summary>
        public string PresetName
        {
            get
            {
                string p = (inter_preset ?? "").Trim().ToLowerInvariant();
                return p == "standard" || p == "firm" ? p : "light";
            }
        }

        // ------------------------------------------------------------ JSON overrides

        /// <summary>
        /// Overrides fields from a flat JSON object ({"inter_preset": "standard", "revive_enabled": false, ...}).
        /// Unknown keys and values of the wrong type are ignored (listed in <paramref name="ignored"/>); a broken
        /// document changes nothing. Pure C# (no JsonUtility in Core), flat objects only.
        /// </summary>
        public bool ApplyJson(string json, List<string> ignored = null)
        {
            Dictionary<string, object> values;
            try
            {
                values = FlatJson.Parse(json);
            }
            catch (FormatException e)
            {
                ignored?.Add("json: " + e.Message);
                return false;
            }
            foreach (var kv in values)
            {
                var f = typeof(AdConfig).GetField(kv.Key, BindingFlags.Public | BindingFlags.Instance);
                if (f == null)
                {
                    ignored?.Add(kv.Key);
                    continue;
                }
                try
                {
                    if (f.FieldType == typeof(bool) && kv.Value is bool b) f.SetValue(this, b);
                    else if (f.FieldType == typeof(string) && kv.Value is string str) f.SetValue(this, str);
                    else if (f.FieldType == typeof(int) && kv.Value is double di && Math.Abs(di - Math.Round(di)) < 1e-9) f.SetValue(this, (int)Math.Round(di));
                    else if (f.FieldType == typeof(float) && kv.Value is double df) f.SetValue(this, (float)df);
                    else ignored?.Add(kv.Key);
                }
                catch (ArgumentException)
                {
                    ignored?.Add(kv.Key);
                }
            }
            return true;
        }
    }

    /// <summary>A minimal reader for one flat JSON object: string, number, true/false/null values.</summary>
    static class FlatJson
    {
        public static Dictionary<string, object> Parse(string text)
        {
            var result = new Dictionary<string, object>();
            if (text == null) throw new FormatException("empty");
            int i = 0;
            Skip(text, ref i);
            Expect(text, ref i, '{');
            Skip(text, ref i);
            if (Peek(text, i) == '}') return result;
            while (true)
            {
                Skip(text, ref i);
                string key = ReadString(text, ref i);
                Skip(text, ref i);
                Expect(text, ref i, ':');
                Skip(text, ref i);
                result[key] = ReadValue(text, ref i);
                Skip(text, ref i);
                char c = Peek(text, i);
                i++;
                if (c == ',') continue;
                if (c == '}') break;
                throw new FormatException("expected , or } at " + (i - 1));
            }
            return result;
        }

        static char Peek(string t, int i) => i < t.Length ? t[i] : '\0';

        static void Skip(string t, ref int i)
        {
            while (i < t.Length && char.IsWhiteSpace(t[i])) i++;
        }

        static void Expect(string t, ref int i, char c)
        {
            if (Peek(t, i) != c) throw new FormatException("expected '" + c + "' at " + i);
            i++;
        }

        static string ReadString(string t, ref int i)
        {
            Expect(t, ref i, '"');
            var sb = new StringBuilder();
            while (i < t.Length && t[i] != '"')
            {
                if (t[i] == '\\' && i + 1 < t.Length)
                {
                    i++;
                    char e = t[i];
                    if (e == 'u' && i + 4 < t.Length)
                    {
                        sb.Append((char)int.Parse(t.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                    }
                    else sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e);
                }
                else sb.Append(t[i]);
                i++;
            }
            Expect(t, ref i, '"');
            return sb.ToString();
        }

        static object ReadValue(string t, ref int i)
        {
            char c = Peek(t, i);
            if (c == '"') return ReadString(t, ref i);
            if (t.Length - i >= 4 && string.CompareOrdinal(t, i, "true", 0, 4) == 0)
            {
                i += 4;
                return true;
            }
            if (t.Length - i >= 5 && string.CompareOrdinal(t, i, "false", 0, 5) == 0)
            {
                i += 5;
                return false;
            }
            if (t.Length - i >= 4 && string.CompareOrdinal(t, i, "null", 0, 4) == 0)
            {
                i += 4;
                return null;
            }
            int start = i;
            while (i < t.Length && (char.IsDigit(t[i]) || t[i] == '-' || t[i] == '+' || t[i] == '.' || t[i] == 'e' || t[i] == 'E')) i++;
            if (i == start) throw new FormatException("unexpected '" + c + "' at " + i);
            return double.Parse(t.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
