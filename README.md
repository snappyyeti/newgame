# Human Cannon (Omul-Ghiulea)

Puzzle 3D infinit de sortare pentru iOS și Android, în Unity. Un tun de circ aruncă grupuri de omuleți colorați pe 7 piedestale; 5 de aceeași culoare în vârf dispar, iar runda ține până nu mai încape niciun grup.

Acesta este **pasul 1 din MVP (prototipul gri)** plus miezul regizorului de grupuri (pasul 3).

## Cum îl deschizi

1. Unity Hub → **Add project from disk** → folderul repo-ului. Versiunea fixată e Unity `6000.6.3f1`; merge și orice Unity 6 (6000.x).
2. Deschide `Assets/Scenes/Main.unity` și apasă **Play**. Scena e goală intenționat: `GameController` construiește arena din primitive la pornire, deci jocul pornește din orice scenă.
3. În Game view alege o rezoluție portret (de exemplu 1080×2340).

Control: click/atingere pe un piedestal = tragi; click pe platforma galbenă din dreapta = rezervă.

## Ce e implementat

| Parte | Stare |
|---|---|
| Regulile 1–7 (grup, tragere, eliminare, lanț, rezervă, coadă, final) | ✅ `Core/Resolver.cs`, `Core/Round.cs` |
| Scor: ×pasul lanțului, dublu peste 5, arena curată +500 | ✅ |
| Cele 4 praguri vizibile + mesajul „Un artist nou intră în arenă!” | ✅ |
| Regizorul: 40 de candidați, tabla prezisă, nota, percentila H, ținta 1 − D | ✅ `Core/Director.cs` |
| Modurile Ajutor și Normal, curba de bază 0,25 → 0,60 (plafon 0,70), zgomot ±0,05 | ✅ |
| Regulile ascunse ReviveClearWave, NewUserBoost, LossStreakMercy, Bază | ✅ `Core/HiddenRules.cs` |
| Revive: elefantul dărâmă cele 3 piedestale cele mai înalte, ReviveBonus 0–10% din record | ✅ (animație gri) |
| Toate numerele în `GameConfig`, cu `ApplyOverrides` pentru Remote Config | ✅ (încă nelegat la serviciul Remote Config) |
| Contur verde/gri, puls roșu la înălțime ≥ 10, crestături pe stinghie | ✅ |
| Timpii din document (zbor 0,30 s + 0,06 s, eliminare 0,40 s, pas de lanț 0,35 s) | ✅ |
| Ecran de final, „Erai la X puncte de record!”, „RECORD NOU!” | ✅ (IMGUI provizoriu) |
| Evenimentele de analytics | ✅ doar în consolă (`[analytics] ...`) |
| Reclame | 🟡 `SimulatedAdService`: un card „AD” în locul LevelPlay |
| Modele 3D, sunet, particule adevărate, vibrații ușoare/medii, URP | ⏳ pasul 2 |
| LevelPlay, UMP, ATT, anul nașterii | ⏳ pasul 4 |
| Clasa de valoare, bandit-ul de interstițiale, grupul de control | ⏳ pașii 5–6 |

### Ce lipsește intenționat

Modul **Moarte** și cele două reguli care îl pornesc (`RewardHighValueAdHarvest`, `MultiClearBoostThenKill`) **nu** sunt implementate. Ele strică în secret grupurile chiar înainte de record, ca jucătorul să piardă și să se uite la o reclamă, și țintesc anume jucătorii care aduc cei mai mulți bani. Revive-ul rămâne: e oferit când jucătorul pierde prin propriile mutări.

## Interpretări ale regulilor

Câteva cazuri pe care documentul nu le acoperă explicit:

- **Rezerva goală nu termină runda.** Dacă grupul din tun nu încape, dar rezerva e goală, jucătorul îl poate parca și primește următorul grup. Runda se termină abia când nici tunul, nici rezerva ocupată nu încap.
- **A doua schimbare e permisă doar dacă tunul nu încape nicăieri.** Altfel, după o schimbare cu un grup care nu încape, jucătorul ar rămâne fără nicio mutare legală.
- **Revive:** la înălțimi egale, elefantul dărâmă întâi piedestalul din stânga.
- **Nota regizorului:** punctele / 10 + 0,5 × creșterea șirului de aceeași culoare − 1 × fiecare graniță de culoare îngropată − 2 × fiecare omuleț peste înălțimea 9. Ponderile sunt în `GameConfig` și se reglează după primul test.

## Echilibru (simulare)

300 de runde pentru fiecare combinație, cu un bot care alege mereu cea mai bună poziție:

| Bot | Grupuri | Scor median | p10–p90 | Trageri |
|---|---|---|---|---|
| lacom | regizor (Normal) | 1.880 | 1.420–2.380 | ~100 |
| lacom | complet aleatorii | 620 | 270–1.180 | ~54 |
| aleatoriu | regizor | 250 | 100–510 | ~46 |

La 1,5–2 s pe tragere, o rundă cu regizorul durează ~3 minute, deci în ținta de 1.500–3.000 de puncte și 3–8 minute.

## Teste

Logica (`Assets/HumanCannon/Scripts/Core`) nu depinde de Unity, deci testele rulează și fără editor:

```
dotnet test Tools/CoreTests
```

În Unity, aceleași teste apar în **Window → General → Test Runner → EditMode**.

## Structură

```
Assets/HumanCannon/Scripts/Core   logica pură (fără UnityEngine): reguli, scor, regizor, reguli ascunse, config
Assets/HumanCannon/Scripts/Game   Unity: arena din primitive, animații, input, HUD, servicii (reclame simulate, analytics, salvare)
Assets/HumanCannon/Tests/Editor   teste NUnit (EditMode)
Tools/CoreTests                   proiect dotnet care rulează testele în afara Unity
```
