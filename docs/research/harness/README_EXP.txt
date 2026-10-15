Experimental core flags (env vars) for the Titan sim harness. Build: dotnet build -c Release ; run: ./r2.sh NAME VAR=val ... (N=games per bot, default 200)
SALVAGE=1 FAR=1 HANDARM=1 SLOTREFILL=1 NOTRIPLE=1 KNOCK=1 KBPC KBMAX STUNB STUNMAX ABASE APC ASPD AMIN BIAS SLOTS CYCLE=1
Recommended candidate: SALVAGE=1 FAR=1 HANDARM=1 SLOTREFILL=1 NOTRIPLE=1 KNOCK=1 KBPC=0.03 KBMAX=1.5 STUNB=0.5 STUNMAX=1.0 ABASE=4 AMIN=12 APC=0.075 CYCLE=1 ASPD=0.98
Outputs: out_<name>.txt
