# Splash Sort: prototip de test (cuburi gri)

Puzzle de sortare: tragi colaci cu înotători colorați în 12 piscine, iar stivele vecine de aceeași culoare se unesc singure. 10 de aceeași culoare în vârf fac SPLASH.

## Cum îl deschizi

1. Pe GitHub: **Code → Download ZIP**, apoi dezarhivezi într-un folder **nou**. Nu peste un proiect vechi.
2. Unity Hub → **Add** → **Add project from disk** → folderul dezarhivat → Unity `6000.6.3f1` (merge orice Unity 6).
3. La prima deschidere, scriptul `Assets/SplashSort/Editor/ProjectSetup.cs` deschide scena `Main`, setează portret, Direct3D 11 și input-ul clasic (Input Manager). Dacă te întreabă dacă repornește, apasă **Repornește**.
4. În **Game** alege o rezoluție portret (de exemplu 1080×2340) și apasă **Play**.

Control: apeși pe un colac de jos, îl tragi peste o piscină goală (se face verde) și dai drumul.

## Regulile

- **Tabla:** 12 piscine, 3 coloane × 4 rânduri. Vecinii sunt sus, jos, stânga și dreapta.
- **Colacii:** jos sunt 3 colaci la alegere; după ce i-ai pus pe toți 3, vin alți 3. Un colac are 2–6 înotători și 1–3 culori, afișați exact cum vor sta în stivă. Se pun doar pe piscine goale.
- **Unirea automată**, după fiecare plasare:
  1. Se caută două piscine vecine cu aceeași culoare în vârf.
  2. Șirul de vârf trece de pe stiva cu șirul mai scurt pe cea cu șirul mai lung. La egalitate, trece pe piscina unde ai pus colacul.
  3. Se repetă până nu mai există nicio pereche.
  4. Orice stivă cu 10+ de aceeași culoare în vârf face SPLASH și înotătorii dispar.
  5. Dacă după SPLASH apar culori noi în vârf, se reia de la 1. Asta e cascada.
- **Pierzi** când nu mai e nicio piscină goală.
- **Scor:** 10 puncte × înotătorii scoși × pasul cascadei (×1, ×2, ×3…).
- **Culori:** 3 la început, 4 de la 500 de puncte, 5 de la 1.500, 6 de la 3.000.

### Cazuri pe care regulile nu le acoperă

- **Ordinea perechilor.** Întâi se fac unirile *în* piscina care tocmai s-a schimbat, ca să adune tot ce e în jur. Apoi celelalte perechi care ating piscina aceea, iar la urmă restul. În fiecare grup are prioritate perechea cu cei mai mulți înotători. Exemplu: pui `22` între `22` și `222`, iar toți cei 7 ajung împreună.
- **Egalitate fără piscina pusă** (în timpul cascadei): șirul trece pe piscina schimbată cel mai recent, altfel pe cea mai de sus/din stânga.
- **Mai multe stive cu 10+ în același timp:** fac SPLASH toate odată, ca un singur pas de cascadă.

## Feedback (obligatoriu chiar și pe gri)

- **Săritura înotătorilor:** 0,15 s fiecare, cu un mic decalaj între ei și un „tic” la fiecare săritură.
- **SPLASH:** zguduirea ecranului (mai tare la fiecare pas de cascadă) și particule în culoarea înotătorilor.
- **Sunet:** urcă un ton întreg la fiecare pas de cascadă (Do5, Re5, Mi5…). E generat din cod, fără fișiere audio.
- **Scorul:** cifrele sar la fiecare SPLASH, iar „+puncte” și „CASCADĂ x2” ies din piscină.

## Ce nu e în test

Regizorul, reclamele, revive-ul și grafica de aquapark. Colacii vin complet aleatori.

## Simulare (400 de jocuri per bot)

| Bot | Scor median | p10–p90 | Colaci puși | SPLASH-uri | Cascade |
|---|---|---|---|---|---|
| aleatoriu | 330 | 0–800 | 23 | 1.180 | 22 |
| lacom | 1.260 | 610–2.030 | 45 | 4.254 | 75 |

Cascadele sunt rare: sub 2% din SPLASH-uri continuă cu un al doilea pas. De urmărit la test dacă oamenii le simt ca pe o recompensă reală.

## Teste

```
dotnet test Tools/CoreTests
```

Ca să le rulezi și din Unity, instalează **Test Framework** din Package Manager. Apoi apar în **Window → General → Test Runner → EditMode**. Pachetul nu e inclus din start, ca proiectul să se deschidă fără erori pe orice Unity 6.

## Structură

```
Assets/SplashSort/Scripts/Core   logica pură (fără UnityEngine): tabla, colacii, unirea, SPLASH, cascada, scorul
Assets/SplashSort/Scripts/Game   Unity: grila din primitive, drag & drop, animații, sunet, zguduire, particule, HUD
Assets/SplashSort/Editor         pregătirea automată a proiectului în editor
Assets/SplashSort/Tests/Editor   teste NUnit (EditMode)
Tools/CoreTests                  proiect dotnet care rulează testele în afara Unity
```
