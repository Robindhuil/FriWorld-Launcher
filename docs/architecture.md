# Architektúra

**Verzia:** 0.1.0-alpha · **Dátum:** 2026-08-26

Ako je launcher poskladaný a prečo tak. Rozhodnutia, ktoré k tomu viedli, sú
v [`decisions/`](decisions/README.md).

---

## Tri projekty, jedna mechanika

```
FriWorld.Launcher.Core     všetka logika, žiadne UI, žiadna závislosť okrem BCL
        ├── FriWorld.Launcher.Cli    bezhlavý front end
        └── FriWorld.Launcher.App    Avalonia okno
```

`Cli` a `App` neobsahujú **žiadnu** aktualizačnú logiku. Obidva volajú ten istý
`UpdateOrchestrator` a líšia sa len tým, ako vykresľujú priebeh a ako sa pýtajú.

Dôvod je praktický: keď sa niečo pokazí, dá sa to odladiť v termináli bez okna v ceste.
Vedľajší efekt je, že UI nemôže mať chybu, ktorú CLI nemá — logika je jedna.

---

## Dve rozhrania, ktoré držia celý návrh

Toto sú jediné dve miesta, ktoré vedia, **odkiaľ** build pochádza.

### `IReleaseSource` — čo je najnovšie

Jedna implementácia, `JsonUrlReleaseSource`, číta manifest z pevnej adresy. Tá adresa
môže byť `https://…` aj `file:///…` a zvyšok programu ten rozdiel nevidí.

### `IContentClient` — ako sa k bajtom dostať

| implementácia | schéma | na čo |
|---|---|---|
| `HttpContentClient` | http, https | ostrá prevádzka, s pokračovaním cez Range |
| `FileContentClient` | file | lokálny mock a testy |
| `CompositeContentClient` | podľa schémy | rozdeľovník |

**Dôsledok, na ktorom stojí celý vývoj:** tá istá cesta kódu beží proti priečinku na disku
aj proti vzdialenému úložisku. Testy nie sú mockované okrem siete — vyrobí sa skutočný
archív, skutočne sa spočíta checksum, skutočne sa rozbalí strom aj s právami a skutočne
sa vymenia priečinky.

---

## Ako aktualizácia prebieha

```
prečítaj manifest
        │
        ├── launcher starší než minLauncherVersion?  →  zastav, povedz to
        │
porovnaj tag s installed.json
        │
        ├── zhoda a inštalácia existuje  →  hraj
        ├── nič nainštalované            →  inštaluj, netreba sa pýtať
        └── líši sa a hra je hrateľná    →  spýtaj sa
                │
        over voľné miesto (3× archív + rezerva)
                │
        stiahni do cache/         s pokračovaním cez Range
                │
        SHA256                    nezhoda → zmaž a skonči
                │
        rozbaľ do game.new/       s právami a symlinkami
                │
        game → game.old, game.new → game
                │
        zapíš installed.json
                │
        spusti; keď prežije, zmaž game.old a zavri okno
```

Posledný krok je jeden: doba odkladu po spustení hry rozhoduje **aj** o upratání predošlej
inštalácie, **aj** o tom, či sa okno zavrie. Hra, ktorá prežila, launcher už nepotrebuje.
Hra, ktorá hneď spadla, potrebuje okno, ktoré povie prečo.

### Prečo sa verzie neporovnávajú na poradie

Pravidlo je jediné: **tag v manifeste sa líši od zapísaného tagu, tak aktualizuj.**

Triediť predvydania podľa SemVer je zbytočná pasca. Launcher vždy chce presne to, čo
manifest práve menuje — aj keď je číslo nižšie. Stiahnutie zlého buildu sa tak opraví
tým, že sa manifest prepíše späť, nie vydaním novej verzie.

Jediná výnimka je `minLauncherVersion`. Tam sa **radí**, lebo strop nedáva zmysel inak.

### Prečo trojnásobok miesta

V špičke ležia na disku naraz tri kópie: archív v `cache/`, rozbalený strom v `game.new/`
a ešte neuprataná predošlá inštalácia v `game.old/`. Pri buildoch okolo pol gigabajtu je
rozdiel medzi „vyšlo to" a „došlo miesto uprostred rozbaľovania".

### Prečo sa `game.old` nemaže hneď

Maže sa až keď nová verzia raz úspešne nabehla a prežila krátku dobu odkladu. Build, ktorý
spadne pri štarte, tak nechá cestu späť — `AtomicInstaller.Rollback()` ju vie vrátiť.
Bez toho by hráč mal rozbitú inštaláciu a launcher by tvrdil, že je všetko v poriadku.

---

## Formát archívu je per platforma

| platforma | formát | prečo |
|---|---|---|
| Windows | `.zip` | natívne, práva netreba |
| Linux | `.tar.gz` | zip stráca **execute bit** na binárke |
| macOS | `.tar.gz` | `.app` obsahuje **symlinky**, zip ich rozbije |

Toto nie je vec vkusu. Zip všade znamená, že sa hra na Linuxe nespustí, lebo nie je
spustiteľná — a chyba sa prejaví až u hráča.

Navyše: `tar` vyrobený **na Windows** žiadny execute bit nezaznamená, lebo ho Windows
súborový systém nemá. Preto ho `ArchiveBuilder` nastavuje explicitne pre binárku, ktorú
manifest menuje.

---

## Rozloženie na disku

| OS | koreň |
|---|---|
| Windows | `%LOCALAPPDATA%\FriWorld\` |
| macOS | `~/Library/Application Support/FriWorld/` |
| Linux | `${XDG_DATA_HOME:-~/.local/share}/FriWorld/` |

```
FriWorld/
  game/            aktuálna inštalácia
  game.new/        rozbaľuje sa sem, kým nie je hotovo
  game.old/        predošlá — drží sa, kým nová raz nenabehne
  cache/           stiahnuté archívy, mažú sa po úspešnej inštalácii
  launcher/        priestor pre launcher
  installed.json   { version, platform, installedAt, sha256, exec, launchConfirmed }
  launcher.log
  launcher.lock    zámok jednej inštancie, mizne so zánikom procesu
```

**Nikdy nie do Program Files** — vyžadovalo by to práva správcu pri každej aktualizácii.

**Uloženia hráča tu nie sú.** Hra píše cez `Application.persistentDataPath`, teda do
`%USERPROFILE%\AppData\LocalLow\Crimsoned Rose\FriWorld`. Keď sa launcher raz zmaže
a prejde sa na Steam, dáta zostanú. Nemeniť.

---

## Konfigurácia

Poradie, najkonkrétnejšie prvé:

1. prepínač príkazového riadka (`--manifest`, `--root`)
2. premenná prostredia (`FRIWORLD_MANIFEST_URL`, `FRIWORLD_LAUNCHER_ROOT`)
3. `launcher.json` vedľa spustiteľného súboru
4. zabudovaná predvoľba

Súbor je až tretí zámerne: patrí inštalácii, nie tomuto behu, takže vývojový beh musí
vedieť ukázať inam bez editovania nasadeného súboru.

```json
{
  "manifestUrl": "https://…/manifest.json",
  "installRoot": "instalacia",
  "keepOpenAfterLaunch": false
}
```

Relatívna cesta v súbore sa počíta **od launchera**, nie od pracovného priečinka —
zástupca môže sedieť kdekoľvek.

---

## Self-update launchera

Zdrojom je sekcia `launcher` v manifeste hry. Jedno stiahnutie, jeden kontrakt.

Poradie krokov je zvolené tak, aby zlyhanie kdekoľvek nechalo funkčný launcher:

1. **Iba `https`.** Prísnejšie než pri archíve hry: launcher sa týmto súborom nahradí.
2. **SHA256 pred čímkoľvek.** Neoverený súbor sa nikdy nedostane tam, kde by sa dal spustiť.
3. **Iba jednosúborové nasadenie.** Build rozsypaný do desiatok DLL sa nedá vymeniť naraz.
4. **Premenovanie, nie prepísanie.** Bežiaci `.exe` sa na Windows prepísať nedá, premenovať
   áno. Starý sa odsunie a maže ho **až ďalší štart**.
5. **Uvoľnenie zámku pred štartom nástupcu.** Nový launcher berie ten istý zámok ako prvú vec.
6. **Návrat pri zlyhaní**, a keď zlyhá aj ten, hláška menuje cestu k odsunutému súboru.
7. **Odkaz vždy zostáva.** Keď sa vymeniť nedá, aspoň povie kam ísť.

---

## Kde čo je

| priečinok | čo |
|---|---|
| `Core/Manifest/` | tvar manifestu, čítanie a zápis, validácia |
| `Core/Sources/` | odkiaľ sa manifest berie |
| `Core/Net/` | sťahovanie, pokračovanie, priebeh |
| `Core/Verify/` | SHA256 |
| `Core/Extract/` | zip a tar.gz, ochrana proti path traversal |
| `Core/Install/` | cesty, stav, výmena priečinkov, miesto, zámok |
| `Core/Launch/` | nájdenie a spustenie hry, otvorenie prehliadača |
| `Core/Update/` | orchestrátor, self-update, preklad chýb |
| `Core/Packaging/` | výroba releasu z Unity výstupu |
| `Core/Mock/` | falošný release na vývoj |
