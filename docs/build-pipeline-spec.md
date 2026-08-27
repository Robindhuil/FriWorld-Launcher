# Build pipeline — zadanie pre repo hry

**Verzia launchera:** 0.1.1-alpha · **Dátum:** 2026-08-26 · **Stav:** zadanie, nezačaté

Dokument je písaný tak, aby sa podľa neho dalo pracovať v session nad repom
`Robindhuil/FriWorld` bez kontextu tejto konverzácie.

---

## Čo sa má dosiahnuť

Po jednom kliknutí v Unity a jednom príkaze v termináli existuje priečinok, ktorý sa dá
nahrať tak, ako je, a launcher si z neho hru stiahne a nainštaluje.

---

## Delenie práce — Unity nebalí

Pôvodný plán chcel, aby editor skript zbuildil, zabalil, zahashoval aj vygeneroval manifest.
**Balenie a manifest patria launcheru, nie Unity.** Dva dôvody:

- `System.Formats.Tar` je .NET 7 a novšie. Unity 6000.4 beží na staršom API a **tar writer
  nemá vôbec**.
- `tar` vyrobený na Windows **stráca execute bit**, lebo Windows ho v súborovom systéme nemá.
  Linuxový build by sa potom nespustil. Launcher pri balení mód nastavuje explicitne.

Tretí, dôležitejší dôvod: manifest je kontrakt medzi dvoma repami. Keby ho jedno repo
zapisovalo a druhé čítalo dvoma nezávislými implementáciami, časom sa rozídu. Takto ho píše
aj číta ten istý kód.

```
Unity editor skript          zbuildí playery do Build/<verzia>/<platforma>/
        │
        ▼
launcher pack                archívy + SHA256 + manifest.json
        │
        ▼
nahratie                     archívy na GitHub Releases, manifest vedľa Hubu
```

---

## Časť 1 — editor skript v repe hry

Súbor `Assets/_Game/Editor/BuildRelease.cs`, položka menu `FriWorld → Build → Release`.

Robí **len** toto:

1. prečíta `PlayerSettings.bundleVersion` — **nikdy ju neprepisuje**, dvíha ju človek
2. pre každý zapnutý target zavolá `BuildPipeline.BuildPlayer` do
   `Build/<bundleVersion>/<platformKey>/`
3. na konci vypíše cestu a presný príkaz na zabalenie, aby sa nemusel pamätať

Kľúče platforiem sú **presne** tieto, sú súčasťou kontraktu:

| kľúč | Unity `BuildTarget` | poznámka |
|---|---|---|
| `win-x64` | `StandaloneWindows64` | povinné |
| `linux-x64` | `StandaloneLinux64` | voliteľné, viď nižšie |
| `osx-arm64` | `StandaloneOSX` | **mimo rozsahu**, nerobiť |

Priečinok `Build/` patrí do `.gitignore`, ak tam ešte nie je.

### Linux áno alebo nie

Pre prvý release stačí `win-x64`. Cieľovka sú slovenské školy, tam je Windows, a všetko
ostatné pokrýva web build. Linux znamená doinštalovať **Linux Build Support (IL2CPP)**
v Unity Hub → Installs → Add modules a druhý archív v každom vydaní.

Launcher aj balič Linux podporujú a otestované to je, takže sa dá pridať kedykoľvek neskôr
bez zmeny kódu. Sprav to ako konštantu alebo checkbox v skripte, nie ako natvrdo zadrôtovaný
zoznam.

### Na čo si dať pozor v Unity

- **Meno linuxovej binárky.** Unity vyrába `FriWorld.x86_64`, nie `FriWorld`. Balič to sám
  nájde, ale keď to budeš niekde písať ručne, toto je to meno.
- **`locationPathName` musí byť súbor, nie priečinok.** Pre Windows
  `Build/<verzia>/win-x64/FriWorld.exe`, pre Linux `Build/<verzia>/linux-x64/FriWorld.x86_64`.
- **`UnityCrashHandler64.exe`** leží vedľa hry. Balič ho za hru nepovažuje, netreba ho mazať.
- Skontroluj návratovú hodnotu `BuildPipeline.BuildPlayer` a pri `BuildResult.Failed`
  skonči chybou. Bez toho sa zabalí polovičný build.

---

## Časť 2 — zabalenie launcherom

Z repa launchera (`ROBIN/dev/friworld-launcher`):

```bash
dotnet run --project src/FriWorld.Launcher.Cli -- pack --input "E:/UNITY/FriWorld/Build/0.1.1-alpha" --version 0.1.1-alpha --notes "Čo je nové."
```

Vznikne `dist/0.1.1-alpha/` s archívmi a `manifest.json`.

Čo balič robí sám:

- `win-x64` do `.zip`, všetko ostatné do `.tar.gz` — zip neunesie execute bit ani symlinky
- nájde spustiteľný súbor, vrátane binárky vnútri `.app` bundlu
- linuxovej binárke nastaví execute bit, aj keď sa balí na Windows
- spočíta SHA256 a veľkosť
- vygeneruje manifest a **hneď si ho aj prečíta späť**, takže sa nevydá manifest,
  ktorý by launcher odmietol

Užitočné prepínače:

| prepínač | na čo |
|---|---|
| `--out <cesta>` | kam (predvolene `dist/<verzia>`) |
| `--base-url <url>` | absolútne adresy archívov v manifeste; bez neho holé názvy súborov |
| `--exec win-x64=FriWorld.exe` | keď detekcia netrafí; dá sa opakovať |
| `--launcher-version` a `--launcher-url` | upozornenie na novšiu verziu launchera |

Holé názvy súborov sú v poriadku a väčšinou lepšie — launcher ich rozráta voči umiestneniu
manifestu, takže priečinok sa dá presunúť bez prepisovania obsahu.

---

## Časť 3 — nahratie

- **Archívy** ako assety GitHub Release na tagu `v<verzia>`. Verejné repo, bez tokenu.
  Limit je 2 GB na súbor.
- **`manifest.json`** ako **statický súbor vedľa Hubu**, nie ako release asset.
  Launcher ho číta z pevnej adresy. Dôvod je v
  [`docs/decisions/2026-08-26-manifest-mimo-github-api.md`](decisions/2026-08-26-manifest-mimo-github-api.md):
  GitHub API má 60 neautentizovaných volaní za hodinu na IP a viacerí ľudia za jedným
  pripojením ho vyčerpajú.

Ak archívy idú na GitHub Releases a manifest inam, treba `--base-url` s adresou releasu.

---

## Miesto na podpis

Nepodpisuje sa nič, viď
[`docs/decisions/2026-08-26-bez-podpisu-launcher-je-most-k-steamu.md`](decisions/2026-08-26-bez-podpisu-launcher-je-most-k-steamu.md).
Ale poradie krokov musí sedieť už teraz:

```
build  →  PODPIS  →  archív  →  SHA256  →  manifest
```

Podpis mení obsah súboru. Pipeline postavená ako `build → archív → hash → podpis` sa neskôr
nedá doplniť, musí sa prerobiť. V `ReleasePacker.PackAsync` je to miesto označené komentárom
tesne pred volaním `ArchiveBuilder.CreateAsync`.

---

## Hotovo, keď

1. `FriWorld → Build → Release` vyrobí `Build/<verzia>/win-x64/` s hrou.
2. `launcher pack` z toho spraví archív, checksum a manifest bez ručného zásahu.
3. `launcher check --manifest dist/<verzia>/manifest.json` vypíše správnu verziu a veľkosť.
4. `launcher run --manifest dist/<verzia>/manifest.json --root .localroot` hru stiahne,
   nainštaluje a spustí.
5. Druhé spustenie bez novej verzie hru len spustí, nesťahuje nič.
6. `bundleVersion` sa nikde neprepisuje automaticky.

---

## Rituál z `CLAUDE.md` repa hry

Nezabudni, ide o repo hry, nie launchera:

- riadok do `CHANGELOG.md` pod `[Unreleased]`, do sekcie podľa typu
- pri zdvihnutí `bundleVersion` sa `[Unreleased]` premenuje na to číslo a otvorí sa nová
  prázdna
- zápis do `docs/decisions/` len ak sa rozhodovalo medzi možnosťami alebo sa narazilo na
  pascu; bežný feature tam nepatrí
- ak zápis vznikne, hneď aj riadok do `docs/decisions/README.md`
- conventional commits, nikdy `git add -A`, vetva `master`
