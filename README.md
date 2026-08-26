# FriWorld Launcher

Samostatná aplikácia, ktorá si stiahne posledný desktop build hry FriWorld, overí ho,
nainštaluje a spustí. Windows, Linux, macOS z jedného kódu.

Implementačný plán, z ktorého to vychádza, žije v repe hry:
`docs/2026-08-25-launcher-implementacny-plan.md`.

- **.NET 10**, C#
- **Avalonia 12** pre okno
- Bez externých závislostí nad rámec Avalonie — sťahovanie, hashovanie aj rozbaľovanie
  stoja na tom, čo je v BCL

---

## Rozloženie

```
src/
  FriWorld.Launcher.Core/   všetka mechanika, bez UI
  FriWorld.Launcher.Cli/    bezhlavý front end — všetko sa dá odladiť bez okna
  FriWorld.Launcher.App/    Avalonia okno
tests/
  FriWorld.Launcher.Core.Tests/
mock/
  store/                    vygenerovaný falošný release (negitované)
```

`Core` nevie, odkiaľ build pochádza. Rozpráva sa s `IReleaseSource` a `IContentClient`,
takže tá istá cesta beží proti priečinku na disku aj proti reálnemu úložisku.

---

## Rýchly štart

Build ešte neexistuje, tak sa jeden vyrobí:

```bash
dotnet run --project src/FriWorld.Launcher.Cli -- mock-release
```

Vznikne `mock/store/` s archívmi pre tri platformy, checksummami a `manifest.json`.
Potom sa proti nemu spustí celá cesta — stiahnutie, overenie, rozbalenie, výmena, štart:

```bash
dotnet run --project src/FriWorld.Launcher.Cli -- run --manifest mock/store/manifest.json --root .localroot
```

`--root` presmeruje inštaláciu mimo skutočného `%LOCALAPPDATA%`, takže sa pri vývoji
nič ostré neprepíše.

Ďalšie príkazy:

```bash
dotnet run --project src/FriWorld.Launcher.Cli -- help
```

| príkaz | čo robí |
|---|---|
| `where` | vypíše cesty, platformu a čo je nainštalované |
| `check` | stiahne manifest a povie, či treba aktualizovať |
| `update` | stiahne, overí, rozbalí a vymení |
| `run` | `update`, potom spustí hru |
| `mock-release` | vygeneruje falošný release |
| `clean --yes` | zmaže celý inštalačný koreň |

---

## Konfigurácia

| premenná | čo prepíše |
|---|---|
| `FRIWORLD_MANIFEST_URL` | odkiaľ sa číta manifest — URL alebo cesta na disku |
| `FRIWORLD_LAUNCHER_ROOT` | kam sa inštaluje |

Prepínače `--manifest` a `--root` majú prednosť pred premennými.

---

## Kde to na disku žije

| OS | koreň |
|---|---|
| Windows | `%LOCALAPPDATA%\FriWorld\` |
| macOS | `~/Library/Application Support/FriWorld/` |
| Linux | `${XDG_DATA_HOME:-~/.local/share}/FriWorld/` |

```
FriWorld/
  game/            aktuálna inštalácia
  game.new/        rozbaľuje sa sem, kým nie je hotovo
  game.old/        predošlá — drží sa, kým sa nová verzia raz nespustí
  cache/           stiahnuté archívy
  launcher/        samotný launcher
  installed.json
  launcher.log
  launcher.lock
```

Nikdy nie do `Program Files` — vyžadovalo by to práva správcu pri každej aktualizácii.

---

## Manifest

Launcher číta jeden JSON súbor. Tvar je zámerne malý a tolerantný, lebo ho build pipeline
prepisuje pri každom builde; neznáme polia sa ignorujú.

```json
{
  "version": "0.1.2-alpha",
  "released": "2026-08-26T10:00:00Z",
  "notes": "Krátky text do launchera.",
  "platforms": {
    "win-x64": {
      "url": "FriWorld-0.1.2-alpha-win-x64.zip",
      "sha256": "…64 hex znakov…",
      "size": 812934144,
      "exec": "FriWorld.exe"
    }
  }
}
```

- `url` môže byť absolútna alebo relatívna voči manifestu
- `exec` musí ukazovať na **skutočnú binárku**, nie na priečinok — na macOS teda
  `FriWorld.app/Contents/MacOS/FriWorld`
- formát archívu sa odvodí z prípony, dá sa prebiť poľom `format`

### Formát archívu je per platforma

| platforma | formát | prečo |
|---|---|---|
| Windows | `.zip` | natívne, práva netreba |
| Linux | `.tar.gz` | zip stráca execute bit na binárke |
| macOS | `.tar.gz` | `.app` bundle obsahuje symlinky, zip ich rozbije |

### Verzie sa neporovnávajú

Pravidlo je jediné: **tag v manifeste sa líši od zapísaného tagu, tak aktualizuj.**
Žiadne triedenie podľa SemVer. Launcher vždy chce presne to, čo manifest práve menuje,
aj keby to bolo staršie číslo.

---

## Ako aktualizácia prebieha

1. prečíta manifest, vyberie balík pre túto platformu
2. porovná tag s `installed.json`
3. overí voľné miesto — treba **trojnásobok** veľkosti archívu plus rezerva
4. stiahne do `cache/`, s pokračovaním cez HTTP Range
5. spočíta SHA256; **pri nezhode archív zmaže a skončí chybou** — neoverený archív sa
   nikdy nerozbaľuje
6. rozbalí do `game.new/`, so zachovaním práv a symlinkov
7. `game` → `game.old`, `game.new` → `game`
8. `game.old` sa zmaže **až** keď sa nová verzia raz úspešne spustila

Krok 8 je zámerný. Build, čo spadne pri štarte, tak nechá cestu späť.

---

## Testy

```bash
dotnet test
```

Testy pipeline nie sú mockované okrem siete: vyrobí sa skutočný archív, skutočne sa
spočíta checksum, skutočne sa rozbalí strom aj s právami a skutočne sa vymenia priečinky.
Jediný rozdiel oproti ostrej prevádzke je `file://` namiesto `https://`.

---

## Známe prostredie

**Smart App Control na Windows 11 blokuje nepodpísané binárky**, vrátane čerstvo
zbuildovaných DLL. Ak `dotnet test` alebo spustenie launchera padne na
`An Application Control policy has blocked this file (0x800711C7)`, je to ono.
Detaily a možnosti sú v [`docs/decisions/2026-08-26-smart-app-control.md`](docs/decisions/2026-08-26-smart-app-control.md).

Bez vypínania Smart App Control sa dá pracovať takto:

| chcem | ako |
|---|---|
| spustiť testy | zdroje `Core` preložiť priamo do testovacieho projektu — postup v zápise vyššie |
| spustiť testy a overiť Linux | kontajner `mcr.microsoft.com/dotnet/sdk:10.0` |
| vidieť a klikať v okne launchera | VM s Windows, kde Smart App Control nebeží |

Kontajner je jediná cesta, ako dnes overiť execute bity a symlinky z `tar.gz`.

---

## Rozsah

Launcher je **most k Steamu, nie produkt**. Steam si aktualizácie, delta patche aj verzie
rieši sám, takže sa sem nestavia nič, čo by aj tak zahodil. Rozhodnutie aj s odôvodnením:
[Bez podpisu, launcher je most k Steamu](docs/decisions/2026-08-26-bez-podpisu-launcher-je-most-k-steamu.md).

Zámerne **nie je a nebude**:

- **Podpis a notarizácia.** Nepodpisuje sa nič, neplatí sa za nič.
- **Self-update launchera.** Len sa zistí, že je novšia verzia, a odkáže sa na Hub.
- **Delta patchovanie.** Steam to robí lepšie a zadarmo.
- **macOS.** Bez Macu sa neotestuje, Gatekeeper je horší než Smart App Control.
- **CI buildy.** Editor skript stačí.

Zatiaľ chýba, ale patrí sem:

- **Reálne úložisko.** Manifest sa dnes číta z lokálneho mocku.
- **Build pipeline na strane hry**, ktorá archívy a manifest vyrobí.
