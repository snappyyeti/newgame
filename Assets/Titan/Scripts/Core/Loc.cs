using System;
using System.Collections.Generic;
using System.Globalization;

namespace Titan.Core
{
    /// <summary>The two languages of the string table (UX-1). The index is the column in <see cref="Loc"/>'s table.</summary>
    public enum Lang
    {
        Ro = 0,
        En = 1,
    }

    /// <summary>
    /// Every player-facing string of the game (UX-1, DESIGN.md 7): <c>Loc.T(key, args)</c> over a
    /// Dictionary&lt;string, string[]&gt; {ro, en}. Arguments use string.Format placeholders ({0}, {1}); numbers are
    /// formatted by the caller (<see cref="HudText.Num"/> follows the language). Pure C#, so the Core text helpers
    /// (HudText, FtueText, DailyText, AdText) and the tests use the same table as the Unity HUD.
    ///
    /// Rules for the English copy (the same as the Romanian): true numbers only, no fear or loss framing ("you will
    /// lose", "don't miss"), no fake urgency. A missing key returns the key itself (a test checks that every key used
    /// in the code exists and that every key in the table is used).
    /// </summary>
    public static class Loc
    {
        /// <summary>The language strings come out in. Ro until the game picks one (tests rely on the Ro default).</summary>
        public static Lang Current { get; private set; } = Lang.Ro;

        /// <summary>Raised after the language changes (the HUD has nothing cached, but a listener may).</summary>
        public static event Action Changed;

        public static void Set(Lang lang)
        {
            if (lang == Current) return;
            Current = lang;
            Changed?.Invoke();
        }

        /// <summary>"ro" / "en" (the profile stores this; "" means the phone's language).</summary>
        public static string Code(Lang lang) => lang == Lang.En ? "en" : "ro";

        /// <summary>Parses a stored language code: "ro" or "en"; anything else (including "") is null (= auto).</summary>
        public static Lang? Parse(string code)
        {
            if (string.Equals(code, "ro", StringComparison.OrdinalIgnoreCase)) return Lang.Ro;
            if (string.Equals(code, "en", StringComparison.OrdinalIgnoreCase)) return Lang.En;
            return null;
        }

        /// <summary>The default when the player never chose: Romanian on a Romanian phone, English everywhere else.</summary>
        public static Lang Resolve(string stored, bool phoneIsRomanian) => Parse(stored) ?? (phoneIsRomanian ? Lang.Ro : Lang.En);

        public static bool Has(string key) => Table.ContainsKey(key);

        /// <summary>All keys (tests).</summary>
        public static IEnumerable<string> Keys => Table.Keys;

        /// <summary>The raw text of <paramref name="key"/> in <paramref name="lang"/> (tests).</summary>
        public static string Raw(string key, Lang lang) => Table.TryGetValue(key, out var row) ? row[(int)lang] : key;

        public static string T(string key)
        {
            return Table.TryGetValue(key, out var row) ? row[(int)Current] : key;
        }

        public static string T(string key, object a0) => Format(T(key), a0);
        public static string T(string key, object a0, object a1) => Format(T(key), a0, a1);
        public static string T(string key, object a0, object a1, object a2) => Format(T(key), a0, a1, a2);
        public static string T(string key, object a0, object a1, object a2, object a3) => Format(T(key), a0, a1, a2, a3);

        static string Format(string text, params object[] args) => string.Format(CultureInfo.InvariantCulture, text, args);

        /// <summary>{ key, { ro, en } }. Keep the groups; a test checks both columns, the placeholders and the usage.</summary>
        static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            // ---------------------------------------------------------------- common
            ["common.back"] = new[] { "Înapoi", "Back" },
            ["common.on"] = new[] { "Pornit", "On" },
            ["common.off"] = new[] { "Oprit", "Off" },
            ["common.no"] = new[] { "Nu", "No" },
            ["common.close"] = new[] { "X", "X" },
            ["common.seconds"] = new[] { "{0} s", "{0} s" },
            ["common.title"] = new[] { "Titanul din Cuburi", "Titanul din Cuburi" },

            // ---------------------------------------------------------------- run HUD
            ["hud.record"] = new[] { "Record {0}", "Best {0}" },
            ["hud.recordNew"] = new[] { "RECORD NOU!", "NEW BEST!" },
            ["hud.recordLegacy"] = new[] { "Record vechi (alt punctaj): {0}", "Old best (old scoring): {0}" },
            ["hud.recordIn"] = new[] { "Record în {0}", "Best in {0}" },
            ["hud.officialAttempt"] = new[] { "Încercarea oficială", "Official attempt" },
            ["hud.cycleLine"] = new[] { "Ciclul {0} · Titanul #{1}", "Cycle {0} · Titan #{1}" },
            ["hud.dailyLine"] = new[] { "{0} #{1} · Titanul {2}/{3}", "{0} #{1} · Titan {2}/{3}" },
            ["hud.wall"] = new[] { "ZIDUL", "WALL" },
            ["hud.emptyDeck"] = new[] { "pachet gol", "deck empty" },
            ["hud.zzz"] = new[] { "zzz", "zzz" },
            ["hud.cut"] = new[] { "TAIE!", "CUTS!" },
            ["hud.cutIn"] = new[] { "taie în {0}", "cuts in {0}" },
            ["hud.hiddenOne"] = new[] { "1 culoare ascunsă ?", "1 hidden color ?" },
            ["hud.hiddenMany"] = new[] { "{0} culori ascunse ?", "{0} hidden colors ?" },
            ["hud.hiddenShort"] = new[] { "ascunse:", "hidden:" },
            ["hud.chainX"] = new[] { "x{0}", "x{0}" },
            ["hud.combo"] = new[] { "COMBO x{0}", "COMBO x{0}" },
            ["hud.wouldJam"] = new[] { "Ar bloca ultimul loc! Alege o culoare vizibilă.", "That would block the last slot! Pick a visible color." },
            ["hud.lineFull"] = new[] { "Toate cele {0} locuri sunt ocupate!", "All {0} slots are taken!" },
            ["hud.titanNew"] = new[] { "TITAN NOU: #{0}", "NEW TITAN: #{0}" },
            ["hud.checkpoint"] = new[] { "Punct de salvare: titanul #{0}", "Checkpoint: titan #{0}" },
            ["hud.defeated"] = new[] { "TITAN DOBORÂT!  +{0}", "TITAN DOWN!  +{0}" },
            ["hud.nearMiss"] = new[] { "LA MUSTAȚĂ!  x{0}  +{1}", "CLOSE CALL!  x{0}  +{1}" },
            ["hud.sprint"] = new[] { "TITAN RAPID!", "FAST TITAN!" },
            ["hud.boss"] = new[] { "TITAN GIGANT!", "GIANT TITAN!" },
            ["hud.titanN"] = new[] { "TITANUL #{0}", "TITAN #{0}" },
            ["hud.salvaged"] = new[] { "RECUPERAT +{0}", "RECOVERED +{0}" },
            ["hud.logCopied"] = new[] { "Jurnal copiat", "Log copied" },

            // ---------------------------------------------------------------- piece popups and stars (CORE-5)
            ["piece.head"] = new[] { "DECAPITARE!", "BEHEADED!" },
            ["piece.clean"] = new[] { "TĂIETURĂ CURATĂ!", "CLEAN CUT!" },
            ["piece.broken"] = new[] { "BUCATĂ RUPTĂ!", "PIECE OFF!" },
            ["star.noHead"] = new[] { "Capul nu a căzut întreg", "The head did not fall whole" },
            ["star.lineCount"] = new[] { "Ai avut {0} tunuri pe zid", "You had {0} cannons on the wall" },

            // ---------------------------------------------------------------- game over and menus (RET-1, RET-2)
            ["over.wall"] = new[] { "Titanul a spart zidul!", "The titan broke through the wall!" },
            ["over.deadlock"] = new[] { "BLOCAT! Niciun tun nu mai\nare în ce trage.", "STUCK! No cannon has\nanything left to shoot." },
            ["over.slotsFull"] = new[] { "Cele 5 locuri sunt pline\nși niciun tun nu are în ce trage!", "All 5 slots are full\nand no cannon can shoot!" },
            ["over.lossWall"] = new[] { "Titanul a spart zidul!", "The titan broke through the wall!" },
            ["over.lossDeadlock"] = new[] { "BLOCAT!", "STUCK!" },
            ["over.lossSlots"] = new[] { "Locurile sunt pline!", "The slots are full!" },
            ["over.bestCut"] = new[] { "Cea mai bună tăietură: +{0}", "Best cut: +{0}" },
            ["over.maxCombo"] = new[] { "Combo maxim: x{0}", "Max combo: x{0}" },
            ["over.playAgain"] = new[] { "Joacă din nou", "Play again" },
            ["over.resumeCycle"] = new[] { "Reia ciclul (titanul #{0})", "Resume cycle (titan #{0})" },
            ["over.nearRecord"] = new[] { "Ai fost la {0}% din record!", "You reached {0}% of your best!" },
            ["over.recordValue"] = new[] { "Record: {0}", "Best: {0}" },
            ["over.runStats"] = new[] { "Titani: {0} · Stele: {1} · Bucăți rupte: {2}", "Titans: {0} · Stars: {1} · Pieces cut: {2}" },
            ["over.goalCheckpoint"] = new[] { "Încă un titan până la punctul de salvare #{0}", "One more titan to checkpoint #{0}" },
            ["over.goalDaily"] = new[] { "Titanul Zilei te așteaptă", "The Daily Titan is waiting" },
            ["over.goalFurthest"] = new[] { "Cel mai departe: titanul #{0}", "Furthest: titan #{0}" },
            ["menu.newGame"] = new[] { "Joc nou", "New game" },
            ["menu.play"] = new[] { "Joacă", "Play" },
            ["menu.continue"] = new[] { "Continuă (titanul #{0})", "Continue (titan #{0})" },
            ["menu.checkpointNote"] = new[] { "Punct de salvare: același titan, cu scorul de atunci ({0}).", "Checkpoint: the same titan, with the score you had then ({0})." },

            // ---------------------------------------------------------------- pause menu (UX-1)
            ["pause.title"] = new[] { "PAUZĂ", "PAUSED" },
            ["pause.continue"] = new[] { "Continuă", "Continue" },
            ["pause.sound"] = new[] { "Sunet: {0}", "Sound: {0}" },
            ["pause.haptics"] = new[] { "Vibrații: {0}", "Vibration: {0}" },
            ["pause.reducedFx"] = new[] { "Efecte reduse: {0}", "Reduced effects: {0}" },
            ["pause.symbols"] = new[] { "Simboluri culori: {0}", "Color symbols: {0}" },
            ["pause.language"] = new[] { "Limba: Română", "Language: English" },
            ["pause.newGameAsk"] = new[] { "Începi un joc nou de la titanul #1?", "Start a new game from titan #1?" },
            ["pause.newGameNote"] = new[] { "Runda de acum se încheie aici; scorul ei contează la record.", "This run ends here; its score still counts toward your best." },
            ["pause.newGameYes"] = new[] { "Da, joc nou", "Yes, new game" },

            // ---------------------------------------------------------------- developer overlays (F7, F8, F9, F10)
            ["dev.perf"] = new[] { "fps {0} · p95 {1} ms (300 cadre)", "fps {0} · p95 {1} ms (300 frames)" },
            ["dev.objects"] = new[] { "obiecte active {0} · corpuri rigide {1}", "active objects {0} · rigidbodies {1}" },
            ["dev.greyscale"] = new[] { "Paletă gri (verificare simboluri): {0}", "Grey palette (symbol check): {0}" },
            ["dev.adsSim"] = new[] { "Reclame simulate: {0}", "Simulated ads: {0}" },
            ["dev.adsFail"] = new[] { "EȘUEAZĂ (ForceFail)", "FAILING (ForceFail)" },
            ["dev.adsNormal"] = new[] { "normal", "normal" },

            // ---------------------------------------------------------------- FTUE hints (FTUE-1)
            ["ftue.h1"] = new[] { "Atinge tunul galben", "Tap the yellow cannon" },
            ["ftue.h2"] = new[] { "zzz = culoarea e ascunsă în spate. Va trage când apare.", "zzz = its color is hidden behind. It fires when the color shows." },
            ["ftue.h3"] = new[] { "Nu umple toate cele 5 locuri!", "Don't fill all 5 slots!" },
            ["ftue.h4Tip"] = new[] { "Sfat: lasă mereu 1-2 locuri libere.", "Tip: always keep 1-2 slots free." },
            ["ftue.causeWall"] = new[] { "Titanul a ajuns la zid.", "The titan reached the wall." },
            ["ftue.causeDeadlock"] = new[] { "Niciun tun nu mai avea în ce trage.", "No cannon had anything left to shoot." },
            ["ftue.causeSlots"] = new[] { "Toate cele 5 locuri erau pline cu tunuri care așteptau.", "All 5 slots were full of waiting cannons." },

            // ---------------------------------------------------------------- Titanul Zilei (RET-3)
            ["daily.title"] = new[] { "Titanul Zilei", "Daily Titan" },
            ["daily.intro"] = new[] { "Încercarea de azi e fără ajutoare și fără reclame — aceeași provocare pentru toți.", "Today's attempt has no helpers and no ads — the same challenge for everyone." },
            ["daily.practice"] = new[] { "ANTRENAMENT", "PRACTICE" },
            ["daily.resumeOfficial"] = new[] { "Continuă încercarea oficială", "Continue the official attempt" },
            ["daily.quitConfirm"] = new[] { "Închei încercarea oficială?", "End the official attempt?" },
            ["daily.shieldUsed"] = new[] { "Un scut ți-a păstrat seria.", "A shield kept your streak." },
            ["daily.streakReset"] = new[] { "Bine ai revenit! Începe o serie nouă azi.", "Welcome back! Start a new streak today." },
            ["daily.copied"] = new[] { "Copiat! Lipește-l unde vrei.", "Copied! Paste it anywhere." },
            ["daily.copy"] = new[] { "Copiază rezultatul", "Copy result" },
            ["daily.shareTitle"] = new[] { "Titanul din Cuburi · Ziua #{0}", "Titanul din Cuburi · Day #{0}" },
            ["daily.resultLine"] = new[] { "Scor {0} · {1} · {2} bucăți rupte", "Score {0} · {1} · {2} pieces cut" },
            ["daily.untried"] = new[] { "neîncercat", "not tried" },
            ["daily.inProgress"] = new[] { "în curs: {0}/{1}", "in progress: {0}/{1}" },
            ["daily.bestStreak"] = new[] { "Cea mai lungă serie: {0}", "Longest streak: {0}" },
            ["daily.streakOne"] = new[] { "Serie: 1 zi", "Streak: 1 day" },
            ["daily.streakMany"] = new[] { "Serie: {0} zile", "Streak: {0} days" },
            ["daily.shieldOne"] = new[] { "1 scut", "1 shield" },
            ["daily.shieldMany"] = new[] { "{0} scuturi", "{0} shields" },
            ["daily.nextIn"] = new[] { "Următorul în {0}", "Next in {0}" },
            ["daily.nextDaily"] = new[] { "Următorul Titan al Zilei în {0}", "Next Daily Titan in {0}" },
            ["daily.officialStatus"] = new[] { "Încercarea oficială: {0}", "Official attempt: {0}" },
            ["daily.officialUntried"] = new[] { "Încercarea oficială: neîncercată", "Official attempt: not tried yet" },
            ["daily.onlyFirst"] = new[] { "Contează doar prima încercare; începe la prima atingere. Apoi poți juca la antrenament oricât.", "Only the first attempt counts; it starts at your first tap. After that, practice as much as you like." },
            ["daily.practiceAfter"] = new[] { "{0}: {1} · cel mai bun azi {2}", "{0}: {1} · today's best {2}" },
            ["daily.practiceToday"] = new[] { "{0} azi: cel mai bun {1}", "{0} today: best {1}" },
            ["daily.preparing"] = new[] { "Se pregătește titanul zilei…", "Preparing today's titan…" },
            ["daily.startOfficial"] = new[] { "Începe încercarea oficială", "Start the official attempt" },
            ["daily.normalGame"] = new[] { "Joc normal", "Normal game" },
            ["daily.quitNote"] = new[] { "Rezultatul de azi rămâne cel de acum.", "Today's result stays as it is now." },
            ["daily.quitYes"] = new[] { "Da, închei", "Yes, end it" },
            ["daily.quitNo"] = new[] { "Nu, continui", "No, keep playing" },
            ["daily.startsAtTap"] = new[] { "Încercarea oficială începe la prima atingere.", "The official attempt starts at your first tap." },
            ["daily.exit"] = new[] { "Ieși", "Exit" },
            ["daily.end"] = new[] { "Închei", "End" },

            // ---------------------------------------------------------------- ads (ADS-1, ADS-2): opt-in cards, between runs
            ["ad.tag"] = new[] { "Reclamă", "Ad" },
            ["ad.beforeRun"] = new[] { "Reclamă înainte de rundă", "Ad before the next run" },
            ["ad.interCard"] = new[] { "Reclamă — Mulțumim că susții jocul gratuit.", "Ad — Thanks for supporting the free game." },
            ["ad.reviveTitle"] = new[] { "A DOUA SUFLARE", "SECOND WIND" },
            ["ad.reviveValue"] = new[] { "Tunurile adormite se întorc în pachet, titanul dă înapoi și primești tunuri cu culori vizibile.", "Sleeping cannons go back to the deck, the titan steps back and you get cannons with visible colors." },
            ["ad.reviveAccept"] = new[] { "Privește reclama și continuă", "Watch the ad and continue" },
            ["ad.reviveAcceptFree"] = new[] { "Revino gratis", "Come back free" },
            ["ad.decline"] = new[] { "Nu, mulțumesc", "No, thanks" },
            ["ad.reviveFooter"] = new[] { "O singură dată pe rundă.", "Once per run." },
            ["ad.reviveFallback"] = new[] { "Reclama nu s-a încărcat. Îți dăm revenirea oricum.", "The ad didn't load. Here's your comeback anyway." },
            ["ad.noneNow"] = new[] { "Nicio reclamă disponibilă acum", "No ad available right now" },
            ["ad.loading"] = new[] { "Se încarcă reclama", "Loading the ad" },
            ["ad.slotButton"] = new[] { "+1 LOC", "+1 SLOT" },
            ["ad.slotTitle"] = new[] { "+1 LOC PE ZID", "+1 SLOT ON THE WALL" },
            ["ad.slotValue"] = new[] { "Un al 6-lea loc pentru tunuri, până dobori acest titan.", "A 6th cannon slot until you defeat this titan." },
            ["ad.watch"] = new[] { "Privește reclama", "Watch the ad" },
            ["ad.slotAcceptFree"] = new[] { "Încearcă gratis", "Try it free" },
            ["ad.slotDecline"] = new[] { "Nu acum", "Not now" },
            ["ad.slotFooter"] = new[] { "Ajutoare rămase în rundă: {0}", "Helps left this run: {0}" },
            ["ad.reviveStake"] = new[] { "Titanul #{0} · {1}% distrus · Scor {2}", "Titan #{0} · {1}% destroyed · Score {2}" },
            ["ad.breakTitle"] = new[] { "PAUZĂ DE RECLAME", "AD BREAK" },
            ["ad.breakValue"] = new[] { "Privește o reclamă acum și ai 30 de minute fără reclame între runde. Reclamele opționale (cu recompensă) rămân la alegerea ta.", "Watch one ad now and get 30 minutes with no ads between runs. Optional (rewarded) ads stay your choice." },
            ["ad.breakLink"] = new[] { "Fără reclame 30 min", "No ads for 30 min" },
            ["ad.breakEnded"] = new[] { "Pauza de reclame s-a încheiat.", "The ad break is over." },
            ["ad.breakRemaining"] = new[] { "Fără reclame încă {0}", "No ads for another {0}" },
            ["ad.simTitle"] = new[] { "RECLAMĂ SIMULATĂ", "SIMULATED AD" },
            ["ad.simNote"] = new[] { "Înlocuitor pentru LevelPlay — nu e o reclamă reală.", "Placeholder for LevelPlay — not a real ad." },
            ["ad.simRewarded"] = new[] { "Reclamă cu recompensă", "Rewarded ad" },
            ["ad.simBetween"] = new[] { "Reclamă între runde", "Ad between runs" },
            ["ad.simCloseWarn"] = new[] { "Dacă închizi acum nu primești recompensa.", "If you close now you don't get the reward." },
            ["ad.simKeepWatching"] = new[] { "Continuă reclama", "Keep watching" },
            ["ad.simClose"] = new[] { "Închide", "Close" },
        };
    }
}
