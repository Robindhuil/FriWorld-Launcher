# Vydanie verzie — postup

**Verzia:** 0.1.0-alpha · **Dátum:** 2026-08-26

Od Unity po hráča. Krok za krokom, aby sa na nič nezabudlo.

---

## Vydanie hry

### 1. Zdvihni `bundleVersion`

V repe hry, `Project Settings → Player`. Robí to **človek**, nie skript — je to jediný
bod, ktorý určuje, ako sa release volá.

Podľa `CLAUDE.md` repa hry sa tým aj `[Unreleased]` v `CHANGELOG.md` premenuje na to číslo
a otvorí sa nová prázdna sekcia.

### 2. Zbuildi playery

V Unity: **FriWorld → Build → Release**.

Vznikne `Build/<bundleVersion>/win-x64/`. Skript na konci vypíše presný `pack` príkaz aj
s cestami.

Linux sa zapína prepínačom **FriWorld → Build → Include Linux** a chce
*Linux Build Support (IL2CPP)* z Unity Hub.

### 3. Zabaľ

```bash
dotnet run --project src/FriWorld.Launcher.Cli -- pack \
  --input "E:/UNITY/FriWorld/Build/0.1.2-alpha" \
  --version 0.1.2-alpha \
  --notes "Čo je nové, jedna až tri vety." \
  --base-url https://ulozisko.example/friworld/0.1.2-alpha
```

Vznikne `dist/0.1.2-alpha/` s archívmi a `manifest.json`.

`--base-url` vynechaj, ak archívy aj manifest pôjdu do toho istého priečinka — vtedy sa
zapíšu holé názvy súborov a priečinok sa dá presunúť kamkoľvek.

### 4. Skús to skôr, než to nahráš

```bash
dotnet run --project src/FriWorld.Launcher.Cli -- run \
  --manifest dist/0.1.2-alpha/manifest.json \
  --root .localroot
```

Toto stiahne, overí, rozbalí a spustí hru z lokálneho priečinka. Keď to prejde tu,
na sieti už zlyhá len sieť.

### 5. Nahraj

- **archívy** na úložisko
- **`manifest.json`** na svoju pevnú adresu — tú, ktorú majú launchery v `launcher.json`

**Manifest nahraj ako posledný.** Kým tam nie je, hráči vidia predošlú verziu, čo je
správne. Keby šiel prvý, launcher by chvíľu ukazoval na archívy, ktoré ešte neexistujú.

### 6. Over z pohľadu hráča

```bash
dotnet run --project src/FriWorld.Launcher.Cli -- check --manifest https://…/manifest.json
```

---

## Vydanie launchera

### 1. Zdvihni verziu

`<Version>` v `Directory.Build.props`. Bez toho self-update nikdy nič nenájde —
porovnáva sa na nerovnosť s tým, čo je v manifeste.

Riadok do `CHANGELOG.md` a premenovanie `[Unreleased]`, rovnaký rituál ako v repe hry.

### 2. Zostav jednosúborový build

```bash
dotnet publish src/FriWorld.Launcher.App/FriWorld.Launcher.App.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -p:DebugType=none \
  -o dist/launcher
```

**Musí to byť jeden súbor.** Self-update odmieta vymeniť build rozsypaný do desiatok DLL,
lebo polovične vymenený launcher je horší než starý.

### 3. Prilož ho do manifestu hry

Launcher nemá vlastný manifest. Vydáva sa spolu s najbližším releasom hry:

```bash
... pack ... \
  --launcher-version 0.2.0-alpha \
  --launcher-url https://friworld.example/stiahnut \
  --launcher-file win-x64=dist/launcher/FriWorld.Launcher.App.exe \
  --launcher-base-url https://friworld.example/launcher
```

`--launcher-base-url` **musí byť https**. Launcher sa tým súborom nahradí, takže tu je
pravidlo prísnejšie než pri archíve hry.

### 4. Nahraj binárku launchera skôr než manifest

To isté pravidlo ako pri hre, z toho istého dôvodu.

---

## Keď treba zamknúť staré launchery

Pridaj `--min-launcher <verzia>`. Používaj striedmo: zamkne to von **každý** launcher
v obehu pod tou verziou.

Má to zmysel len vtedy, keď je alternatívou to, že tie staré launchery spravia niečo zle —
napríklad keď manifest začne znamenať niečo, čo nevedia správne interpretovať. Samotné
pridanie poľa dôvod nie je; neznáme polia staré launchery ignorujú.

Pred nasadením stropu sa uisti, že v tom istom manifeste je aj `launcher` sekcia
s binárkou. Inak zamkneš ľudí von bez cesty von.

---

## Keď sa release pokazí

Prepíš `manifest.json` späť na predošlú verziu. Launcher verzie neporovnáva na poradie,
takže krok späť je pre neho bežná aktualizácia a hráči sa vrátia sami.

Archívy predošlej verzie preto **nemaž hneď** po vydaní novej.

---

## Kontrolný zoznam

```
[ ] bundleVersion zdvihnutá
[ ] CHANGELOG.md doplnený, [Unreleased] premenovaný
[ ] Unity: FriWorld → Build → Release prebehol bez chýb
[ ] launcher pack prebehol
[ ] lokálne overené: run --manifest dist/<verzia>/manifest.json --root .localroot
[ ] archívy nahraté
[ ] manifest nahratý AKO POSLEDNÝ
[ ] check proti ostrej adrese vracia správnu verziu
[ ] archívy predošlej verzie ponechané pre prípad návratu
```
