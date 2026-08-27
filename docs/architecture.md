# Architektúra

**Verzia:** 0.1.8-alpha · **Dátum:** 2026-08-26

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
        spusti; keď prežije, zmaž game.old a skry okno
```

Posledný krok je jeden: doba odkladu po spustení hry rozhoduje **aj** o upratání predošlej
inštalácie, **aj** o tom, či sa okno skryje. Hra, ktorá prežila, launcher medzitým
nepotrebuje — vráti sa, keď skončí. Hra, ktorá hneď spadla, potrebuje okno, ktoré povie
prečo, takže to nezmizne.

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

## Čo okno ponúka

Jedno hlavné tlačidlo, ktoré mení význam podľa stavu — tak, ako to robí bežný herný launcher.
Rozhodnutie robí `LauncherActions` v `Core`, nie okno: je to otázka o tom, čo je na disku
a čo hovorí manifest, nie o tom, ako vyzerá UI.

| stav | hlavné tlačidlo | vedľajšie |
|---|---|---|
| nič nainštalované | **Inštalovať** | — |
| nainštalované a aktuálne | **Hrať** | — |
| je novšia verzia | **Aktualizovať** | Hrať *(stará verzia)* |
| launcher je pristarý | **Hrať**, ak je čo hrať | — |
| zlyhalo pred prvou kontrolou | **Skúsiť znova** | — |
| práve pracuje | zakázané | Zrušiť *(pri priebehu)* |

Naľavo od nich sú tri akcie na hre, ktoré sa objavia **len keď je hra nainštalovaná**: dva
štvorce s ikonami — kľúč pre opravu, priečinok pre otvorenie — a **Odinštalovať**. Poradie
je zámerné: každé ďalšie doprava je hlasnejšie a hlavné je najširšie. Odinštalovanie stojí
vedľa hrania, tak nesmie vyzerať ako jeho rovnocenný sused; hlavnú farbu nenesie nikdy.

Ponuka `⋯` vľavo hore nesie **akcie na launcheri**, nie na hre: skontrolovať znova
a otvoriť denník. To delenie platí aj pre to, čo pribudne — hra dole, launcher hore.

**Klávesnica.** Enter stlačí tlačidlo, ktoré má fokus; hlavné ho chytá cez `IsDefault`, čo
platí len pre Enter, ktorý si nevzal nikto iný. Escape ustúpi od toho, čo je najviac vpredu
— odpovie na otázku, zruší sťahovanie, alebo sa spýta na zavretie. Sám nezavrie nikdy
a počas rozbaľovania nespraví nič. Poradie je v `DismissChoice` v `Core`.

**Otázky sú modal.** Zavretie aj odinštalovanie zatienia okno a položia kartu do stredu.
Obsah pod ňou je zakázaný, nie len prekrytý — zatienenie zastaví kliknutia, ale zakázané
prvky navyše preskočí tabulátor, a hlavné tlačidlo je predvolené, takže inak by ním Enter
prešiel rovno do inštalácie.

**Veľkosť okna sa počíta zo screenu.** Vnútro je navrhnuté v jednotkách 980 × 720
a `WindowFit` v `Core` vráti jeden faktor pre celok. Podrobne v
[rozhodnutí](decisions/2026-08-27-okno-sa-skaluje-jednym-faktorom.md).

Odinštalovanie zmaže `game`, `game.new`, `game.old` a `cache`, ale **log necháva**.
Najpravdepodobnejší dôvod, prečo niekto odinštaluje, je že sa niečo pokazilo, a log je
jediný záznam o tom. Uloženia hráča sú mimo inštalačného koreňa, takže sa ich to netýka.

**Nič veľké sa nedeje samo.** Otvorenie launchera skontroluje, čo je vonku, a potom čaká.
Stiahnuť stovky megabajtov preto, že niekto otvoril okno, nie je rozhodnutie launchera —
a to platí aj vtedy, keď je to jediná zmysluplná vec, ktorú by človek spravil.

---

## Kým hra beží

Launcher sa po spustení hry **skryje a po jej zatvorení vráti**. Nezatvára sa: to, čo človek
chce najskôr po dohraní, je zvyčajne práve launcher.

```
Hrať  →  spustenie  →  ochranná lehota 5 s
                            │
              spadla ───────┤────── beží
                 │          │         │
      okno zostane      okno sa skryje
      a povie to        (aj z panela úloh)
                                      │
                            hra skončí │
                                      ▼
                        okno sa vráti a skontroluje znova
```

Návrat okna je vo `finally`. Čokoľvek vyhodené po skrytí by inak nechalo launcher bežať bez
okna a bez spôsobu, ako sa k nemu dostať — pričom stále drží zámok jednej inštancie.

**Naraz beží jedna hra.** `GameLauncher.IsGameRunning` hľadá proces, ktorý beží z priečinka
inštalácie, a `LaunchAsync` pri ňom odmietne spustiť ďalší. Dve kópie zdieľajú jeden
priečinok s uloženými pozíciami a jedny nastavenia, a tá, ktorá skončí druhá, rozhodne, čo
bolo to prvé sedenie hodné. To isté pravidlo dávno platí pre aktualizáciu a odinštalovanie,
kde ide o niečo iné: na Windows sa otvorený súbor nedá premenovať preč.

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
| `Core/Packaging/` | výroba releasu z Unity výstupu, prepis sekcie `launcher` |
| `Core/Platform/` | cesty, kľúče platforiem, veľkosť okna |
| `Core/Mock/` | falošný release na vývoj |
| `App/ViewModels/` | čo okno hovorí a čo tlačidlá robia |
| `App/` | `MainWindow.axaml` a jeho code-behind |
| `Cli/` | rozbor prepínačov a jednotlivé príkazy |

Testy sú v dvoch projektoch, lebo testujú dve rôzne veci:

| projekt | čo |
|---|---|
| `Core.Tests` | mechanika bez UI — manifest, sťahovanie, výmena, balenie, rozhodnutia |
| `App.Tests` | skutočné okno cez `Avalonia.Headless` — klávesy, fokus, modal, veľkosť |

Druhý existuje preto, že smerovanie klávesov a poradie vykonania na UI vlákne sa zo zdrojáku
vyčítať nedá; viď [pasce v okne](decisions/2026-08-27-dve-pasce-v-okne.md).

Obidva bežia na CI pri každom push, na Windows aj Linuxe. Na vývojovom stroji ich Smart App
Control už nespustí — [prečo](decisions/2026-08-27-testy-bezia-na-ci.md).
