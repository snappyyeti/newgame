# Harness de simulare (arhivă de cercetare)

Rezultatele echipei de analiză din 15.10.2026, păstrate pentru istoric. Ele sunt citate în `docs/DESIGN.md` (secțiunile 3 și 12.1).

**Codul s-a mutat în `Tools/Sim`** (itemul FND-1): simulatorul compilează acum direct `Assets/Titan/Scripts/Core`,
fără copie și fără patch, iar steagurile experimentale devin câmpuri reale din `TitanConfig` pe măsură ce
intră regulile (CORE-1..CORE-4). Vezi `README.md` (secțiunea Simulare) și `docs/DESIGN.md` 16.4.
Harness-ul vechi (`Program.cs`, `r2.sh`) e în istoricul git, înainte de commit-ul care l-a mutat.

Ce rămâne aici:

| Fișier | Ce este |
|---|---|
| core_experiments.diff | patch-ul experimental aplicat atunci pe o copie a Core (steaguri prin variabile de mediu) |
| README_EXP.txt | lista steagurilor și combinația recomandată |
| out_base.txt | jocul actual (fără reparații) |
| out_salv.txt | + muniția pleacă odată cu bucata (C1) |
| out_hand.txt | + țintire de la vârf spre încheietură, mâna = culoarea brațului (C2) |
| out_F3.txt | + tavă cu reumplere pe loc + fără 3 de aceeași culoare, FĂRĂ ceasul nou (prea ușor) |
| out_G1.txt | + ceasul titanului (apropiere rapidă, recul la tăiere) |
| out_C1.txt | + ciclurile de 5 titani (SPRINT / BOSS) |

Corespondența steag vechi → câmp `TitanConfig` (presetul `v2` din `Tools/Sim/Presets.cs`):
SALVAGE → SalvageEnabled, FAR → TargetOrder=FarthestFirst, HANDARM → regula din builder (CORE-2),
SLOTREFILL → TrayRefill=PerSlot, NOTRIPLE → NoTripleColor, KNOCK/KBPC/KBMAX/STUNB/STUNMAX → Knockback*/Stun*,
ABASE/APC/ASPD/AMIN → Approach*/MinApproachSeconds, CYCLE → UseCycles, BIAS → VisibleColorBias (blocat la 0,65).
