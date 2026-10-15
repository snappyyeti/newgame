using UnityEngine;

namespace Titan.Game
{
    /// <summary>
    /// Player settings read by the view, the HUD and Fx. Only "Efecte reduse" exists so far (CORE-4 reads it); UX-1 adds
    /// the pause menu that changes it and the other settings. Stored in PlayerPrefs until RET-1 moves it into the profile.
    /// </summary>
    public static class Settings
    {
        const string ReducedFxKey = "set_reducedFx";
        static bool loaded;
        static bool reducedFx;

        /// <summary>"Efecte reduse": no shake, no pulsing borders (they stay static).</summary>
        public static bool ReducedFx
        {
            get
            {
                if (!loaded)
                {
                    reducedFx = PlayerPrefs.GetInt(ReducedFxKey, 0) == 1;
                    loaded = true;
                }
                return reducedFx;
            }
            set
            {
                reducedFx = value;
                loaded = true;
                PlayerPrefs.SetInt(ReducedFxKey, value ? 1 : 0);
            }
        }
    }

    /// <summary>
    /// Procedural HUD glyphs (no art assets, no font glyphs): one symbol per palette color (DESIGN.md 7: circle, square,
    /// triangle, diamond, cross, star), a dot, a ring and a crown. White on transparent, tinted with GUI.color.
    /// </summary>
    public static class Glyphs
    {
        const int Size = 48;
        static Texture2D[] symbols;
        static Texture2D dot, ring, crown;

        /// <summary>The symbol of palette color <paramref name="color"/>.</summary>
        public static Texture2D Symbol(int color)
        {
            if (symbols == null)
            {
                symbols = new[]
                {
                    Make("circle", (x, y) => x * x + y * y < 0.40f * 0.40f),
                    Make("square", (x, y) => Mathf.Abs(x) < 0.33f && Mathf.Abs(y) < 0.33f),
                    Make("triangle", (x, y) => y > -0.33f && y < 0.42f && Mathf.Abs(x) < 0.44f * (0.42f - y) / 0.75f),
                    Make("diamond", (x, y) => Mathf.Abs(x) + Mathf.Abs(y) < 0.44f),
                    Make("cross", (x, y) => (Mathf.Abs(x) < 0.13f && Mathf.Abs(y) < 0.42f) || (Mathf.Abs(y) < 0.13f && Mathf.Abs(x) < 0.42f)),
                    Make("star", Star),
                };
            }
            return symbols[((color % symbols.Length) + symbols.Length) % symbols.Length];
        }

        public static Texture2D Dot => dot != null ? dot : (dot = Make("dot", (x, y) => x * x + y * y < 0.42f * 0.42f));

        public static Texture2D Ring => ring != null ? ring : (ring = Make("ring", (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return d < 0.44f && d > 0.32f;
        }));

        public static Texture2D Crown => crown != null ? crown : (crown = Make("crown", (x, y) =>
        {
            if (y < -0.34f || y > 0.40f || Mathf.Abs(x) > 0.44f) return false;
            if (y < -0.08f) return true; // the band
            // Three spikes rising from the band.
            foreach (float cx in new[] { -0.32f, 0f, 0.32f })
            {
                float half = 0.13f * (0.40f - y) / 0.48f;
                if (Mathf.Abs(x - cx) < half) return true;
            }
            return false;
        }));

        static bool Star(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Atan2(x, y); // 0 at the top
            float sector = Mathf.PI * 2f / 5f;
            float k = Mathf.Abs(Mathf.Repeat(a, sector) / sector * 2f - 1f); // 1 at a point, 0 between points
            return r < Mathf.Lerp(0.20f, 0.46f, k);
        }

        /// <summary>Renders an inside test over [-0.5, 0.5]² with 4×4 supersampling (soft edges). Texture rows go up.</summary>
        static Texture2D Make(string name, System.Func<float, float, bool> inside)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[Size * Size];
            for (int py = 0; py < Size; py++)
                for (int px = 0; px < Size; px++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                        {
                            float x = (px + (sx + 0.5f) / 4f) / Size - 0.5f;
                            float y = (py + (sy + 0.5f) / 4f) / Size - 0.5f;
                            if (inside(x, y)) hits++;
                        }
                    pixels[py * Size + px] = new Color(1f, 1f, 1f, hits / 16f);
                }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
    }
}
