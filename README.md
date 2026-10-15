# Titanul din Cuburi

Un titan uriaș făcut din cuburi colorate pășește spre zidul tău. Jos ai 3 tunuri; atingi unul și el urcă pe zid, apoi trage singur în cuburile de culoarea lui. Rupi brațe și capete, doborî titanul, și vine altul, mai mare. Infinit.

## Cum îl deschizi

1. Pe GitHub: **Code → Download ZIP**, apoi dezarhivezi într-un folder **nou**. Nu peste un proiect vechi.
2. Unity Hub → **Add** → **Add project from disk** → folderul dezarhivat → Unity `6000.6.3f1` (merge orice Unity 6).
3. La prima deschidere, `Assets/Titan/Editor/ProjectSetup.cs` deschide scena `Main`, setează portret, Direct3D 11 și input-ul clasic. Dacă te întreabă dacă repornește, apasă **Repornește**.
4. În **Game** alege o rezoluție portret (de exemplu 1080×2340) și apasă **Play**.

Control: atingi / dai click pe un tun de jos.

## Regulile

1. **Titanul.** Pășește spre zid; ajunge la el în 60–70 s la primul titan. Bara de sus arată cât de aproape e.
2. **Tunurile.** Jos ai 3 tunuri, fiecare cu o culoare și o muniție (numărul de pe el). Când le-ai folosit pe toate 3, vin alte 3 (ca la Block Blast).
3. **Linia de tragere.** Atingi un tun și el urcă pe zid, în primul din cele **5 locuri**. Trage singur, cam 3 lovituri pe secundă, în cuburile **vizibile** de culoarea lui, până rămâne fără muniție. Atunci pleacă și eliberează locul.
4. **Așteptarea și Recuperarea.** Un tun fără cuburi vizibile de culoarea lui stă în locul lui și așteaptă (scrie „zzz”). Începe să tragă singur când apare culoarea lui. „zzz” înseamnă mereu că mai există cuburi de culoarea lui, ascunse în spate. Când cade o bucată, muniția care nu mai are ce lovi pleacă odată cu ea: un tun de pe zid a cărui culoare a dispărut de tot zboară de pe zid cu **„RECUPERAT +puncte”**; din tunurile din tavă se scade muniția în plus (un „-N” zboară din bucată spre tun și numărul scade sub ochii tăi; un tun ajuns la 0 dispare); din pachet se scade pe nevăzute. Pe zid nu rămâne niciodată un tun mort.
5. **Tăieturile (combo).** Brațele stau pe umeri, iar capul pe gât. Umerii și gâtul sunt **încheieturile**: au culoarea lor și o dungă albă („cusătura”). Brațele și capul nu cad singure: tunul de culoarea brațului îl „mănâncă” de la mână spre umăr, fără să-l rupă. Bucata se rupe doar când distrugi încheietura (sau corpul de sub ea), cade cu fizică și toate cuburile din ea îți dau puncte. Mâna are culoarea brațului (o recunoști după rama închisă). Eticheta galbenă **TAIE!** apare deasupra unui tun din tavă doar dacă muniția **lui**, trimis acum, ajunge să rupă o bucată (jocul simulează țintirea pe o copie a titanului, deci eticheta nu minte). Dacă tunul singur nu ajunge, dar mai ai pe zid sau în tavă alt tun de aceeași culoare, apare un contur gol „taie în N” (N = câte lovituri de culoarea asta mai trebuie).
6. **Pierzi** când titanul atinge zidul sau când toate cele 5 locuri sunt pline cu tunuri care n-au în ce trage.

**Scor:** 10 puncte pe cub lovit, 25 pe fiecare cub dintr-o bucată ruptă, 5 pe fiecare lovitură recuperată (de pe zid sau din tavă), 200 × numărul titanului când îl dobori, plus 2 pe fiecare lovitură rămasă în tunuri.

### Alegeri făcute pentru prototip

- **„Vizibil”** = cubul din față al fiecărei coloane (ce vezi tu). Când îl spargi, se vede cel din spate, care poate avea altă culoare. Trunchiul are o culoare ascunsă înăuntru.
- **Ordinea țintelor:** fiecare tun trage în cubul vizibil de culoarea lui cel mai **depărtat de corp** (câți pași de la cub la cub îl despart de picioare și trunchi), apoi în cel mai înalt, apoi în cel mai apropiat de el. Așa brațele și capul se mănâncă de la vârf spre încheietură. (Vechea ordine, „cel mai înalt întâi”, rămâne în cod ca `TargetOrder.HighestFirst`, doar pentru comparații.)
- **Culorile titanului:** brațul nu are niciodată culoarea umerilor, a gâtului sau a capului, ca tunul brațului să nu poată rupe nimic singur.
- **Recuperarea** socotește întâi muniția deja pusă pe zid: ce trimiți pe zid acoperă primele cuburi ale culorii, iar din tavă și din pachet pleacă doar ce rămâne în plus. Din pachet se scade întâi din tunurile cele mai mici. Muniția recuperată din pachet nu dă puncte (nu o vezi).
- **Ochii** sunt doar pe fața capului. Dacă un tun de culoarea capului mănâncă tot capul din jurul lor, ochii (cu cuburile ascunse în spatele lor) cad ca o bucată mică.
- **Ce se rupe:** corpul și picioarele stau pe pământ chiar dacă sunt ciuruite. Se rup doar brațele și capul. Altfel, tunul de culoarea picioarelor tăia șoldurile și cădea tot titanul în câteva secunde.
- **Muniția** fiecărui titan acoperă exact cuburile lui, împărțită în tunuri de 10–30. În 65% din cazuri, tunul nou are o culoare care se vede acum, iar în rest e la întâmplare. Nu există alt „regizor”.

## Titanii

| Titan | Cuburi | Culori | Timp până la zid |
|---|---|---|---|
| 1 | 142 | 3 | 61 s |
| 2 | 160 | 3 | 62 s |
| 3 | 206 | 4 | 69 s |
| 4 | 477 | 4 | 124 s |
| 7 | 996 | 6 | 201 s |

## Simulare (echilibrul jocului)

Simulatorul e în `Tools/Sim`: joacă nucleul real al jocului (`Assets/Titan/Scripts/Core`, compilat direct, fără copii) cu trei roboți — **Random** (atinge orice tun), **Greedy** (alege culori vizibile), **Smart** (urmărește încheieturile, nu umple zidul cu tunuri care așteaptă). Roboții ating un tun la 0,6–1,6 s și **nu folosesc niciodată ajutoarele din reclame**.

```
dotnet run -c Release --project Tools/Sim -- --games 300 --preset current
dotnet run -c Release --project Tools/Sim -- --games 300 --preset v2 --set ApproachSecondsPerCube=0.08
```

Presetul `current` e `TitanConfig` așa cum e acum; `v2` e tabelul din `docs/DESIGN.md` 4.3 și se completează singur pe măsură ce regulile noi intră în cod (până atunci simulatorul scrie ce lipsește). `--set Cheie=Valoare` schimbă orice câmp din `TitanConfig`.

Raportul are metricile din `docs/DESIGN.md`, secțiunea 12.1 (titani doborâți, durata rundei, de ce se pierde, timpi per titan, decizii, bucăți, tunuri moarte) și raportul Smart : Random. Țintele și cifrele de azi sunt în `docs/DESIGN.md` (secțiunile 3 și 12.1), nu aici, ca să nu rămână vechi.

## Teste

```
dotnet test Tools/CoreTests          # logica + testul de echilibru (50 de jocuri/robot, câteva secunde)
dotnet build Tools/UnityCheck        # verifică că scripturile de joc compilează cu Unity
dotnet build Tools/EditorCheck       # verifică scripturile de editor
```

Logica (`Assets/Titan/Scripts/Core`) nu depinde de Unity. Testele stau în `Tools/CoreTests`, în afara folderului `Assets`, ca Unity să nu le compileze. Testul de echilibru (`BalanceTests`) rulează mereu; cu `SKIP_BALANCE=1` e sărit.

## Structură

```
Assets/Titan/Scripts/Core   logica pură: titanul din voxeli, rupturile, tunurile, linia de tragere, simularea
Assets/Titan/Scripts/Game   Unity: arena, titanul, tunurile, proiectilele, bucățile cu fizică, sunete, HUD
Assets/Titan/Editor         pregătirea automată a proiectului în editor
Tools/CoreTests             testele NUnit ale logicii și testul de echilibru, rulate cu dotnet
Tools/Sim                   simulatorul de echilibru (roboți care joacă nucleul real)
Tools/UnityCheck            verificare de compilare a scripturilor de joc, fără Unity
Tools/EditorCheck           verificare de compilare a scripturilor de editor, fără Unity
```
