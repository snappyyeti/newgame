# Titanul din Cuburi

Un titan uriaș făcut din cuburi colorate pășește spre zidul tău. Jos ai 3 tunuri; atingi unul și el urcă pe zid, apoi trage singur în cuburile de culoarea lui. Rupi brațe și capete, doborî titanul, și vine următorul, în cicluri de câte 5 (al 4-lea e un sprint, al 5-lea un titan gigant). Infinit.

## Cum îl deschizi

1. Pe GitHub: **Code → Download ZIP**, apoi dezarhivezi într-un folder **nou**. Nu peste un proiect vechi.
2. Unity Hub → **Add** → **Add project from disk** → folderul dezarhivat → Unity `6000.6.3f1` (merge orice Unity 6).
3. La prima deschidere, `Assets/Titan/Editor/ProjectSetup.cs` deschide scena `Main`, setează portret, Direct3D 11 și input-ul clasic. Dacă te întreabă dacă repornește, apasă **Repornește**.
4. În **Game** alege o rezoluție portret (de exemplu 1080×2340) și apasă **Play**.

Control: atingi / dai click pe un tun de jos. Butonul de pauză e sus în dreapta.

## Regulile

1. **Titanul și ciclurile.** Titanul pășește spre zid; bara de sus arată cât de aproape e. Titanii vin în **cicluri de câte 5**: încălzire, construcție, împingere, **TITAN RAPID!** (un titan mic, eliberarea dinaintea vârfului) și **TITAN GIGANT!** (vârful ciclului, bonus ×2). Sus scrie „Ciclul c · Titanul #n”, cu 5 puncte (al 5-lea e o coroană) care se umplu pe măsură ce dobori titanii ciclului. Vezi tabelul de mai jos.
2. **Tunurile (tava-bandă).** Jos ai 3 tunuri, fiecare cu o culoare și o muniție (numărul de pe el). Când trimiți unul, un tun nou urcă imediat în locul lui. Tava nu primește niciodată 3 tunuri de aceeași culoare dacă mai există alte culori în pachet. Când pachetul titanului s-a golit, locul rămâne gol, cu un cerc închis și „pachet gol”.
3. **Linia de tragere.** Atingi un tun și el urcă pe zid, în primul din cele **5 locuri** (un al 6-lea doar cu „+1 Loc”, vezi Reclamele). Trage singur, cam 3 lovituri pe secundă, în cuburile **vizibile** de culoarea lui, până rămâne fără muniție. Atunci pleacă și eliberează locul. Eticheta de muniție a unui tun din tavă stă **sub** tun; a unui tun de pe zid, deasupra lui. Pe fața zidului, 5 pătrățele arată locurile: gol = doar contur, trage = culoarea tunului cu simbolul ei, așteaptă = gri cu „zzz”. Chenarul lor e alb, chihlimbar când 3 tunuri așteaptă, roșu pulsând la 4 sau mai multe.
4. **Așteptarea și Recuperarea.** Un tun fără cuburi vizibile de culoarea lui stă în locul lui și așteaptă (scrie „zzz”). Începe să tragă singur când apare culoarea lui. „zzz” înseamnă mereu că mai există cuburi de culoarea lui, ascunse în spate; câte culori sunt doar ascunse arată cipul „N culori ascunse ?” de sub bară. Când cade o bucată, muniția care nu mai are ce lovi pleacă odată cu ea: un tun de pe zid a cărui culoare a dispărut de tot zboară de pe zid cu **„RECUPERAT +puncte”**; din tunurile din tavă se scade muniția în plus (un „-N” zboară din bucată spre tun și numărul scade sub ochii tăi; un tun ajuns la 0 dispare și urcă altul în locul lui); din pachet se scade pe nevăzute. Pe zid nu rămâne niciodată un tun mort.
5. **Tăieturile (combo).** Brațele stau pe umeri, iar capul pe gât. Umerii și gâtul sunt **încheieturile**: au culoarea lor și o dungă albă („cusătura”). Brațele și capul nu cad singure: tunul de culoarea brațului îl „mănâncă” de la mână spre umăr, fără să-l rupă. Bucata se rupe doar când distrugi încheietura (sau corpul de sub ea), cade cu fizică și toate cuburile din ea îți dau puncte. Mâna are culoarea brațului (o recunoști după rama închisă). Eticheta galbenă **TAIE!** apare deasupra unui tun din tavă doar dacă muniția **lui**, trimis acum, ajunge să rupă o bucată (jocul simulează țintirea pe o copie a titanului, deci eticheta nu minte). Dacă tunul singur nu ajunge, dar mai ai pe zid sau în tavă alt tun de aceeași culoare, apare un contur gol „taie în N” (N = câte lovituri de culoarea asta mai trebuie). Cât timp brațul sau capul mai are cel puțin 80% din cuburile lui, cusătura încheieturii e **aurie**: tăiat acum, iese o **tăietură curată** (vezi scorul).
6. **Tăietura cumpără timp.** Fiecare bucată căzută împinge titanul înapoi cu 0,03 × cuburile ei (între 0,3 și 1,5 unități, niciodată mai departe de unde a pornit) și îi întârzie pasul următor cu 0,5 + 0,01 × cuburi secunde (cel mult 1 s). Titanul se clatină înapoi. Bucata care îl doboară de tot nu-l mai împinge.
7. **„La mustață”.** Dacă dobori titanul când mai are sub 15% din drum până la zid, bonusul de doborâre se înmulțește cu 1,5: **„LA MUSTAȚĂ! x1.5”**. Când titanul a trecut de 75% din drum, marginea ecranului pulsează roșu și se aude o bătaie de inimă.
8. **Paza ultimului loc.** Jocul refuză atingerea care ar pune pe ultimul loc liber un tun fără țintă, când niciun tun de pe zid nu trage (ai fi pierdut pe loc). Tunul se scutură și scrie **„Ar bloca ultimul loc! Alege o culoare vizibilă.”** O atingere refuzată nu deschide niciodată nimic altceva.
9. **Pierzi** când titanul atinge zidul, când toate locurile (5, sau 6 cu „+1 Loc”) sunt pline cu tunuri care n-au în ce trage, sau la **„BLOCAT!”**: niciun tun de pe zid nu are țintă și nu ai nicio mutare utilă (0 locuri libere, sau 1 loc liber și niciun tun din tavă cu o culoare vizibilă) timp de 1,5 s.

### Primele runde (tutorialul)

- La primele **3 runde** aplicația intră direct în joc (fără meniu), iar **titanul 1 e unul pregătit**: picioare, umeri, gât și ochi galbeni, trunchi roșu, brațe și cap albastre. Primul tun din tavă (stânga) e **galben** și are exact câtă muniție îi trebuie ca să taie gâtul și ambii umeri: o atingere și, în ~2 s, cad capul și ambele brațe. Celelalte două tunuri vin normal din pachet, iar muniția totală rămâne egală cu cuburile.
- **Titanul 1 nu pășește până la prima ta atingere.** Titanii 2+ sunt cei obișnuiți.
- **4 indicii**, câte una odată, fiecare o singură dată pe telefon, fără să blocheze nimic și fără să acopere tava: „Atinge tunul galben” (inel în jurul tunului, până îl atingi); „zzz = culoarea e ascunsă în spate. Va trage când apare.” (prima dată când un tun de pe zid așteaptă 1 s; 2,5 s); „Nu umple toate cele 5 locuri!” (o săgeată spre pătrățelele locurilor, prima dată când 3 tunuri așteaptă; 2,5 s); la primul final de rundă, pe ecranul de rezultate, cauza într-un rând și „Sfat: lasă mereu 1-2 locuri libere.”
- Nicio reclamă și nicio ofertă de reclamă înainte de 3 runde **și** 10 minute de joc.

### Scorul (un singur model de multiplicatori)

- **10** puncte pe cub lovit.
- **Bucată ruptă:** 25 pe cub × **2 dacă e curată** × **lanțul**. Curată = bucata a păstrat cel puțin 80% din cuburile părții ei (brațul sau capul): „TĂIETURĂ CURATĂ!”, iar la cap „DECAPITARE!”; altfel „BUCATĂ RUPTĂ!”. **Lanțul:** bucățile rupte la cel mult 4 s una după alta urcă lanțul (×2, ×3, … cel mult ×5, „COMBO xN”); lângă scor, un cerc de puncte care se golește arată cât mai ai din cele 4 s. Lanțul o ia de la capăt la fiecare titan. Exemplu: un braț întreg de 40 de cuburi, al treilea din lanț = 40 × 25 × 2 × 3 = 6.000.
- **5** pe fiecare lovitură recuperată (de pe zid sau din tavă).
- **Doborâre:** 200 × numărul titanului (×2 la titanul gigant, ×1,5 „la mustață”), plus 2 pe fiecare lovitură rămasă în tunuri pe zid (fără multiplicatori).
- Pe ecran e un singur mesaj mare de bucată odată (unul nou se adaugă la cel de pe ecran); mesajul crește cu lanțul (cel mult ×1,6) și e auriu când multiplicatorul e cel puțin 4.

### Nota titanului (3 stele)

Fiecare titan doborât primește până la 3 stele: **Doborât**; **Decapitare** (a căzut o bucată de cap curată); **Linie curată** (n-ai avut niciodată mai mult de **4** tunuri pe zid în timpul titanului). Stelele nu dau puncte. Sub bara de pericol, 3 puncte arată nota pe loc: al doilea devine auriu la decapitare, al treilea se face gri în clipa în care pui al 5-lea tun pe zid. La doborâre stelele apar una câte una; una ratată e gri și are motivul scris („Capul nu a căzut întreg” / „Ai avut 5 tunuri pe zid”).

### Recordul și punctele de salvare

- **Recordul** e cel cu punctajul nou (cheia `titan_best_v2`). Scorurile vechi nu se mai compară: recordul vechi apare doar ca „Record vechi (alt punctaj)” până faci primul scor nou.
- În rundă, de la 80% din record: **„Record în N”** auriu sub scor, iar scorul pulsează (static cu „Efecte reduse”). Când îl depășești: **„RECORD NOU!”**, fanfară și confetti, o singură dată pe rundă. La un titan la care n-ai ajuns niciodată: **„TITAN NOU: #n”**.
- **Puncte de salvare gratuite** (niciodată cu reclame): când ajungi la titanul 6, 11, 16, … apare „Punct de salvare: titanul #n”. Fiecare rundă are o sămânță, deci titanul n are aceeași formă când îl reiei (același titan; tunurile depind de cum joci).
- **La final:** motivul, scorul, „RECORD NOU!” sau „Ai fost la X% din record!” (doar de la 85% în sus) sau „Record: N”, apoi „Titani · Stele · Bucăți rupte”, cea mai bună tăietură, combo-ul maxim și un singur obiectiv următor („Încă un titan până la punctul de salvare #n”, altfel „Cel mai departe: titanul #n”). Butoane: **Joacă din nou** (de unde ai pornit runda; de la titanul 1 cu un titan nou), **Reia ciclul (titanul #n)** (doar dacă ai un punct de salvare; scorul revine la cel din momentul salvării) și **Joc nou** (de la titanul 1; punctul de salvare se șterge). Repornirea refolosește arena, deci e aproape instantă.
- **La pornire**, după cel puțin 3 runde, apare meniul dacă ai un punct de salvare, dacă n-ai terminat încă Titanul Zilei de azi sau dacă ai un mesaj despre serie: „Continuă (titanul #n)” (dacă ai punct de salvare), „Joc nou” / „Joacă” și „Titanul Zilei”. Altfel jocul pornește direct.
- **Profilul** se salvează în `titan_profile.json` (în `Application.persistentDataPath`), cu o copie în PlayerPrefs: recordul, cel mai departe titan, rundele, timpul jucat, punctul de salvare, setările și un jurnal local de 300 de evenimente de joc (fără date despre telefon). În editor, **F9** (pe telefon: atingere cu 3 degete pe scor) copiază jurnalul în clipboard: „Jurnal copiat”.

### Titanul Zilei și seria

- **Titanul Zilei**: în fiecare zi (după ceasul telefonului) toți primesc aceiași **3 titani** (nivelurile 3, 4 și un titan gigant 5, cu ritmul titanilor 6–8). Dobori titanul 3 = **ai câștigat** („Completed”). Butonul „Titanul Zilei” e în meniu și pe ecranul de rezultate (după primele 3 runde), cu starea (neîncercat / în curs: k/3 / k/3) și numărătoarea reală până la miezul nopții.
- **Prima încercare e cea oficială** și începe la **prima atingere**: până atunci titanul stă, iar „Înapoi” nu o consumă. Progresul se salvează după fiecare titan doborât; dacă aplicația se oprește, la pornire alegi „Continuă încercarea oficială” (de la titanul următor, cu scorul de atunci). Se încheie la finalul rundei, cu „Închei” → „Închei încercarea oficială?” sau la miezul nopții. Apoi: **ANTRENAMENT** oricât (se ține doar cel mai bun scor de antrenament al zilei).
- **Fără reclame și fără ajutoare** în nicio rundă a Titanului Zilei (nici antrenamentul), nici înainte, nici după: „Încercarea de azi e fără ajutoare și fără reclame — aceeași provocare pentru toți.” Rundele zilnice nu schimbă recordul, cel mai departe titan sau punctul de salvare.
- **Corectitudine:** pentru fiecare zi, jocul verifică dinainte (cu un robot simplu, 40 de încercări, la fel pe orice telefon) că ziua poate fi trecută: dintre 4 variante („sare” 0–3) alege prima în care robotul trece de toți 3 titanii în ≥ 35% din încercări și de primul în ≥ 90%; dacă niciuna nu trece (0,5% din zile), ziua folosește titani mai mici (2, 3, 4).
- **Panoul zilei**: „Titanul Zilei #ziua”, 3 pătrate (verde cu bifă = doborât, roșu cu X = titanul pe care s-a terminat, gri = neatins), scorul, timpul (m:ss), bucățile rupte și **„Copiază rezultatul”**, care pune în clipboard 4 rânduri fără nimic despre tablă: „Titanul din Cuburi · Ziua #288”, 🟩🟩🟥, „Scor 41.200 · 2:47 · 9 bucăți rupte” și linkul magazinului (deocamdată un înlocuitor). Apoi „Copiat! Lipește-l unde vrei.”
- **Seria**: +1 pentru fiecare zi în care termini încercarea oficială (oricum ar ieși). **Scuturi**: primul Titan al Zilei îți dă 1, fiecare a 7-a zi de serie încă 1, cel mult 2; se folosesc singure pentru zilele ratate („Un scut ți-a păstrat seria.”). Fără scuturi destule, seria o ia de la capăt: „Bine ai revenit! Începe o serie nouă azi.” Rămâne „Cea mai lungă serie: N”. Seria apare doar pe panoul zilei și în meniu.

### Reclamele (simulate deocamdată)

Reclamele sunt **simulate**: un ecran „RECLAMĂ SIMULATĂ — Înlocuitor pentru LevelPlay” (6 s cu recompensă, 4 s între runde). Nu e nicio rețea de reclame și niciun pachet nou; LevelPlay vine mai târziu, separat. Toate regulile sunt în `docs/DESIGN.md`, secțiunile 10 și 10.5. Pe scurt:

- **Nimic înainte de 3 runde și 10 minute de joc**, nimic în Titanul Zilei (nici înainte, nici după, nici la antrenament), niciun banner. În timpul rundei nu pornește nicio reclamă: singura ofertă e butonul „+1 Loc”, pe care îl apeși doar dacă vrei.
- **„A doua suflare”** (o dată pe rundă): când pierzi după ce ai doborât un titan sau ai distrus ≥ 15% din cel curent, după 0,6 s apare cardul „A DOUA SUFLARE” în locul ecranului de final (titanul, cât la sută e distrus, scorul). Dacă accepți: tunurile adormite de pe zid se întorc în pachet, titanul dă înapoi un sfert din drum și stă 2 s, iar dacă niciun tun din tavă nu are culoare vizibilă, tava se reface din culori vizibile. Toate stricăciunile titanului rămân. Prima dată în viață e gratis („Revino gratis”). Dacă refuzi, mergi la rezultate și **nu urmează nicio reclamă**.
- **„+1 Loc”**: un buton auriu „+1 LOC” apare pe zid (dreapta, sus) când ai cel puțin 4 tunuri pe zid și 3 dintre ele așteaptă, 1 s la rând. Îl apeși tu; jocul se oprește și vezi cardul. Primești un al 6-lea loc (cu margine aurie) până dobori titanul. O dată pe titan, de cel mult 2 ori pe rundă, din titanul 2; prima dată e gratis. După un refuz butonul dispare 20 s; după două, nu mai apare nici pe titanul următor. **O atingere pe tavă nu deschide niciodată o ofertă.**
- **Între runde** (doar când apeși „Joacă din nou”, „Reia ciclul” sau „Joc nou”): uneori o reclamă, și atunci sub butoane scrie dinainte „Reclamă înainte de rundă”. Niciodată la primul final din sesiune, în prima sesiune, după un refuz al revenirii, după un record nou sau un titan nou, după o rundă sub 45 s; cel mult o dată la 4 minute (presetarea „light”), cel mult 5 pe sesiune și 15 pe zi.
- **„Fără reclame 30 min”** (pe ecranul de rezultate și în meniul de pauză, niciodată în Titanul Zilei): o reclamă acum = 30 de minute fără reclame între runde (cel mult 4 pe zi). Cât ține, scrie „Fără reclame încă mm:ss”. Reclamele opționale rămân la alegerea ta.
- Cât timp un card sau o reclamă e pe ecran, jocul stă pe loc (fără sunet, arena întunecată). În editor, **F10** face ca toate reclamele simulate să eșueze (pentru test: nimic nu trebuie să rămână blocat).

### Culori, pauză, setări și limbă

- **Fiecare culoare are un model pe cuburi și un simbol** (pentru daltonism): roșu = plin / cerc, albastru = dungi orizontale / pătrat, galben = puncte / triunghi, verde = diagonale / romb, violet = tablă de șah / cruce, portocaliu = grilă / stea. Modelele sunt texturi mici desenate de joc pe fiecare cub; culoarea medie a cubului rămâne exact culoarea lui. Simbolul apare pe eticheta de muniție a fiecărui tun (tavă și zid), pe pătrățelele locurilor și pe cipul culorilor ascunse (care scrie atunci „ascunse:” urmat de simbolurile lor).
- **Pauza:** butonul din dreapta sus, sau singură când aplicația iese în fundal ori pierde focusul (și în editor, când dai click în afara ferestrei Game). În pauză nimic nu se mișcă și nu se aude nimic; jocul pornește din nou doar când apeși **Continuă** (fără numărătoare).
- **Meniul de pauză:** Continuă · Sunet · Vibrații · Efecte reduse · Simboluri culori · Limba (Română / English) · „Fără reclame 30 min” (doar dacă e disponibilă; cât ține pauza de reclame, scrie cât a mai rămas) · **Joc nou** (cu confirmare: runda de acum se încheie aici, scorul ei contează la record, punctul de salvare rămâne; începe imediat de la titanul 1, fără reclamă). În Titanul Zilei meniul are doar Continuă și setările (ai deja „Închei” / „Ieși”).
- **Setările** (în profil): Sunet (pornit), Vibrații (pornit; doar pe telefon), Efecte reduse (oprit), Simboluri culori (pornit), Limba (a telefonului: română pe un telefon în română, altfel engleză, până alegi tu). **Simboluri culori** oprit ascunde simbolurile de pe etichete și de pe cip și face modelele de pe cuburi mai discrete (±15% în loc de ±30%); pătrățelele locurilor își păstrează simbolurile. **Efecte reduse** oprește tremuratul camerei, opririle scurte, „lovitura” camerei și încetinirea, iar marginile care pulsau rămân fixe.
- **Senzația:** o bucată de 12+ cuburi oprește jocul o clipă (0,08 s) și camera „lovește” (FOV −3%); un cap sau o tăietură curată de 40+ cuburi aduce și o încetinire scurtă (cel mult 0,15 s); doborârea unui titan oprește jocul 0,1 s (din cele 0,12 cerute: totalul opririlor e cel mult 0,1 s în orice secundă). **Nicio oprire și nicio încetinire în ultimele 15% din drumul titanului.** Sunetul loviturilor urcă în ton cu seria fiecărui loc (până la ×1,6; seria se pierde după 0,5 s fără lovitură). Telefonul vibrează la bucăți de 40+ cuburi, la un titan gigant doborât și „la mustață”, cel mult o dată la 0,6 s.
- **Taste în editor:** **F7** arată paleta în gri (verifici că fiecare tun se recunoaște după simbol), **F8** (pe telefon: 3 degete ținute 1 s) arată fps, p95 al ultimelor 300 de cadre, obiectele active și corpurile rigide. La pornire, în editor, consola spune dacă fontul IMGUI are literele ă â î ș ț.

### Alegeri făcute pentru prototip

- **„Vizibil”** = cubul din față al fiecărei coloane (ce vezi tu). Când îl spargi, se vede cel din spate, care poate avea altă culoare. Trunchiul are o culoare ascunsă înăuntru.
- **Ordinea țintelor:** fiecare tun trage în cubul vizibil de culoarea lui cel mai **depărtat de corp** (câți pași de la cub la cub îl despart de picioare și trunchi), apoi în cel mai înalt, apoi în cel mai apropiat de el. Așa brațele și capul se mănâncă de la vârf spre încheietură. (Vechea ordine, „cel mai înalt întâi”, rămâne în cod ca `TargetOrder.HighestFirst`, doar pentru comparații.)
- **Culorile titanului:** brațul nu are niciodată culoarea umerilor, a gâtului sau a capului, ca tunul brațului să nu poată rupe nimic singur.
- **Recuperarea** socotește întâi muniția deja pusă pe zid: ce trimiți pe zid acoperă primele cuburi ale culorii, iar din tavă și din pachet pleacă doar ce rămâne în plus. Din pachet se scade întâi din tunurile cele mai mici. Muniția recuperată din pachet nu dă puncte (nu o vezi).
- **Ochii** sunt doar pe fața capului. Dacă un tun de culoarea capului mănâncă tot capul din jurul lor, ochii (cu cuburile ascunse în spatele lor) cad ca o bucată mică.
- **Ce se rupe:** corpul și picioarele stau pe pământ chiar dacă sunt ciuruite. Se rup doar brațele și capul. Altfel, tunul de culoarea picioarelor tăia șoldurile și cădea tot titanul în câteva secunde.
- **Muniția** fiecărui titan acoperă exact cuburile lui, împărțită în tunuri de 10–30. În 65% din cazuri, tunul nou are o culoare care se vede acum, iar în rest e la întâmplare. Nu există alt „regizor”.

## Titanii

Titanul #n e construit la un „nivel” L ales de ciclul de 5 (c = ciclul, de la 0): încălzire L = min(1+c, 5), construcție min(2+c, 6), împingere min(3+c, 6), **sprint** L1 (L2 din ciclul 3), **gigant** min(4+c, 8). Timpul până la zid = max(12 s, (4 + s/cub × cuburi) × max(0,6; 0,98^(n−1))), cu 0,11 s/cub la titanii 1–3 (ritm de învățare) și 0,075 s/cub după. Medii pe 200 de titani:

| Titan | Rol | L | Cuburi | Culori | Timp până la zid |
|---|---|---|---|---|---|
| 1 | încălzire | 1 | 142 | 3 | 20 s |
| 2 | construcție | 2 | 160 | 3 | 21 s |
| 3 | împingere | 3 | 206 | 4 | 26 s |
| 4 | **sprint** | 1 | 142 | 3 | 14 s |
| 5 | **gigant** | 4 | 477 | 4 | 37 s |
| 6 | încălzire | 2 | 160 | 3 | 14 s |
| 8 | împingere | 4 | 477 | 4 | 35 s |
| 9 | **sprint** | 1 | 142 | 3 | 12 s |
| 10 | **gigant** | 5 | 516 | 5 | 36 s |
| 15 | **gigant** | 6 | 561 | 5 | 35 s |
| 20 | **gigant** | 7 | 996 | 5–6 | 54 s |
| 25 | **gigant** | 8 | 996 | 5–6 | 48 s |

Tabelul complet (n = 1–25): `dotnet run -c Release --project Tools/Sim -- --static --games 1`. Reculul de la tăieturi lungește timpul real.

## Simulare (echilibrul jocului)

Simulatorul e în `Tools/Sim`: joacă nucleul real al jocului (`Assets/Titan/Scripts/Core`, compilat direct, fără copii) cu trei roboți — **Random** (atinge orice tun), **Greedy** (alege culori vizibile), **Smart** (urmărește încheieturile, nu umple zidul cu tunuri care așteaptă). Roboții ating un tun la 0,6–1,6 s și **nu folosesc niciodată ajutoarele din reclame**.

```
dotnet run -c Release --project Tools/Sim -- --games 300 --preset current
dotnet run -c Release --project Tools/Sim -- --games 300 --preset v2 --set ApproachSecondsPerCube=0.08
```

Presetul `current` e `TitanConfig` așa cum e acum; `v2` e tabelul din `docs/DESIGN.md` 4.3 (azi identic cu `current`: valorile v2 sunt cele implicite din cod). `--set Cheie=Valoare` schimbă orice câmp din `TitanConfig`.

`--ftue` pune titanul pregătit al tutorialului la titanul 1 și arată cât de des e doborât și cât durează până cade prima bucată după prima atingere (nu e o rulare de echilibru).

`--revive` măsoară doar cât valorează „A doua suflare” (o revenire la prima pierdere, cu împingerile 0,25 / 0,40 / 0,60); nu e o rulare de echilibru, iar fără acest flag ajutoarele sunt mereu oprite.

Raportul are metricile din `docs/DESIGN.md`, secțiunea 12.1 (titani doborâți, durata rundei, de ce se pierde, timpi per titan, decizii, bucăți, tunuri moarte) și raportul Smart : Random. Țintele și cifrele de azi sunt în `docs/DESIGN.md` (secțiunile 3 și 12.1), nu aici, ca să nu rămână vechi.

## Teste

```
dotnet test Tools/CoreTests          # logica + testul de echilibru (50 de jocuri/robot, câteva secunde)
dotnet build Tools/UnityCheck        # verifică că scripturile de joc compilează cu Unity
dotnet build Tools/EditorCheck       # verifică scripturile de editor
```

Logica (`Assets/Titan/Scripts/Core`) nu depinde de Unity. Testele stau în `Tools/CoreTests`, în afara folderului `Assets`, ca Unity să nu le compileze. Testul de echilibru (`BalanceTests`) rulează mereu; cu `SKIP_BALANCE=1` e sărit. Verificarea Titanului Zilei rulează implicit pe fiecare a 10-a zi din zilele 0–730; cu `DAILY_SWEEP=1` le verifică pe toate (~40 s).

## Structură

```
Assets/Titan/Scripts/Core   logica pură: titanul din voxeli, rupturile, tunurile, linia de tragere, simularea,
                            tabelul de texte RO/EN (Loc), modelele și simbolurile culorilor, regulile de „senzație”
Assets/Titan/Scripts/Core/Ads  regulile reclamelor (guvernatorul, pauza de reclame, cohortele), fără Unity
Assets/Titan/Scripts/Game   Unity: arena, titanul, tunurile, proiectilele, bucățile cu fizică, sunete, HUD
Assets/Titan/Scripts/Game/Ads  reclama simulată (înlocuitor LevelPlay), cardurile, contractul de pauză
Assets/Titan/Editor         pregătirea automată a proiectului în editor
Tools/CoreTests             testele NUnit ale logicii și testul de echilibru, rulate cu dotnet
Tools/Sim                   simulatorul de echilibru (roboți care joacă nucleul real)
Tools/UnityCheck            verificare de compilare a scripturilor de joc, fără Unity
Tools/EditorCheck           verificare de compilare a scripturilor de editor, fără Unity
```
