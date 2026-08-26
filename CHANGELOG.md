# Changelog

Jeden riadok na zmenu, písaný z pohľadu hráča alebo vývojára — nie zoznam súborov.
Podrobnosti sú v commite; netriviálne rozhodnutia v `docs/decisions/`.

Formát podľa [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Launcher má **vlastné číslovanie**, nezávislé od `bundleVersion` hry. Verzia je
`<Version>` v `Directory.Build.props`; keď sa zdvihne, `[Unreleased]` sa premenuje
na to číslo.

## [Unreleased]

_Nazbierané od poslednej verzie. Aktuálna verzia: **0.1.0-alpha**._

### Added
- Kostra riešenia — `Core` s celou mechanikou, `Cli` ako bezhlavý front end, `App`
  s Avalonia oknom, a testy. `Core` nezávisí na ničom okrem BCL.
- Celá aktualizačná cesta: prečítanie manifestu, porovnanie s `installed.json`,
  kontrola voľného miesta, sťahovanie s pokračovaním, overenie SHA256, rozbalenie,
  atomická výmena priečinkov, spustenie hry.
- `IReleaseSource` a `IContentClient` ako jediné miesta, ktoré vedia, odkiaľ build
  pochádza. Tá istá pipeline beží proti `file://` aj proti `https://`, takže sa dá
  vyvíjať skôr, než reálne úložisko existuje.
- `launcher mock-release` vygeneruje falošný release — tri archívy v správnych
  formátoch, checksummy a manifest. Bez neho by sa nedalo skúšať nič, kým hra
  nezačne vydávať buildy.
- `game.old` sa drží, kým sa nová verzia raz úspešne nespustí, a `AtomicInstaller.Rollback`
  ju vie vrátiť. Build, čo spadne pri štarte, tak nenechá hráča so zabetónovanou inštaláciou.
- Zámok na jednu inštanciu. Dva launchery sťahujúce do toho istého `game.new` by si
  prepisovali dáta.
- Kontrola voľného miesta počíta s **trojnásobkom** archívu — v špičke ležia na disku
  naraz archív v cache, rozbalený `game.new` a ešte neuprataný `game.old`.
- Ochrana proti path traversal pri rozbaľovaní. Archívy chodia zo siete, takže položka
  menom `../../nieco` nie je teoretická.

### Changed
- Manifest sa číta ako **statický JSON súbor na pevnej URL**, nie cez GitHub Releases API.
  Neautentizované API má strop 60 volaní za hodinu na IP a viacerí hráči za jedným NAT-om
  ho vyčerpajú. Navyše je to vrstva nepriamosti — presun buildov na iné úložisko potom
  znamená úpravu jedného súboru, nie vydanie nového launchera.
  (`docs/decisions/2026-08-26-manifest-mimo-github-api.md`)
- `exec` v manifeste musí byť cesta na **skutočnú binárku**, nie na `.app` priečinok.
  Pôvodný plán mal `"exec": "FriWorld.app"`, čo sa spustiť nedá. Launcher si vie binárku
  v bundli dohľadať, ale ohlási to ako varovanie.

- Rozsah je uzavretý: launcher je most k Steamu, nie produkt. Nepodpisuje sa nič,
  neplatí sa za nič, a nestavia sa self-update, delta patche, macOS ani CI buildy —
  Steam ich aj tak nahradí. Blokuje sa ale hra, nie launcher, a Steam podpis
  nenahradí; certifikát je otvorená vec na doriešenie s fakultou, ktorá je
  organizáciou a vie ho zaobstarať. Do tej doby je pre cieľovku tejto hry —
  žiaci škôl — hlavnou cestou web build, nie desktop.
  (`docs/decisions/2026-08-26-bez-podpisu-launcher-je-most-k-steamu.md`)

### Fixed
- Odvodené vlastnosti `PlatformPackage` sa serializovali do manifestu a padali na
  relatívnej URL. Odhalil to end-to-end test proti mock releasu.
