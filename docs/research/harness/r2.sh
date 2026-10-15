#!/bin/bash
name=$1; shift
env "$@" dotnet bin/Release/net8.0/Sim.dll 0 base ${N:-200} 2>/dev/null > out_$name.txt
echo "##### $name ($*)"
awk '/^=====/{b=$2} b!="Greedy"' out_$name.txt | grep -E "^=====|titans defeated|run length|lose reasons|survival|pieces triggered|combos:|decision points|time shares|titan [1-7]: n=" | grep -v "pieces per titan" | sed -E 's/line cannons stranded.*//; s/ \(p10 [0-9]+ p90 [0-9]+\)//'
