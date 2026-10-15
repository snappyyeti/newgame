using System;

namespace Titan.Core
{
    /// <summary>The cube pattern of a palette color (UX-1, DESIGN.md 7).</summary>
    public enum CubePattern
    {
        Plain,
        HorizontalStripes,
        Dots,
        Diagonal,
        Checker,
        Grid,
    }

    /// <summary>The symbol of a palette color, drawn procedurally (never a font glyph).</summary>
    public enum ColorSymbol
    {
        Circle,
        Square,
        Triangle,
        Diamond,
        Cross,
        Star,
    }

    /// <summary>
    /// Color identity without color (UX-1, DESIGN.md 7): each of the 6 palette colors has its own cube pattern and its
    /// own symbol. This is the single source of the palette RGB (Game/ColorStyle.cs builds its Colors from it), the
    /// 32×32 pattern masks and the symbol shapes, so the colorblind rules are unit tested outside Unity.
    ///
    /// | index | color   | pattern            | symbol   |
    /// |-------|---------|--------------------|----------|
    /// | 0     | red     | plain              | circle   |
    /// | 1     | blue    | horizontal stripes | square   |
    /// | 2     | yellow  | dots               | triangle |
    /// | 3     | green   | diagonal           | diamond  |
    /// | 4     | purple  | checker            | cross    |
    /// | 5     | orange  | grid               | star     |
    /// </summary>
    public static class ColorStyleTable
    {
        public const int Count = 6;
        /// <summary>Side of the procedural pattern texture (point filtered, one per color).</summary>
        public const int PatternSize = 32;
        /// <summary>Albedo contrast of the pattern: ±15% normally, ±30% with "Simboluri culori" on.</summary>
        public const float ContrastNormal = 0.15f, ContrastStrong = 0.30f;

        /// <summary>sRGB of the palette (0..1): red, blue, yellow, green, purple, orange.</summary>
        public static readonly float[][] Rgb =
        {
            new[] { 0.93f, 0.23f, 0.21f },
            new[] { 0.18f, 0.47f, 0.95f },
            new[] { 1.00f, 0.82f, 0.12f },
            new[] { 0.24f, 0.78f, 0.30f },
            new[] { 0.62f, 0.30f, 0.88f },
            new[] { 1.00f, 0.52f, 0.10f },
        };

        public static readonly string[] Names = { "red", "blue", "yellow", "green", "purple", "orange" };

        static readonly CubePattern[] Patterns =
        {
            CubePattern.Plain, CubePattern.HorizontalStripes, CubePattern.Dots, CubePattern.Diagonal, CubePattern.Checker, CubePattern.Grid,
        };

        static readonly ColorSymbol[] Symbols =
        {
            ColorSymbol.Circle, ColorSymbol.Square, ColorSymbol.Triangle, ColorSymbol.Diamond, ColorSymbol.Cross, ColorSymbol.Star,
        };

        static int Wrap(int color) => ((color % Count) + Count) % Count;

        public static CubePattern PatternOf(int color) => Patterns[Wrap(color)];
        public static ColorSymbol SymbolOf(int color) => Symbols[Wrap(color)];

        /// <summary>True where the pattern draws its darker "ink" on the 32×32 texture (x, y in 0..31, y up).</summary>
        public static bool PatternInk(CubePattern p, int x, int y)
        {
            switch (p)
            {
                case CubePattern.HorizontalStripes: return (y / 4) % 2 == 1;
                case CubePattern.Dots:
                {
                    // A dot of radius 2.5 px in the middle of every 8×8 cell.
                    float dx = (x % 8) - 3.5f, dy = (y % 8) - 3.5f;
                    return dx * dx + dy * dy <= 6.25f;
                }
                case CubePattern.Diagonal: return ((x + y) / 4) % 2 == 1;
                case CubePattern.Checker: return ((x / 8) + (y / 8)) % 2 == 1;
                case CubePattern.Grid: return x % 8 < 2 || y % 8 < 2;
                default: return false;
            }
        }

        /// <summary>Share of ink pixels of a pattern (0 for plain).</summary>
        public static float InkCoverage(CubePattern p)
        {
            int ink = 0;
            for (int y = 0; y < PatternSize; y++)
                for (int x = 0; x < PatternSize; x++)
                    if (PatternInk(p, x, y)) ink++;
            return ink / (float)(PatternSize * PatternSize);
        }

        /// <summary>
        /// The texture values and the material tint of a pattern at a contrast c: base pixels are 1, ink pixels are
        /// (1−c)/(1+c) of them, and the tint scales the palette so the face's MEAN albedo is exactly the palette color
        /// (the pattern adds texture, never a different color; a plain cube is the palette color itself).
        /// </summary>
        public static void PatternShades(CubePattern p, float contrast, out float inkValue, out float tint)
        {
            float f = InkCoverage(p);
            inkValue = f > 0f ? (1f - contrast) / (1f + contrast) : 1f;
            tint = 1f / ((1f - f) + f * inkValue);
        }

        /// <summary>
        /// Inside test of a symbol over [-0.5, 0.5]² (y up). Glyphs rasterize these with supersampling; the tests
        /// check the 6 shapes are distinct.
        /// </summary>
        public static bool SymbolInside(ColorSymbol s, float x, float y)
        {
            switch (s)
            {
                case ColorSymbol.Circle: return x * x + y * y < 0.31f * 0.31f;
                case ColorSymbol.Square: return Math.Abs(x) < 0.37f && Math.Abs(y) < 0.37f;
                case ColorSymbol.Triangle: return y > -0.33f && y < 0.42f && Math.Abs(x) < 0.44f * (0.42f - y) / 0.75f;
                case ColorSymbol.Diamond: return Math.Abs(x) + Math.Abs(y) < 0.44f;
                case ColorSymbol.Cross: return (Math.Abs(x) < 0.13f && Math.Abs(y) < 0.42f) || (Math.Abs(y) < 0.13f && Math.Abs(x) < 0.42f);
                default: return StarInside(x, y);
            }
        }

        /// <summary>A five-pointed star (also the titan grade star).</summary>
        public static bool StarInside(float x, float y)
        {
            double r = Math.Sqrt(x * x + y * y);
            double a = Math.Atan2(x, y); // 0 at the top
            double sector = Math.PI * 2.0 / 5.0;
            double m = a % sector;
            if (m < 0) m += sector;
            double k = Math.Abs(m / sector * 2.0 - 1.0); // 1 at a point, 0 between points
            return r < 0.20 + (0.46 - 0.20) * k;
        }
    }

    /// <summary>
    /// Color-vision helpers for the palette check (UX-1): Machado et al. (2009) full-severity protanopia and
    /// deuteranopia matrices applied in linear RGB, CIE L*a*b* (D65) and ΔE76.
    /// </summary>
    public static class ColorVision
    {
        public enum Deficiency
        {
            None,
            Protanopia,
            Deuteranopia,
        }

        static readonly double[,] Protan =
        {
            { 0.152286, 1.052583, -0.204868 },
            { 0.114503, 0.786281, 0.099216 },
            { -0.003882, -0.048116, 1.051998 },
        };

        static readonly double[,] Deutan =
        {
            { 0.367322, 0.860646, -0.227968 },
            { 0.280085, 0.672501, 0.047413 },
            { -0.011820, 0.042940, 0.968881 },
        };

        static double ToLinear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        static double ToSrgb(double c) => c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;
        static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

        /// <summary>The sRGB color as seen with <paramref name="d"/> (sRGB in, sRGB out, clamped).</summary>
        public static double[] Simulate(float[] srgb, Deficiency d)
        {
            var lin = new[] { ToLinear(srgb[0]), ToLinear(srgb[1]), ToLinear(srgb[2]) };
            if (d == Deficiency.None) return new double[] { srgb[0], srgb[1], srgb[2] };
            var m = d == Deficiency.Protanopia ? Protan : Deutan;
            var o = new double[3];
            for (int i = 0; i < 3; i++)
                o[i] = ToSrgb(Clamp01(m[i, 0] * lin[0] + m[i, 1] * lin[1] + m[i, 2] * lin[2]));
            return o;
        }

        /// <summary>CIE L*a*b* (D65) of an sRGB color.</summary>
        public static double[] Lab(double[] srgb)
        {
            double r = ToLinear(srgb[0]), g = ToLinear(srgb[1]), b = ToLinear(srgb[2]);
            double x = (0.4124564 * r + 0.3575761 * g + 0.1804375 * b) / 0.95047;
            double y = (0.2126729 * r + 0.7151522 * g + 0.0721750 * b) / 1.00000;
            double z = (0.0193339 * r + 0.1191920 * g + 0.9503041 * b) / 1.08883;
            double fx = F(x), fy = F(y), fz = F(z);
            return new[] { 116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz) };
        }

        static double F(double t) => t > 216.0 / 24389.0 ? Math.Pow(t, 1.0 / 3.0) : (24389.0 / 27.0 * t + 16) / 116.0;

        public static double DeltaE76(double[] lab1, double[] lab2)
        {
            double dl = lab1[0] - lab2[0], da = lab1[1] - lab2[1], db = lab1[2] - lab2[2];
            return Math.Sqrt(dl * dl + da * da + db * db);
        }

        /// <summary>ΔE76 between palette colors i and j as seen with <paramref name="d"/>.</summary>
        public static double PaletteDeltaE(int i, int j, Deficiency d) =>
            DeltaE76(Lab(Simulate(ColorStyleTable.Rgb[i], d)), Lab(Simulate(ColorStyleTable.Rgb[j], d)));
    }
}
