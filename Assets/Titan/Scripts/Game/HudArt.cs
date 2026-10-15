using UnityEngine;

namespace Titan.Game
{
    /// <summary>
    /// Player settings, the one static class Fx, ArenaView and the HUD read (UX-1). They live in the profile (RET-1,
    /// <see cref="Titan.Core.ProfileSettings"/>): <see cref="Bind"/> connects them at startup, and every change asks
    /// for a save and raises <see cref="Changed"/>. Before Bind (or without a profile) they fall back to the
    /// defaults (sfx, haptics and color symbols on, reduced effects off, language auto) and the CORE-4 PlayerPrefs
    /// key for reduced effects, which a new profile migrates. The pause menu changes them.
    /// </summary>
    public static class Settings
    {
        const string ReducedFxKey = "set_reducedFx";
        static Titan.Core.ProfileSettings bound;
        static System.Action save;
        static readonly Titan.Core.ProfileSettings unbound = new Titan.Core.ProfileSettings();

        /// <summary>After any change (the game re-applies sound, language and the cube patterns).</summary>
        public static event System.Action Changed;

        /// <summary>Settings now read and write <paramref name="settings"/>; <paramref name="onChanged"/> saves the profile.</summary>
        public static void Bind(Titan.Core.ProfileSettings settings, System.Action onChanged)
        {
            bound = settings;
            save = onChanged;
            Changed?.Invoke();
        }

        static Titan.Core.ProfileSettings Current => bound ?? unbound;

        static void Set(ref bool field, bool value)
        {
            if (field == value) return;
            field = value;
            save?.Invoke();
            Changed?.Invoke();
        }

        /// <summary>"Efecte reduse": no shake, no hitstop, no FOV punch, no slow-mo, no pulsing (borders stay static).</summary>
        public static bool ReducedFx
        {
            get => bound != null ? bound.reducedFx : PlayerPrefs.GetInt(ReducedFxKey, 0) == 1;
            set
            {
                if (bound == null)
                {
                    PlayerPrefs.SetInt(ReducedFxKey, value ? 1 : 0);
                    Changed?.Invoke();
                    return;
                }
                Set(ref bound.reducedFx, value);
            }
        }

        /// <summary>"Sunet": every sound (AudioListener.volume 1 or 0).</summary>
        public static bool Sfx
        {
            get => Current.sfx;
            set => Set(ref Current.sfx, value);
        }

        /// <summary>"Vibrații": the FEEL-1 haptics (phones only).</summary>
        public static bool Haptics
        {
            get => Current.haptics;
            set => Set(ref Current.haptics, value);
        }

        /// <summary>"Simboluri culori": symbols on the cannon tags and the hidden chip, and stronger cube patterns (±30%).</summary>
        public static bool ColorSymbols
        {
            get => Current.colorSymbols;
            set => Set(ref Current.colorSymbols, value);
        }

        /// <summary>The stored language: "" (the phone's), "ro" or "en".</summary>
        public static string Lang
        {
            get => Current.lang ?? "";
            set
            {
                value = value ?? "";
                if (Current.lang == value) return;
                Current.lang = value;
                save?.Invoke();
                Changed?.Invoke();
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

        /// <summary>The symbol of palette color <paramref name="color"/> (shapes: Titan.Core.ColorStyleTable, tested distinct).</summary>
        public static Texture2D Symbol(int color)
        {
            if (symbols == null)
            {
                symbols = new Texture2D[Titan.Core.ColorStyleTable.Count];
                for (int i = 0; i < symbols.Length; i++)
                {
                    var shape = Titan.Core.ColorStyleTable.SymbolOf(i);
                    symbols[i] = Make(shape.ToString(), (x, y) => Titan.Core.ColorStyleTable.SymbolInside(shape, x, y));
                }
            }
            return symbols[((color % symbols.Length) + symbols.Length) % symbols.Length];
        }

        static Texture2D check, ex, pause;

        /// <summary>A check mark (a defeated titan on the Titanul Zilei squares: state by shape, not only green).</summary>
        public static Texture2D Check => check != null ? check : (check = Make("check", (x, y) =>
            DistToSegment(x, y, -0.32f, 0.02f, -0.10f, -0.22f) < 0.08f || DistToSegment(x, y, -0.10f, -0.22f, 0.34f, 0.28f) < 0.08f));

        /// <summary>An X (the titan an attempt ended on: state by shape, not only red).</summary>
        public static Texture2D Ex => ex != null ? ex : (ex = Make("ex", (x, y) =>
            DistToSegment(x, y, -0.28f, -0.28f, 0.28f, 0.28f) < 0.08f || DistToSegment(x, y, -0.28f, 0.28f, 0.28f, -0.28f) < 0.08f));

        /// <summary>The pause icon: two bars.</summary>
        public static Texture2D Pause => pause != null ? pause : (pause = Make("pause", (x, y) =>
            Mathf.Abs(y) < 0.30f && (Mathf.Abs(x + 0.15f) < 0.08f || Mathf.Abs(x - 0.15f) < 0.08f)));

        static float DistToSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
            float cx = ax + t * dx - px, cy = ay + t * dy - py;
            return Mathf.Sqrt(cx * cx + cy * cy);
        }

        static Texture2D bigStar;

        /// <summary>A five-pointed star filling the texture (the titan grade on the defeat banner).</summary>
        public static Texture2D StarGlyph => bigStar != null ? bigStar : (bigStar = Make("grade-star", (x, y) => Titan.Core.ColorStyleTable.StarInside(x * 0.92f, y * 0.92f)));

        public static Texture2D Dot => dot != null ? dot : (dot = Make("dot", (x, y) => x * x + y * y < 0.42f * 0.42f));

        public static Texture2D Ring => ring != null ? ring : (ring = Make("ring", (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return d < 0.44f && d > 0.32f;
        }));

        static Texture2D arrowDown;

        /// <summary>A down-pointing arrow (FTUE hint H3 points at the slot pips with it).</summary>
        public static Texture2D ArrowDown => arrowDown != null ? arrowDown : (arrowDown = Make("arrow-down", (x, y) =>
            (y > 0.02f && y < 0.44f && Mathf.Abs(x) < 0.11f) || (y <= 0.06f && y > -0.42f && Mathf.Abs(x) < 0.40f * (y + 0.42f) / 0.48f)));

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
