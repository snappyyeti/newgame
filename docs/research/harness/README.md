# Harness de simulare (arhivă de cercetare)

Copie a harness-ului folosit de echipa de analiză pe 15.10.2026, ca să nu se piardă din directorul temporar.
Nu se compilează direct din acest folder: `Sim.csproj` include `Core/*.cs`, adică o copie a
`Assets/Titan/Scripts/Core` cu patch-ul `core_experiments.diff` aplicat (steagurile din `README_EXP.txt`).

Pentru a reproduce:

```
mkdir /tmp/sim && cp -r docs/research/harness/* /tmp/sim/ && mkdir /tmp/sim/Core
cp Assets/Titan/Scripts/Core/*.cs /tmp/sim/Core/ && (cd /tmp/sim && patch -p0 < core_experiments.diff)
cd /tmp/sim && dotnet build -c Release && N=200 ./r2.sh NUME SALVAGE=1 FAR=1 ...
```

Rezultatele `out_*.txt` sunt cele citate în `docs/DESIGN.md`:

| Fișier | Configurație |
|---|---|
| out_base.txt | jocul actual (fără reparații) |
| out_salv.txt | + muniția pleacă odată cu bucata (C1) |
| out_hand.txt | + țintire de la vârf spre încheietură, mâna = culoarea brațului (C2) |
| out_F3.txt | + tavă cu reumplere pe loc + fără 3 de aceeași culoare, FĂRĂ ceasul nou (prea ușor) |
| out_G1.txt | + ceasul titanului (apropiere rapidă, recul la tăiere) |
| out_C1.txt | + ciclurile de 5 titani (SPRINT / BOSS) |

Itemul 1 din backlog (docs/DESIGN.md, secțiunea 13) mută acest harness, curat, în `Tools/Sim`.
