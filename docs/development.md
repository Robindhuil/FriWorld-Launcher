# Vývoj

**Verzia:** 0.1.0-alpha · **Dátum:** 2026-08-26

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
a manifest. Falošná „hra" je skript, ktorý vypíše riadok a skončí.

`--root .localroot` presmeruje inštaláciu mimo skutočného `%LOCALAPPDATA%`, takže sa nič
ostré neprepíše. Priečinok je v `.gitignore`.

---

## Testy

```bash
dotnet test
```

129 testov. Pipeline **nie je mockovaná okrem siete**: vyrobí sa skutočný archív, skutočne
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

Blokovanie je **po jednotlivých súboroch a nekonzistentné**. Keď `dotnet test` zrazu padne
na tú istú chybu, pomáha preložiť to v inej konfigurácii (`-c Release`), lebo sa zmení hash.

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
