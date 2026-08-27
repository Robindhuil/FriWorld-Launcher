# Vývoj

**Verzia:** 0.1.1-alpha · **Dátum:** 2026-08-26

---

## Čo treba

- **.NET 10 SDK** (overené na 10.0.101)
- nič ďalšie; Avalonia príde cez NuGet

---

## Prvý beh bez toho, aby existoval build hry

```bash
dotnet run --project src/FriWorld.Launcher.Cli -- mock-release
dotnet run --project src/FriWorld.Launcher.Cli -- run --manifest mock/store/manifest.json --root .localroot
```

`mock-release` vyrobí falošný release — tri archívy v správnych formátoch, checksummy
a manifest. Falošná „hra" je skript.

`--stub-seconds <n>` rozhoduje, ako dlho ten skript beží, a to určuje, ktorú polovicu
launchera vyskúšaš:

| | čo sa stane |
|---|---|
| `0` (predvolené) | skript hneď skončí — launcher to vyhodnotí ako build padnutý pri štarte |
| dlhšie než doba odkladu | zapíše sa potvrdenie, uprace sa predošlá inštalácia, okno sa zavrie |

Bez toho druhého sa úspešný štart nedá odskúšať vôbec.

`--root .localroot` presmeruje inštaláciu mimo skutočného `%LOCALAPPDATA%`, takže sa nič
ostré neprepíše. Priečinok je v `.gitignore`.

---

## Testy

```bash
dotnet test
```

199 testov. Pipeline **nie je mockovaná okrem siete**: vyrobí sa skutočný archív, skutočne
sa spočíta checksum, skutočne sa rozbalí strom aj s právami a skutočne sa vymenia
priečinky. Jediný rozdiel oproti ostrej prevádzke je `file://` namiesto `https://`.

Testy self-updatu sa zámerne sústredia na to, **čo prežije zlyhanie**, nie na šťastnú cestu.
Šťastná cesta sa dá vyskúšať; zlyhanie uprostred výmeny na cudzom stroji nie.

---

## Smart App Control

Na Windows 11 s **zapnutým Smart App Control** sa nepodpísané binárky nespustia. Preklad
funguje, beh nie:

```
An Application Control policy has blocked this file. (0x800711C7)
```

Zmerané kombinácie:

| | výsledok |
|---|---|
| `dotnet build` | prejde vždy |
| apphost `.exe`, aj jednosúborový self-contained | **blokované** |
| `dotnet exec` + samostatná `Core.dll` | **blokované** |
| `dotnet exec` + jedna zlúčená assembly, `OutputType=Exe` | **prejde** |
| `dotnet exec` + jedna zlúčená assembly, `OutputType=WinExe` | **blokované** |

Skript to rieši sám:

```powershell
./tools/run-under-smart-app-control.ps1              # okno proti mock releasu
./tools/run-under-smart-app-control.ps1 -Target cli -Arguments 'check'
```

Vypnutie Smart App Control je od marca/apríla 2026 vratné, takže je to aj legitímna
možnosť. Podrobnosti v
[`decisions/2026-08-26-smart-app-control.md`](decisions/2026-08-26-smart-app-control.md).

Blokovanie je **po jednotlivých súboroch a nekonzistentné**, a rozhoduje pri ňom aj **meno
assembly**, nielen obsah. Keď zrazu prestane bežať niečo, čo pred chvíľou bežalo, pomáha
zmeniť `AssemblyName` alebo konfiguráciu — obidve zmenia identitu súboru. Niekedy treba
skúsiť dvakrát; je to lotéria, nie deterministické pravidlo.

Verdikt sa navyše **zhoršuje časom**: meno, ktoré mesiac prechádzalo, začne byť blokované
konzistentne. Stalo sa to `FriWorldLauncher.dll` v testovacom balíčku — po premenovaní na
`FriWorldLauncherHost.dll` to zase išlo. Preto sa na túto barlu nedá spoliehať; je to
pomôcka pri vývoji, nie riešenie pre používateľa.

Stalo sa to potom **aj `FriWorld.Launcher.Core.dll`**, a to zobralo so sebou celú testovaciu
sadu naraz — všetkých 172 testov padlo na `FileLoadException` pri načítaní tej istej knižnice.
Preto má projekt `AssemblyName` iný, než je jeho meno:

```xml
<AssemblyName>FriWorldLauncherCoreLib</AssemblyName>
```

Menné priestory sa nemenia, len súbor na disku. Padlo to potom aj na samotnej testovacej
assembly, takže tá sa volá `FriWorldLauncherSuite` — a `InternalsVisibleTo` v `Core` musí
sedieť s tým menom, nie s menom projektu.

### Premenovanie prestalo pomáhať

**27. 8. 2026 sa to zlomilo úplne.** Nové, nikdy nepoužité meno je zablokované okamžite;
zmena mena už nekupuje nič. Zmerané:

| čo | výsledok |
|---|---|
| `dotnet test` | knižnica sa nenačíta, celá sada padne |
| nové meno assembly | zablokované hneď |
| build mimo repa (`BaseOutputPath` do `%TEMP%`) | testovacia assembly sa načíta, `FriWorldLauncherCoreLib.dll` nie |
| `run-under-smart-app-control.ps1` | **funguje ďalej** |

Rozdiel medzi posledným riadkom a ostatnými nie je meno ani priečinok. Je to počet
assembly: skript zlúči zdroje `Core` a vstupného projektu do **jednej** a spustí ju cez
`dotnet exec`. Blokuje sa **referencovaná nepodpísaná knižnica načítaná za behu**, nie
proces ako taký.

**Testy sa preto na tomto stroji spustiť nedajú.** Beží ich CI, na Windows aj Linuxe, kde
Smart App Control nie je — to je od 0.1.4-alpha aj dôvod, prečo CI existuje. Lokálne zostáva
`run-under-smart-app-control.ps1` na skúšanie okna a CLI.

Keby to raz začalo prekážať, cesta von je spraviť to isté, čo robí skript: nechať testovací
projekt kompilovať zdroje `Core` priamo namiesto `ProjectReference`. Zatiaľ to nestojí za
zložitosť, ktorú by to prinieslo do build súborov.

Z rovnakého dôvodu si `run-under-smart-app-control.ps1` vyrába **nové meno pri každom
spustení**. Kto chce build medzi spusteniami cachovať, dá `-AssemblyName`.

---

## Ako si pozrieť stavy, ktoré normálne prebehnú príliš rýchlo

Lokálny zdroj skopíruje 415 MB za štyri sekundy, takže progress bar aj Cancel len bliknú.
`FRIWORLD_SIMULATED_BANDWIDTH` obmedzí `FileContentClient` na daný počet bajtov za sekundu:

```bash
FRIWORLD_SIMULATED_BANDWIDTH=20971520 dotnet run --project src/FriWorld.Launcher.Cli -- run --manifest mock/store/manifest.json --root .localroot
```

Chybové stavy sa vyvolávajú takto:

| stav | ako |
|---|---|
| poškodené stiahnutie | prepíš `sha256` v manifeste na nezmysel |
| beží hra | spusti hru a skús aktualizovať |
| launcher je starý | daj do manifestu `minLauncherVersion` vyššiu než tvoju |
| nedostupné úložisko | ukáž `--manifest` na adresu, ktorá neexistuje |
| poškodená inštalácia | zmaž súbor z `game/` a pozri, že `check` to nevidí, ale `repair` opraví |
| hra spadne pri štarte | `mock-release --stub-seconds 0` |

---

## Linux a macOS

Kód obidve platformy podporuje a testy execute bitov aj tar.gz prechádzajú. Ale:

- **overiť sa to na Windows nedá.** Na skutočné práva a symlinky treba kontajner
  `mcr.microsoft.com/dotnet/sdk:10.0` alebo Linux.
- **macOS je mimo rozsahu** — viď rozhodnutia.

---

## Konvencie

- **Bez namespace v editor skriptoch pre repo hry** — matchuje ich existujúci štýl.
- **Komentáre vysvetľujú prečo, nie čo.** Ak z kódu nie je zrejmé, prečo je niečo tak,
  patrí to do komentára. Ak je zrejmé, čo kód robí, komentár tam nepatrí.
- **Conventional commits**, vetva `master`, nikdy `git add -A`.
- **Riadok do `CHANGELOG.md` po každej dokončenej zmene**, do sekcie podľa typu.
- **Zápis do `docs/decisions/`** len keď sa rozhodovalo medzi možnosťami, narazilo sa na
  pascu, alebo príčina bola inde než prejav. Bežný feature tam nepatrí.
- **Verzia launchera** je `<Version>` v `Directory.Build.props`, nezávislá od hry.

---

## Kde hľadať, keď niečo nejde

| príznak | kde |
|---|---|
| čokoľvek za behu | `<root>/launcher.log` — píše sa tam aj to, čo okno nestihlo ukázať |
| kde to vlastne hľadá | `launcher where` |
| manifest sa nedá prečítať | `launcher check --manifest <adresa> --verbose` |
| poškodená inštalácia | `launcher repair` |
| úplne od začiatku | `launcher clean --yes` |

`--verbose` navyše zrkadlí log na stderr a vypisuje celé výnimky.
