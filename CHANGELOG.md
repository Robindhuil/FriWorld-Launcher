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
- `launcher pack` — z Unity výstupu vyrobí archívy, checksummy a manifest. Balenie je
  na strane launchera zámerne: Unity beží na staršom .NET a tar writer nemá vôbec,
  a `tar` vyrobený na Windows stratí execute bit, takže by sa linuxový build nespustil.
  Hlavný dôvod je ale, že manifest je kontrakt medzi dvoma repami — takto ho píše aj
  číta ten istý kód a nemôže sa rozísť. Zadanie pre repo hry je v `docs/build-pipeline-spec.md`.
- Upozornenie na novšiu verziu launchera. Manifest smie niesť voliteľnú sekciu `launcher`;
  keď sa verzia líši od bežiacej, okno aj `check` ponúknu odkaz na stiahnutie. Launcher
  sa **nikdy nevymieňa sám** — je to najkrehkejšia časť a Steam ho aj tak nahradí.
  Adresa z manifestu sa otvára len keď je `http` alebo `https`.

- `launcher.json` vedľa spustiteľného súboru — nepovinný, nesie `manifestUrl` a `installRoot`.
  Bez neho by bola adresa manifestu zadrôtovaná v binárke, čo znamená iný build pre každé
  nasadenie. Poradie: prepínač, potom premenná prostredia, potom tento súbor, potom
  zabudovaná predvoľba. Relatívna cesta v súbore sa počíta od launchera, nie od pracovného
  priečinka — spúšťač môže byť kdekoľvek. Rozbitý súbor launcher nezastaví.

- Aktualizácia je ponuka, nie mýtna brána. Keď je nainštalovaná hrateľná verzia a vyjde
  nová, launcher sa spýta a ponúkne obe možnosti naraz. Bez nainštalovanej hry sa
  neinštaluje z čoho vyberať, tak sa nepýta.
- Self-update launchera. Zdrojom je sekcia `launcher` v manifeste hry. Iba `https`,
  iba jednosúborové nasadenie, SHA256 pred výmenou, starý súbor sa maže až ďalším štartom
  a pri zlyhaní sa vracia späť. Keď sa vymeniť nedá, zostáva odkaz na stiahnutie.
  (`docs/decisions/2026-08-26-launcher-raz-a-poriadne.md`)
- `minLauncherVersion` v manifeste. Jediné miesto, kde sa verzie radia. Bez neho by sa
  formát manifestu nedal nikdy zmeniť — tolerancia neznámych polí pomôže len dovtedy, kým
  ich ignorovanie ešte dáva správny výsledok.
- `repair` — preinštaluje aktuálnu verziu cez poškodenú. Kontrola verzií porovnáva iba tagy,
  takže chýbajúci súbor nevidí.
- `play` — spustí nainštalované bez kontroly aktualizácií.
- Zrušenie sťahovania tlačidlom. Rozstiahnutá časť zostáva a pokračuje sa pri ďalšom behu.
- `FailureMessages` — jedno miesto, ktoré prekladá výnimky na vetu, radu a príznak, či má
  zmysel skúsiť znova. Používa ho okno aj CLI, takže tú istú poruchu nemôžu opísať inak.

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

- `tools/game-repo/BuildRelease.cs` — editor skript pre repo hry. Zbuildí desktop playery
  do `Build/<bundleVersion>/<platformKey>/` a skončí. Balenie robí `launcher pack`.
  Linux je prepínač v menu, predvolene vypnutý. Skript `bundleVersion` len číta.

- Dokumentácia: [architektúra](docs/architecture.md), [manifest ako kontrakt](docs/manifest.md),
  [postup vydania](docs/releasing.md) s kontrolným zoznamom a [vývoj](docs/development.md).
  README prepísané na to, čo projekt naozaj je.

### Fixed
- **Self-update štartoval nový launcher, kým starý ešte držal zámok jednej inštancie.**
  Nový by ako prvú vec ohlásil, že beží iný launcher, a aktualizácia by vyzerala, že všetko
  rozbila. Zámok sa uvoľní pred štartom nástupcu a `TryAcquire` chvíľu počká.
- **Cancel na už uvoľnenom `CancellationTokenSource` zhodil aplikáciu.** Z obsluhy tlačidla
  je neošetrená výnimka koniec procesu.
- **`Detail` sa v dvoch stavoch nastavoval a nikdy nezobrazil** — pri ponuke aktualizácie
  a po zrušení. Vykresľoval sa iba v paneli priebehu a v paneli chyby, takže práve tie
  upokojujúce vety boli neviditeľné.
- Tlačidlo Repair svietilo, aj keď nebolo nič nainštalované.
- `UpdateException` padala do vetvy „Something went wrong" namiesto vlastnej vety.
- Do releasu sa balil priečinok `FriWorld_BurstDebugInformation_DoNotShip`. Unity ho vyrába
  vedľa hry a v názve sám hovorí, že sa nemá posielať — sú v ňom debug symboly a absolútne
  cesty z build stroja. Balič ho aj `*_BackUpThisFolder_ButDontShipItWithYourGame` vynecháva
  a vypíše, čo vynechal. Odhalilo sa to až na skutočnom Unity builde.
- Chyby v okne sa nikde nezapisovali. Launcher, ktorý padne na cudzom stroji, sa diagnostikuje
  z `launcher.log` alebo nijak, takže `Fail` teraz loguje aj s výnimkou.
- Riadok „Starting" v konzolovom výstupe sa neukončil a výstup hry sa naň nalepil.
- Odvodené vlastnosti `PlatformPackage` sa serializovali do manifestu a padali na
  relatívnej URL. Odhalil to end-to-end test proti mock releasu.
