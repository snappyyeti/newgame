using Titan.Core;
using UnityEngine;

namespace Titan.Game
{
    /// <summary>
    /// The Unity side of the RO/EN string table (UX-1). The table itself and <c>Loc.T(key, args)</c> are
    /// <see cref="Titan.Core.Loc"/> (pure C#, so the Core text helpers and the tests share it); this picks the
    /// language from the setting ("" = the phone's: Romanian on a Romanian phone, English otherwise) and runs the
    /// editor font check.
    /// </summary>
    public static class Language
    {
        /// <summary>The language the setting resolves to now.</summary>
        public static Lang Resolved => Titan.Core.Loc.Resolve(Settings.Lang, Application.systemLanguage == SystemLanguage.Romanian);

        public static void Apply() => Titan.Core.Loc.Set(Resolved);

        /// <summary>The pause menu's "Limba RO/EN": flips the language and stores the explicit choice.</summary>
        public static void Toggle()
        {
            var next = Resolved == Lang.Ro ? Lang.En : Lang.Ro;
            Settings.Lang = Titan.Core.Loc.Code(next);
            Apply();
        }

        const string Diacritics = "ăâîșțĂÂÎȘȚ";
        static bool fontChecked;

        /// <summary>
        /// Editor only, once, from OnGUI (the IMGUI skin exists only there): asks the default IMGUI font for
        /// "ăâîșțĂÂÎȘȚ" and logs every glyph it cannot render. A bundled font would be a separate asset decision.
        /// </summary>
        public static void CheckFontOnce()
        {
            if (fontChecked || !Application.isEditor) return;
            fontChecked = true;
            var font = GUI.skin != null ? GUI.skin.font : null;
            if (font == null)
            {
                Debug.LogWarning("[Titan] Font check: the IMGUI skin has no font to check.");
                return;
            }
            var missing = "";
            if (font.dynamic) font.RequestCharactersInTexture(Diacritics, 0, FontStyle.Normal);
            foreach (char c in Diacritics)
            {
                bool ok = font.dynamic ? font.GetCharacterInfo(c, out _, 0, FontStyle.Normal) : font.HasCharacter(c);
                if (!ok) missing += c;
            }
            if (missing.Length == 0) Debug.Log("[Titan] Font check: '" + Diacritics + "' all render in the IMGUI font '" + font.name + "'.");
            else Debug.LogWarning("[Titan] Font check: the IMGUI font '" + font.name + "' cannot render '" + missing + "' (U+" + ((int)missing[0]).ToString("X4") + "...).");
        }
    }
}
