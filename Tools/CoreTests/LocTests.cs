using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Titan.Core;
using Titan.Core.Ads;

namespace Titan.Tests
{
    /// <summary>
    /// UX-1 localization: the RO/EN string table, its use in the code, and the rule that no player-facing string
    /// literal is left in the Unity side outside Loc (the acceptance criterion's grep check).
    /// </summary>
    public class LocTests
    {
        static string RepoRoot()
        {
            var loc = EthicsLockTestsAccess.RepoFile("Assets/Titan/Scripts/Core/Loc.cs");
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(loc), "..", "..", "..", ".."));
        }

        static IEnumerable<string> SourceFiles(string relativeDir) =>
            Directory.GetFiles(Path.Combine(RepoRoot(), relativeDir), "*.cs", SearchOption.AllDirectories).OrderBy(f => f, System.StringComparer.Ordinal);

        [TearDown]
        public void BackToRomanian() => Loc.Set(Lang.Ro);

        [Test]
        public void EveryKeyHasBothLanguagesWithTheSamePlaceholders()
        {
            var placeholder = new Regex(@"\{(\d+)\}");
            int keys = 0;
            foreach (var key in Loc.Keys)
            {
                keys++;
                string ro = Loc.Raw(key, Lang.Ro), en = Loc.Raw(key, Lang.En);
                Assert.IsFalse(string.IsNullOrWhiteSpace(ro), key + " ro");
                Assert.IsFalse(string.IsNullOrWhiteSpace(en), key + " en");
                var pr = placeholder.Matches(ro).Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x).ToArray();
                var pe = placeholder.Matches(en).Select(m => m.Groups[1].Value).Distinct().OrderBy(x => x).ToArray();
                CollectionAssert.AreEqual(pr, pe, key + ": the same {n} in both languages");
                for (int i = 0; i < pr.Length; i++) Assert.AreEqual(i.ToString(), pr[i], key + ": placeholders are 0..n-1");
            }
            Assert.Greater(keys, 100);
        }

        [Test]
        public void EnglishCopyFollowsTheSameRules()
        {
            // No fear or loss framing, no fake urgency (DESIGN.md 11.6), in either language.
            var banned = new[] { "you will lose", "you'll lose", "don't miss", "last chance", "hurry", "before it's too late", "vei pierde", "ultima șansă", "grăbește" };
            foreach (var key in Loc.Keys)
                foreach (var lang in new[] { Lang.Ro, Lang.En })
                {
                    string text = Loc.Raw(key, lang).ToLowerInvariant();
                    foreach (var b in banned) StringAssert.DoesNotContain(b, text, key + " " + lang);
                }
            // The ad cards carry no record-gap line in English either.
            Loc.Set(Lang.En);
            foreach (var text in new[] { AdText.ReviveTitle, AdText.ReviveValue, AdText.ReviveFooter, AdText.ExtraSlotValue, AdText.AdBreakValue })
            {
                StringAssert.DoesNotContain("record", text.ToLowerInvariant());
                StringAssert.DoesNotContain("best", text.ToLowerInvariant());
            }
            Assert.AreEqual(AdText.AdBreakAccept, AdText.ExtraSlotAccept);
        }

        [Test]
        public void OnlyGlyphsTheFontCheckCovers()
        {
            // Romanian uses ș and ț with a comma below (U+0219, U+021B), never the cedilla forms ş ţ. Everything else is
            // printable ASCII plus a few punctuation marks; the editor font check renders exactly these extra glyphs.
            const string extra = "ăâîșțĂÂÎȘȚ·—…";
            foreach (var key in Loc.Keys)
                foreach (var lang in new[] { Lang.Ro, Lang.En })
                    foreach (char c in Loc.Raw(key, lang))
                    {
                        bool ok = (c >= ' ' && c <= '~') || c == '\n' || extra.IndexOf(c) >= 0;
                        Assert.IsTrue(ok, key + " " + lang + " has U+" + ((int)c).ToString("X4"));
                    }
        }

        [Test]
        public void EveryKeyInTheCodeExistsAndEveryKeyIsUsed()
        {
            var used = new HashSet<string>();
            var call = new Regex("Loc\\.T\\(\\s*\"([^\"]+)\"");
            foreach (var file in SourceFiles("Assets"))
                foreach (Match m in call.Matches(File.ReadAllText(file)))
                {
                    used.Add(m.Groups[1].Value);
                    Assert.IsTrue(Loc.Has(m.Groups[1].Value), Path.GetFileName(file) + " uses a missing key '" + m.Groups[1].Value + "'");
                }
            var dead = Loc.Keys.Where(k => !used.Contains(k)).ToList();
            Assert.IsEmpty(dead, "keys no code uses: " + string.Join(", ", dead));
        }

        [Test]
        public void LanguageDefaultsAndSwitch()
        {
            Assert.AreEqual(Lang.Ro, Loc.Resolve("", true), "auto on a Romanian phone");
            Assert.AreEqual(Lang.En, Loc.Resolve("", false), "auto elsewhere");
            Assert.AreEqual(Lang.En, Loc.Resolve("en", true), "a stored choice wins");
            Assert.AreEqual(Lang.Ro, Loc.Resolve("ro", false));
            Assert.IsNull(Loc.Parse("fr"));
            Assert.AreEqual("en", Loc.Code(Lang.En));

            Assert.AreEqual("Record în 1.300", HudText.PbMarker(9000, 10300));
            Assert.AreEqual("41.200", HudText.Num(41200));
            Loc.Set(Lang.En);
            Assert.AreEqual("Best in 1,300", HudText.PbMarker(9000, 10300));
            Assert.AreEqual("41,200", HudText.Num(41200));
            Assert.AreEqual("You reached 85% of your best!", HudText.NearMissText(85, 100));
            Assert.AreEqual("Tap the yellow cannon", FtueText.H1);
            Assert.AreEqual("Titan #5 · 62% destroyed · Score 31,400", AdText.ReviveStake(5, 0.62f, 31400));
            Assert.AreEqual("No ads for another 12:40", AdText.AdBreakRemaining(760));
            Assert.AreEqual("Streak: 3 days", DailyText.CurrentStreak(3));
            Assert.AreEqual("missing.key", Loc.T("missing.key"), "a missing key shows itself, never crashes");
            Loc.Set(Lang.Ro);
            Assert.AreEqual("Serie: 3 zile", DailyText.CurrentStreak(3));
        }

        // ------------------------------------------------------------ the grep check (UX-1 acceptance criterion)

        /// <summary>
        /// Contexts where a string literal is not player-facing: object, texture, sound and control names, debug logs,
        /// format strings, preference keys, telemetry details. Matched against the statement the literal is in (string
        /// contents blanked), or (the "^" ones) against the text right before it.
        /// </summary>
        static readonly Regex[] StatementContexts =
        {
            new Regex(@"\bDebug\.\w+\("), new Regex(@"new GameObject\("), new Regex(@"\bPrimitive\("), new Regex(@"\bMake\("),
            new Regex(@"\bSynth\("), new Regex(@"\bLogEvent\("), new Regex(@"new RewardedOffer\("), new Regex(@"\bstring detail\b"),
            new Regex(@"\bconst string \w*(Key|Diacritics)\b"), new Regex(@"\bPlayerPrefs\."), new Regex(@"\bResources\.Load<\w+>\("),
        };

        static readonly Regex[] ImmediateContexts =
        {
            new Regex(@"Loc\.T\(\s*$"), new Regex(@"\bname\s*=\s*$"), new Regex(@"\.tag\s*=\s*$"), new Regex(@"ToString\(\s*$"),
            new Regex(@"ControlName\(\s*$"), new Regex(@"FocusControl\(\s*$"),
        };

        [Test]
        public void NoPlayerFacingLiteralOutsideLoc()
        {
            var offenders = new List<string>();
            int literals = 0, keys = 0;
            foreach (var file in SourceFiles(Path.Combine("Assets", "Titan", "Scripts", "Game")))
            {
                string code = File.ReadAllText(file);
                foreach (var lit in CSharpLexer.StringLiterals(code, out string blanked))
                {
                    literals++;
                    if (!Regex.Replace(lit.Text, @"\\.", "").Any(char.IsLetter)) continue; // separators, digits, "\n", "+", "-"
                    string before = blanked.Substring(0, lit.Start);
                    if (ImmediateContexts.Any(r => r.IsMatch(before)))
                    {
                        keys++;
                        continue;
                    }
                    int stmt = before.LastIndexOfAny(new[] { ';', '{', '}' }) + 1;
                    string statement = blanked.Substring(stmt, lit.Start - stmt);
                    if (StatementContexts.Any(r => r.IsMatch(statement))) continue;
                    offenders.Add(Path.GetFileName(file) + ":" + lit.Line + " \"" + lit.Text + "\"");
                }
            }
            Assert.IsEmpty(offenders, "player-facing literals outside Loc:\n" + string.Join("\n", offenders));
            Assert.Greater(keys, 80, "the HUD goes through Loc");
            Assert.Greater(literals, keys);
        }

        [Test]
        public void TheGrepCanFail()
        {
            string code = "void A() { GUI.Label(r, \"Joacă din nou\"); var go = new GameObject(\"Cube\"); GUI.Label(r, Loc.T(\"over.playAgain\")); }";
            var lits = CSharpLexer.StringLiterals(code, out string blanked).ToList();
            Assert.AreEqual(3, lits.Count);
            Assert.AreEqual("Joacă din nou", lits[0].Text);
            int stmt = blanked.Substring(0, lits[0].Start).LastIndexOfAny(new[] { ';', '{', '}' }) + 1;
            Assert.IsFalse(StatementContexts.Any(r => r.IsMatch(blanked.Substring(stmt, lits[0].Start - stmt))), "a GUI.Label literal is caught");
            Assert.IsTrue(ImmediateContexts.Any(r => r.IsMatch(blanked.Substring(0, lits[2].Start))), "a Loc key is accepted");
            // Comments and char literals are not strings.
            var none = CSharpLexer.StringLiterals("// \"comment\"\n/* \"block\" */ char c = '\"';", out _).ToList();
            Assert.IsEmpty(none);
        }
    }

    /// <summary>A minimal C# lexer for the grep checks: string literals (regular, verbatim, interpolated) and comments.</summary>
    static class CSharpLexer
    {
        public struct Literal
        {
            /// <summary>Index of the opening quote (or of the @/$ prefix).</summary>
            public int Start;
            public int Line;
            public string Text;
        }

        /// <summary>The literals of <paramref name="code"/>; <paramref name="blanked"/> is the code with comments and literal contents replaced by spaces (same length).</summary>
        public static IEnumerable<Literal> StringLiterals(string code, out string blanked)
        {
            var result = new List<Literal>();
            var sb = new StringBuilder(code);
            int i = 0, line = 1;
            while (i < code.Length)
            {
                char c = code[i];
                if (c == '\n')
                {
                    line++;
                    i++;
                    continue;
                }
                if (c == '/' && i + 1 < code.Length && code[i + 1] == '/')
                {
                    while (i < code.Length && code[i] != '\n') sb[i++] = ' ';
                    continue;
                }
                if (c == '/' && i + 1 < code.Length && code[i + 1] == '*')
                {
                    while (i < code.Length && !(code[i] == '*' && i + 1 < code.Length && code[i + 1] == '/'))
                    {
                        if (code[i] == '\n') line++;
                        else sb[i] = ' ';
                        i++;
                    }
                    if (i < code.Length) sb[i] = sb[i + 1] = ' ';
                    i += 2;
                    continue;
                }
                if (c == '\'')
                {
                    // A char literal: '"', '\'', '\\', 'x'.
                    int j = i + 1;
                    if (j < code.Length && code[j] == '\\') j++;
                    j++;
                    while (j < code.Length && code[j] != '\'') j++;
                    for (int k = i + 1; k < j && k < code.Length; k++) sb[k] = ' ';
                    i = j + 1;
                    continue;
                }
                int start = i;
                bool verbatim = false;
                int p = i;
                while (p < code.Length && (code[p] == '@' || code[p] == '$') && p - i < 2)
                {
                    if (code[p] == '@') verbatim = true;
                    p++;
                }
                if (p < code.Length && code[p] == '"' && (p == i || char.IsWhiteSpace(code[i - 1]) || "(,=+?:[{!&|<>;".IndexOf(code[i - 1]) >= 0 || p > i))
                {
                    int j = p + 1;
                    var text = new StringBuilder();
                    while (j < code.Length)
                    {
                        if (verbatim && code[j] == '"' && j + 1 < code.Length && code[j + 1] == '"')
                        {
                            text.Append('"');
                            j += 2;
                            continue;
                        }
                        if (!verbatim && code[j] == '\\')
                        {
                            text.Append(code[j]).Append(code[j + 1]);
                            j += 2;
                            continue;
                        }
                        if (code[j] == '"') break;
                        if (code[j] == '\n') line++;
                        text.Append(code[j]);
                        j++;
                    }
                    for (int k = p + 1; k < j; k++)
                        if (code[k] != '\n') sb[k] = ' ';
                    result.Add(new Literal { Start = start, Line = line, Text = text.ToString() });
                    i = j + 1;
                    continue;
                }
                i++;
            }
            blanked = sb.ToString();
            return result;
        }
    }
}
