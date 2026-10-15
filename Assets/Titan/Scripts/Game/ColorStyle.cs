using Titan.Core;
using UnityEngine;

namespace Titan.Game
{
    /// <summary>
    /// The look of the 6 palette colors (UX-1, DESIGN.md 7). The table (pattern, symbol, RGB) is Titan.Core's
    /// <see cref="ColorStyleTable"/>, unit tested; this builds the Unity side of it:
    ///
    /// | Palette index | Cube pattern       | Symbol   |
    /// |---------------|--------------------|----------|
    /// | 0 red         | plain              | circle   |
    /// | 1 blue        | horizontal stripes | square   |
    /// | 2 yellow      | dots               | triangle |
    /// | 3 green       | diagonal           | diamond  |
    /// | 4 purple      | checker            | cross    |
    /// | 5 orange      | grid               | star     |
    ///
    /// Cubes: one shared material per color with a procedural 32×32 point-filtered pattern texture; contrast ±15%,
    /// ±30% with "Simboluri culori" on. The face's mean albedo is the palette color (cosmetics and effects never
    /// change it). Symbols: <see cref="Glyphs.Symbol"/> (procedural textures, never font glyphs).
    ///
    /// Editor debug (F7): the palette is shown as greys of the same luminance on every cube, cannon, projectile and
    /// HUD tag, to check that each color can be told by its symbol and pattern alone.
    /// </summary>
    public static class ColorStyle
    {
        static Color[] palette;
        static Texture2D[] patterns;
        static Material[] cubeMaterials;
        static Material[] plainMaterials;
        static float builtContrast = -1f;
        static bool builtGrey;

        /// <summary>The palette (the real colors, whatever the debug view shows).</summary>
        public static Color[] Palette
        {
            get
            {
                if (palette != null) return palette;
                palette = new Color[ColorStyleTable.Count];
                for (int i = 0; i < palette.Length; i++)
                {
                    var c = ColorStyleTable.Rgb[i];
                    palette[i] = new Color(c[0], c[1], c[2]);
                }
                return palette;
            }
        }

        /// <summary>Editor debug view: the palette as luminance greys (see the class summary).</summary>
        public static bool DebugGreyscale { get; private set; }

        public static void SetDebugGreyscale(bool on)
        {
            if (DebugGreyscale == on) return;
            DebugGreyscale = on;
            Refresh();
        }

        static int Wrap(int color) => ((color % ColorStyleTable.Count) + ColorStyleTable.Count) % ColorStyleTable.Count;

        /// <summary>The color to draw palette color <paramref name="color"/> with (grey in the debug view).</summary>
        public static Color Shown(int color)
        {
            var c = Palette[Wrap(color)];
            if (!DebugGreyscale) return c;
            float y = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            return new Color(y, y, y, c.a);
        }

        /// <summary>The pattern contrast in effect: ±30% with "Simboluri culori" on, ±15% off.</summary>
        static float Contrast => Settings.ColorSymbols ? ColorStyleTable.ContrastStrong : ColorStyleTable.ContrastNormal;

        /// <summary>The shared cube material of a color (pattern texture, tint so the mean is the palette color).</summary>
        public static Material CubeMaterial(int color, System.Func<Color, Material> make)
        {
            Ensure(make);
            return cubeMaterials[Wrap(color)];
        }

        /// <summary>The plain (untextured) material of a color: cannon bands and barrels, projectiles, sparks.</summary>
        public static Material PlainMaterial(int color, System.Func<Color, Material> make)
        {
            Ensure(make);
            return plainMaterials[Wrap(color)];
        }

        static void Ensure(System.Func<Color, Material> make)
        {
            // Rebuilt when missing, or when Unity destroyed the old ones (play mode re-entered without a domain reload).
            if (cubeMaterials != null && cubeMaterials[0] != null && patterns[0] != null) return;
            int n = ColorStyleTable.Count;
            patterns = new Texture2D[n];
            cubeMaterials = new Material[n];
            plainMaterials = new Material[n];
            for (int i = 0; i < n; i++)
            {
                patterns[i] = new Texture2D(ColorStyleTable.PatternSize, ColorStyleTable.PatternSize, TextureFormat.RGBA32, false)
                {
                    name = "pattern-" + ColorStyleTable.Names[i],
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Repeat,
                };
                cubeMaterials[i] = make(Palette[i]);
                cubeMaterials[i].mainTexture = patterns[i];
                plainMaterials[i] = make(Palette[i]);
            }
            builtContrast = -1f;
            Refresh();
        }

        /// <summary>
        /// Rewrites the pattern textures and material colors in place (materials keep their identity, so nothing
        /// needs to be re-assigned): after "Simboluri culori" changes, and when the debug grey view toggles.
        /// </summary>
        public static void Refresh()
        {
            if (cubeMaterials == null || cubeMaterials[0] == null || patterns[0] == null) return;
            float contrast = Contrast;
            bool texturesStale = !Mathf.Approximately(contrast, builtContrast);
            int size = ColorStyleTable.PatternSize;
            var pixels = new Color32[size * size];
            for (int i = 0; i < cubeMaterials.Length; i++)
            {
                var pattern = ColorStyleTable.PatternOf(i);
                ColorStyleTable.PatternShades(pattern, contrast, out float ink, out float tint);
                if (texturesStale)
                {
                    byte inkByte = (byte)Mathf.RoundToInt(255f * ink);
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            byte v = ColorStyleTable.PatternInk(pattern, x, y) ? inkByte : (byte)255;
                            pixels[y * size + x] = new Color32(v, v, v, 255);
                        }
                    patterns[i].SetPixels32(pixels);
                    patterns[i].Apply(false);
                }
                var shown = Shown(i);
                // Tint above 1 is fine for a material color: base pixels come out at palette × tint, ink pixels darker.
                cubeMaterials[i].color = new Color(shown.r * tint, shown.g * tint, shown.b * tint, 1f);
                plainMaterials[i].color = new Color(shown.r, shown.g, shown.b, 1f);
            }
            builtContrast = contrast;
            builtGrey = DebugGreyscale;
        }

        /// <summary>True when the textures or colors are out of date (the settings changed since the last refresh).</summary>
        public static bool Stale => cubeMaterials != null && (!Mathf.Approximately(Contrast, builtContrast) || builtGrey != DebugGreyscale);
    }
}
