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
4. **Așteptarea.** Un tun fără cuburi vizibile de culoarea lui stă în locul lui și așteaptă (scrie „zzz”). Începe să tragă singur când apare culoarea lui.
5. **Combo.** Brațele stau pe umeri, iar capul pe gât. Umerii și gâtul au culoarea lor. Dacă le distrugi, bucata se rupe, cade cu fizică și toate cuburile din ea îți dau puncte.
6. **Pierzi** când titanul atinge zidul sau când toate cele 5 locuri sunt pline cu tunuri care n-au în ce trage.

**Scor:** 10 puncte pe cub lovit, 25 pe fiecare cub dintr-o bucată ruptă, 200 × numărul titanului când îl dobori, plus 2 pe fiecare lovitură rămasă în tunuri.

### Alegeri făcute pentru prototip

- **„Vizibil”** = cubul din față al fiecărei coloane (ce vezi tu). Când îl spargi, se vede cel din spate, care poate avea altă culoare. Trunchiul are o culoare ascunsă înăuntru.
- **Ordinea țintelor:** fiecare tun trage întâi în cel mai înalt cub vizibil de culoarea lui, apoi în cel mai apropiat de el.
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
