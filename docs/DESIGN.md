# Titanul din Cuburi — Document de design (GDD)

**Versiune:** 1.0 · **Data:** 15 octombrie 2026 · **Autor:** directorul de creație (echipa de agenți) · **Pentru:** proprietarul jocului
**Platformă:** Unity 6 (6000.6), mobil, portret · **Monetizare:** exclusiv reclame (fără magazin, fără IAP)

Acest document ia toate rapoartele echipei (scouterii pe Pixel Flow și pe reclame, analistul de retenție, analistul de cod cu simulatorul, 33 de propuneri și două runde de critică) și le transformă într-o singură decizie: ce joc facem, după ce reguli, și ce construim în sprintul următor.

---

## Cuprins

1. Viziune
2. Pilonii jocului
3. Ce am aflat (diagnosticul jocului actual)
4. Bucla de bază — regulile exacte (versiunea 2)
5. Progresia titanilor (ritmul în cicluri)
6. Sistemul de tăieturi și scorul
7. Lizibilitate, accesibilitate, senzație
8. Primele minute (FTUE)
9. Retenție și revenire
10. Reclamele — fiecare plasament, cu declanșator, valoare, limită și text
11. Linia etică (reguli care nu se negociază)
12. KPI și ținte de echilibru
13. Backlog-ul sprintului (ordinea de construcție)
14. Ce am omorât și de ce
15. Ce amânăm (după primul playtest)
16. Cercetarea: concluzii și surse

---

## 1. Viziune

> **Un titan din cuburi pășește spre zidul tău. Tu alegi ce tun trimiți și când. Fiecare tăietură e o decizie, fiecare titan doborât e o mică explozie, fiecare înfrângere e vina ta — și știi exact cum o repari data viitoare.**

Pixel Flow a demonstrat în 2025–2026 că genul „shooter pe culori cu muniție limitată și locuri de așteptare” poate ajunge în topuri (≈198 milioane $ în primul an, Joc al Anului la Pocket Gamer Awards 2026). Dar jucătorii lui se plâng de același lucru: nivele grele care par măsluite ca să plătești, reclame forțate după fiecare nivel, nicio cale de a primi ajutor prin reclame. Block Blast a arătat că un joc doar cu reclame poate fi cel mai mare câștigător din reclame din lume, dacă e ușor cu ele și obsedat de retenție.

Poziția noastră: **calitatea buclei Pixel Flow + un titan care se prăbușește fizic (diferențiatorul nostru) + corectitudine totală + reclame pe care jucătorul le vrea, pentru că îl ajută cu adevărat la momentul potrivit.**

## 2. Pilonii jocului

Orice funcție nouă trebuie să servească cel puțin un pilon și să nu încalce niciunul.

1. **Corect și lizibil.** Orice înfrângere se vede venind și se poate înțelege. Nicio regulă ascunsă, niciun „regizor” care lucrează împotriva jucătorului, nicio dificultate legată de reclame. Dacă jucătorul pierde, trebuie să spună „am greșit eu”, nu „m-a păcălit”.
2. **Spectacol câștigat.** Tăierea unui braț sau a capului e momentul-semnătură. După reparații, tăietura vine dintr-o decizie (trimiți tunul încheieturii la momentul potrivit), nu din noroc. Fizica titanului care se prăbușește este și recompensa, și materialul pentru reclamele video ale jocului.
3. **Încă o rundă.** Titani scurți (10–40 s), ritm tensiune–eliberare în cicluri de 5, restart în sub o secundă, progres salvat gratuit la începutul fiecărui ciclu și un motiv nou în fiecare zi (Titanul Zilei).
4. **Reclamele sunt un cadou, nu o taxă.** Reclamele cu recompensă sunt mereu opționale, cu valoare reală, oferite exact când jucătorul are nevoie. Reclamele interstițiale apar doar între runde, rar, și se pot opri 30 de minute cu o singură reclamă. Fără banner.

## 3. Ce am aflat (diagnosticul jocului actual)

Analistul a construit un simulator care rulează nucleul real al jocului cu roboți (Random = atinge la întâmplare, Greedy = alege culori vizibile, Smart = gândește, Expert = privește 12 s înainte). 600 de jocuri per robot. Concluziile, pe scurt:

| Problemă | Măsurătoare | Efect |
|---|---|---|
| **Muniția moartă** — când cade un braț, muniția pentru cuburile lui rămâne în tunuri. Tunurile acestea stau pe zid cu „zzz” pentru totdeauna și arată la fel ca cele care doar așteaptă. | 95–96% din înfrângeri au cel puțin un tun mort pe zid (în medie 3,7 din 5 locuri). Primul tun mort apare la 7 s, în 98% din jocuri. | Combo-ul, momentul-semnătură, e chiar cel care omoară jucătorul. Pare măsluit, deși e un bug. |
| **Combo-urile vin singure** | Random: 3,93 bucăți/titan, Smart: 3,74. Doar 36–45% din bucăți vin dintr-o lovitură pe încheietură. | Nu există abilitate de stăpânit. |
| **Abilitatea aproape nu contează** | Random 1,98 titani, Smart 2,34. | Nicio „încă o încercare, acum știu”. |
| **Titanul nu omoară pe nimeni** | 0% din înfrângeri la zid (1.800 de jocuri). Titanii mor cu 80% din drum rămas. | Fantezia titanului care vine spre tine nu are miză. |
| **Început brutal** | 13–18% din runde se termină sub 20 s; 9–13% pierd la titanul 1. | Prima impresie e o înfrângere fără explicație. |
| **Puține decizii reale** | 46–61% din momentele de decizie nu au nicio alegere; zidul e plin 37–49% din timp. | Jucătorul așteaptă în loc să joace. |
| **Pierzi din propria atingere** | 40–73% din înfrângerile „locuri pline” vin din atingerea jucătorului, fără avertisment. | Frustrare, „rage quit”. |
| **Prăpastie la titanul 4** | 206 → 477 cuburi; supraviețuire Smart 52% → 12%. | Un zid de dificultate, apoi titani de 50–60 s. |
| **Nicio retenție** | Singura dată salvată e „titan_best”. | Niciun motiv să revii mâine. |

**Ce e bun:** ritm rapid la început (titanii 1–3 în 11–18 s), activitate vizuală continuă, spectacol fizic frecvent, o singură atingere ca input, restart instant, un nucleu determinist care se simulează în câteva secunde.

**Reparațiile, măsurate în simulator pe nucleul real** (fișierele în `docs/research/harness/`):

| Configurație | Titani (Random / Smart) | Rundă mediană (Random / Smart) | Runde < 20 s | Pierderi la zid (Random) |
|---|---|---|---|---|
| Jocul actual | 1,98 / 2,34 | 39 s / 52–54 s | 18% / 13% | 0% |
| + muniția pleacă odată cu bucata | 5,87 / 6,35 | 157 s / 164 s | 0% / 0% | 0% |
| + tavă reumplută pe loc + ceasul titanului | 5,64 / 11,27 | 151 s / 545 s | — | 42% |
| + ciclurile de 5 titani | 7,61 / 13,21 | 164 s / 283 s | 7% / 2% | 71% |

Cu toate reparațiile: decizii reale în 77–81% din momente (față de 30–46%), 76–82% din bucăți vin din lovituri pe încheietură, Smart face de 1,74 ori mai mult decât Random. Ultima linie arată și ce mai e de reglat: prea multe pierderi la zid (71%, ținta e 35–55%) și 7% runde scurte pentru Random — de aceea titanii 1–3 au ritm de tutorial și ceasul se reglează în sprint (vezi 12.1).

## 4. Bucla de bază — regulile exacte (versiunea 2)

### 4.1 Pe scurt

Titanul pășește spre zid. Jos ai **3 tunuri** (culoare + muniție 10–30). Atingi unul: urcă în primul loc liber din cele **5 locuri** de pe zid și trage singur ~3 lovituri/s în cuburile **vizibile** de culoarea lui. Când rămâne fără muniție, pleacă. Dacă nu vede cuburi de culoarea lui, așteaptă („zzz”). Tăiești umerii sau gâtul → brațul sau capul cade cu fizică, titanul se clatină înapoi. Dobori titanul → vine următorul.

### 4.2 Regulile, una câte una

1. **Muniția acoperă exact cuburile.** La apariția titanului, muniția totală per culoare = numărul de cuburi de acea culoare, împărțită în tunuri de 10–30.
2. **Muniția pleacă odată cu bucata (Recuperare).** Când cade o bucată, muniția care nu mai are ce lovi se scoate: întâi din pachet (invizibil), apoi din tava (numărul de pe tun scade vizibil, cu un „-N” care zboară din bucată spre tun), iar un tun de pe zid a cărui culoare nu mai există zboară de pe zid cu **„RECUPERAT +puncte”**. **Invariant:** după fiecare pas, niciun tun de pe zid nu are o culoare dispărută, iar pentru fiecare culoare, muniția din pachet + tavă ≤ cuburile rămase.
3. **Țintirea de la vârf spre încheietură.** Un tun trage în cubul vizibil de culoarea lui cel mai *depărtat* de corpul ancorat (apoi cel mai înalt, apoi cel mai apropiat de tun). Brațele și capul se „mănâncă” de la vârf spre încheietură și **nu cad singure**. O bucată cade doar când jucătorul trimite culoarea încheieturii (umăr sau gât). Mâna are culoarea brațului.
4. **Tava ca o bandă.** Când trimiți un tun, un tun nou urcă imediat în locul lui. Tava nu primește niciodată 3 tunuri de aceeași culoare dacă pachetul are altă culoare. Tunul nou are o culoare vizibilă acum în 65% din cazuri (valoare blocată prin test — vezi Linia etică). Când pachetul se golește, locul rămâne gol cu eticheta „pachet gol”.
5. **Ceasul titanului.** Timpul până la zid = max(12 s, (4 + 0,075 × cuburi) × max(0,6; 0,98^(n−1))), unde n = numărul titanului. Titanii 1–3 folosesc 0,11 s/cub (ritm de tutorial). *Reglat în simulator (itemul 4, vezi 4.3.2): valorile de mai sus rămân cele finale.*
6. **Reculul la tăiere.** Fiecare bucată căzută împinge titanul înapoi cu clamp(0,03 × cuburi; 0,3; 1,5) unități și îi întârzie pasul următor cu min(1,0; 0,5 + 0,01 × cuburi) s. Tăietura cumpără timp — e a doua miză a deciziei de tăiere.
7. **„La mustață”.** Dacă dobori titanul când mai are sub 15% din drum, bonusul de doborâre × 1,5 și mesajul **„LA MUSTAȚĂ! ×1,5”**.
8. **Paza ultimului loc.** Jocul refuză o atingere care ar pune al 5-lea tun fără țintă pe zid (sigur ar pierde), cu mesajul **„Ar bloca ultimul loc! Alege o culoare vizibilă.”** Tunul din tavă se scutură. Nu se deschide niciodată o reclamă din această atingere.
9. **Blocat.** Dacă niciun tun de pe zid nu are țintă și nu mai ai nicio mutare posibilă (0 locuri libere, sau 1 loc liber și niciun tun din tavă nu are țintă) timp de 1,5 s, runda se termină: **„BLOCAT! Niciun tun nu mai are în ce trage.”**
10. **Pierzi** când titanul atinge zidul, când toate cele 5 locuri au tunuri fără țintă, sau la „Blocat”.

### 4.3 Valorile de configurare (TitanConfig v2)

Din itemul 4, coloana v2 e valoarea implicită din `TitanConfig` (presetul `v2` din simulator și `current` sunt identice). Coloana „Înainte” e bucla veche, păstrată pentru comparație.

| Parametru | Înainte | v2 (în cod) |
|---|---|---|
| TraySize / LineSlots | 3 / 5 | 3 / 5 |
| TrayRefill | Batch (toate 3) | **PerSlot** (Batch rămâne doar pentru teste A/B) |
| NoTripleColor | — | true |
| VisibleColorBias | 0,65 | 0,65 (**blocat prin test**) |
| TargetOrder | cel mai înalt | **FarthestFirst** |
| SalvageEnabled / PointsPerSalvagedAmmo | — | true / 5 |
| ApproachBase / PerCube / Speedup / Min | 25 / 0,25 / 0,95 / 40 | 4 / 0,075 / 0,98 pe n / 12; factor minim 0,6 |
| TutorialTitans / TutorialSecondsPerCube | — | 3 / 0,11 |
| Knockback per cub / min / max | — | 0,03 / 0,3 / 1,5 |
| Stun bază / per cub / max | — | 0,5 / 0,01 / 1,0 s |
| NearMissFraction / NearMissBonusMult | — | 0,15 / 1,5 |
| DeadlockSeconds | — | 1,5 |
| UseCycles / BossBonusMult / BossExtraColor | — | true / 2 / false |
| CleanCutThreshold / CleanCutMult / ChainWindowSeconds / ChainMaxMult | — | 0,8 / 2 / 4 s / 5 |
| CleanLineMax (steaua „Linie curată”) | — | **4** (reglat în simulator, vezi 6.4) |

### 4.3.1 Decizii luate la implementare (itemii 2 și 3: Recuperare, Tăieturi câștigate)

**Recuperarea (4.2.2).**
- Surplusul unei culori se socotește **cu muniția de pe zid inclusă**: surplus = pachet + tavă + zid − cuburi rămase, scos doar din pachet și tavă (cel mult pachet + tavă). Specificația scria „pachet + tavă − cuburi”; cu formula aceea, după o tăietură, fiecare lovitură a unui tun de aceeași culoare de pe zid scotea încă 1 din tavă, cu un „-1” care nu venea din nicio bucată căzută. Cu zidul inclus, toată muniția moartă pleacă o singură dată, odată cu bucata. Invariantul cerut rămâne adevărat (pachet + tavă ≤ cuburi), iar în plus jocul nu rămâne niciodată fără muniție (pachet + tavă + zid ≥ cuburi). Ambele sunt testate după fiecare pas (200 de semințe × 120 s).
- Recuperarea rulează după o bucată căzută și înaintea fiecărei verificări „locuri pline”, deci după fiecare pas. Un tun de pe zid ajuns la 0 pleacă normal (fără „RECUPERAT +0”). Când ultima bucată golește titanul, nu se face recuperare: muniția de pe zid primește bonusul de doborâre (2/lovitură).
- Puncte: 5 pe lovitură recuperată de pe zid sau din tavă; recuperarea din pachet e invizibilă și nu dă puncte. Din pachet se scade întâi din tunurile mici, ca să dispară tunuri întregi.
- `TitanGame.SalvageActive` (= `TitanConfig.SalvageEnabled`, implicit pornit) e poarta pentru reclamele de salvare (11.7).

**Tăieturi câștigate (4.2.3, 6.1).**
- Etichetele de părți sunt API-ul unic: `PartOf` (0 corp, 1 brațul stâng, 2 brațul drept, 3 capul; 4+ rezervat), `IsJoint`, `OriginalPartSize`, `RemainingPartSize`, `FallenPiece.Part`. **Încheieturile (umerii și gâtul) sunt etichetate „corp”**: rămân pe trunchi când membrul cade, deci o tăietură curată dă exact `OriginalPartSize` cuburi (de folosit la „tăietura curată ≥ 80%”, itemul 7). Etichetele țin de poziție: răspund și pentru un cub deja tras sau căzut.
- Culorile: brațul nu are culoarea umerilor, a gâtului sau a capului (ordinea alegerii: încheietura umerilor, brațul, gâtul ≠ trunchi și ≠ braț, capul ≠ gât și ≠ braț). Altfel tunul brațului ar fi tăiat singur gâtul sau ar fi desprins ochii. Mâna are culoarea brațului; rândul de jos al brațului e marcat „mănușă” doar pentru vizual (un cub interior de aceeași culoare într-o ramă închisă, aceeași luminozitate).
- `supportDist` (distanța de la corpul ancorat) nu se recalculează de la zero la fiecare lovitură: `Remove` o actualizează pe loc, recalculând doar cuburile al căror drum cel mai scurt trecea prin cubul scos. Rezultatul e identic cu un BFS complet (test pe 8 niveluri × 120 de scoateri). `PickTarget` nu rulează niciodată BFS-ul dacă nu s-a schimbat nimic. Măsurat: 1000 de apeluri `PickTarget` pe un titan L8 (996 de cuburi) ≈ 13 ms; prima predicție `ShotsToSever` pe L8 ≈ 2 ms (de la 28 ms cu BFS complet), apoi din cache.
- **TAIE!** = `ShotsToSever(tun)`: indexul (de la 0) loviturii la care cade o bucată, dacă tunul ar fi trimis acum singur, din locul în care ar ajunge (primul loc liber), pe o copie a titanului. Orice bucată contează. Cache pe (culoare, loc, epoca titanului). „taie în N” = `ShotsToCut(culoare, loc, muniția tuturor tunurilor de acea culoare din tavă și de pe zid)`: N lovituri în total, de la 1.
- Ce rămâne în afara încheieturilor (simulator, Random, 200 de jocuri): 66% din bucăți vin din lovituri pe încheieturi; ~12% sunt ochii care cad când tunul capului mănâncă tot capul din jurul lor (ochiul ascunde cuburile din spatele lui, deci acelea nu pot fi ținte); ~11% sunt membre rupte pentru că s-a mâncat trunchiul de sub umăr sau gât; ~11% sunt resturi de încheieturi rămase după tăietură. Ținta „≥ 60%” e atinsă.

**Măsurat (Tools/Sim, 300 de jocuri/robot, sămânța 1, restul setărilor ca în cod la acel moment):**

| Configurație | Titani Random / Greedy / Smart | Rundă mediană (s) | Bucăți/titan | Din lovituri pe încheieturi | Runde < 20 s | Tunuri moarte pe zid | Tava doar cu tunuri moarte |
|---|---|---|---|---|---|---|---|
| Înainte (bucla veche) | 1,89 / 2,07 / 2,42 | 37 / 41 / 56 | 3,8–4,0 | 35–44% | 11–19% | 1,3–1,5 / joc | 3,5–25,5% |
| + Recuperare (HighestFirst) | 6,38 / 6,92 / 6,68 | 165 / 196 / 179 | 4,0–4,3 | 23–32% | 0% | 0,00 | 0,0% |
| + Tăieturi câștigate (FarthestFirst, implicit) | 5,91 / 6,29 / 6,20 | 175 / 193 / 187 | 3,09–3,10 | 64–71% | 0% | 0,00 | 0,0% |

Mâna = culoarea brațului e deja inclusă în rândul „+ Recuperare” (constructorul se schimbă o singură dată). Raportul Smart : Random a rămas 1,05×; pierderile sunt încă 100% „locuri pline”. Reglajul (ceasul, tava-bandă, ciclurile) e treaba itemului 4.

### 4.3.2 Decizii luate la implementare (itemii 4 și 5: Bucla v2, Amenințare lizibilă)

**Tava-bandă (4.2.4).**
- `TrayRefill.PerSlot` (implicit): după o atingere, `Tray[i] = Draw(i)` și evenimentul `TrayRefilled` cu `TraySlot = i` (`-1` = toată tava: titan nou sau modul `Batch`, păstrat doar pentru teste și A/B). Un loc golit de Recuperare se reumple la fel, imediat.
- La un titan nou, tava veche se golește *înainte* de tragere, ca regula „fără 3 de aceeași culoare” să nu se uite la tunurile titanului trecut.
- „Fără triplete”: dacă celelalte două tunuri din tavă au culoarea X și tragerea normală (cu bias-ul de 65% neschimbat) dă tot X, se alege uniform un tun din pachet de altă culoare, dacă există. Testul compară 500 de trageri cu regula și fără ea (fără regulă apar triplete, deci testul poate pica).
- Pachet gol → locul rămâne `null`; în vizual, un cerc închis din 16 blocuri și eticheta „pachet gol”.

**Ceasul titanului (4.2.5–4.2.7).**
- `ApproachSeconds(n, cuburi)` folosește **numărul** titanului n (pentru 0,98^(n−1) și pentru ritmul de tutorial) și cuburile titanului construit la nivelul din ciclu.
- Reculul se aplică pentru fiecare bucată căzută, inclusiv întârzierea pasului (se adună dacă dintr-o lovitură cad două bucăți). **Excepție:** lovitura care golește titanul nu mai împinge (titanul e doborât oricum, iar „la mustață” se judecă după distanța de dinaintea ultimei lovituri). Evenimentul nou `Staggered` (Distance, Piece) vine după `PieceFell`.
- „La mustață”: `Distance ≤ 0,15 × StartDistance` în momentul doborârii; bonusul = rotunjire(200 × n × (boss ? 2 : 1) × 1,5); muniția rămasă pe zid (2/lovitură) se adună după, fără multiplicator.
- `TitanGame.NextStepIn` (secunde până la pasul următor) și `TitanGame.Danger` sunt publice (teste, HUD).

**Ciclurile (5.1).** `TitanConfig.BuildLevel(n)` și `RoleOf(n)` (statice și ușor de testat: tabelul n = 1–30 e în teste). `TitanGame.Level` rămâne n; `TitanGame.BuildLevel` și `Role` sunt noi; `GameEvent.Role` vine pe `TitanSpawned` și `TitanDefeated`. `BossExtraColor` (oprit) dă bossului o culoare în plus prin `MonsterBuilder.Build(nivel, rng, extraColors)`; nevalidat în simulator. Constructorul `TitanGame` primește opțional o sursă de titani (`Func<int, Monster>`), folosită de teste și pregătită pentru titanul scenarizat din itemul 6.

**Paza ultimului loc și „Blocat” (4.2.8–4.2.9).**
- `DeployBlock(i)` întoarce `NotAvailable` (joc terminat, loc invalid sau gol), `LineFull`, `WouldJam` (exact 1 loc liber, niciun tun de pe zid cu culoare vizibilă, tunul atins fără culoare vizibilă) sau `None`; `CanDeploy` = `None`.
- „Blocat” se verifică la sfârșitul lui `Tick`, după Recuperare și după verificarea „5 locuri pline” (care rămâne imediată). Pragul are o toleranță de 0,0001 s: suma în float a 30 de pași de 0,05 s nu trebuie să adauge un cadru (testul cere exact 1,5 s). `StuckSeconds` e public.
- `VisibleCountByColor()` întoarce o copie, calculată o dată pe epoca titanului (cache în Core, pe lângă cache-ul pe cadru din HUD); `HiddenOnlyColorCount()` o folosește.
- Roboții aleg doar tunuri pe care jocul le acceptă (`BotView.Tappable`): un jucător care primește „Ar bloca ultimul loc!” alege alt tun. Smart dă +60 unui tun cu eticheta TAIE! (informație pe care o vede și jucătorul).

**HUD (abateri mici de la specificație).**
- Pătratele locurilor stau **pe fața zidului**, sub fiecare loc, nu deasupra liniei zidului: deasupra ar acoperi tunurile de pe zid (criteriul cere să nu se suprapună). De verificat în editor la 360×640.
- Cipul scrie la singular „1 culoare ascunsă ?” și la plural „{k} culori ascunse ?”.
- „Efecte reduse” există deja ca `Settings.ReducedFx` (PlayerPrefs `set_reducedFx`), fără meniu: itemul 8 adaugă meniul. Cu el pornit: fără tremurat de cameră, marginea roșie și chenarul roșu al pătratelor sunt statice.
- Simbolurile culorilor (cerc, pătrat, triunghi, romb, cruce, stea), punctele ciclului și coroana sunt texturi procedurale (`Game/HudArt.cs`), refolosibile de itemul 8.
- Bannerele din evenimente (doborât, la mustață, rolul titanului nou) stau la coadă în spatele celui de pe ecran; mesajele unei atingeri refuzate apar imediat.

**Reglajul (Tools/Sim, 300 de jocuri/robot, sămânța 1, Random și Smart).** Căutarea cerută (ApproachSecondsPerCube 0,075–0,095 × ApproachSpeedup 0,98/0,985; TutorialSecondsPerCube 0,10–0,14) cu reculul neschimbat:

| s/cub | Speedup | Titani R / S | Raport | Rundă mediană R / S | Zid R / S | Zid plin R / S |
|---|---|---|---|---|---|---|
| **0,075** | **0,98** | **9,5 / 16,8** | **1,77×** | **194 / 345 s** | **96% / 99%** | **26% / 31%** |
| 0,075 | 0,985 | 11,0 / 20,4 | 1,85× | 228 / 484 s | 93% / 99% | 28% / 32% |
| 0,085 | 0,98 | 13,0 / 22,7 | 1,75× | 297 / 542 s | 89% / 99% | 29% / 33% |
| 0,085 | 0,985 | 14,8 / 27,5 | 1,86× | 357 / 710 s | 87% / 99% | 31% / 34% |
| 0,095 | 0,98 | 15,8 / 29,8 | 1,89× | 395 / 694 s | 85% / 99% | 31% / 34% |
| 0,095 | 0,985 | 18,4 / 36,6 | 1,99× | 448 / 973 s | 82% / 98% | 32% / 35% |

(Rândurile non-alese sunt măsurate înainte ca Smart să folosească TAIE!, care îi adaugă ~0,4 titani; tendințele nu se schimbă.) TutorialSecondsPerCube între 0,10 și 0,14 nu schimbă nimic măsurabil (titanii 1–3 nu se pierd niciodată), deci rămâne 0,11. **Ales: 0,075 / 0,98 / 0,11**, singura combinație cu ambele runde mediane în țintă; restul țintelor sunt în 12.1. Sămânțele 2 și 3 dau același tablou (raport 1,78× / 1,75×, Random 203 / 204 s, Smart 377 / 350 s).

**Ținta „cota pierderilor la zid” (Random 35–55%, Smart 40–70%) nu se poate atinge cu regulile din itemii 2 și 5.** Ea a fost fixată pe out_C1, unde 29% din rundele Random se terminau cu „locuri pline”, 65% din ele prin atingerea proprie a celui de-al 5-lea tun fără țintă, plus tunuri moarte rămase pe zid. Recuperarea (itemul 2) a scos tunurile moarte, iar paza ultimului loc (itemul 5) face imposibilă pierderea prin atingerea proprie. Măsurat cu aceeași configurație și paza scoasă temporar: Random 72% zid / 28% locuri pline (87% din ele prin atingerea proprie), raport 1,89×; cu paza: 96% / 4%, raport 1,77×. „Blocat” nu apare niciodată la roboți (0% din pierderi, timp blocat maxim 0,00 s): starea cere 4 tunuri care așteaptă, nimic care trage și o tavă fără nicio culoare vizibilă, iar bias-ul de 65% o face foarte rară. Nicio valoare din intervalele permise nu coboară zidul sub ~82%. **Propunere pentru proprietar:** ținta devine „pierderile vin din zid (≥ 80%), iar «locuri pline» + «blocat» rămân posibile (≥ 1% la Random)”; până la decizie, CI verifică doar limitele de jos (vezi 12.1). Tot aici: „timp cu zidul plin” e 26% (Random) / 35% (Greedy) / 31% (Smart) față de ținta ≤ 25%; e o măsură a stilului roboților (Smart cu „păstrează locuri libere” a căzut la 0,91× Random), nu a regulilor, așa că n-am forțat-o.

### 4.4 Ce simte jucătorul, într-un titan

0–2 s: titanul apare (banner cu rolul: normal, **TITAN RAPID!**, **TITAN GIGANT!**). 2–10 s: trimiți tunuri pe culorile vizibile, cuburile zboară, sunetul crește în ton cu seria de lovituri. Vezi încheietura galbenă cu „cusătura” albă și un tun galben în tavă cu eticheta **TAIE!**. Decizia: îl trimiți acum (braț întreg, bucată uriașă, recul mare) sau curăți întâi altceva? Tai: o fracțiune de secundă de oprire, camera „lovește”, brațul cade, titanul se clatină înapoi, tunurile albastre devenite inutile zboară cu „RECUPERAT”. Bara de pericol e roșie, se aude bătaia inimii, mai ai două cuburi: **LA MUSTAȚĂ!** Titanul se prăbușește. Următorul.

## 5. Progresia titanilor (ritmul în cicluri)

### 5.1 Ciclul de 5 titani

Titanii nu mai cresc la nesfârșit (asta producea prăpastia de la titanul 4 și titanii de 60 s). Numărul titanului n se transformă într-un „nivel de construcție” L printr-un ciclu de 5:

| Poziție în ciclu | Rol | Nivel de construcție (c = ciclul, de la 0) |
|---|---|---|
| 1 | Încălzire | min(1 + c, 5) |
| 2 | Construcție | min(2 + c, 6) |
| 3 | Împingere | min(3 + c, 6) |
| 4 | **SPRINT** (eliberare înainte de vârf) | c ≥ 2 ? 2 : 1 |
| 5 | **BOSS** (vârful ciclului, bonus ×2) | min(4 + c, 8) |

Exemple: n=5 → L4 (boss), n=9 → L1 (sprint), n=14 → L2, n=25 → L8.

- Ce vede jucătorul: numărul real al titanului (#n), „Ciclul c+1”, 5 puncte de ciclu (al 5-lea cu o coroană).
- Durata unui titan: 10–40 s (sprint 10–16 s, boss 27–40 s). Un ciclu durează ~1,5–3 minute.
- Dificultatea pe termen lung vine din ceas (factorul 0,98 pe titan, minim 0,6) și din culori mai multe; nivelul de construcție se oprește la 8.
- **Între titani nu se întâmplă nimic legat de reclame. Niciodată.** Titanii se înlănțuie continuu.

### 5.2 Ce vine după (nu în acest sprint)

Planuri de corp noi (Patru Brațe, Coarne, Brută, Gât Lung), cuburi blindate (2 lovituri) și tunuri duble — câte o noutate pe ciclu, debut pe boss, fiecare cu un indiciu în context. Se fac doar după ce simulatorul arată că nu cresc pierderile „locuri pline” cu mai mult de 5 puncte, și se reglează **doar cu ajutoarele din reclame oprite**.

## 6. Sistemul de tăieturi și scorul

### 6.1 Tăieturile

- **Încheieturile** (umerii, gâtul) au o dungă albă („cusătură”). Ca să tai, trimiți tunul de culoarea încheieturii.
- **Eticheta TAIE!** apare deasupra unui tun din tavă doar dacă muniția **acestui tun** ajunge să taie încheietura, simulând ordinea de țintire pe o copie. Altfel: „taie în N” (contur gol) sau nimic. Eticheta nu minte niciodată (test automat).
- **Tăietură curată:** o bucată care mai are ≥ 80% din cuburile inițiale. Se vede dinainte: cusătura încheieturii are un contur auriu cât timp membrul e încă ≥ 80% întreg.
- **Lanț:** bucăți tăiate la cel mult 4 s una după alta. Fereastra se vede ca un arc care se golește lângă scor.

### 6.2 Scorul (un singur model de multiplicatori)

| Sursă | Puncte |
|---|---|
| Lovitură | 10 |
| Cub dintr-o bucată căzută | 25 × (curată ? 2 : 1) × min(lanț, 5) |
| Muniție recuperată | 5 pe lovitură |
| Doborâre | 200 × n × (boss ? 2 : 1) × (la mustață ? 1,5 : 1) |
| Muniție rămasă pe zid la doborâre | 2 pe lovitură |

Texte (prioritate pe ecran: un singur popup mare odată): „DECAPITARE!” (cap tăiat curat), „TĂIETURĂ CURATĂ!”, „BUCATĂ RUPTĂ!”, apoi „COMBO ×N”, apoi „+puncte”. Popup-ul crește cu lanțul (max ×1,6), auriu la multiplicator ≥ 4.

### 6.3 Nota titanului (3 stele)

La doborâre, trei realizări cu motiv scris pentru cele ratate: **Doborât**; **Decapitare** (capul tăiat curat); **Linie curată** (niciodată mai mult de K tunuri pe zid; K se reglează în simulator astfel încât Smart să o ia la 25–40% din titani fără să crească pierderile la zid). Stelele nu adaugă alt multiplicator: alimentează statisticile, recordurile și, mai târziu, rangul. Indicatorul live (3 puncte sub bara de pericol) arată imediat când o stea se pierde.

Cheia recordului devine `titan_best_v2` (scorurile vechi nu se mai compară).

### 6.4 Decizii luate la implementare (itemul 7: scorul tăieturilor și nota)

- **Un singur loc pentru multiplicatori:** `TitanGame.ScorePiece`, apelat pentru fiecare bucată din `Shoot`. Puncte = cuburi × 25 × (curată ? 2 : 1) × min(lanț, 5). Doborârea (boss ×2, „la mustață” ×1,5) rămâne separată și neschimbată; stelele nu adaugă nimic.
- **Curată** = bucata are ≥ 80% din cuburile inițiale ale părții ei (`FallenPiece.Part`, capul câștigă la egalitate), cu o toleranță de 0,001 cuburi ca 0,8f × 10 să numere 8 cuburi drept curat. O bucată a corpului (de exemplu un ciot de umăr) nu e niciodată curată. Când trunchiul de sub gât e mâncat și cad gâtul și capul împreună, bucata are majoritate „cap” și e curată: e tot o decapitare.
- **Lanțul** folosește timpul simulat (`GameTime`, suma `dt`); fereastra e ≤ 4 s inclusiv. Bucățile căzute din aceeași lovitură cresc lanțul una după alta (a doua din aceeași lovitură are deja ×2). Lanțul și fereastra se resetează la fiecare titan nou.
- **`IsCleanEligible(part)`** = partea are ≥ 80% din cuburi și cel puțin un cub (deci e încă prinsă: cuburile libere cad imediat). Pentru corp e mereu fals. Vizual: cusătura încheieturii devine aurie; încheietura își află membrul după primul vecin care nu e corp.
- **Statistici de rundă:** `MaxPiecePoints`, `MaxChain`, `PiecesCut`, `CleanCuts`, `HeadsCut` (= decapitări, adică bucăți de cap **curate**), `TitansDefeated`, `StarsEarned`.
- **Stele:** `GameEvent.Stars` e o mască de biți (1 doborât, 2 decapitare, 4 linie curată), plus `GameEvent.MaxLine` pentru motivul scris. „Cel mai plin zid” se măsoară la fiecare `Deploy` și la începutul fiecărui `Tick`, și pornește de la 0 la fiecare titan.
- **Popup-ul mare:** unul singur pe ecran, 1,1 s. Unul nou așteaptă doar dacă celui de pe ecran îi mai rămân ≤ 0,3 s; altfel se contopește cu el (eticheta cea mai bună, lanțul cel mai mare, punctele adunate). „RECUPERAT +N” rămâne un popup mic.
- **CleanLineMax = 4, nu 3.** Ținta „Smart ia steaua la 25–40% din titani” **nu se atinge** fără să strice ținta itemului 4 (Smart : Random 1,7–2,3×). Măsurat cu 300 de jocuri/robot, sămânța 1 (R / G / S):

| Variantă | Steaua 3 (Smart) | Smart : Random titani | Steaua 3 (Random / Greedy) |
|---|---|---|---|
| K = 3, Smart neschimbat | 0% | 1,77× | 1% / 0% |
| **K = 4, Smart neschimbat (în cod)** | **9%** | **1,77×** | 13% / 3% |
| K = 4, Smart ține al 5-lea tun cât pericolul < 15% | 18% | 1,74× | — |
| K = 4, … pericol < 20% | 26% | 1,69× | — |
| K = 4, … pericol < 25% | 34% | 1,63–1,70× (semințele 1–3) | — |
| K = 3, … pericol < 40% | 40% | 1,17× | — |

  Concluzie: steaua costă titani, pentru că a ține tunuri în tavă scade puterea de foc. Cu K = 3 steaua e practic imposibilă în joc normal (al treilea punct s-ar face gri aproape la fiecare titan), deci am ales K = 4 și am lăsat Smart neschimbat, ca țintele itemului 4 să rămână valabile. Cota zidului din pierderi nu se schimbă (stelele nu schimbă jocul): 96% / 86% / 99%. **De decis de proprietar:** fie ținta devine „steaua 3 e posibilă (≥ 5% pentru Smart), iar un jucător care o urmărește plătește cu ~0,7 titani pe rundă”, fie steaua se redefinește (de exemplu „niciodată 5 tunuri pe zid”).
- Tăieturi curate per titan: Smart 2,02, Random 1,26 → **1,60×** (ținta ≥ 1,5× ✔); Greedy 1,15. Decapitări: Smart 65%, Random 50%, Greedy 45% din titanii doborâți.

## 7. Lizibilitate, accesibilitate, senzație

- **Daltonism (obligatoriu pentru topuri).** Fiecare culoare are un model și un simbol propriu: 0 roșu = plin/cerc, 1 albastru = dungi orizontale/pătrat, 2 galben = puncte/triunghi, 3 verde = diagonale/romb, 4 violet = tablă de șah/cruce, 5 portocaliu = grilă/stea. Modelele apar discret pe cuburi (texturi procedurale de 32×32, contrast ±15%), simbolurile apar pe tunuri, pe indicatoarele de locuri și în cartea culorilor. Setarea „Simboluri culori” (implicit pornită) mărește contrastul. Culoarea cuburilor titanului nu e schimbată niciodată de cosmetice sau efecte.
- **Indicatorul de locuri:** 5 pătrate deasupra zidului; gol = contur, trage = umplut cu culoarea, așteaptă = gri cu „zzz”. Chenar alb (0–2 în așteptare), chihlimbar (3), roșu pulsând (4+). Formă + culoare, nu doar culoare.
- **„? culori ascunse”:** un singur cip care arată câte culori există doar în interiorul titanului (cartea completă a culorilor vine doar dacă testele arată confuzie).
- **Senzație:** oprire scurtă (≤ 0,1 s) la tăieturi ≥ 12 cuburi, „lovitura” camerei (FOV −3%), sunet care crește în ton cu seria de lovituri (până la ×1,6), vibrație la boss doborât, „la mustață” și bucăți de 40+ cuburi (cel mult o dată la 0,6 s). Fără încetinire mai lungă de 0,15 s și niciodată în ultimele 15% din drumul titanului.
- **„Efecte reduse”** (setare): fără tremurat, fără oprire, fără „lovitura” camerei, margine roșie statică în loc de pulsație.
- **Pericol:** la ≥ 75% din drum, bătăi de inimă procedurale și o margine roșie care pulsează sub 3 Hz.
- **Pauză:** buton de pauză; jocul intră automat în pauză când aplicația iese în fundal (fără numărătoare la revenire — apeși „Continuă”). Meniul de pauză: Continuă, Sunet, Vibrații, Efecte reduse, Simboluri culori, Limba (RO/EN), „Fără reclame 30 min”, Joc nou.
- **Limbă:** toate textele trec printr-un tabel RO + EN; limba implicită după limba telefonului. Fontul trebuie verificat pe dispozitiv pentru ș, ț, ă, î, â.
- **Performanță:** țintă 60 fps; pragul: frame p95 ≤ 33 ms la un boss L8 pe un Android de gamă medie din 2022. Proiectile în pool; dacă pragul nu e atins, corpul ancorat se combină în mesh-uri per culoare.

### 7.1 Decizii luate la implementare (itemii 8 și 9: Accesibilitate și setări, Senzație și performanță)

**Culori (UX-1).** Tabelul (model, simbol, RGB) e în `Core/ColorStyleTable.cs`, singura sursă a paletei; `Game/ColorStyle.cs` construiește din el texturile și materialele. Texturi procedurale de 32×32, filtrare „point”, câte un material comun pe culoare (cele 3 nuanțe vechi pe culoare au dispărut: 6 materiale de cub în loc de 18). Contrastul: pixelii de bază au 1, cei de „cerneală” (1−c)/(1+c), iar materialul e nuanțat cu un factor ales astfel încât **media feței să fie exact culoarea paletei** (un cub „plin” e chiar culoarea paletei); c = ±30% cu „Simboluri culori” pornit (implicit), ±15% oprit. Modele cu perioada 8 px: dungi de 4 px, puncte de rază 2,5 px, diagonale de 4 px, tablă de șah de 8 px, grilă cu linii de 2 px (acoperire 20–50%). Simbolurile sunt funcții de formă în Core (testate: cele 6 forme au IoU < 0,8 între ele); la test, cercul (r 0,40) și pătratul (0,33) se suprapuneau 81%, așa că acum **cercul e mai mic (r 0,31) și pătratul mai mare (0,37)** (IoU 0,53). Unde apar simbolurile: pe eticheta de muniție a fiecărui tun (tavă și zid; și la tunurile care așteaptă, pe gri), pe pătrățelele locurilor, pe cipul culorilor ascunse și, ca stare, pe pătratele Titanului Zilei (bifă = doborât, X = titanul pe care s-a terminat; verdele și roșul nu mai poartă singure informația). „Simboluri culori” oprit ascunde simbolurile de pe etichete și cip; pătrățelele locurilor le păstrează (ele arată starea „trage”). Verificarea în gri: **F7** în editor arată paleta ca gri de aceeași luminanță pe cuburi, tunuri, proiectile și etichete (nu e un efect de post-procesare: culoarea vine numai din paletă, deci asta e echivalent pentru ce contează); o vedere de depanare, nu un efect de joc.

**Verificarea pentru daltonism** (test, matricile Machado 2009 la severitate 1, ΔE76 în L\*a\*b\* D65): perechile care se apropie sub ΔE 20 și se bazează pe model + simbol sunt deuteranopie roșu/verde 12,5, albastru/violet 11,9, galben/portocaliu 17,6; protanopie albastru/violet 7,9, verde/portocaliu 13,1. Toate au modele (și simboluri) diferite, deci regula „ΔE ≥ 20 sau model diferit” e mereu adevărată; testul o documentează. Cu vedere normală toate perechile sunt ≥ 20.

**Pauza.** Butonul de pauză (48×48 s) e sus în dreapta, în zona sigură; ca să nu-l atingă, cercul lanțului s-a mutat de la +150 s la +128 s față de centrul scorului. Pauza automată vine din `OnApplicationPause(true)` și `OnApplicationFocus(false)` doar cât runda chiar merge (nu pe rezultate, nu peste un card de reclamă, care are deja contractul lui de pauză). În pauză: fără `Tick`, fără atingeri pe tavă, `Time.timeScale = 0`, `AudioListener.pause = true`, ora rundei nu curge; butonul „+1 LOC” nu apare. **Joc nou** din pauză: runda se încheie fără „game over” (`RunSession.Abandon`: `run_length` și `run_end` cu motivul 99, `totalRuns + 1`, scorul contează la record, **punctul de salvare rămâne**), apoi pornește imediat o rundă de la titanul 1, **fără interstițial** (interstițialul rămâne legat doar de butoanele ecranului de rezultate). În Titanul Zilei meniul nu are „Joc nou” și nici rândul de reclame. Rândul „Fără reclame 30 min” e desenat din `GameController.Ads.cs` (`PauseMenuAdBreakRow`), cu aceeași regulă ca pe ecranul de rezultate (`CanOffer(AdBreak)`), cu eticheta „Reclamă”; cardul se deschide peste meniu și revine la el.

**Limba.** Tabelul e `Core/Loc.cs` (`Loc.T(cheie, argumente)`, `Dictionary<string, string[]> {ro, en}`), nu `Game/Loc.cs` cum spunea backlogul: textele din Core (`HudText`, `FtueText`, `DailyText`, `AdText`) trec și ele prin tabel și sunt testate în afara Unity. `Game/Loc.cs` alege limba (`""` = a telefonului: română dacă `Application.systemLanguage` e română, altfel engleză) și face verificarea fontului. Numerele: punct pentru mii în română (41.200), virgulă în engleză (41,200). Numele jocului rămâne „Titanul din Cuburi” în ambele limbi. Testele verifică: fiecare cheie are ambele limbi cu aceleași `{n}`; fiecare cheie folosită în cod există și fiecare cheie din tabel e folosită; nicio frază de frică („you will lose”, „don't miss”, „vei pierde” …) în nicio limbă; doar ASCII și ăâîșțĂÂÎȘȚ·—… (ș și ț cu virgulă, nu cu sedilă); **niciun șir afișat jucătorului în `Assets/Titan/Scripts/Game` în afara `Loc`** (un lexer C# mic găsește toate șirurile; sunt permise doar numele de obiecte/texturi/sunete, mesajele `Debug`, formatele numerice, cheile de preferințe și detaliile de telemetrie). Mesajele de lansare („Un scut ți-a păstrat seria.”) și cauza H4 se compun o dată, în limba de atunci.

**Fontul.** La prima desenare în editor, `Language.CheckFontOnce` cere fontului IMGUI implicit „ăâîșțĂÂÎȘȚ” și scrie în consolă ce lipsește. Un font inclus în proiect ar fi o decizie separată (asset).

**Așezarea la 1080×2340 și 720×1600** (calculată cu proiecția camerei, fără editor): etichetele de muniție ale tunurilor din tavă stăteau peste pătrățelele locurilor la orice titan (vârful tunurilor din tavă ajunge pe ecran la nivelul feței zidului), așa că acum **stau sub tun**; „TAIE!” a rămas unde era (deasupra tunului, deasupra pătrățelelor). Etichetele de pe zid sunt cel mult cât distanța dintre locuri pe ecran (la L8 sunt 54 s, nu 64 s), iar „zzz” stă deasupra etichetei. Cipul culorilor ascunse rămâne pe un rând între stelele live și „ZIDUL”.

**Senzația (FEEL-1).** Regulile sunt în `Core/Feel.cs`, testate: `TimeWarp` (oprire și încetinire pe ceasul nescalat; apelurile nu se adună, câștigă sfârșitul cel mai târziu; **plafon: cel mult 10% oprire în orice fereastră de 1 s**; nimic sub „Efecte reduse”; nimic de la pericol ≥ 0,85), `Juice.ForPiece` (tabelul de declanșare), `HitStreak`, `HapticGate`, `FrameStats`. Interpretări: „cap sau tăietură curată ≥ 40 de cuburi” = bucată de ≥ 40 de cuburi care e cap sau curată; atunci oprirea e max(0,08; 0,06) = 0,08 s, urmată de încetinire ×0,35 de cel mult 0,15 s. **„Nicio încetinire de niciun fel” la pericol ≥ 0,85 include și oprirea** (nu doar încetinirea); camera tot „lovește”. Oprirea de doborâre cerută e 0,12 s, dar plafonul de 10%/s o taie la **0,10 s**. Declanșarea e în clipa în care bucata se desprinde pe ecran (după zborul proiectilului), nu la evenimentul din nucleu. Dacă titanul trece de 0,85 în timpul unei încetiniri, ea se oprește. Oprirea și încetinirea se anulează la pauză, la „game over” și la o rundă nouă (scara timpului revine la 1). În editor, dacă cadrele înghețate depășesc 10% din ultima secundă (+ un cadru), consola dă un avertisment. Vibrația: `Handheld.Vibrate()` doar pe Android/iOS.

**Performanța.** Proiectilele erau deja în pool; acum zborul lor, reculul tunului și „pocnetul” cubului lovit sunt liste pașite în `Update` (înainte: 3 corutine pe lovitură), scânteile sunt în pool (înainte: 6 `GameObject` noi pe lovitură), iar mesajele (cele mici și cel mare de bucată) se refolosesc. Pe partea de vizualizare o lovitură nu mai creează niciun obiect; rămâne alocarea unui `GameEvent` în nucleu. O bucată cu **> 60 de cuburi** devine la cădere câte o plasă combinată pe material (`Mesh.CombineMeshes`) cu un singur `BoxCollider`; plasele se distrug odată cu bucata (`MeshOwner`). Suprapunerea de dezvoltare (**F8** / 3 degete ținute 1 s): fps, p95 pe ultimele 300 de cadre, obiecte active și corpuri rigide (numărate din rădăcinile scenei, fără `Find*`). `perf` (p95 ms, nivelul titanului) se scrie în jurnal la sfârșitul fiecărei runde (exista din RET-1) și la „Joc nou” din pauză.

| Măsurătoare (boss L8, 3 bucăți căzând) | Țintă | Măsurat |
|---|---|---|
| p95 cadru, Android de gamă medie 2022 | ≤ 33 ms | **nemăsurat** (nu există dispozitiv aici) |
| p95 cadru, editor, calitate „Low” | ≤ 33 ms | **nemăsurat** (nu există editor aici) |
| GC pe lovitură (Profiler) | ~0 | nemăsurat; în cod: niciun `Instantiate`/`new GameObject` și nicio corutină pe lovitură |

Procedura: Play pe un profil cu punct de salvare la titanul 21+ (sau `--static` ca să găsești un L8), **F8**, citește p95 după ce cad 3 bucăți, apoi F9 pentru `perf`. Combinarea corpului ancorat în plase pe culoare (pasul 8 din backlog) **nu e făcută**: e condiționată de o măsurătoare care să arate că pragul e ratat.

**Ce n-am putut verifica în Unity:** totul de mai sus se compilează în `Tools/UnityCheck` și `Tools/EditorCheck` fără erori și fără avertismente, dar nu a fost văzut: modelele pe cuburi (și cum arată la distanță cu filtrarea „point”), simbolurile pe etichete, F7, meniul de pauză la 1080×2340 / 720×1600 / 360×640, pauza automată pe telefon, oprirea, încetinirea și „lovitura” camerei, vibrația, plasele combinate (și coliziunea lor cu o singură cutie), suprapunerea F8 și verificarea fontului.

## 8. Primele minute (FTUE)

Obiectiv: momentul „aha” (tăierea = spectacol) în primele 10 secunde, nu un blocaj neexplicat la 7 s.

1. **Fără meniu înainte de joc.** Aplicația pornește direct în joc la primele rulări.
2. **Titanul 1 scenarizat** (primele 3 runde): paletă fixă (picioare galbene, trunchi roșu, umeri și gât galbeni, brațe și cap albastre). Tava de start: un tun galben a cărui muniție ajunge exact să taie ambii umeri și gâtul (calculată la construcție), plus două tunuri din pachetul normal.
3. **Titanul nu pășește până la prima atingere.**
4. **Indicii** (maximum 4, câte una odată, fiecare o singură dată, niciuna nu blochează atingerile și niciuna nu acoperă tava):
   - H1: un inel pulsând în jurul tunului galben și textul **„Atinge tunul galben”** (deasupra tăvii).
   - H2: **„zzz = culoarea e ascunsă în spate. Va trage când apare.”** — prima dată când un tun așteaptă ≥ 1 s (titanul 2).
   - H3: o săgeată spre indicatorul de locuri: **„Nu umple toate cele 5 locuri!”** — prima dată când 3 locuri așteaptă.
   - H4: la prima înfrângere, cauza într-un rând („Toate cele 5 locuri erau pline cu tunuri care așteptau.” / „Titanul a ajuns la zid.”) și **„Sfat: lasă mereu 1–2 locuri libere.”**
5. **Titanii 1–3 au ritm de tutorial** (0,11 s/cub). Titanul 4 e primul SPRINT, titanul 5 primul BOSS.
6. **Fără nicio reclamă și nicio ofertă de reclamă** până la 3 runde ȘI 10 minute de joc în total.
7. **Pâlnia:** fiecare pas se înregistrează (tut_step_N_shown/done, titan_n_reached, run_length, lose_reason).

### 8.1 Decizii luate la implementare (itemul 6: FTUE)

- **Unde stă logica:** în Core și testat. `RunPlan` (cum diferă o rundă de jocul infinit: un titan scenarizat, un număr fix de titani, niveluri/roluri/ceas pe titan) cu interfața `ITitanScript`; `TutorialScript` (titanul 1 scenarizat); `Ftue` (constante, biții din `profile.ftueFlags`); `FtueHints` (cele 4 indicii și pâlnia); `FtueText` (textele). În Unity: `Game/FtueHud.cs` doar desenează.
- **Titanul scenarizat** = `MonsterBuilder.Build(1, culori fixe)` (o nouă suprascriere a constructorului cu paleta fixă; forma e cea de nivel 1, fără numere aleatoare): cizme, picioare, miez, umeri, gât și ochi galbeni (2), față și centură roșii (0), brațe și cap albastre (1). Brațul are aici culoarea capului (abatere voită de la regula 4.2.3: e un titan de învățare, iar tunul albastru nu e cel arătat). Se folosește la titanul 1 cât timp `profile.totalRuns < 3` (runda 1, 2 și 3); titanii 2+ sunt cei normali ai seminței.
- **Tunul galben** din locul 0 al tăvii: muniția = loviturile pe care le trage singur din locul 0 (regula reală de țintire, simulată pe o copie la construcție) până cad ambele brațe și capul, plafonată la cuburile galbene. Azi: **8 lovituri** (2 ochi, 2 cuburi de gât, 2 + 2 cuburi de umăr). Muniția lui se scoate din pachetul titanului (galbenul rămas se reîmparte în tunuri de 10–30), deci muniția totală = cuburile. Locurile 1–2 se trag normal din pachet. Măsurat: capul cade la 1,2 s după atingere, brațele la 1,8 s și 2,4 s (testul `ScriptedTitanGiantCut` cere ≥ 3 bucăți în 4,0 s și rulează la fiecare `dotnet test`, deci o schimbare a țintirii care îl strică pică CI-ul).
- **`TitanGame.HoldApproach`:** cât e adevărat, ceasul pașilor stă pe loc (reculul nu are cum să apară: nimic nu trage). Se pune la apariția titanului pe care planul îl ține (titanul scenarizat; și primul titan al Titanului Zilei, vezi 9.7) și se șterge la primul `Deploy`. Se poate pune și de mână (testul AC).
- **Indiciile** (`FtueHints`): câte una odată; fiecare o singură dată pe instalare (bitul se pune când apare, deci o aplicație oprită în timpul unei indicii nu o repetă); niciuna nu e buton (sunt texturi și etichete IMGUI; atingerile se citesc din `Input` în `Update`), deci inelul nu poate intercepta atingeri; textul nu acoperă tava.
  - H1: doar pe titanul scenarizat, până la prima atingere (fără limită de timp); inel galben pulsând în jurul cercului de atingere al locului 0, text deasupra tăvii.
  - H2: primul tun de pe zid care așteaptă ≥ 1,0 s continuu (același tun, aceeași poziție); inel pe locul lui 2,5 s.
  - H3: prima dată când ≥ 3 tunuri de pe zid așteaptă; o săgeată spre pătrățelul din mijloc al indicatorului de locuri, 2,5 s. Dacă H2 e pe ecran, H3 așteaptă să se termine (dacă starea mai ține).
  - H4: la primul final de rundă din viață (nu la o victorie în Titanul Zilei), pe ecranul de rezultate, sub motiv: cauza („Toate cele 5 locuri erau pline cu tunuri care așteptau.” / „Titanul a ajuns la zid.” / „Niciun tun nu mai avea în ce trage.”) și „Sfat: lasă mereu 1-2 locuri libere.” Se termină când pleci de pe ecranul de rezultate.
  - Textele sunt deocamdată doar în română, în `FtueText` (tabelul RO/EN vine cu itemul 8).
- **Pornirea:** cât timp `totalRuns < 3`, aplicația intră direct în joc (meniul apare doar de la 3 runde). **Poarta reclamelor** e cea din `AdGovernor` (itemul 13): nicio ofertă (nici prima folosire gratuită) și niciun interstițial sub 3 runde SAU sub 600 s de joc; testul `NoAdOrOfferBeforeThreeRunsAndTenMinutes`.
- **Pâlnia** (nume stabile): `tut_step_{1..4}_shown` și `_done`; `titan_n_reached(n, numărul rundei)` la fiecare titan atins pentru prima dată în viață (doar în jocul infinit); `run_length(secunde, motiv)` la fiecare final de rundă, lângă `run_end(n, motiv)` al itemului 10 (care rămâne cum era: nu schimbăm un nume deja folosit); `run_3_started_same_session` când a treia rundă din viață pornește în aceeași sesiune cu prima. Numele vechi `ftue(pas)` nu se trimite.
- **Simulatorul:** `--ftue` (nu e o rulare de echilibru) pune titanul scenarizat la titanul 1. 300 de jocuri/robot, sămânța 1: titanul 1 doborât de Random / Greedy / Smart în **100% / 100% / 100%** (ținta ≥ 99%); prima bucată după prima atingere la **1,5 / 2,6 / 1,2 s** mediană (p90 4,4 / 5,7 / 1,2 s; ținta mediană ≤ 6 s).
- **Neverificat în editor:** că indiciile apar o singură dată și nu se suprapun la 360×640, inelul H1 (nu interceptează atingeri: e o textură, dar de văzut), săgeata H3 spre pătrățele.

## 9. Retenție și revenire

Totul se construiește local (fără server, fără pachete noi).

### 9.1 Profilul și jurnalul

Un profil versionat, salvat sigur (fișier temporar + copie în PlayerPrefs): recordul, cel mai departe titan, numărul de runde, timpul jucat, pașii de tutorial, punctul de salvare, setările, starea zilnică și seria. Un jurnal local circular de 300 de evenimente, cu interfața `IAnalyticsSink`, ca un SDK de analiză să se poată conecta mai târziu fără a atinge jocul. Ziua se calculează local (`DayClock`), cu protecție la ceasul dat înapoi.

### 9.2 Puncte de salvare gratuite la începutul ciclurilor

- Fiecare rundă are o sămânță; fiecare titan își derivă generatorul din (sămânța rundei, n). Același titan, aceeași formă.
- Când ajungi la titanul 6, 11, 16, … se salvează un punct: **„Punct de salvare: titanul #11”**.
- La final, butoanele: **„Joacă din nou”** (repornește de unde ai pornit), **„Reia ciclul (titanul #11)”** (același titan, scorul restaurat la cel din momentul salvării, deci nu se poate „cultiva” scor), **„Joc nou”** (de la titanul 1, sămânță nouă).
- La pornirea aplicației, dacă ai un punct de salvare: **„Continuă (titanul #11)”** (implicit) și „Joc nou”.
- **Niciodată nu costă o reclamă.** Copia e cinstită: „același titan”, nu „identic” (tunurile depind de cum joci).

### 9.3 Vânătoarea recordului și restartul instant

- În rundă, când ești la ≥ 80% din record: **„Record în 2.300”** în auriu. Când îl depășești: **„RECORD NOU!”**, confetti, fanfară (o singură dată pe rundă). La un titan niciodată atins: **„TITAN NOU: #12”**.
- La final: **„Ai fost la 97% din record!”** doar dacă e adevărat (≥ 85%); altfel recordul. Un singur rând de „următorul obiectiv” (punctul de salvare următor sau cel mai departe titan).
- **„Joacă din nou”** pornește în sub 300 ms: arena se refolosește, nu se reconstruiește.

### 9.4 Titanul Zilei

- În fiecare zi (locală), toți jucătorii primesc aceeași provocare: 3 titani generați din data zilei (niveluri de construcție 3, 4 și un boss 5).
- **Prima încercare e cea oficială**: se înregistrează, ștampilează ziua și produce rezultatul de distribuit. Apoi: antrenament nelimitat, marcat „ANTRENAMENT”.
- Încercarea oficială se salvează după fiecare titan doborât. Dacă aplicația e închisă (apel, sistemul o oprește), la repornire apare **„Continuă încercarea oficială”**. Se finalizează doar dacă ieși explicit sau la miezul nopții.
- **Fără reclame și fără ajutoare** în încercarea oficială (nici înainte, nici după): „Încercarea de azi e fără ajutoare și fără reclame — aceeași provocare pentru toți.”
- **Corectitudine:** un test verifică dinainte (zilele 0–730) că fiecare zi poate fi trecută de robotul Greedy în ≥ 35% din simulări și că primul titan e trecut în ≥ 90%. Telefonul alege aceeași „sare” (cel mult 4 încercări, același rezultat pe orice telefon).
- **Distribuire:** „Copiază rezultatul” pune în clipboard:
  ```
  Titanul din Cuburi · Ziua #288
  🟩🟩🟥
  Scor 41.200 · 2:47 · 9 bucăți rupte
  <link magazin>
  ```
  Fără informații despre tablă (nu strică surpriza). Pe ecran pătratele sunt desenate, nu emoji.
- Numărătoare reală până la titanul de mâine (niciodată un cronometru fals).

### 9.5 Seria iertătoare

- Seria crește cu 1 pentru fiecare zi în care ai jucat încercarea oficială (orice rezultat).
- **Scuturi**: primul Titan al Zilei terminat îți dă 1; fiecare 7 zile de serie încă 1; maximum 2. Se folosesc automat pentru zilele ratate: **„Un scut ți-a păstrat seria.”**
- Dacă seria se rupe: **„Bine ai revenit! Începe o serie nouă azi.”** Numărul vechi nu se arată; „Cea mai lungă serie: N” rămâne.
- Fără texte de tip „vei pierde”, fără oferte de răscumpărare a seriei după ce ai ratat o zi, fără notificări în v1.

### 9.6 Decizii luate la implementare (itemii 10 și 11: profil, puncte de salvare, record, restart)

- **Unde stă logica:** totul e în Core și testat: `Profile` (date simple, câmpuri publice, cheile JSON = numele câmpurilor), `DayClock`, `Telemetry` + `IAnalyticsSink`, `ProfileStore` (ordinea de încărcare și salvarea sigură, cu System.IO), `RunSession` (o rundă: jurnal, record, cel mai departe titan, puncte de salvare) și `HudText` (textele adevărate ale recordului, ca funcții pure). În Unity rămân doar `JsonUtility`, `PlayerPrefs` și `Application.persistentDataPath` (`Game/ProfileStore.cs`), plus HUD-ul.
- **Profilul** are în plus `legacyBest`: recordul vechi (`titan_best`), arătat o singură dată ca „Record vechi (alt punctaj)” până faci un scor nou, niciodată comparat. Un profil nou preia și „Efecte reduse” din vechea cheie `set_reducedFx`. Setările stau de acum în profil (`Settings.Bind`); orice schimbare salvează profilul. Recordul se scrie și ca număr în `titan_best_v2`.
- **Salvarea:** `titan_profile.json.tmp` → `File.Copy` peste `titan_profile.json` → ștergere `.tmp` → aceeași copie în PlayerPrefs (`titan_profile_v1`) → `PlayerPrefs.Save()`. Încărcarea: fișierul, apoi PlayerPrefs, apoi un profil nou; o sursă care nu se citește se sare și se notează `save_corrupt(0 fișier / 1 PlayerPrefs)`. Testul simulează oprirea în timpul scrierii `.tmp` (fișierul rămâne cel vechi) și în timpul copierii (fișier rupt → se încarcă oglinda din PlayerPrefs). Se salvează la titan doborât, la final de rundă, la punctul de salvare, la schimbarea setărilor, la `OnApplicationPause(true)` și la `OnApplicationQuit`; niciodată pe cadru.
- **Ziua** = zile de la 2026-01-01 în ora **locală**. Protecția la ceas: azi = max(ziua ceasului, maxSeenDay − 1). O zi e „nouă” doar dacă e după `lastPlayDay`.
- **Jurnalul:** inel de 300 de intrări `{t, e, a, b}`, `t` = secunde Unix. Evenimente: `session_start(zi, zile de la ultima; −1 prima dată)`, `run_start(titan de start, mod: 0 normal, 3 punct de salvare)`, `titan_spawn(n, rol)`, `titan_defeat(n, secunde de joc)`, `run_end(n, motiv)`, `perf(p95 ms al cadrelor din toată runda, n)` la final de rundă, `restart_ms(ms)` doar pentru butoanele de la final, măsurat până la primul cadru desenat al rundei noi. `ftue(pas)` a rămas doar ca nume: itemul 6 (FTUE) trimite în locul lui pâlnia `tut_step_N_shown/done` (8.1). Modurile 1 și 2 ale `run_start` le folosește Titanul Zilei (9.7). Exportul de depanare (F9 în editor sau atingere cu 3 degete pe scor) copiază jurnalul JSON și arată „Jurnal copiat”.
- **Rundele cu sămânță:** `new TitanGame(cfg, runSeed, startTitan, startScore)`; titanul n își ia generatorul din `Derive(runSeed, n)` pentru formă, pachet și toate tragerile din timpul lui. Constructorul vechi cu `IRandom` rămâne pentru teste și simulator.
- **Punctele de salvare** se salvează la `TitanSpawned` cu n = 6, 11, 16, … (scorul din acel moment vine în eveniment, `GameEvent.Score`). O rundă care **pornește** dintr-un punct de salvare nu îl salvează din nou. `totalRuns` crește la finalul rundei. „Joacă din nou” după o rundă pornită de la titanul 1 = sămânță nouă; după o rundă pornită dintr-un punct de salvare = același punct (aceeași sămânță, același scor). „Joc nou” șterge punctul de salvare. La pornire, ecranul „Continuă (titanul #n)” apare doar cu ≥ 3 runde terminate și un punct de salvare; Enter/Space = Continuă.
- **Recordul în rundă** se compară cu recordul de la începutul rundei. Bannerul „RECORD NOU!” și marcajul „Record în N” apar doar dacă exista un record (> 0): la prima rundă din viață nu are sens. La final, „RECORD NOU!” apare însă și la prima rundă (e adevărat). „TITAN NOU: #n” apare o singură dată pe rundă, la primul titan peste vechiul maxim, și doar dacă exista un maxim. Textul de la final folosește `floor(100 × scor / record)` și apare doar între 85% și 100% (la egalitate: 100%).
- **Restartul rapid:** arena (cameră, zid, plăci, materiale) se construiește o dată; `ArenaView.Reset(joc)` oprește toate corutinele vederii (și pe cele din `Fx`: bătăile inimii), întoarce proiectilele în zbor în pool și distruge dintr-o dată rădăcina rundei („Run”: titani, tunuri, bucăți care cad). Proiectilele sunt acum refolosite (pool), nu create și distruse la fiecare lovitură.
- **Neverificat în editor** (nu avem Unity aici): ținta < 300 ms, numărul de obiecte după 10 restarturi (±5) și „nicio corutină veche după Reset” se verifică manual: joacă până la un boss L8, apasă „Joacă din nou” de 10 ori, uită-te la `restart_ms` în jurnal (F9) și la numărul de obiecte din ierarhie.

### 9.7 Decizii luate la implementare (itemul 12: Titanul Zilei și seria)

- **Unde stă logica:** în Core și testat: `Daily` (sămânța, planul de 3 titani), `DailyValidator` (alegerea „sării”), `StreakRules` (seria), `DailyChallenge` (ziua, încercarea oficială, antrenamentul, rezultatul, seria, mesajul de la pornire), `DailyText` (textele, inclusiv textul de distribuit). Profilul primește `daily` (`DailyState`) și `streak` (`StreakState`). În Unity: `Game/DailyMenus.cs` (butonul, panoul, controalele rundei zilnice) și meniul din `RunMenus.cs`.
- **Modul:** `TitanGame` primește un `RunPlan` cu `MaxTitans` = 3; când cade titanul 3, runda se termină cu `LoseReason.Completed` (o victorie: fanfară, nu „ai pierdut”; nu se poate „reînvia”). Titanii 1–3 au nivelurile 3, 4, 5, titanul 3 e BOSS (bonus ×2), iar ceasul lor folosește n = 6, 7, 8. Restul e configurația v2 a jocului infinit. Titanul k folosește `Derive(daySeed, k)`, adică exact constructorul cu sămânță al itemului 10 cu `runSeed = daySeed`. Ziua = `DayClock` (locală, cu protecția la ceas).
- **Corectitudinea:** `DailyValidator.PickSalt(zi)` încearcă sarea 0..3; pentru fiecare, 40 de încercări ale unui robot Greedy propriu nucleului (Core nu are simulatorul): la 1,0 ± 0,4 s ia cel mai mare tun cu culoare vizibilă, altfel unul la întâmplare dacă nimic nu trage sau ≥ 4 locuri sunt libere; dt 0,05; abaterea din `SeededRandom(daySeed + i)`. Prima sare cu „toți 3 doborâți” ≥ 35% și „titanul 1” ≥ 90% e a zilei; altfel sarea 0 cu nivelurile {2, 3, 4} (fără altă verificare). Încercările se joacă în loturi de 4 și calculul se oprește când răspunsul e sigur (numărătorile doar cresc, deci e același răspuns ca după toate 40; testat). Rezultatul se ține în `profile.daily` (o dată pe zi). În joc se calculează pe un fir separat la pornire (C# pur), ca să nu blocheze ecranul.
  - Toate zilele 0–730 (`DAILY_SWEEP=1 dotnet test`, ~40 s): **727 de zile trec** (sarea 0: 575, 1: 121, 2: 28, 3: 3), **4 zile (0,5%) folosesc rezerva** {2, 3, 4}. Implicit testul verifică fiecare a 10-a zi (74 de zile).
  - Viteză: 30 de zile la întâmplare, în paralel pe 4 nuclee, ~1,4–1,9 s (ținta < 2 s; testul ia cea mai bună din 2 măsurători). Ca să ajungem aici, `Monster` ține acum mulțimea cuburilor vizibile pe culoare (actualizată la `Set` și la ștergere, testat față de o renumărare completă): `HasVisibleOfColor` e O(1), iar `PickTarget` trece doar prin cuburile vizibile. `Remove` refolosește colecțiile de lucru, iar `Salvage` (rulează la fiecare pas) face o singură trecere prin pachet. Simulatorul dă exact aceleași rezultate ca înainte (verificat cu diff pe 150 de jocuri/robot).
  - Rata de trecere e aproape „totul sau nimic” pe zi (100% sau ~0%): abaterea de ±0,4 s schimbă puțin; o zi grea pică de obicei la toate 40.
- **Încercarea oficială:** pornește la **prima atingere** (titanul 1 așteaptă, `HoldApproach`); până atunci, „Înapoi” nu folosește încercarea. După fiecare titan doborât se salvează progresul (titanul, scorul, bucățile și timpul de până atunci). Dacă aplicația e oprită, la pornire meniul arată „Titanul Zilei · în curs: k/3”, iar panoul „Continuă încercarea oficială”: aceeași sămânță, titanul k+1, scorul de după titanul k (ce s-a jucat după ultima salvare se pierde; e regula din specificație, ușor de „încercat din nou” un titan, acceptat). Se termină: la finalul rundei (pierdere sau victorie, cu scorul de atunci), la „Închei” → „Închei încercarea oficială?” → „Da, închei” (pe titanul de pe ecran, cu scorul de pe ecran) sau la miezul nopții (la următoarea pornire / schimbare de zi, cu progresul salvat, pe ziua încercării). O încercare oficială aflată pe ecran la miezul nopții se termină normal și se înregistrează pe ziua ei.
  - Pătratele: verde = doborât, roșu = titanul pe care s-a terminat (și la „Închei”), gri = neatins.
- **Antrenament:** după încercarea oficială, „ANTRENAMENT” nelimitat (aceeași sămânță), care ține doar `bestPracticeScore` (pe zi). Rundele zilnice (oficiale sau antrenament) contează ca runde (`totalRuns`), dar nu schimbă recordul infinit, cel mai departe titan sau punctul de salvare și nu au bannere de record.
- **Fără reclame și fără ajutoare:** pentru **orice** rundă zilnică (și antrenamentul: alegerea mai simplă și mai generoasă) `isDailyRanked = true` în ambele contexte de reclame; în plus, ecranul de rezultate nu arată nicio reclamă înainte de o rundă zilnică cerută (`InterCtx(…, nextIsDaily)`; butonul „Titanul Zilei” deschide panoul fără să treacă prin poarta interstițialului) și nici după (contextul rundei de pe ecran). „A doua suflare” nici nu se evaluează. Panoul zilnic nu are legătura „Fără reclame 30 min”. Teste: `TheDailyGetsNoOfferAndNoInterstitial`, `TheGameMarksEveryDailyRunAsRanked`, iar `TheDailyAndFtuePathsReferenceNoAdApi` interzice orice nume de reclamă în `Daily.cs`, `Ftue.cs`, `RunPlan.cs`, `DailyMenus.cs`, `FtueHud.cs`.
- **Distribuire:** „Copiază rezultatul” pune exact 4 rânduri în clipboard: „Titanul din Cuburi · Ziua #{zi}”, cele 3 emoji (🟩 U+1F7E9 / 🟥 U+1F7E5 / ⬜ U+2B1C), „Scor {scor cu punct la mii} · {m:ss} · {bucăți} bucăți rupte” și linkul magazinului (`DailyConfig.StoreLink`, deocamdată un înlocuitor), apoi „Copiat! Lipește-l unde vrei.” Ziua = numărul zilei din `DayClock` (zile de la 2026-01-01). Timpul = secundele de joc ale încercării (și părțile de dinainte de o reluare).
- **Seria:** `StreakState {current, best, lastDay, shields, firstDailyDone}`. La terminarea încercării oficiale a zilei D cu `lastDay < D`: +1, `lastDay = D`, `best = max`; primul Titan al Zilei din viață dă 1 scut, fiecare `current % 7 == 0` încă 1, maximum 2. La pornire, înainte de orice ecran: gap = azi − lastDay − 1; ≤ 0 nimic; ≤ scuturi: se consumă `gap` scuturi, `lastDay` devine ieri, „Un scut ți-a păstrat seria.”; altfel `current = 0` (și `lastDay` se uită), „Bine ai revenit! Începe o serie nouă azi.” Scuturile rămase se păstrează. A doua rezolvare în aceeași zi nu schimbă nimic (testat). Seria apare doar pe panoul zilnic și în meniu, niciodată pe ecranul de rezultate al jocului infinit. Fără „vei pierde”, fără răscumpărare, fără notificări.
- **Meniul de la pornire** (după 3 runde) apare când există un punct de salvare **sau** încercarea de azi nu e terminată **sau** există un mesaj de serie; altfel jocul pornește direct. Are „Continuă (titanul #n)” (dacă există), „Joc nou”/„Joacă”, „Titanul Zilei · {neîncercat | în curs: k/3 | k/3}” cu „Următorul în h:mm:ss” (numărătoare reală până la miezul nopții locale) și seria. Ecranul de rezultate al jocului infinit are același buton (după primele 3 runde); rândul „următorul obiectiv” spune „Titanul Zilei te așteaptă” cât timp încercarea de azi nu e începută.
- **Jurnal:** `daily_start(zi, mod)` (oficial: la prima atingere; antrenament: la pornire), `daily_end(zi, titani)`, `streak(nou, vechi)` la fiecare schimbare a numărului, `shield_used(gap, scuturi rămase)`, `share_tap(zi)`.
- **Neverificat în editor:** panoul zilnic și meniul la 360×640, caseta de început și butoanele „Înapoi” / „Închei” / „Ieși” (colțul din stânga sus, să nu se suprapună cu scorul), cardul de confirmare, clipboard-ul cu emoji pe Android/iOS, firul de calcul al sării pe telefon (ar trebui să dureze sub o secundă; până atunci butonul scrie „Se pregătește titanul zilei…”).

## 10. Reclamele

### 10.1 Arhitectura

- `IAdService` (Unity) cu `SimulatedAdService` acum și LevelPlay mai târziu, într-o ramură separată de test (pachetele noi au stricat proiectul înainte).
- `AdGovernor` în Core, C# pur, testat unitar: eligibilitate, limite, pauze, perioade de grație. Toate valorile în `AdConfig` (cu suprascriere din `Resources/ad_config.json` și, mai târziu, configurare la distanță).
- **Contractul de pauză:** cât timp o ofertă sau o reclamă e pe ecran, jocul nu avansează, tava nu primește atingeri, sunetul e oprit, arena e întunecată 70%.
- **Cardul de ofertă:** eticheta mică „Reclamă”, titlu, un rând care spune exact ce primești, două butoane de **aceeași mărime și stil**: „Privește reclama” / „Nu, mulțumesc”. Fără cronometre, fără căsuțe bifate dinainte, fără X care înseamnă „accept”.
- **O singură ofertă de reclamă pe ecran**, cu prioritate: revenire > pauză de reclame.
- Recompensa se dă doar la „Completed”. Dacă reclama nu se încarcă la revenire, o dăm gratis o dată pe sesiune.
- **Fără banner.** Nici în joc, nici în meniuri, la lansare.
- Conformitate: recompensa afișată înainte de fiecare reclamă și opt-in separat pentru fiecare (AdMob); interstițiale doar la pauze naturale, închidere ≤ 15 s (Google Play); buton de închidere vizibil și „Raportează o reclamă” în setări (Apple 2.5.18); poartă de vârstă neutră și reclame nepersonalizate pentru sub 13 ani și vârstă necunoscută (COPPA); nu intrăm în programul Families. Poarta de vârstă și raportarea sunt obligatorii înainte de publicare, nu pentru playtest.

### 10.2 Plasamentele aprobate

#### A. „A doua suflare” — revenire cu recompensă (flagship)

| | |
|---|---|
| **Declanșator** | Runda s-a terminat (zid, locuri pline, blocat), după o pauză de 0,6 s pe cadrul final. |
| **Eligibil** | Bucla v2 cu Recuperare activă (altfel e dezactivat prin test); nu în încercarea oficială a zilei; 0 reveniri folosite în această rundă; progres real: ≥ 1 titan doborât sau ≥ 15% din titanul curent distrus; FTUE trecut (≥ 3 runde și ≥ 10 min). |
| **Valoare** | Continui *același* titan cu toate stricăciunile. Tunurile care așteaptă pe zid se întorc în pachet (muniția nu se pierde). Titanul dă înapoi cu **25%** din drum (ales în simulator dintre 0,25 / 0,40 / 0,60: cea mai mică valoare la care Smart câștigă ≥ 1 titan în ≥ 50% din reveniri; măsurat 93%, vezi 10.5) și stă 2 s. Dacă niciun tun din tavă nu are țintă, tava se reface din culori vizibile. Jocul revenit nu e niciodată în starea „blocat” (test). |
| **Limită** | 1 pe rundă. Prima revenire din viață e gratuită („Revino gratis”), ca să înveți că există. |
| **Text** | Titlu: **A DOUA SUFLARE** · „Titanul #5 · 62% distrus · Scor 31.400” · „Tunurile adormite se întorc în pachet, titanul dă înapoi și primești tunuri cu culori vizibile.” · [Privește reclama și continuă] [Nu, mulțumesc] · „O singură dată pe rundă.” |
| **Refuz** | Mergi direct la rezultate. **Niciun interstițial după un refuz.** |
| **Fără** | Fără rândul „ești la X puncte de record” pe card (presiune pe frica de pierdere). |

#### B. „+1 Loc” — al 6-lea loc pe zid

| | |
|---|---|
| **Declanșator** | Doar un buton auriu pe zid (sus, departe de tavă), care apare când ≥ 4 locuri sunt ocupate și ≥ 3 așteaptă, continuu timp de 1 s. Apăsarea lui deschide cardul. **Nicio atingere pe tavă nu deschide vreodată această ofertă.** |
| **Eligibil** | Titanul ≥ 2; nu în încercarea oficială; nefolosit pe acest titan; bugetul de ajutoare în rundă < 2; FTUE trecut. |
| **Valoare** | Un al 6-lea loc (cu margine aurie) până dobori titanul curent. |
| **Limită** | 1 pe titan, 2 pe rundă. După un refuz, butonul dispare 20 s; după două refuzuri, nu mai apare pe titanul următor. Prima folosire e gratuită. |
| **Text** | **+1 LOC PE ZID** · „Un al 6-lea loc pentru tunuri, până dobori acest titan.” · [Privește reclama] [Nu acum] · „Ajutoare rămase în rundă: 2” |
| **Condiție de livrare** | Dacă simulatorul arată că starea-declanșator apare în < 5% din titani după bucla v2, plasamentul se livrează oprit (rămâne pentru test). |

#### C. Interstițial între runde

| | |
|---|---|
| **Unde** | Doar când apeși „Joacă din nou”, „Reia ciclul” sau „Joc nou” pe ecranul de rezultate. Un singur punct în cod. Niciodată în rundă, între titani sau la deschiderea aplicației. |
| **Onestitate** | Când guvernatorul știe că urmează o reclamă, sub buton scrie „Reclamă înainte de rundă”. Apoi 0,8 s: „Reclamă — Mulțumim că susții jocul gratuit.” |
| **Nu apare dacă** (prima regulă care pică se înregistrează) | 1) e oprit; 2) e prima sesiune; 3) < 5 runde în total; 4) < 600 s de joc în total; 5) pauza de reclame e activă; 6) < 180 s de la ultimul interstițial; 7) < 150 s de joc de la ultimul; 8) < 180 s de la ultima reclamă cu recompensă; 9) runda a durat < 45 s; 10) ai refuzat revenirea la acest final; 11) e primul final din sesiune; 12) ≥ 5 în sesiune; 13) ≥ 15 azi; 14) ecranul de rezultate a stat < 1,5 s; 15) runda a făcut un record nou sau un titan nou; 16) încercarea oficială a zilei; 17) reclama nu e gata în 1 s. |
| **Presetări** | „light” (implicit la soft launch): 240 / 210 / 8 runde de grație; „standard”: 180 / 150 / 5; „firm”: 150 / 120 / 4. Pragurile minime sunt scrise în cod: pauza ≥ 120 s, joc între ≥ 90 s. |
| **Închidere** | ≤ 15 s; preferăm reclame care se pot sări după 5 s; blocăm creative video > 30 s unde rețeaua permite. |

#### D. „Pauză de reclame” — înlocuitorul cinstit pentru „Elimină reclamele”

| | |
|---|---|
| **Unde** | Link pe ecranul de rezultate și în meniul de pauză: „Fără reclame 30 min”. Card proactiv o dată pe zi, la ecranul de rezultate, după al 2-lea interstițial al zilei. |
| **Valoare** | 30 de minute fără interstițiale (se cumulează până la 60). Ofertele opționale rămân la alegerea ta. |
| **Limită** | 4 pe zi. Când e activă: „Fără reclame încă 12:40” (neinteractiv). La expirare: un rând simplu „Pauza de reclame s-a încheiat.” la următorul ecran de rezultate, fără popup. |
| **Text** | **PAUZĂ DE RECLAME** · „Privește o reclamă acum și ai 30 de minute fără reclame între runde. Reclamele opționale (cu recompensă) rămân la alegerea ta.” · [Privește reclama] [Nu, mulțumesc] |

### 10.3 De ce sunt „aproape irezistibile”, cinstit

- Sunt oferite exact în momentele de cea mai mare nevoie (Unity: 38,1% implicare la momente contextuale vs 23,8% între nivele).
- Valoarea e reală și vizibilă dinainte (ce păstrezi: „Titanul #5 · 62% distrus”).
- Sunt rare (1 revenire pe rundă), deci rămân prețioase (modelul Block Blast).
- Prima folosire e gratuită, deci jucătorul știe exact ce primește.
- Repară exact plângerea nr. 1 a jucătorilor Pixel Flow: „nu pot viziona o reclamă pentru ajutor”.
- Pauza de reclame repară plângerea nr. 1 de la Block Blast: „nu pot scăpa de reclame”.

### 10.4 Experimente și protecții

- La prima pornire: `install_id` și un `bucket` stabil (0–99). Bucket 0–9: **fără interstițiale, pentru totdeauna** (grupul de control). Bucket 10–14: fără „+1 Loc” (măsurăm dacă ajutoarele schimbă retenția).
- Fiecare plasament are un comutator `<nume>_enabled`.
- Fiecare eveniment poartă bucket-ul. Evenimente: ad_offer_eligible / shown / accepted / declined, ad_show_result, ad_reward_granted, ad_fallback_grant, ad_inter_eligible / skipped (cu motivul) / shown / closed, app_background_after_ad (aplicația iese în ≤ 30 s după o reclamă), revive_outcome (titani și secunde după revenire).
- Reguli de retragere automată: D1 cu > 1,5 pp sub grupul de control sau D7 cu > 1,0 pp sub → presetarea interstițialelor trece un pas mai ușor. app_background_after_ad > 8% → la fel. Interstițiale la 10 minute de joc ≤ 3,5 (limită și în cod).
- Sub pragul minim de acceptare 7 zile la rând → redesenăm recompensa, nu apăsăm mai tare.
- Primele teste (max 2 simultan, ≥ 14 zile și ≥ 2.000 de instalări per braț): reveniri 1 vs 2 pe rundă; împingere 0,25 / 0,40 / 0,60; light vs standard; pauză de reclame 20 / 30 / 45 min; buget de ajutoare 1 vs 2.

### 10.5 Decizii luate la implementare (itemii 13 și 14: fundația de reclame, reclamele de salvare)

**Arhitectura.** Regulile stau în `Assets/Titan/Scripts/Core/Ads` (C# pur, testate): `AdConfig` (toate valorile, cu suprascriere dintr-un JSON plat: `Resources/ad_config.json`, dacă există; nu e livrat), `AdState` (ce se ține minte, chei `ad_*` printr-un `IStore`: PlayerPrefs în joc), `AdGovernor` (toate deciziile), `InterstitialGate` (singurul punct de unde poate porni un interstițial), `RewardedOffer` (un card de ofertă de la deschidere la închidere, comun pentru revenire, „+1 Loc” și pauza de reclame), `ExtraSlotButton` (temporizarea butonului de pe zid), `AdText` (textele). Partea Unity e în `Assets/Titan/Scripts/Game/Ads`: `SimulatedAdService` (**înlocuitor pentru LevelPlay**, scrie „RECLAMĂ SIMULATĂ — Înlocuitor pentru LevelPlay” pe ecran), `AdPause` (contractul de pauză), `AdOfferCard` (cardul IMGUI) și `GameController.Ads.cs`.
- `IAdService`, `AdResult` și `Placement` stau în Core, nu în Game (nu folosesc nimic din Unity), ca fluxurile să fie testate cu o rețea de reclame falsă. `SimulatedAdService` și, mai târziu, adaptorul LevelPlay le implementează.
- Starea reclamelor e **separată de profil**: `Profile.cs`, `RunSession.cs`, `ProfileStore` și `RunMenus.cs` nu pot numi nimic legat de reclame (testul `NoCheckpointPathReferencesAnAdApi`). Cele trei butoane ale ecranului de rezultate cheamă `LeaveResults(start)`; acolo e poarta interstițialului, iar runda aleasă pornește **la orice rezultat** al reclamei (punctul de salvare nu depinde de nicio reclamă). Ecranul de pornire („Continuă”) pornește runda direct: niciun interstițial la deschiderea aplicației.
- Regulile jocului nu știu de reclame: `TitanGame.Revive` și `TitanGame.GrantExtraSlot` sunt reguli de joc (mută tunuri, deschid un loc) și nu știu cine le-a cerut. Testele de etică verifică prin reflecție și grep că `TitanGame`, `Monster`, `MonsterBuilder`, `SeededRandom`, `RunSession` și `Profile` nu ating `Titan.Core.Ads`.

**Interstițialul (10.2 C).** Regulile se verifică în ordinea din 10.2 C; prima care pică e motivul din `ad_inter_skipped`: `inter_disabled` (1, și grupul de control 0–9), `first_session` (2), `grace_runs` (3), `min_lifetime_play` (4), `ad_break_active` (5), `cooldown` (6), `min_play_since_inter` (7), `rate_cap`, `after_rewarded` (8), `short_run` (9), `declined_revive` (10), `first_gameover` (11), `session_cap` (12), `daily_cap` (13), `results_too_short` (14), `new_record` (15, record nou sau titan nou), `daily_ranked` (16), `no_fill` (17).
- Presetări: light 240 / 210 / 8 (implicit), standard 180 / 150 / 5, firm 150 / 120 / 4; o presetare necunoscută devine light. Pragurile minime sunt **constante în cod**: pauza ≥ 120 s, joc între ≥ 90 s, rundele de grație nu coboară sub poarta de 3 runde; o valoare mai mică din JSON e ignorată.
- „≤ 3,5 interstițiale la 10 minute de joc” e o regulă separată (`rate_cap`, după regula 7): o fereastră glisantă pe **timpul de joc** (doar cât rulează runda; nu meniuri, carduri sau reclame), cel mult 3 în orice 600 s de joc (3 + 1 > 3,5). Testul: 20 de runde de 40 s → 0; o sesiune de 30 min cu runde de 3 min → 5 (light, standard și firm; limita de 5 pe sesiune decide), cel mult 3 în orice 10 minute de joc.
- O **sesiune** începe la pornire sau la revenirea în aplicație după ≥ 1.800 s în fundal; regula 2 cere a doua sesiune din viață. Regula 11 numără finalurile de rundă adevărate (o revenire nu e un final).
- **Eticheta „Reclamă înainte de rundă”** apare sub cele trei butoane când regulile 1–16 trec. Se calculează o dată pe cadru, iar la atingere poarta primește exact același context (aceleași secunde pe ecranul de rezultate): fără etichetă pe ecran, nicio reclamă. Apoi: până la 1,0 s așteptare după reclamă (regula 17), cardul de 0,8 s „Reclamă — Mulțumim că susții jocul gratuit.”, reclama. Fiecare evaluare scrie exact un `ad_inter_shown` sau un `ad_inter_skipped` (o reclamă care eșuează la afișare e `no_fill`). Un interstițial închis după 2 s contează ca afișat.
- **Pauza de reclame:** acordarea = min(max(acum, până_la) + 30 min, acum + 60 min), 4 pe zi locală. Cât timp e activă, ecranul de rezultate arată doar „Fără reclame încă mm:ss” (neinteractiv); legătura „Fără reclame 30 min” apare doar când pauza nu e activă (și doar după poarta FTUE). „Stiva plină” = au rămas peste 30 de minute (încă o pauză n-ar mai adăuga 30 de minute întregi): oferta se ascunde. Cu regula de mai sus, plafonul de 60 min nu se atinge din interfață; formula rămâne în cod și e testată. Meniul de pauză (UX-1, secțiunea 7.1) are rândul „Fără reclame 30 min”, cu aceeași regulă, niciodată în Titanul Zilei. Cardul proactiv: pe ecranul de rezultate, după al 2-lea interstițial al zilei locale, o dată pe zi, nu cât timp pauza e activă și nu dacă la acest final a fost cardul de revenire. La expirare, un singur rând „Pauza de reclame s-a încheiat.” pe următorul ecran de rezultate.
- **Recompensa** se dă doar la `Completed`. Prima folosire din viață a revenirii („Revino gratis”) și a lui „+1 Loc” („Încearcă gratis”) nu arată nicio reclamă și nu pornește regula 8 (n-ai văzut nicio reclamă). `ClosedEarly`: revenirea se întoarce la același card; „+1 Loc” și pauza de reclame se închid fără schimbare.
- **Contractul de pauză:** cât timp un card sau o reclamă e pe ecran: fără `Tick`, fără atingeri pe tavă, `Time.timeScale = 0`, `AudioListener.pause = true`, arena întunecată 70%. Totul se restabilește din callback și e verificat din nou la fiecare cadru (`AdPause.Apply`). Fiecare flux se termină în „acordat”, „refuzat” sau „închis”: butonul de refuz nu depinde niciodată de reclamă. În editor, **F10** comută `SimulatedAdService.ForceFail`.
- **Cohorte:** `install_id` = GUID la prima pornire, `bucket` = FNV-1a 64 (`SeededRandom.FromString`) mod 100. Bucket 0–9: fără interstițiale pentru totdeauna (motivul `inter_disabled`); 10–14: fără „+1 Loc” (motivul `holdout`). Fiecare plasament are `<nume>_enabled`. Bannerul nu există (`banner_enabled = false`, fără cod).
- **Telemetria** merge în jurnalul RET-1: `e` = „nume[:parametru]|experimente”, `a` = valoarea, `b` = bucket-ul. Experimentele: „inter=light;rv_pb=0.25” plus etichetele din `experiments`. Evenimente: `ad_offer_eligible:<plasament>/<motiv>`, `ad_offer_shown:<plasament>[/detalii]` (la revenire: motivul pierderii și titanii rundei, cu `a` = % distrus), `ad_offer_accepted`, `ad_offer_declined`, `ad_show_result:<plasament>/<rezultat>`, `ad_reward_granted`, `ad_free_grant`, `ad_fallback_grant`, `ad_inter_eligible`, `ad_inter_skipped:<motiv>`, `ad_inter_shown`, `ad_inter_closed` (`a` = secunde), `app_background_after_ad` (ieșire din aplicație ≤ 30 s după ce s-a închis o reclamă), `revive_outcome:t<titani după>/best<0|1>` (`a` = secunde după), `extra_slot_titan_end:idle<n>/occ<m>` (`a` = loviturile din locul 6).
- Confidențialitate: `SetPrivacy(false, true)` (nepersonalizat, pentru copii) până există poarta de vârstă.

**„A doua suflare” (10.2 A).** `TitanGame.Revive(împingere, grație)`: doar după o pierdere la zid, „locuri pline” sau „blocat”. Tunurile de pe zid fără culoare vizibilă se întorc în pachet la o poziție aleasă de generatorul titanului (cele fără cuburi rămase dispar), apoi Recuperarea taie din pachet surplusul (un tun întors poate avea mai multă muniție decât cuburile rămase ale culorii); titanul dă înapoi, următorul pas vine după `StepInterval + grație`; dacă niciun tun din tavă nu are o culoare vizibilă, tava se întoarce în pachet și se trage din nou cu bias 1,0 (și fără triplete, rămânând la culori vizibile când se poate); evenimente `Revived` și `TrayRefilled` (-1). Toate cuburile rămân. `TitanGame.Revives` numără revenirile; limita de 1 pe rundă e a guvernatorului.
- Jocul **ține evenimentul `GameOver`** cât timp cardul e deschis: `RunSession` (run_end, numărul de runde, salvarea) îl primește doar dacă runda chiar se termină (refuz). Ordinea: 0,6 s pe cadrul final cu bannerul pierderii, apoi cardul în locul ecranului de rezultate. Reclama nu se încarcă: „Se încarcă reclama…” până la 3 s, apoi o dată pe sesiune revenirea gratuită („Reclama nu s-a încărcat. Îți dăm revenirea oricum.”), altfel „Nicio reclamă disponibilă acum” și doar „Nu, mulțumesc” rămâne activ. După revenire, arena se luminează în 0,5 s; titanul se clatină înapoi, tunurile adormite zboară spre tavă și dispar în pachet.
- **Împingerea aleasă: 0,25.** `dotnet run -c Release --project Tools/Sim -- --games 300 --bots smart,random --revive` (o revenire la prima pierdere, când ar fi oferită; în rest ajutoarele sunt oprite):

| Împingere | Smart: ≥ 1 titan în plus | Smart: titani după (mediană / medie) | Random: ≥ 1 titan în plus | Random: titani după (mediană / medie) |
|---|---|---|---|---|
| **0,25** | **93%** | 4 / 5,69 | 85% | 3 / 3,71 |
| 0,40 | 98% | 4,5 / 5,89 | 91% | 3 / 4,00 |
| 0,60 | 99% | 5 / 5,98 | 98% | 3 / 4,35 |

  Toate cele trei trec pragul de 50%, deci regula („cea mai mică valoare care trece”) alege 0,25. Revenirea e oferită la 100% din primele pierderi (aproape mereu e deja doborât un titan). Observație pentru proprietar: o revenire valorează mult (Smart mai doboară în medie ~5,7 titani, aproape o treime dintr-o rundă întreagă de ~17); dacă se vrea o revenire mai mică, ținta „≥ 50%” ar trebui înlocuită cu una pe „titani după”.

**„+1 Loc” (10.2 B).** `Line` are `LineSlots + 1` locuri; `ActiveSlots` = 5 și devine 6 după `GrantExtraSlot()` (o dată pe titan, eveniment `SlotAdded`, locul 5 la x = 7,2, ceilalți nu se mută); `SpawnTitan` îl resetează. Toate buclele pe linie (tragerea, paza, „blocat”, „locuri pline”, recuperarea, bonusul de muniție rămasă, steaua „Linie curată”) merg pe `ActiveSlots`.
- Camera încadrează mereu și locul 6 (x = 7,2 + 1,2), chiar închis, ca să nu se miște când se deschide; încadrarea e puțin mai largă decât înainte.
- Butonul auriu „+1 LOC” (110 dp, cel mult 24% din lățime) stă pe zid, în dreapta locului 5, deasupra liniei zidului; e strâns în zona sigură și urcat dacă ar atinge zona de atingere a tăvii (în editor se scrie un avertisment dacă iese din ecran sau atinge tava).
- Apare după 1,0 s continuu cu ≥ 4 locuri ocupate și ≥ 3 care așteaptă; dispare după 2 s fără condiție; un singur puls de 0,3 s. Un refuz îl ascunde 20 s de timp de joc; **după 2 refuzuri în rundă dispare pentru restul titanului și pentru tot titanul următor**, apoi numărătoarea refuzurilor o ia de la 0 (interpretarea lui „nu mai apare pe titanul următor”).
- Bugetul de ajutoare în rundă (2) numără doar „+1 Loc”; revenirea are limita ei (1 pe rundă).
- Condiția de livrare: Random vede condiția butonului în 16,1% din titanii n ≥ 2 (Greedy 16,4%, Smart 4,2%; 300 de jocuri, sămânța 1) ≥ 5% → **livrat pornit**. Raportul simulatorului o afișează la fiecare rulare.

**Nu am putut verifica în Unity** (nu există editor aici; codul compilează în `Tools/UnityCheck` și `Tools/EditorCheck` fără erori și fără avertismente): cardurile la 360×640, butonul „+1 LOC” și locul 6 la 9:19,5 și 9:16 (pe ecran, fără să atingă tava), animația revenirii și a locului 6, contractul de pauză cu reclama simulată, F10 / ForceFail în toate fluxurile.

## 11. Linia etică (reguli care nu se negociază și nu se testează A/B)

1. **Nicio dificultate legată de reclame.** VisibleColorBias, ceasul, culorile, nivelurile de construcție și orice parametru de dificultate sunt identice indiferent dacă jucătorul a văzut sau nu reclame. Test automat.
2. **Toate țintele de echilibru se ating cu ajutoarele din reclame oprite**, verificat în CI cu roboții simulatorului.
3. **Refuzul nu declanșează niciodată o reclamă.** Butonul de refuz are aceeași mărime ca acceptarea.
4. **Nicio atingere pe un element de joc nu deschide o ofertă de reclamă.**
5. **Nicio reclamă în timpul rundei** (ofertele în rundă sunt doar butoane pe care le apeși tu și care pun jocul pe pauză).
6. **Fără cronometre false, fără texte de frică** („vei pierde”), fără răscumpărarea seriei după o zi ratată.
7. **Nu vindem ușurare pentru un bug.** Revenirea și „+1 Loc” sunt dezactivate prin test în orice build fără Recuperare.
8. **Recompensele câștigate nu sunt ținute ostatice** (nu există „ia ×1” vs „ia ×2”).
9. **Titanul Zilei oficial e curat**: fără reclame, fără ajutoare.
10. **Minorii**: reclame nepersonalizate, fără mesaje de serie în pericol.
11. Lista celor care nu se testează niciodată A/B: mărimea și paritatea butonului de refuz, reclame după refuz, VisibleColorBias, dificultate dependentă de istoricul de reclame, cronometre pe oferte, texte de frică.

## 12. KPI și ținte de echilibru

### 12.1 Ținte pentru simulator (robot, ajutoare oprite)

| Măsură | Țintă | Harness (out_C1) | Măsurat azi (itemii 1–5 în cod, Tools/Sim, 300 jocuri/robot, sămânța 1; R / G / S) |
|---|---|---|---|
| Pierdere la titanul 1 | Random < 2%, Smart < 1% | 6% / 1% | 0% / 0% / 0% ✔ |
| Runde < 20 s | Random ≤ 1%, Smart 0% | 7% / 2% | 0% / 0% / 0% ✔ |
| Runde < 30 s (Random) | ≤ 3% | 9% | 0% ✔ |
| Raport titani Smart : Random | 1,7–2,3× | 1,74× | 1,77× (16,81 / 9,52) ✔ |
| Cota pierderilor la zid (Random) | 35–55% | 71% | 96% ✘ (vezi 4.3.2) |
| Cota pierderilor la zid (Smart) | 40–70% | 90% | 99% ✘ (vezi 4.3.2) |
| Timp per titan (median), titanii 1–16 | 10–40 s; sprint ≤ 16 s; boss ≤ 40 s | 12–37 s | 10–35 s; sprint ≤ 12 s; boss ≤ 35 s ✔ |
| Rundă mediană | Random 120–200 s; Smart 240–420 s | 164 s / 283 s | 194 s / 251 s / 345 s ✔ |
| Smart atinge limita de 30 min | < 2% | — | 0% ✔ |
| Tunuri moarte pe zid | 0 (invariant) | 0,00 | 0,00 ✔ |
| Decizii cu ≥ 2 opțiuni vizibile (Smart) | ≥ 65% | 77% | 77% ✔ |
| Decizii fără alegere | ≤ 25% | 16–20% | 16% / 14% / 20% ✔ |
| Timp cu zidul plin | ≤ 25% | 26–32% | 26% / 35% / 31% ✘ (vezi 4.3.2) |
| Marja la doborâre (mediană) | 0,25–0,45 | 0,21–0,47 | 0,37 / 0,40 / 0,39 ✔ |
| Timp în pericol ≥ 75% | 4–10% | 5–6% | 6% / 6% / 5% ✔ |
| Pierderi „locuri pline” prin atingerea proprie | 0% | 65% din „locuri pline” | 0% ✔ |
| Timp blocat continuu maxim | ≤ 1,55 s | 1,60 s | 0,00 s ✔ |
| Bucăți din lovituri pe încheietură | ≥ 65% | 76–82% | 75% / 63% / 81% (Greedy sub țintă, itemul 3) |
| Bucăți per titan | 2,5–3,5 | ~3 | 3,06 / 3,20 / 3,12 ✔ |
| Tăieturi curate per titan, Smart vs Random | ≥ 1,5× | de măsurat | 1,26 / 1,15 / 2,02 → 1,60× ✔ |
| Steaua „Linie curată” pentru Smart | 25–40% din titani | de măsurat | 9% (K = 4) ✘ (vezi 6.4) |
| Titanul 1 scenarizat | ≥ 3 bucăți în 4 s după prima atingere | test | 3 bucăți la 1,2 / 1,8 / 2,4 s ✔ (`ScriptedTitanGiantCut`, 8 semințe) |
| Titanul 1 scenarizat în simulator (`--ftue`) | Random doboară titanul 1 în ≥ 99%; prima bucată la mediana ≤ 6 s după prima atingere | — | 100%; 1,5 s (Greedy 2,6 s, Smart 1,2 s) ✔ (8.1) |
| Revenire (doar în modul `--revive`, nu în echilibru) | Smart câștigă ≥ 1 titan în ≥ 50% din reveniri | de măsurat | împingere 0,25: Smart 93%, Random 85% ✔ (10.5) |
| „+1 Loc”: condiția butonului (≥ 4 ocupate, ≥ 3 așteaptă, 1 s) | ≥ 5% din titanii n ≥ 2 pentru Random, altfel se livrează oprit | de măsurat | 16,1% / 16,4% / 4,2% ✔ (livrat pornit) |
| Interstițiale | 20 de runde de 40 s la rând → 0; ≤ 3,5 la 10 min | test | 0; 5 în 30 min (light, standard, firm); ≤ 3 în orice 10 min de joc ✔ (test) |
| Titanul Zilei | fiecare zi 0–730: Greedy trece ≥ 35%, primul titan ≥ 90% | test | 727 de zile trec, 4 (0,5%) pe rezerva {2, 3, 4}; 30 de zile în ~1,4–1,9 s ✔ (9.7) |

**Testul de echilibru din CI** (`Tools/CoreTests/BalanceTests.cs`, categoria `Balance`, 50 de jocuri/robot, sămânța 20261015, presetul `v2`, ajutoare oprite) verifică versiuni grosiere ale țintelor de mai sus (50 de jocuri au zgomot; țintele exacte se verifică în `Tools/Sim` cu 300):
- presetul `v2` e complet (toate câmpurile există în `TitanConfig`);
- nicio rundă nu atinge limita de 30 min (≤ 2%);
- media titanilor doborâți: Random ≥ 5, Smart ≥ 9; raportul Smart : Random între 1,4× și 2,6×;
- pierdere la titanul 1: Random ≤ 4%, Smart ≤ 2%; runde < 20 s: Random ≤ 2%, Smart 0%; runde < 30 s (Random) ≤ 6%;
- runda mediană: Random 100–260 s, Smart 200–500 s;
- cota pierderilor la zid: doar limitele de jos (Random ≥ 35%, Smart ≥ 40%), limitele de sus sunt depășite știut (4.3.2);
- timp per titan median (titanii 1–16 cu ≥ 10 doborâri): 8–44 s, sprint ≤ 18 s, boss ≤ 44 s, pentru fiecare robot;
- Smart: decizii cu ≥ 2 culori vizibile ≥ 60%, fără alegere ≤ 30%; zid plin ≤ 40% pentru fiecare robot (ținta 25% e depășită știut);
- marja mediană la doborâre 0,20–0,50 și timp în pericol ≥ 75% între 2% și 12%, pentru fiecare robot;
- paza și „blocat” (itemul 5): 0 pierderi „locuri pline” prin atingerea proprie, timp blocat continuu ≤ 1,55 s;
- bucăți per titan între 1 și 8, iar loviturile pe încheietură taie bucăți;
- Recuperare (itemul 2): 0 tunuri moarte pe zid, 0% timp cu un tun mort pe zid, 0% decizii cu tava doar cu tunuri moarte;
- Tăieturi câștigate (itemul 3): ≥ 60% din bucăți din lovituri pe încheieturi, 2,5–3,5 bucăți per titan;
- Scorul tăieturilor (itemul 7): tăieturi curate per titan Smart : Random ≥ 1,3×; steaua „Decapitare” între 20% și 90% din titani pentru fiecare robot; steaua „Linie curată” sub 50% pentru fiecare robot și atinsă măcar o dată (ținta 25–40% pentru Smart e ratată știut, 6.4);
- simularea e deterministă (aceeași sămânță → același rezultat).
Separat, mereu (și cu `SKIP_BALANCE=1`), în `Tools/CoreTests/EthicsLockTests.cs`: codul roboților nu atinge ajutoare sau reclame, `BotView` expune doar numere, `VisibleColorBias` = 0,65 în cod și în toate preseturile; regulile jocului (`TitanGame`, `Monster`, `MonsterBuilder`, `SeededRandom`, `RunSession`, `Profile`) nu au nicio legătură cu `Titan.Core.Ads` (reflecție + grep) și dau aceleași evenimente pentru aceeași sămânță oricare ar fi istoricul de reclame; un refuz nu duce niciodată la un interstițial; Titanul Zilei oficial nu primește nicio ofertă; poarta FTUE (3 runde, 600 s) blochează tot; pragurile minime sunt constante în cod; atingerea tăvii trece doar prin `TrayTap.Handle`, care nu poate deschide un card; modul de revenire al simulatorului e oprit implicit.

### 12.2 KPI pentru primul soft launch

| KPI | Țintă | Prag sub care reparăm bucla înainte de orice altceva |
|---|---|---|
| D1 | ≥ 40% | 30% |
| D7 | ≥ 15% | 10% |
| D30 | ≥ 6% | 4% |
| Titanul 1 trecut la prima rundă | ≥ 90% | 80% |
| Prima bucată căzută (median) | ≤ 10 s | 20 s |
| Pașii tutorialului finalizați | ≥ 85% fiecare | 70% |
| Sesiuni / DAU | ≥ 4 | 2,5 |
| Timp / DAU | 12–20 min | 8 min |
| Runde / sesiune | ≥ 3 | 2 |
| Restart (atingere → joc) | < 1 s (p95 < 300 ms pe gamă medie) | 2 s |
| Revenire: acceptare | 45–60% | 30% |
| „+1 Loc”: acceptare | 25–40% | 20% |
| Pauza de reclame: % DAU / zi | 5–12% | — |
| % DAU cu ≥ 1 reclamă cu recompensă | ≥ 55% | 36% |
| Reclame cu recompensă / DAU | 2–4 | — |
| Interstițiale / DAU | 2–4 | (maxim 3,5 la 10 min) |
| D1 / D7 vs grupul fără interstițiale | ≥ −1,5 pp / ≥ −1,0 pp | retragere automată |
| Aplicația iese ≤ 30 s după o reclamă | < 8% din interstițiale | retragere |
| Titanul Zilei: % DAU | ≥ 40% | 20% |
| Distribuiri / încercări oficiale | ≥ 8% | — |
| „Continuă” folosit când există punct de salvare | ≥ 50% | — |
| Nota în magazin | ≥ 4,5 | 4,2 |
| Recenzii 1 stea despre reclame | < 10% din recenziile de 1 stea | 25% |
| Fără crash | ≥ 99,5% sesiuni | 99% |

Repere: mediana pieței e ~22% D1 / < 4% D7 / ~0,8% D30 (GameAnalytics 2026); top 10% ~40% D1; jocurile de sortare top-grossing ~52/27/14 (Gamigion 2026); Royal Match 16,5% D30.

## 13. Backlog-ul sprintului (ordinea de construcție)

Fiecare item e un commit (sau o serie mică), cu teste `dotnet test Tools/CoreTests` pentru logică și verificare în Unity pentru vizual. README.md se actualizează în același commit cu regulile pe care le schimbă.

| # | Item | Zonă | Depinde de |
|---|---|---|---|
| 1 | Simulatorul în repo + SeededRandom + test de echilibru în CI | core | — |
| 2 | Recuperare: muniția pleacă odată cu bucata | ambele | 1 |
| 3 | Tăieturi câștigate: țintire de la vârf, API de părți, TAIE! cinstit | ambele | 2 |
| 4 | Bucla v2: tavă-bandă, ceasul titanului, cicluri de 5 | ambele | 2, 3 |
| 5 | Amenințare lizibilă: paza ultimului loc, blocat, indicatorul de locuri | ambele | 4 |
| 6 | FTUE: titanul 1 scenarizat + indicii | ambele | 3, 4, 5 |
| 7 | Scorul tăieturilor + nota titanului | ambele | 3 |
| 8 | Accesibilitate, setări, pauză, RO/EN | unity | — |
| 9 | Senzație (juice) + buget de performanță | unity | 3, 8 |
| 10 | Profil + jurnal + puncte de salvare gratuite | ambele | 1, 4 |
| 11 | Vânătoarea recordului + restart sub o secundă | ambele | 10 |
| 12 | Titanul Zilei + seria iertătoare | ambele | 10 |
| 13 | Fundația de reclame + interstițiale + pauza de reclame | ambele | 10 |
| 14 | Reclame de salvare: „A doua suflare” + „+1 Loc” | ambele | 2, 5, 13 |

Specificațiile tehnice complete și criteriile de acceptare sunt în backlog-ul de producție al echipei de ingineri. Mai jos e rezumatul, cu trimitere la secțiunile care descriu regulile.

1. **Simulatorul în repo.** `Tools/Sim` (consolă dotnet, compilează direct `Assets/Titan/Scripts/Core`), roboții Random/Greedy/Smart, fără ajutoare din reclame, raportul de metrici din 12.1. `SeededRandom` (SplitMix64) în Core, același pe toate platformele, folosit de toate sistemele cu semințe. Un test redus (50 de jocuri/robot) în CoreTests verifică țintele principale.
2. **Recuperare.** Regula 4.2.2, evenimentul `Salvaged`, animațiile „RECUPERAT +N” și „-N”, invariantul testat pe 200 de semințe.
3. **Tăieturi câștigate.** `supportDist` (BFS din corpul ancorat), FarthestFirst, mâna = culoarea brațului, etichetele de părți (`PartOf`, `IsJoint`, `OriginalPartSize`) ca API unic, cusătura, „TAIE!” cinstit.
4. **Bucla v2.** Reumplere pe loc, fără triplete, ceasul, reculul, „la mustață”, ciclurile și rolurile, bannerele, inima și marginea roșie; reglaj final în simulator pentru țintele din 12.1.
5. **Amenințare lizibilă.** `WouldJam` și `DeployBlockReason`, starea „blocat” după 1,5 s, cele 5 pătrate, cipul „? culori ascunse”.
6. **FTUE.** Secțiunea 8.
7. **Scor.** Secțiunea 6.
8. **Accesibilitate.** Secțiunea 7 (modele, simboluri, setări, pauză automată, tabel RO/EN).
9. **Juice și performanță.** Secțiunea 7.
10. **Profil și puncte de salvare.** Secțiunile 9.1 și 9.2.
11. **Record și restart.** Secțiunea 9.3.
12. **Titanul Zilei și seria.** Secțiunile 9.4 și 9.5.
13. **Fundația de reclame.** Secțiunile 10.1, 10.2 C–D, 10.4.
14. **Reclame de salvare.** Secțiunea 10.2 A–B.

## 14. Ce am omorât și de ce

| Idee | De ce |
|---|---|
| Start de la titanul N plătit cu reclamă (cu puncte creditate) | Ar vinde ieșirea dintr-o plictiseală creată de noi și ar cumpăra scor cu reclame. Punctele de salvare sunt gratuite. |
| Interstițial după boss | Titanii se înlănțuie: ar întrerupe o rundă în desfășurare. Între titani nu există reclame. |
| Oferta „+1 Loc” deschisă la atingerea unui tun când zidul e plin | Ar transforma o atingere de joc într-o capcană de reclamă și ar intra în conflict cu paza ultimului loc. |
| Rândul „Ești la X puncte de record!” pe cardul de revenire | Presiune prin frica de pierdere chiar în momentul vânzării. Informația stă pe ecranul de rezultate. |
| Banner (în joc sau meniuri) la lansare | Câștig mic (eCPM ~1/10 din cel cu recompensă), risc mare de atingeri accidentale lângă tavă. |
| „×2 XP” după rundă | Nu există încă un sistem de XP; un ×2 pe scor nu valorează nimic și ar eroda încrederea. |
| Tava nouă prin reclamă (Tunuri proaspete) ca reclamă | După reparații starea devine rară și ar vinde ușurare din propriul nostru tiraj aleator. Dacă revine, devine o acțiune gratuită. |
| Al doilea lanț de multiplicatori („Lanț Perfect”) | Prea mulți multiplicatori, scor de neînțeles. Un singur model (secțiunea 6). |
| Bestiarul de 108 „specii” pe combinații de culori | Colecție de schimbări de paletă, fără sens mecanic, scump de construit. |
| Onboarding cu semințe căutate (R2 separat) | Fragil (depinde de ritmul atingerilor). Unit cu titanul 1 scenarizat. |
| „Porțile” la titanii 4, 7, 10 | Legate de vechea creștere monotonă; înlocuite de punctele de salvare la începutul ciclurilor. |
| Tabăra (hub) cu 6 carduri | Depinde de sisteme amânate și adaugă fricțiune înainte de joc. Ecranul de rezultate e hub-ul în v1. |
| Capcana de serie de tip Pixel Flow (pierzi multiplicatorul ×10 și ți se oferă o ofertă) | Încalcă linia etică. |
| Orice „regizor” de dificultate care citește istoricul de reclame | Încalcă linia etică. |
| Finalizarea încercării zilnice dacă aplicația e închisă | Pedepsește apelurile și închiderile de sistem. Se continuă de la ultimul titan doborât. |

## 15. Ce amânăm (după primul playtest)

- „Prăbușirea” finală (titanul se dărâmă sub 8%) — după verificare în simulator și test de performanță (max ~150 de corpuri fizice).
- Planurile de corp noi (întâi Patru Brațe sau Coarne, debut pe boss la titanul 10), apoi cuburi blindate și tunuri duble.
- Rangul de Zidar cu ~6–20 de cosmetice (zid și cer, niciodată culorile cuburilor), cu ritm gândit pentru cine nu vede reclame; XP din reclame ≤ 10% din XP-ul unei zile.
- Calendarul lunar și medaliile; arhiva zilelor ratate; dificultate pe zile ale săptămânii la Titanul Zilei.
- Provocarea Săptămânii (întâi „Tunuri Grele” și „Titani Grăbiți”, doar configurație); medaliile contează doar fără revenire.
- „×2 recompensa zilei” și reîncărcarea proactivă a scutului prin reclamă (doar cu seria intactă, max 1 / 7 zile).
- „Schimbă tava” gratuit o dată pe titan, dacă simulatorul arată tava complet ascunsă în ≥ 5% din decizii.
- Sala trofeelor cu capetele tăiate (cheie pe plan de corp × mărime × boss).
- Tabăra (hub) când există ≥ 3 sisteme zilnice.
- Integrarea LevelPlay în ramură separată; poarta de vârstă; „Raportează o reclamă”; CMP pentru consimțământ.
- Salvarea rundei în curs la ieșirea din aplicație.
- Notificări locale (necesită pachet; opt-in, după al 3-lea Titan al Zilei, oprite implicit pentru minori).
- Mai multe limbi (ES, PT, DE, FR).
- Mod de captură pentru videoclipuri de reclamă (tăieturi mari, boss-uri) și un test ieftin de CPI înainte de meta grea.
- Testul de banner (doar pe ecranul de rezultate, după luna 2, doar dacă D7 e stabil).

## 16. Cercetarea: concluzii și surse

**Limită importantă:** din mediul de lucru nu s-au putut deschide direct paginile (DNS/proxy). Toate cifrele vin din rezumatele motoarelor de căutare. Sunt aproape de sursa primară doar: politicile Google Play / AdMob / Apple, studiul de caz AdMob despre Block Blast (+8% retenție), lucrarea Duolingo KDD 2020 și D30 de 16,5% la Royal Match. Restul sunt estimări.

### 16.1 Pixel Flow și verii lui

- Puzzle-ul vine din spațiu limitat (5 locuri de așteptare), nu din țintire. Nivelele sunt fixe, deci eșecul se poate învăța. *(Deconstructor of Fun, „Pixel Flow: The Publisher's Dream”, 13.02.2026; Gamigion „Pixel Flow: Success Analysis”)*
- Ritmul tensiune–eliberare contează mai mult decât mecanica: nivel normal ~71 s, greu ~143 s, foarte greu ~180 s; ~26% din primele 100 sunt grele; după un vârf vine un nivel scurt (< 40 s). Clonele cu curbe plate au eșuat. *(Gamigion, „Difficulty curve is key”, „Why the Latecomers Could NOT Replicate Pixel Flow”)*
- Primul vârf de dificultate vine imediat după ce primești o unealtă (nivelul 18 → 19: de la 26 s la 165 s). *(Gamigion, „Interesting Tricks in Pixel Flow”)*
- ~198,4 mil. $ în primul an, ~34 mil. instalări, SUA = 32% din instalări și 66% din venit; Scopely a cumpărat majoritatea la o evaluare > 1 mld. $. *(PocketGamer.biz / AppMagic; Scopely newsroom)*
- Plângeri: nivele grele care par măsluite, interstițiale forțate după fiecare nivel (de la ~nivelul 20), nicio reclamă pentru ajutor, nivele reciclate după 350. Laude: ideea „deșteaptă, captivantă”, nivele noi frecvent. Note: ~4,7 iOS, ~4,5 Google Play. *(recenzii App Store / Google Play; marlvel.ai)*
- Clonele care doar schimbă tema rămân mici (Yarn Loop ~10 mil. $); diferențierea prin fizică iese în evidență (Sand Loop ~15 mil. $). *(Gamigion, „What Pixel Flow-like games are worth your attention”)*

### 16.2 Reclame

- Block Blast: ~584 mii $/zi din reclame, IAP aproape zero; o singură revenire cu recompensă pe joc; ~25.000 de teste A/B în 2025, axate pe retenție; +8% retenție după reproiectarea reclamelor. *(Udonis, Balancy, studiu de caz AdMob)*
- Cea mai mare plângere la Block Blast: frecvența reclamelor și imposibilitatea de a le elimina; în 2026 a lansat Block Blast!+ pe Apple Arcade, fără reclame (3.09.2026). *(iphoneincanada.ca, mobilegamer.biz)*
- Plasamentele contextuale: 38,1% implicare vs 23,8% între nivele. *(Unity Mobile Growth & Monetization Report 2024)*
- Revenirea e acceptată cu atât mai ușor cu cât e mai mult progres în joc; multiplicatorul de recompensă e cel mai comun plasament în puzzle. *(Unity/ironSource, „Top 5 rewarded video placements for puzzle games”)*
- Costul interstițialelor: ZiMAD +18% ARPDAU pentru −2,5% D7; > 1 la 2 minute → până la −20% D7 (neconfirmat); primul interstițial mutat de la nivelul 3 la 8 a îmbunătățit și retenția, și ARPU. *(Unity; AppsFlyer via Advergize; Unity best practices)*
- eCPM: recompensă ~10–20 $, interstițial ~10 $, banner ~0,3–2,5 $ (SUA). *(Appodeal Q4 2024; Playio 2026)*
- 84% ar dezinstala un joc din cauza unei experiențe proaste cu reclamele. *(AppHarbr via GamesBeat)*
- Timp fără reclame ca recompensă: modelul Spotify „Sponsored Session”. *(adjoe; RevenueCat)*
- Politici: Google Play 9857753 (închidere ≤ 15 s, fără reclame la început de nivel sau în joc), Families 12918983 (≤ 5 s inclusiv cu recompensă), AdMob 7313578 și 9884467 (recompensa afișată înainte, opt-in separat), Apple 2.5.18 (închidere vizibilă, raportare), COPPA (conformare din 22.04.2026), DSA ghid minori (iulie 2025, serii oprite implicit pentru minori), Digital Fairness Act (propunere T4 2026).

### 16.3 Retenție

- Mediana pieței: ~22% D1, < 4% D7, ~0,8% D30; top 25% > 30% D1, top 10% ~40%. *(GameAnalytics 2026)*
- Puzzle e cel mai bun gen la retenție pe termen mediu și lung; Royal Match 16,5% D30. *(GameAnalytics; App Annie via PocketGamer.biz)*
- Block Blast combină modul infinit cu un strat vizibil de progres (Adventure). *(fișele din magazine; analize)*
- Seria Duolingo: cel mai mare motor de creștere, iertătoare, cu „înghețări” câștigate dinainte; notificări optimizate: +2% retenție la utilizatori noi. *(Lenny's Podcast; Yancey & Settles, KDD 2020)*
- Provocarea zilnică cu sămânță comună (Spelunky) și rezultatul distribuibil fără spoilere (Wordle: de la < 1.000 la ~2 mil. de jucători în câteva săptămâni). *(Game Developer; TechCrunch)*
- Jucătorii care văd o reclamă cu recompensă în prima săptămână au 53–68% D30 vs 13% (autoselecție, nu cauzalitate). *(ironSource/Unity)*

### 16.4 Simulatorul nostru

Simulatorul e în `Tools/Sim` (itemul 1, făcut): o consolă dotnet care compilează direct `Assets/Titan/Scripts/Core`, deci orice schimbare din `TitanConfig` sau din reguli se vede fără altă editare. Roboții (`Tools/Sim/Bots.cs`) văd doar numere printr-un `BotView` și răspund cu indexul tunului de atins; nu pot ajunge la joc, la ajutoare sau la reclame (test automat). Fiecare joc i are sămânța `SeededRandom.Derive(sămânțaMaster, i)`, aceeași pentru toți roboții, deci rezultatele sunt reproductibile pe orice mașină.

Comanda: `dotnet run -c Release --project Tools/Sim -- --games 300 --preset current|v2 [--set Cheie=Valoare ...] [--seed S] [--static]`. Moduri de măsurare care nu sunt rulări de echilibru: `--revive` (10.5) și `--ftue` (titanul 1 scenarizat, 8.1).

Rezultatele brute ale cercetării inițiale (harness-ul vechi, cu steaguri prin variabile de mediu) rămân în `docs/research/harness/` (`out_*.txt`, `core_experiments.diff`); codul vechi e în istoricul git. Verificare: `Tools/Sim` cu presetul `current` (200 de jocuri, sămânța 1) dă Random 1,99 / Smart 2,44 titani, 100% pierderi „locuri pline”, față de 1,98 / 2,34 în harness-ul vechi (diferența vine doar din alt generator de numere aleatoare).

Decizii luate la mutare:
- Robotul **Expert** (rollout-uri de 12 s) nu a fost mutat: nu e cerut în ținte și avea nevoie de clonarea prin reflecție a jocului.
- **Smart** „nu umple ultimul loc”: un tun fără țintă e trimis doar dacă nimic nu trage sau dacă mai sunt ≥ 3 locuri libere (≥ 2 când toată tava e moartă). Când nimic nu trage, a aștepta nu ajută, așa că trimite oricum (exact ca harness-ul măsurat).
- „Timp blocat” = nimic nu trage și nu ai nicio atingere utilă (0 locuri libere, sau 1 loc liber și niciun tun din tavă cu culoare vizibilă) — aceeași definiție ca regula 4.2.9.
- De la itemul 7 raportul arată tăieturile curate per titan (și cota lor din bucăți), rata stelelor „Decapitare” și „Linie curată” din titanii doborâți și raportul Smart : Random al tăieturilor curate.
- De la itemul 4: roboții aleg doar tunuri pe care jocul le acceptă (`BotView.Tappable`, paza ultimului loc), Smart dă +60 etichetei TAIE! (`BotView.TrayCutShot`), iar raportul arată cota zidului din pierderi, marja mediană, „la mustață”, rolul fiecărui titan și intervalul timpilor medii pe titan. `--static` listează titanii n = 1–25 cu rolul și nivelul din ciclu.
