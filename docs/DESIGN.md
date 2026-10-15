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
5. **Ceasul titanului.** Timpul până la zid = max(12 s, (4 + 0,075 × cuburi) × max(0,6; 0,98^(n−1))), unde n = numărul titanului. Titanii 1–3 folosesc 0,11 s/cub (ritm de tutorial). *Valorile finale se reglează în sprint pentru țintele din 12.1.*
6. **Reculul la tăiere.** Fiecare bucată căzută împinge titanul înapoi cu clamp(0,03 × cuburi; 0,3; 1,5) unități și îi întârzie pasul următor cu min(1,0; 0,5 + 0,01 × cuburi) s. Tăietura cumpără timp — e a doua miză a deciziei de tăiere.
7. **„La mustață”.** Dacă dobori titanul când mai are sub 15% din drum, bonusul de doborâre × 1,5 și mesajul **„LA MUSTAȚĂ! ×1,5”**.
8. **Paza ultimului loc.** Jocul refuză o atingere care ar pune al 5-lea tun fără țintă pe zid (sigur ar pierde), cu mesajul **„Ar bloca ultimul loc! Alege o culoare vizibilă.”** Tunul din tavă se scutură. Nu se deschide niciodată o reclamă din această atingere.
9. **Blocat.** Dacă niciun tun de pe zid nu are țintă și nu mai ai nicio mutare posibilă (0 locuri libere, sau 1 loc liber și niciun tun din tavă nu are țintă) timp de 1,5 s, runda se termină: **„BLOCAT! Niciun tun nu mai are în ce trage.”**
10. **Pierzi** când titanul atinge zidul, când toate cele 5 locuri au tunuri fără țintă, sau la „Blocat”.

### 4.3 Valorile de configurare (TitanConfig v2)

| Parametru | Acum | v2 |
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
| UseCycles | — | true |

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
| **Valoare** | Continui *același* titan cu toate stricăciunile. Tunurile care așteaptă pe zid se întorc în pachet (muniția nu se pierde). Titanul dă înapoi cu 40% din drum (valoare finală aleasă în simulator între 0,25 / 0,40 / 0,60, astfel încât Smart să câștige ≥ 1 titan în ≥ 50% din reveniri) și stă 2 s. Dacă niciun tun din tavă nu are țintă, tava se reface din culori vizibile. Jocul revenit nu e niciodată în starea „blocat” (test). |
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

| Măsură | Țintă | Măsurat azi (bucla v2 completă, out_C1) |
|---|---|---|
| Pierdere la titanul 1 | Random < 2%, Smart < 1% | 6% / 1% |
| Runde < 20 s | Random ≤ 1%, Smart 0% | 7% / 2% |
| Runde < 30 s (Random) | ≤ 3% | 9% |
| Raport titani Smart : Random | 1,7–2,3× | 1,74× |
| Cota pierderilor la zid (Random) | 35–55% | 71% |
| Cota pierderilor la zid (Smart) | 40–70% | 90% |
| Timp per titan (median), titanii 1–16 | 10–40 s; sprint ≤ 16 s; boss ≤ 40 s | 12–37 s |
| Rundă mediană | Random 120–200 s; Smart 240–420 s | 164 s / 283 s |
| Smart atinge limita de 30 min | < 2% | — |
| Tunuri moarte pe zid | 0 (invariant) | 0,00 |
| Decizii cu ≥ 2 opțiuni vizibile (Smart) | ≥ 65% | 77% |
| Decizii fără alegere | ≤ 25% | 16–20% |
| Timp cu zidul plin | ≤ 25% | 26–32% |
| Marja la doborâre (mediană) | 0,25–0,45 | 0,21–0,47 |
| Timp în pericol ≥ 75% | 4–10% | 5–6% |
| Bucăți din lovituri pe încheietură | ≥ 65% | 76–82% |
| Bucăți per titan | 2,5–3,5 | ~3 |
| Tăieturi curate per titan, Smart vs Random | ≥ 1,5× | de măsurat |
| Steaua „Linie curată” pentru Smart | 25–40% din titani | de măsurat |
| Titanul 1 scenarizat | ≥ 3 bucăți în 4 s după prima atingere | test |
| Revenire | Smart câștigă ≥ 1 titan în ≥ 50% din reveniri | de măsurat |
| Interstițiale | 20 de runde de 40 s la rând → 0; ≤ 3,5 la 10 min | test |
| Titanul Zilei | fiecare zi 0–730: Greedy trece ≥ 35%, primul titan ≥ 90% | test |

**Testul de echilibru din CI** (`Tools/CoreTests/BalanceTests.cs`, categoria `Balance`, 50 de jocuri/robot, sămânța 20261015, ajutoare oprite) verifică azi doar praguri grosiere pentru bucla actuală; itemul 4 (bucla v2) le strânge la țintele de mai sus:
- nicio rundă nu atinge limita de 30 min (≤ 2%);
- media titanilor doborâți: Random ≥ 1,4, Smart ≥ 1,8; Smart ≥ Random;
- pierdere la titanul 1: Random ≤ 30%, Smart ≤ 25%;
- bucăți per titan între 1 și 8, iar loviturile pe încheietură taie bucăți;
- simularea e deterministă (aceeași sămânță → același rezultat).
Separat, mereu (și cu `SKIP_BALANCE=1`): codul roboților nu atinge ajutoare sau reclame, `BotView` expune doar numere, `VisibleColorBias` = 0,65 în cod și în toate preseturile.

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

Comanda: `dotnet run -c Release --project Tools/Sim -- --games 300 --preset current|v2 [--set Cheie=Valoare ...] [--seed S] [--static]`.

Rezultatele brute ale cercetării inițiale (harness-ul vechi, cu steaguri prin variabile de mediu) rămân în `docs/research/harness/` (`out_*.txt`, `core_experiments.diff`); codul vechi e în istoricul git. Verificare: `Tools/Sim` cu presetul `current` (200 de jocuri, sămânța 1) dă Random 1,99 / Smart 2,44 titani, 100% pierderi „locuri pline”, față de 1,98 / 2,34 în harness-ul vechi (diferența vine doar din alt generator de numere aleatoare).

Decizii luate la mutare:
- Robotul **Expert** (rollout-uri de 12 s) nu a fost mutat: nu e cerut în ținte și avea nevoie de clonarea prin reflecție a jocului.
- **Smart** „nu umple ultimul loc”: un tun fără țintă e trimis doar dacă nimic nu trage sau dacă mai sunt ≥ 3 locuri libere (≥ 2 când toată tava e moartă). Când nimic nu trage, a aștepta nu ajută, așa că trimite oricum (exact ca harness-ul măsurat).
- „Timp blocat” = nimic nu trage și nu ai nicio atingere utilă (0 locuri libere, sau 1 loc liber și niciun tun din tavă cu culoare vizibilă) — aceeași definiție ca regula 4.2.9.
- Tăieturile curate și stelele apar în raport abia cu itemul 7 (scorul).
