# Nasadenie — web aj desktop

**Verzia launchera:** 0.1.8-alpha · **Dátum:** 2026-08-26

Jediný runbook pre obidve platformy. Písaný tak, aby sa podľa neho dalo nasadiť bez pamätania
si čohokoľvek — vrátane toho, aby to podľa neho vedel spraviť Claude v novej session.

**Vstup:** hotové buildy z Unity. **Výstup:** hráč si stiahne alebo zahrá novú verziu.

---

## 1. Prehľad — čo kam ide

| | web | desktop |
|---|---|---|
| build z Unity | WebGL | Windows Standalone |
| kam sa nahrá | **Cloudflare R2** | **GitHub Releases** repa hry |
| čo o ňom hovorí | `manifest.json` na R2 | `manifest.json` v repe launchera |
| ako sa k nemu hráč dostane | `fri-world-hub.vercel.app/play` | launcher |
| veľkosť | ~130 MB | ~415 MB archív |

Tri repozitáre, každý so svojou úlohou:

```
Robindhuil/FriWorld            hra, Unity projekt, GitHub Releases s archívom
Robindhuil/FriWorld-Hub        web, stránky, releases/manifest.json pre web
Robindhuil/FriWorld-Launcher   launcher, releases/manifest.json pre desktop
```

---

## 2. Pozor: dva rôzne súbory s rovnakým menom

Toto je jediná vec, na ktorej sa dá vážne pomýliť.

| súbor | čo je | kto ho číta |
|---|---|---|
| `public/game/manifest.json` (Hub) | **zoznam súborov WebGL buildu** | `/api/game` v prehliadači |
| `releases/manifest.json` (launcher) | **kontrakt o vydaní** — verzie, checksummy, adresy | launcher na disku hráča |

Nemajú spolu nič spoločné a nikdy sa nesmú zameniť. Ten prvý existuje preto, že objektové
úložisko nevie cez HTTP vypísať obsah priečinka. Ten druhý je popísaný v
[`manifest.md`](manifest.md).

---

## 3. Predpoklady

### Čo musí byť nastavené

| | |
|---|---|
| `gh` prihlásené | `gh auth status` — rozsah `repo` |
| .NET 10 SDK | `dotnet --version` |
| Node | pre web build a upload |
| R2 prihlasovacie údaje | ako **trvalé používateľské premenné**, viď nižšie |

### R2 prihlasovacie údaje

Nahrávanie webu potrebuje:

```
R2_ACCOUNT_ID
R2_ACCESS_KEY_ID
R2_SECRET_ACCESS_KEY
R2_BUCKET        nepovinné, predvolene friworld-web
```

**Nastav ich raz ako trvalé používateľské premenné** (Windows: `setx`, alebo Systém →
Premenné prostredia). Potom ich zdedí každý nový terminál a Claude vie nahrávanie spustiť
bez toho, aby hodnoty kedykoľvek videl. Nikdy ich nikam nevpisuj do repa a nikam neposielaj
v správe.

Bez nich sa dá spraviť všetko okrem samotného nahratia webu na R2.

### Čo na tomto stroji nefunguje

**Smart App Control blokuje `rclone`.** Pôvodný postup nahrával web cez `rclone sync`; na
tomto stroji sa `rclone.exe` nespustí. Náhrada je `npm run game:upload`, ktorá robí to isté
cez Node a AWS SDK — a `node.exe` podpísaný je.

Blokuje aj launcher `.exe`. Pri vývoji použi `tools/run-under-smart-app-control.ps1`,
podrobnosti v [`development.md`](development.md).

### Kde čo býva

| | |
|---|---|
| R2 bucket | `friworld-web` |
| verejná adresa R2 | `https://pub-db8f9e4528594f8e8ecd4dc13ab771fb.r2.dev` |
| premenná na Verceli | `GAME_BASE_URL` = tá istá adresa |
| web | `https://fri-world-hub.vercel.app` |
| manifest launchera | `https://raw.githubusercontent.com/Robindhuil/FriWorld-Launcher/master/releases/manifest.json` |

---

## 4. Web build

### 4.1 Vymeniť súbory

Rozbaliť WebGL export do `friworld-web/public/game/` tak, aby vznikla táto štruktúra:

```
public/game/
  Build/            <prefix>.loader.js, .data, .framework.js, .wasm, .worker.js
  StreamingAssets/
  TemplateData/
```

**Starý obsah zmazať, nie premiešať.** Dva rôzne `*.loader.js` v `Build/` znamenajú, že si
appka vyberie nesprávny. Na názve buildu nezáleží, prefix sa deteguje z `*.loader.js`.

Priečinok je v `.gitignore` — do gitu nejde.

### 4.2 Overiť lokálne

```bash
npm run dev
```

Bez nastavenej `GAME_BASE_URL` sa hra načíta z `public/game/`, takže sa dá vyskúšať ešte pred
nahratím. Otvor `/play`.

### 4.3 Vygenerovať zoznam súborov

```bash
npm run game:manifest
```

Vyrobí `public/game/manifest.json`. **Krok sa nedá preskočiť** — objektové úložisko nevie cez
HTTP vypísať obsah priečinka, takže bez tohto súboru appka build na R2 nenájde.

### 4.4 Nahrať na R2

Najprv nasucho:

```bash
npm run game:upload -- --dry
```

Prejsť výpis. Nahrávanie **zrkadlí** — čo nie je lokálne, zmaže sa aj na R2. Presne preto sa
používa, inak by tam po každom builde zostali mŕtve súbory predošlého. A presne preto sa
oplatí najprv pozrieť, čo zmizne.

Keď výpis sedí:

```bash
npm run game:upload
```

<details>
<summary>Pôvodný postup cez rclone — na tomto stroji nefunguje</summary>

```bash
rclone sync ./public/game r2:friworld-web --dry-run
rclone sync ./public/game r2:friworld-web --progress --transfers 8 \
  --header-upload "Cache-Control: public, max-age=31536000, immutable"
rclone copyto ./public/game/manifest.json r2:friworld-web/manifest.json \
  --header-upload "Cache-Control: public, max-age=60"
```

Ročná cache je zámerná: súbory majú verziu v názve, takže sa nikdy nemenia. `manifest.json`
má krátku, aby sa výmena buildu prejavila hneď.

</details>

### 4.5 Overiť na R2

```bash
curl -s https://pub-db8f9e4528594f8e8ecd4dc13ab771fb.r2.dev/manifest.json
```

Musí vypísať súbory **nového** buildu.

```bash
curl -I https://pub-db8f9e4528594f8e8ecd4dc13ab771fb.r2.dev/Build/<prefix>.wasm
```

Sleduj dve veci: `200 OK` a `Content-Type: application/wasm`. Keby tam bolo
`application/octet-stream`, prehliadač nemôže použiť streamovanú kompiláciu a načítanie je
pomalšie.

### 4.6 Zapísať verziu na web

Čísla verzií sú staticky v repe, takže sa neaktualizujú samé.

**`src/content/game.ts`** — verzia v hlavičke a pätičke:

```ts
version: '0.1.1',
status: 'Alfa',
```

**`src/content/versions.ts`** — nový záznam na začiatok poľa:

```ts
{
  version: '0.1.1',
  date: '2026-08-26',            // ISO, zoradenie ide podľa neho
  type: 'Patch',                 // 'Vydanie' | 'Patch' | 'Hotfix'
  summary: 'Krátky popis, čo prináša.',
  changes: [
    { kind: 'Pridané', items: ['Prvá zmena', 'Druhá zmena'] },
  ],
},
```

Čo písať do `items` — toto je jediná časť, ktorú hráč naozaj číta:

- **Čo sa zmenilo v hre**, nie ktorý skript. „Plynulejší pohyb kamery", nie „refactor PlayerLook".
- Jedna zmena = jedna položka, bez bodky na konci.
- **Preskoč vnútorné veci.** Ak sa to neprejaví na obrazovke, nepatrí to sem.
- Prázdne skupiny `kind` neuvádzaj.
- `summary` je **jedna veta** — objaví sa na úvodnej stránke.

Pri väčšom vydaní aj článok do `src/content/news.ts`. Changelog odpovedá na „čo sa zmenilo",
článok na „prečo". `body` renderuje zjednodušený markdown: `## ` nadpis, `- ` odrážka,
čokoľvek iné odsek. Nič ďalšie.

Potom commit a push — Vercel nasadí sám.

**Číslo verzie drž rovnaké ako prefix buildu.** Nie je to vynútené, ale keď sa raz niečo
pokazí, prefix v adrese je jediné, podľa čoho spätne zistíš, ktorý build je vonku.

### 4.7 Otvoriť

Na nasadenej stránke `/api/game` musí vrátiť adresy nového buildu a
`workerUrl: "/api/game/worker"`. Potom `/play`.

Výmena buildu samotná redeploy nevyžaduje — `GAME_BASE_URL` sa nemení a manifest sa číta za
behu. Redeploy prebehne kvôli kroku 4.6, lebo texty verzií sú v gite.

---

## 5. Desktop build

### 5.1 Zdvihnúť `bundleVersion`

V Unity, `Project Settings → Player`. Robí to **človek**, nie skript — je to jediný bod,
ktorý určuje, ako sa vydanie volá. Podľa `CLAUDE.md` repa hry sa tým aj `[Unreleased]`
v `CHANGELOG.md` premenuje na to číslo.

### 5.2 Zbuildiť playery

V Unity: **FriWorld → Build → Release**. Vznikne `Build/<bundleVersion>/win-x64/`.

Editor skript zatiaľ **nie je nasadený** — leží v [`tools/game-repo/`](../tools/game-repo/)
a treba ho skopírovať do `Assets/_Game/Editor/`. Zadanie je v
[`build-pipeline-spec.md`](build-pipeline-spec.md).

### 5.3 Zabaliť

Z repa launchera:

```bash
dotnet run --project src/FriWorld.Launcher.Cli -- pack \
  --input "<cesta k Build/<verzia>>" \
  --version <verzia> \
  --notes "Čo je nové, jedna až tri vety." \
  --base-url "https://github.com/Robindhuil/FriWorld/releases/download/v<verzia>"
```

Vznikne `dist/<verzia>/` s archívom a `manifest.json`. Balič sám vynechá Unity priečinky
označené `DoNotShip`, nastaví execute bit v tar.gz a manifest si po zápise **prečíta späť**,
takže sa nevydá súbor, ktorý by launcher odmietol.

### 5.4 Vyskúšať lokálne, ešte pred nahratím

```bash
dotnet run --project src/FriWorld.Launcher.Cli -- run \
  --manifest dist/<verzia>/manifest.json --root .localroot
```

Keď to prejde tu, na sieti už zlyhá len sieť.

### 5.5 Vydať archív hry

```bash
gh release create v<verzia> "dist/<verzia>/FriWorld-<verzia>-win-x64.zip" \
  --repo Robindhuil/FriWorld \
  --title "<verzia>" --notes-file <poznamky.md> --prerelease
```

Tag musí sedieť s tým, čo je v `--base-url`, inak adresa v manifeste nikam nevedie.

### 5.6 Zverejniť manifest

**Až teraz**, keď archív naozaj existuje:

```bash
cp dist/<verzia>/manifest.json releases/manifest.json
git add releases && git commit -m "chore: manifest oznamuje <verzia>" && git push
```

Manifest sa podáva z `raw.githubusercontent.com` — bez limitu API a s päťminútovou cache.

---

## 6. Vydanie samotného launchera

Robí sa len keď sa zmenil launcher, nie pri každom vydaní hry.

### 6.1 Zdvihnúť verziu

`<Version>` v `Directory.Build.props`. **Bez toho self-update nikdy nič nenájde** —
porovnáva na nerovnosť s tým, čo je v manifeste, nie na poradie.

Riadok do `CHANGELOG.md`, `[Unreleased]` premenovať na to číslo a otvoriť novú prázdnu.

### 6.2 Postaviť obidva assety

```powershell
./tools/build-release-package.ps1
```

Vyrobí `.exe` aj zip do `dist/launcher/<verzia>/` a **overí, že obidva nesú tú istú verziu**.
Ručné skladanie raz vydalo zip, ktorého záloha hlásila predošlú verziu — taký launcher by
donekonečna ponúkal aktualizáciu sám na seba a nemohol by ju použiť.

| asset | na čo |
|---|---|
| `FriWorldLauncher.exe` | jeden súbor. **Toto si launcher ťahá pri self-update.** |
| `FriWorld-Launcher-<verzia>-win-x64.zip` | celý balíček so zálohou pre Smart App Control |

Rozdelenie nie je kozmetické: self-update je jedno premenovanie a jeden presun. Zip by sa
musel najprv rozbaliť, čím sa z jednej atomickej operácie stane niekoľko.

### 6.3 Vydať

```bash
gh release create v<verzia> \
  "dist/launcher/<verzia>/FriWorldLauncher.exe" \
  "dist/launcher/<verzia>/FriWorld-Launcher-<verzia>-win-x64.zip" \
  --repo Robindhuil/FriWorld-Launcher \
  --title "<verzia>" --notes-file <poznamky.md> --prerelease
```

### 6.4 Oznámiť v manifeste hry

Launcher nemá vlastný manifest — vydáva sa cez sekciu `launcher` v manifeste hry:

```bash
dotnet run --project src/FriWorld.Launcher.Cli -- pack \
  --input "<Build hry>" --version <verzia hry> \
  --base-url "https://github.com/Robindhuil/FriWorld/releases/download/v<verzia hry>" \
  --launcher-version <verzia launchera> \
  --launcher-notes "Čo je v ňom nové." \
  --launcher-url "https://github.com/Robindhuil/FriWorld-Launcher/releases/latest" \
  --launcher-file "win-x64=dist/launcher/<verzia>/FriWorldLauncher.exe" \
  --launcher-base-url "https://github.com/Robindhuil/FriWorld-Launcher/releases/download/v<verzia>"
```

Potom `manifest.json` do `releases/` a pushnúť.

**Keď build hry nie je po ruke** — a po vydaní hry býva `Build/` už zmazaný — použije sa
`--launcher-only`. Prepíše **iba sekciu `launcher`** v manifeste, ktorý už existuje; sekcie
hry sa nedotkne a nechá v súbore aj to, čomu sám nerozumie:

```bash
dotnet run --project src/FriWorld.Launcher.Cli -- pack --launcher-only   --manifest releases/manifest.json   --launcher-version <verzia>   --launcher-notes "Jedna veta."   --launcher-url "https://github.com/Robindhuil/FriWorld-Launcher/releases/latest"   --launcher-file "win-x64=dist/launcher/<verzia>/FriWorldLauncher.exe"   --launcher-base-url "https://github.com/Robindhuil/FriWorld-Launcher/releases/download/v<verzia>"
```

Sha256 aj veľkosť si spočíta sám zo súboru a manifest si po zápise **prečíta späť**, takže
sa nevydá niečo, čo by launcher odmietol. Odmietne aj binárku, ktorú by launcher ticho
ignoroval — krátky sha256, nulovú veľkosť alebo adresu, ktorá nie je https.

`--drop-launcher` sekciu odstráni. To je bezpečný návrat: bez nej launcher len ukáže odkaz
na stiahnutie.

Potom to ešte over proti tomu, čo dostane hráč — nie proti lokálnej kópii:

```bash
dotnet test                                     # PublishedManifestTests číta releases/manifest.json
dotnet run --project src/FriWorld.Launcher.Cli -- check --manifest releases/manifest.json
```

`check` musí vypísať hru bez zmeny a riadok o novšom launcheri.

`--launcher-base-url` **musí byť https**. Launcher sa tým súborom nahradí, takže je tu
pravidlo prísnejšie než pri archíve hry: manifest zo zneužitého spojenia nesmie vedieť
podstrčiť spustiteľný súbor.

### 6.5 Aktualizovať odkaz na Hube

`friworld-web/src/content/launcher.ts` — verzia a adresy na stiahnutie. Jeden súbor, potom
commit a push.

### 6.6 Keď treba zamknúť staré launchery

Pridaj `--min-launcher <verzia>`. Používaj striedmo: zamkne to von **každý** launcher
v obehu pod tou verziou.

Má to zmysel len vtedy, keď je alternatívou to, že tie staré launchery spravia niečo zle —
napríklad keď manifest začne znamenať niečo, čo nevedia správne interpretovať. Samotné
pridanie poľa dôvod nie je; neznáme polia staré launchery ignorujú. Podrobne v
[`manifest.md`](manifest.md).

Pred nasadením stropu sa uisti, že v tom istom manifeste je aj sekcia `launcher`
s binárkou. Inak zamkneš ľudí von bez cesty von.

### 6.7 Skúška self-updatu

**Overené 2026-08-27** na vydaní 0.1.2-alpha: launcher 0.1.1-alpha ponuku ukázal, vymenil
sa a nová verzia nabehla. Dovtedy výmena bežala len v testoch nad skutočnými súbormi
(`SelfUpdateSwapTests`) — jednosúborový build, ktorý na to treba, Smart App Control na
vývojovom stroji blokuje, takže sa to inak nedalo vyskúšať.

Skúšku zopakuj vždy, keď sa dotkneš výmeny, zámku jednej inštancie alebo spôsobu
nasadenia. Na stroji, kde sa jednosúborový build spustiť dá:

```
[ ] stará verzia sa spustí a ohlási novú
[ ] Aktualizovať a reštartovať stiahne, overí a vymení
[ ] nová verzia nabehne sama, bez hlásenia „beží iný launcher"
[ ] pri druhom štarte zmizne súbor .superseded
[ ] hra zostane nainštalovaná a hrateľná
```

Ten tretí bod je ten, na ktorom to raz už padlo: launcher štartoval nástupcu skôr, než
uvoľnil zámok jednej inštancie.

---

## 7. Poradie, keď sa vydáva oboje naraz

```
1. web build   →  R2  →  overiť  →  texty verzií  →  push (Vercel nasadí)
2. desktop     →  pack  →  vyskúšať lokálne  →  release  →  AŽ POTOM manifest
3. launcher    →  release s binárkou  →  AŽ POTOM manifest  →  odkaz na Hube
```

**Manifest vždy až po tom, čo naň ukazuje.** Opačné poradie znamená, že launchery u ľudí
chvíľu ukazujú na súbor, ktorý neexistuje. Naopak to nevadí: kým manifest o novej verzii
nevie, hráči vidia predošlú, čo je správne.

Web a desktop sú na sebe nezávislé. Pokojne sa vydáva len jedno.

---

## 8. Overenie po nasadení

```bash
# web
curl -s https://pub-db8f9e4528594f8e8ecd4dc13ab771fb.r2.dev/manifest.json | head -5
curl -s https://fri-world-hub.vercel.app/api/game

# desktop
dotnet run --project src/FriWorld.Launcher.Cli -- check \
  --manifest https://raw.githubusercontent.com/Robindhuil/FriWorld-Launcher/master/releases/manifest.json
```

`check` musí vypísať novú verziu, správnu veľkosť archívu a — keď sa vydával aj launcher —
riadok o novšom launcheri.

Checksum sa oplatí overiť proti **súboru stiahnutému z GitHubu**, nie proti lokálnej kópii.
Zaujíma nás, čo dostane hráč, nie čo máme na disku.

---

## 9. Návrat, keď sa vydanie pokazí

**Desktop:** prepíš `releases/manifest.json` späť na predošlú verziu a pushni. Launcher
verzie neporovnáva na poradie, len na rozdiel, takže krok späť je preň bežná aktualizácia
a hráči sa vrátia sami. **Archívy predošlej verzie preto nemaž.**

**Launcher:** to isté, alebo sekciu `launcher` z manifestu úplne odstráň. Bez nej launcher
len ukáže odkaz na stiahnutie, čo je bezpečný stav.

**Web:** nahraj predošlý build a manifest. Cache manifestu je minútová, takže sa to prejaví
rýchlo; samotné súbory buildu majú v názve verziu, takže sa nekolidujú.

---

## 10. Keď to nejde

### Web

| príznak | príčina |
|---|---|
| zasekne sa na 90 %, v konzole `[object Event]` | chýba `*.worker.js` v `Build/` alebo v manifeste — spusti `game:manifest` znova a nahraj |
| „chýbajú hlavičky COOP/COEP" | hra nebeží cez Vercel; hlavičky posiela `next.config.ts` |
| „len cez zabezpečené pripojenie" | otvorené cez `http://` mimo `localhost`; viacvláknové spracovanie chce HTTPS |
| `Failed to download file`, ale `curl` vracia 200 | prehliadač drží starú neúspešnú odpoveď — súbory majú ročnú `immutable` cache. Ctrl+Shift+R |
| načítanie trvá dlho | build je nekomprimovaný; export s Brotli zmenší `.data` a `.wasm` asi na tretinu |

Worker je zvláštny zámerne: prehliadače odmietajú worker skripty z cudzej domény bez ohľadu
na CORS, tak tie 2 kB neservíruje R2, ale `/api/game/worker` z vlastnej domény. Preto
`workerUrl` nesmie ukazovať priamo na R2.

### Desktop

| príznak | príčina |
|---|---|
| `pack` nenájde platformu | podpriečinok sa musí volať presne `win-x64` / `linux-x64` |
| `pack` nevie, čo spustiť | daj `--exec win-x64=FriWorld.exe` |
| launcher hlási poškodené stiahnutie | archív na úložisku nesedí s manifestom — prebaľ a nahraj obidva |
| launcher nevidí novú verziu | manifest sa ešte neprepísal, alebo drží päťminútová cache `raw.githubusercontent.com` |
| „Tento launcher je príliš starý" | manifest má `minLauncherVersion` vyššiu než launcher u hráča |
| Windows nespustí `.exe` | Smart App Control; viď [`development.md`](development.md) |

Čokoľvek za behu launchera je v `%LOCALAPPDATA%\FriWorld\launcher.log`. Je v ňom aj to, čo
okno nestihlo ukázať.

---

## 11. Čo môže spraviť Claude a čo nie

Tento dokument je písaný aj preto, aby nasadenie vedel odviesť Claude. Hranice:

**Vie** zabaliť desktop build, vyskúšať ho lokálne, vytvoriť release cez `gh`, prebaliť
a zverejniť manifest, upraviť texty verzií, commitnúť a pushnúť, a všetko overiť.

**Nevie** zbuildiť v Unity — to musí spraviť človek.

**Nevie a nebude** zadávať prihlasovacie údaje. R2 kľúče musia byť nastavené ako trvalé
premenné prostredia; potom sa nahrávanie dá spustiť bez toho, aby ich Claude videl. Nikdy
ich neposielaj v správe.

**Nezapne a nevypne** Smart App Control ani iné bezpečnostné nastavenia.

Čo mu treba povedať, aby vedel začať: **kde sú buildy a aké je číslo verzie.** Zvyšok je
v tomto dokumente.

---

## 12. Kontrolný zoznam

**Desktop build hry**

```
[ ] bundleVersion zdvihnutá v Unity
[ ] CHANGELOG.md doplnený, [Unreleased] premenovaný
[ ] FriWorld → Build → Release prebehol bez chýb
[ ] launcher pack prebehol
[ ] lokálne overené: run --manifest dist/<verzia>/manifest.json --root .localroot
[ ] archív vydaný na GitHub Releases
[ ] manifest zverejnený AKO POSLEDNÝ
[ ] check proti ostrej adrese vracia správnu verziu
[ ] archívy predošlej verzie ponechané pre prípad návratu
```

**Web build**

```
[ ] public/game/ vymenené, starý obsah zmazaný
[ ] npm run dev, /play sa načíta
[ ] npm run game:manifest
[ ] npm run game:upload -- --dry prejdené očami
[ ] npm run game:upload
[ ] manifest na R2 vypisuje nový build
[ ] .wasm vracia 200 a Content-Type: application/wasm
[ ] src/content/game.ts a versions.ts doplnené, pushnuté
[ ] /api/game na nasadenej stránke vracia nové adresy
```

**Launcher**

```
[ ] <Version> v Directory.Build.props zdvihnutá
[ ] CHANGELOG.md doplnený
[ ] tools/build-release-package.ps1 prebehol bez chyby o nesúlade verzií
[ ] release s .exe aj zipom
[ ] pack s --launcher-* prebehol, --launcher-base-url je https
[ ] manifest zverejnený AŽ POTOM
[ ] friworld-web/src/content/launcher.ts aktualizovaný a pushnutý
[ ] binárka predošlej verzie ponechaná pre prípad návratu
```
