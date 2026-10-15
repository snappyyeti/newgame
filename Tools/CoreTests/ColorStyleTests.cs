using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Titan.Core;

namespace Titan.Tests
{
    /// <summary>UX-1 color identity: 6 unique patterns, 6 unique symbols, and the colorblind check of the palette.</summary>
    public class ColorStyleTests
    {
        const int N = ColorStyleTable.Count;

        [Test]
        public void SixUniquePatternsAndSixUniqueSymbols()
        {
            var patterns = Enumerable.Range(0, N).Select(ColorStyleTable.PatternOf).ToList();
            var symbols = Enumerable.Range(0, N).Select(ColorStyleTable.SymbolOf).ToList();
            Assert.AreEqual(N, patterns.Distinct().Count());
            Assert.AreEqual(N, symbols.Distinct().Count());
            // The table of DESIGN.md 7 / the backlog, index by index.
            CollectionAssert.AreEqual(new[] { CubePattern.Plain, CubePattern.HorizontalStripes, CubePattern.Dots, CubePattern.Diagonal, CubePattern.Checker, CubePattern.Grid }, patterns);
            CollectionAssert.AreEqual(new[] { ColorSymbol.Circle, ColorSymbol.Square, ColorSymbol.Triangle, ColorSymbol.Diamond, ColorSymbol.Cross, ColorSymbol.Star }, symbols);
            Assert.AreEqual(N, ColorStyleTable.Rgb.Length);
        }

        static bool[] PatternMask(CubePattern p)
        {
            int n = ColorStyleTable.PatternSize;
            var m = new bool[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    m[y * n + x] = ColorStyleTable.PatternInk(p, x, y);
            return m;
        }

        static bool[] SymbolMask(ColorSymbol s, int n = 32)
        {
            var m = new bool[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    m[y * n + x] = ColorStyleTable.SymbolInside(s, (x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f);
            return m;
        }

        [Test]
        public void PatternMasksAreDistinctAndVisible()
        {
            var masks = Enumerable.Range(0, N).Select(i => PatternMask(ColorStyleTable.PatternOf(i))).ToList();
            for (int i = 0; i < N; i++)
            {
                float coverage = ColorStyleTable.InkCoverage(ColorStyleTable.PatternOf(i));
                if (ColorStyleTable.PatternOf(i) == CubePattern.Plain) Assert.AreEqual(0f, coverage);
                else Assert.That(coverage, Is.InRange(0.15f, 0.6f), "pattern " + i + " is visible but not a different color");
                for (int j = i + 1; j < N; j++)
                {
                    int diff = masks[i].Zip(masks[j], (a, b) => a != b).Count(d => d);
                    Assert.Greater(diff, masks[i].Length / 8, "patterns " + i + " and " + j + " differ in at least 1/8 of the pixels");
                }
            }
        }

        [Test]
        public void SymbolShapesAreDistinct()
        {
            // The greyscale check relies on these shapes: pairwise intersection-over-union stays well below 1.
            var masks = Enumerable.Range(0, N).Select(i => SymbolMask(ColorStyleTable.SymbolOf(i))).ToList();
            for (int i = 0; i < N; i++)
            {
                float area = masks[i].Count(b => b) / (float)masks[i].Length;
                Assert.That(area, Is.InRange(0.12f, 0.6f), "symbol " + i + " area");
                for (int j = i + 1; j < N; j++)
                {
                    int inter = masks[i].Zip(masks[j], (a, b) => a && b).Count(x => x);
                    int union = masks[i].Zip(masks[j], (a, b) => a || b).Count(x => x);
                    TestContext.WriteLine(i + "/" + j + " IoU " + (inter / (float)union).ToString("0.00"));
                    Assert.Less(inter / (float)union, 0.8f, "symbols " + i + " and " + j);
                }
            }
        }

        [Test]
        public void PatternsKeepTheMeanAlbedo()
        {
            foreach (CubePattern p in Enum.GetValues(typeof(CubePattern)))
                foreach (float c in new[] { ColorStyleTable.ContrastNormal, ColorStyleTable.ContrastStrong })
                {
                    ColorStyleTable.PatternShades(p, c, out float ink, out float tint);
                    float f = ColorStyleTable.InkCoverage(p);
                    Assert.AreEqual(1.0, tint * ((1 - f) + f * ink), 1e-5, p + " " + c + ": the face's mean is the palette color");
                    if (p == CubePattern.Plain)
                    {
                        Assert.AreEqual(1f, ink);
                        Assert.AreEqual(1f, tint);
                    }
                    else Assert.AreEqual((1 - c) / (1 + c), ink, 1e-6, "base and ink are ±c around their middle");
                }
            ColorStyleTable.PatternShades(CubePattern.Grid, ColorStyleTable.ContrastNormal, out float soft, out _);
            ColorStyleTable.PatternShades(CubePattern.Grid, ColorStyleTable.ContrastStrong, out float strong, out _);
            Assert.Less(strong, soft, "'Simboluri culori' makes the pattern stronger");
        }

        [Test]
        public void DeuteranopiaAndProtanopiaPairsAreSeparatedByLightnessHueOrPattern()
        {
            // For every pair of the 6 colors: either the Machado-simulated colors differ by ΔE76 ≥ 20, or the patterns
            // differ. The patterns always differ, so this always holds: the test documents that the game relies on the
            // patterns (and symbols) for the pairs listed below, not on color alone.
            var reliesOnPattern = new List<string>();
            foreach (var d in new[] { ColorVision.Deficiency.Deuteranopia, ColorVision.Deficiency.Protanopia })
                for (int i = 0; i < N; i++)
                    for (int j = i + 1; j < N; j++)
                    {
                        double de = ColorVision.PaletteDeltaE(i, j, d);
                        bool patternsDiffer = ColorStyleTable.PatternOf(i) != ColorStyleTable.PatternOf(j);
                        Assert.IsTrue(de >= 20.0 || patternsDiffer, d + " " + i + "/" + j);
                        if (de < 20.0) reliesOnPattern.Add(d + ": " + ColorStyleTable.Names[i] + "/" + ColorStyleTable.Names[j] + " ΔE " + de.ToString("0.0"));
                    }
            TestContext.WriteLine("Pairs that need the pattern/symbol (ΔE76 < 20):\n" + string.Join("\n", reliesOnPattern));
            // With normal vision every pair is far apart.
            for (int i = 0; i < N; i++)
                for (int j = i + 1; j < N; j++)
                    Assert.GreaterOrEqual(ColorVision.PaletteDeltaE(i, j, ColorVision.Deficiency.None), 20.0, i + "/" + j);
        }

        [Test]
        public void ColorVisionMathMatchesKnownValues()
        {
            // White stays white under both simulations (the matrices' rows sum to 1), and Lab of white is (100, 0, 0).
            var white = new[] { 1f, 1f, 1f };
            foreach (var d in new[] { ColorVision.Deficiency.Protanopia, ColorVision.Deficiency.Deuteranopia })
            {
                var sim = ColorVision.Simulate(white, d);
                foreach (var v in sim) Assert.AreEqual(1.0, v, 1e-3);
            }
            var lab = ColorVision.Lab(new[] { 1.0, 1.0, 1.0 });
            Assert.AreEqual(100.0, lab[0], 0.01);
            Assert.AreEqual(0.0, lab[1], 0.01);
            Assert.AreEqual(0.0, lab[2], 0.01);
            // sRGB red (1,0,0): L* 53.24, a* 80.09, b* 67.20.
            var red = ColorVision.Lab(new[] { 1.0, 0.0, 0.0 });
            Assert.AreEqual(53.24, red[0], 0.05);
            Assert.AreEqual(80.09, red[1], 0.1);
            Assert.AreEqual(67.20, red[2], 0.1);
            // Red and green collapse toward each other under deuteranopia.
            Assert.Less(ColorVision.PaletteDeltaE(0, 3, ColorVision.Deficiency.Deuteranopia), ColorVision.PaletteDeltaE(0, 3, ColorVision.Deficiency.None));
        }
    }
}
