# FriWorld Launcher

Stiahne posledný desktop build hry [FriWorld](https://github.com/Robindhuil/FriWorld),
overí ho, nainštaluje a spustí. Aby hráč nemusel ručne sťahovať nové verzie a mazať staré.

FriWorld je interaktívna 3D prehliadka Fakulty riadenia a informatiky Žilinskej univerzity.

```
.NET 10 · Avalonia 12 · Windows a Linux · bez závislostí nad rámec Avalonie
```

---

## Čo to robí

```
prečíta manifest  →  porovná s tým, čo je nainštalované  →  spýta sa alebo nainštaluje
                                                                    │
   stiahne (s pokračovaním)  →  overí SHA256  →  rozbalí  →  vymení  →  spustí
```

- **Aktualizácia je ponuka, nie mýtna brána.** Keď už hru máš a vyjde nová verzia,
  launcher sa spýta a nechá obe možnosti.
- **Neoverený archív sa nikdy nerozbaľuje.** Nezhoda SHA256 znamená zmazať a skončiť.
- **Nainštalovaná hra zostáva hrateľná**, aj keď je server nedostupný alebo sťahovanie zlyhá.
- **Predošlá inštalácia sa drží**, kým nová raz úspešne nenabehne.
- **Oprava inštalácie** jedným tlačidlom, keď sa súbory poškodia.
- **Odinštalovanie a otvorenie priečinka s hrou** v ponuke `⋯`, keď je hra nainštalovaná.
- **Launcher sa vie aktualizovať sám**, s overením a s návratom pri zlyhaní.

---

## Rýchly štart

Build hry ešte nemusí existovať — vyrobí sa falošný:

```bash
dotnet run --project src/FriWorld.Launcher.Cli -- mock-release
```

```bash
dotnet run --project src/FriWorld.Launcher.Cli -- run --manifest mock/store/manifest.json --root .localroot
```

Prebehne celá cesta: stiahnutie, overenie, rozbalenie, výmena, spustenie. `--root`
presmeruje inštaláciu mimo skutočného `%LOCALAPPDATA%`, takže sa nič ostré neprepíše.

---

## Rozloženie

```
src/
  FriWorld.Launcher.Core/   všetka mechanika, bez UI
  FriWorld.Launcher.Cli/    bezhlavý front end — všetko sa dá odladiť bez okna
  FriWorld.Launcher.App/    Avalonia okno
tests/                      172 testov
tools/
  game-repo/                súbory, ktoré patria do repa hry
docs/
```

`Core` nevie, odkiaľ build pochádza. Rozpráva sa s `IReleaseSource` a `IContentClient`,
takže tá istá cesta beží proti priečinku na disku aj proti vzdialenému úložisku. Testy
preto nie sú mockované okrem siete.

---

## Príkazy

```bash
dotnet run --project src/FriWorld.Launcher.Cli -- help
```

| príkaz | čo robí |
|---|---|
| `where` | vypíše cesty, platformu a čo je nainštalované |
| `check` | stiahne manifest a povie, či treba aktualizovať |
| `update` | stiahne, overí, rozbalí a vymení |
| `run` | `update`, potom spustí hru |
| `play` | spustí nainštalované bez kontroly aktualizácií |
| `repair` | preinštaluje aktuálnu verziu cez poškodenú |
| `uninstall --yes` | zmaže nainštalovanú hru, log aj uložené pozície ponechá |
| `self-update` | vymení samotný launcher |
| `pack` | z Unity buildov spraví archívy, checksummy a manifest |
| `mock-release` | vygeneruje falošný release |
| `clean --yes` | zmaže celý inštalačný koreň |

---

## Konfigurácia

Poradie, najkonkrétnejšie prvé: prepínač → premenná prostredia → `launcher.json` vedľa
spustiteľného súboru → predvoľba.

```json
{
  "manifestUrl": "https://…/manifest.json",
  "installRoot": "instalacia"
}
```

| premenná | čo prepíše |
|---|---|
| `FRIWORLD_MANIFEST_URL` | odkiaľ sa číta manifest — URL alebo cesta na disku |
| `FRIWORLD_LAUNCHER_ROOT` | kam sa inštaluje |

Presun buildov na iné úložisko je preto úprava jedného riadka, nie vydanie nového launchera.

---

## Dokumentácia

| dokument | o čom |
|---|---|
| [Architektúra](docs/architecture.md) | ako je to poskladané a prečo tak |
| [Manifest](docs/manifest.md) | kontrakt medzi hrou a launcherom, pole po poli |
| [Nasadenie](docs/deploying.md) | **web aj desktop od Unity po hráča**, s kontrolnými zoznamami |
| [Vývoj](docs/development.md) | prostredie, testy, Smart App Control |
| [Zadanie pre UI](docs/ui-spec.md) | okno, stavy, tlačidlá, texty — podklad pre návrh |
| [Build pipeline](docs/build-pipeline-spec.md) | zadanie pre repo hry |
| [Rozhodnutia](docs/decisions/README.md) | prečo je niečo tak a nie inak |

---

## Rozsah

Launcher je **most k Steamu, nie produkt**. Steam si aktualizácie, delta patche aj verzie
rieši sám, takže sa sem nestavia nič, čo by aj tak zahodil.

Zámerne nie je a nebude:

- **podpis a notarizácia** — stojí peniaze; je to krok pri balení, nie kód
- **delta patchovanie** — Steam to robí lepšie a zadarmo
- **macOS** — bez Macu sa neotestuje, Gatekeeper je horší než Smart App Control
- **CI buildy** — editor skript stačí

Odôvodnenie je
v [rozhodnutiach](docs/decisions/2026-08-26-bez-podpisu-launcher-je-most-k-steamu.md).

---

## Známe prostredie

**Smart App Control na Windows 11 blokuje nepodpísané binárky.** Týka sa to launchera aj
hry, a nerieši to ani Steam — jediné, čo to rieši, je podpis.

Pri vývoji sa dá obísť skriptom:

```powershell
./tools/run-under-smart-app-control.ps1
```

Podrobnosti a zmerané kombinácie sú vo [Vývoji](docs/development.md) a
v [rozhodnutí](docs/decisions/2026-08-26-smart-app-control.md).

---

## Stav

**0.1.2-alpha.** Jadro, balenie, CLI aj okno sú hotové a overené proti skutočnému
746 MB Unity buildu; hra sa sťahuje z GitHub Releases a launcher je na stiahnutie
[na Hube](https://fri-world-hub.vercel.app/download).

**Celá cesta bola prejdená naostro**, vrátane tej poslednej: 0.1.1-alpha sa cez ponuku
v okne vymenil na 0.1.2-alpha a nová verzia nabehla. Postup na zopakovanie skúšky je
v [Nasadení](docs/deploying.md#67-skúška-self-updatu).

Otvorené: editor skript z [`tools/game-repo/`](tools/game-repo/) ešte nie je v repe hry,
logo je zatiaľ dvojfarebný text namiesto obrázka, a launcher nie je podpísaný.
