# Changelog

Jeden riadok na zmenu, písaný z pohľadu hráča alebo vývojára — nie zoznam súborov.
Podrobnosti sú v commite; netriviálne rozhodnutia v `docs/decisions/`.

Formát podľa [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Launcher má **vlastné číslovanie**, nezávislé od `bundleVersion` hry. Verzia je
`<Version>` v `Directory.Build.props`; keď sa zdvihne, `[Unreleased]` sa premenuje
na to číslo.

## [Unreleased]

_Nazbierané od poslednej verzie. Aktuálna verzia: **0.1.8-alpha**; ďalšie funkcie idú
ako **0.2.0-alpha**._

## [0.1.8-alpha] - 2026-08-27

### Changed
- **Otázky na zavretie a na odinštalovanie sú skutočný modal.** Boli to panely v strede okna,
  ktoré len nahradili obsah — všetko okolo zostávalo klikateľné aj natabovateľné, takže to
  bola otázka, ktorú sa dalo obísť. Teraz je to zatienené pozadie a karta v strede nad
  všetkým ostatným.
  Modal sú **tri sľuby, nie jeden**: je navrchu (poradie v strome), nič za ním sa nedá
  kliknúť (zatienenie má pozadie, takže kliknutia pohíta) a nič za ním sa nedá dosiahnuť
  klávesnicou — to posledné zatienenie nespraví, takže obsah pod ním je zakázaný; zakázané
  prvky tabulátor preskočí. Hlavné tlačidlo je pritom predvolené, takže bez toho by Enter
  prešiel modalom rovno do inštalácie.
  Fokus ide pri otvorení na bezpečnú odpoveď, lebo obsah, ktorý ho mal, oň práve zakázaním
  prišiel.
  Obidve otázky kreslí jedna karta a delia sa o jeden text a jednu bezpečnú odpoveď — dve
  naraz by ukazovali nadpis jednej s tlačidlami druhej.
- Poznámky k verzii a chybové hlásenie zostávajú pod modalom vidieť. Predtým sa skrývali,
  lebo si s ňou delili miesto; teraz ich zatienenie len potláča, čo je celý zmysel modalu.

## [0.1.7-alpha.2] - 2026-08-27

### Fixed
- **Launcher sa po zatvorení hry vrátil zaseknutý na „Kontrolujem aktualizácie".** Hlásenia
  o priebehu sa posielali cez `Dispatcher.Post` vždy, aj keď ich vyvolal samotné UI vlákno.
  Keď práca dobehla bez toho, aby sa raz uspala — čo sa pri kontrole stane, lenže sa dá
  odpovedať z cache — výsledok sa zapísal skôr a **zaradené hlásenie ho potom prepísalo späť**
  na názov fázy a pruh priebehu. Okno tak zostalo stáť na texte, ktorý kontrola nastavuje
  ešte pred začiatkom.
  Hlásenie vyvolané na UI vlákne sa teraz aplikuje hneď, takže poradie zostane zachované,
  a hlásenie, ktoré dorazí, keď už nič nebeží, sa zahodí.
  Chybu bolo vidieť len tam, kde je kontrola rýchla; preto prežila všetky doterajšie behy.

### Added
- Test celého cyklu spustenia hry nad skutočným oknom — nainštaluje, spustí, počká na koniec
  hry a trvá na tom, že sa launcher vráti pripravený. Reprodukoval chybu na oboch platformách.

## [0.1.7-alpha] - 2026-08-27

### Changed
- **Launcher sa po spustení hry už nezatvára, ale skryje — a po zatvorení hry sa vráti.**
  To, čo človek chce najskôr po dohratí, je zvyčajne práve launcher; zatvorenie znamenalo,
  že ho musel hľadať druhýkrát. Skryté, nie minimalizované: medzitým sa v ňom nedá nič robiť.
  Vráti sa a **skontroluje aktualizácie znova**, lebo sedenie môže trvať hodinu.
  Návrat okna je vo `finally`: čokoľvek vyhodené po skrytí by inak nechalo launcher bežať
  bez okna a bez spôsobu, ako sa k nemu dostať.

### Added
- **Naraz beží jedna hra.** `IsGameRunning` sa doteraz kontrolovalo pred aktualizáciou
  a odinštalovaním, ale nie pred spustením. Dve kópie zdieľajú jeden priečinok s uloženými
  pozíciami a jedny nastavenia, a tá, ktorá skončí druhá, rozhodne, čo bolo to prvé sedenie
  hodné. Platí to aj pre `launcher play`, nielen pre okno, a aj vtedy, keď hru niekto spustil
  mimo launchera.
- Zoznam bežiacich procesov je v `GameLauncher` za spojom. Mock hra je shell skript, takže
  proces, ktorý operačný systém naozaj spustí, je interpreter a skutočné snímanie ho pod
  inštaláciou nikdy neuvidí — čím by bolo každé pravidlo postavené na tomto neoveriteľné.
  Skutočný Unity build je spustiteľný súbor v tom priečinku a vidí sa bežne.

## [0.1.6-alpha] - 2026-08-27

### Added
- **Okno sa prispôsobí obrazovke.** 980 × 720 bolo pevných, čo na menšom displeji zaberalo
  väčšiu časť plochy, než launcher potrebuje.
  Nemeňuje sa rozloženie, ale **celé okno jedným faktorom** — písmo, tlačidlá aj odsadenia
  spolu s rámom. Všetko vnútri zostáva navrhnuté v jednotkách 980 × 720 a pomer strán sa
  nemení; dva rôzne faktory by render aj písmo rozťahovali.
  Medze sú najviac 50 % šírky a 65 % výšky pracovnej plochy, najmenej 0,70 a najviac 1,00 —
  odvodené z plochy 2103 × 1183, kde bola návrhová veľkosť posúdená ako správna, a položené
  kúsok nad ňu, aby jej okno zostalo plné aj po odrátaní panela úloh.
  Spodná medza ustúpi, keď by okno pretieklo cez okraj: okno bez systémovej lišty sa nedá
  pritiahnuť späť.
  Počíta sa raz, ešte pred zobrazením — okno, ktoré sa zmenší až na obrazovke, je bliknutie.

## [0.1.5-alpha.2] - 2026-08-27

### Fixed
- **Test zbieral progress do obyčajného `List`** z callbackov, ktoré `Progress<T>` púšťa na
  thread poole. Dva naraz zoznam poškodia a zhodia celý test host — na Windows zriedka, na
  Linuxe spoľahlivo. Našlo to CI.
- **Enter obchádzal fokus.** 0.1.5-alpha odchátával Enter na celom okne a spúšťal hlavné
  tlačidlo bez ohľadu na to, kde Tab skončil — posúvanie fokusu tak nerobilo nič a prstenec
  klamal o tom, čo Enter spraví. Enter teraz stlačí tlačidlo, ktoré má fokus; hlavné tlačidlo
  ho chytá cez `IsDefault`, čo platí len pre Enter, ktorý si nevzal nikto iný.

### Added
- **Otázka pred zavretím okna.** `✕` aj Escape sa najprv spýtajú. Druhý riadok otázky
  hovorí, čo to naozaj stojí — že sa sťahovanie obnoví, že rozrobenú inštaláciu bude treba
  spraviť znova, alebo že hra zostane nainštalovaná. Okno, ktoré sa zatvára samo po spustení
  hry, sa nepýta; to nie je nikto, kto sa pýta.
- Escape tým prestal zatvárať okno úplne — len sa pýta, a počas rozbaľovania ani to nie.
- **Testy nad skutočným oknom** (`Avalonia.Headless`). Smerovanie kláves sa z kódu vyčítať
  nedá — či Enter dostane zafokusované tlačidlo alebo predvolené, je vlastnosť Avalonie —
  a hádanie práve vydalo launcher, v ktorom Tab posúval fokus a Enter ho ignoroval.

### Changed
- Smart App Control **prestal púšťať testy na vývojovom stroji úplne**. Nové meno assembly
  už nepomáha; blokuje sa každá referencovaná nepodpísaná knižnica načítaná za behu.
  `run-under-smart-app-control.ps1` beží ďalej, lebo zlučuje všetko do jednej assembly.
  Testy bežia na CI. Zmerané v `docs/development.md`.

## [0.1.5-alpha] - 2026-08-27

### Added
- **Klávesnica.** Vlastný rám okna znamená, že systém neponúke žiadnu skratku sám, takže
  Enter a Escape doteraz nerobili nič.
  **Enter** stlačí hlavné tlačidlo — okrem chvíle, keď je na obrazovke otázka na
  odinštalovanie. Odpoveď na ňu sa nesmie dať tým istým Enterom, ktorým sa o sekundu skôr
  spúšťala hra.
  **Escape** ustupuje od toho, čo je najviac vpredu: odpovie na otázku *Ponechať*, zruší
  sťahovanie, alebo zavrie okno. Počas rozbaľovania a výmeny priečinkov **nerobí nič** —
  Escape je reflex a reflex nesmie zabiť proces uprostred výmeny. Zavrieť sa dá ďalej
  tlačidlom, čo je rozhodnutie.
  Poradie je v `DismissChoice` v Core, nie v okne, aby sa dalo otestovať — vrátane toho, že
  počas práce Escape nikdy nezavrie okno.
- **Fokus je vidieť.** Predvolený čiarkovaný rámček sa na fotografickom pozadí stratí;
  tlačidlá majú biely dvojpixelový obrys pri `:focus-visible`, takže sa neobjaví po kliknutí
  myšou.

### Fixed
- Smart App Control zablokoval aj **testovaciu assembly**, po `FriWorldLauncher.dll`
  a `FriWorld.Launcher.Core.dll` už tretiu v poradí, a zhodil tým celú sadu naraz.
  Volá sa teraz `FriWorldLauncherSuite`; `InternalsVisibleTo` v `Core` sedí s tým menom.
  Na CI sa to nedeje.

## [0.1.4-alpha] - 2026-08-27

### Added
- **`pack --launcher-only`** prepíše iba sekciu `launcher` v manifeste, ktorý už existuje.
  Vydanie launchera sa hry netyká, a všetok build hry býva dovtedy dávno zmazaný, takže
  celý `pack` nemá z čoho čítať — sekcia sa upravovala ručne, čím sa stratívala jediná
  záruka, ktorú balenie má: že sa manifest po zápise prečíta späť. Úprava sa robi nad
  JSON stromom, takže v súbore zostane aj to, čomu launcher nerozumie — tolerovať neznáme
  polia pri čítaní nemá cenu, ak ich nástroje ticho zahadzujú.
  `--drop-launcher` sekciu odstráni; to je bezpečný návrat.
- Režim odmietne aj binárku, **ktorú by launcher ticho ignoroval** — krátky sha256, nulovú
  veľkosť, adresu, ktorá nie je https. Vydanie by vyzeralo hotovo a self-update by potichu
  klesol na obyčajný odkaz.

### Fixed
- **Zápis manifestu prepísal `+` na `+`** v poliach, ktorých sa úprava netýkala —
  časový posun, verzia s build metadátami. Platný JSON, ale každé vydanie launchera by
  vyrábalo diff, ktorý sa nedá prečítať. Manifest sa ťahá ako JSON a nikdy nevkladá do
  stránky, takže to escapovanie nič nechráni.
- **Test potvrdenia spustenia vyhrával preteky, nie test.** Čakal, že mock stub prežije
  milisekundovú lehotu — čo na Windows platilo len preto, že `cmd.exe` sa spašťa pomaly než
  to. Na rychlejsom shelli tie preteky prehral. Stub teraz beží tri sekundy, takže lehota
  vyprší dávno pred ním. Našlo to CI hneď pri prvom behu.
- Test návratu po zlyhanej výmene launchera je označený ako windowsový. Provokuje zlyhanie
  zámkom súboru, a mimo Windows sú zámky poradné — presun by prešiel. Preskočenie je
  zámerné: test, ktorý ticho prejde bez toho, aby niečo skúsil, je horší než žiadny.
- **CI na GitHub Actions.** `dotnet build -c Release` a `dotnet test` na Windows aj Linuxe pri
  každom push a pull requeste. `PublishedManifestTests` tým prestávajú závisieť na tom, či si
  niekto spomenie ich spustiť — manifest, ktorý by sa nedal prečítať, zhodí build.

## [0.1.3-alpha] - 2026-08-27

### Changed
- **Odinštalovanie a otvorenie priečinka s hrou sú v akčnom pásme, nie v ponuke.** Boli
  schované za `⋯` v hlavičke, kde ich nikto nehľadal. Teraz sú tam, kde už ruka je: dva štvorce
  s ikonami — kľúč pre opravu, priečinok pre otvorenie — a za nimi **Odinštalovať**. Všetky
  tri sa objavia len vtedy, keď je hra nainštalovaná.
- **Opraviť už nie je popis vedľajšieho tlačidla, ale vlastná ikona.** Dáva zmysel vždy, keď
  je hra na disku — nielen vtedy, keď sa práve nedeje nič iné. Vedľajšie tlačidlo tým zostalo
  na jedinú vec, ktorú naozaj znamená: podržať si nainštalovanú verziu, keď je ponukávaná nová.
- Ponuka `⋯` je vľavo hore a **obsahuje akcie na launcheri, nie na hre** — skontrolovať
  znova a otvoriť denník. Delenie je zámerné a platí aj pre to, čo pribúdne: hra dole,
  launcher hore. Vľavo je preto, aby nebola po ceste k tomu, na čo sa naozaj kliká.
- Logo aj riadok verzie sú dvakrát väčšie.

### Added
- `run-under-smart-app-control.ps1 -Real` beží proti zverejnenému manifestu a skutočnej
  inštalácii. Bez toho sa stavy, ktoré existujú len s nainštalovanou hrou, nedali pozrieť inak
  než stiahnutím 415 MB mocku.

### Fixed
- **Smart App Control zablokoval `FriWorld.Launcher.Core.dll` a vzal so sebou celú testovaciu
  sadu** — všetkých 172 testov padlo na `FileLoadException` pri načítaní tej istej knižnice.
  Verdikt sa podľa mena zhoršuje časom, presne ako predtým pri `FriWorldLauncher.dll`, tak
  má projekt `AssemblyName` `FriWorldLauncherCoreLib`. Menné priestory sa nemenia.

### Added
- `PublishedManifestTests` čítajú `releases/manifest.json` tak, ako ho číta launcher. Keď sa
  sekcia `launcher` upraví ručne — čo sa deje vždy, keď je build hry už zmazaný — nie je inak
  nič, čo by manifest overilo pred zverejnením. Kontroluje sa aj to, že binárka launchera je
  na https.

### Changed
- `run-under-smart-app-control.ps1` si vyrába nové meno assembly pri každom spustení, lebo
  aj `FriWorldLauncherSingle` sa nakoniec zablokovalo, a stampuje ho verziou
  z `Directory.Build.props`. Predtým hlásil natvrdo `0.1.0-alpha`, takže si sám na seba
  ponúkal aktualizáciu.
- `docs/deploying.md` popisuje aj vydanie launchera bez buildu hry po ruke.

## [0.1.2-alpha] - 2026-08-27

### Fixed
- **Odinštalovanie hry sa nedalo dokončiť.** Položka v ponuke `⋯` otázku zapla, ale
  v okne nebolo nič, čo by ju vykreslilo — stred sa vyprázdnil a ďalej sa nedalo. Otázka
  teraz existuje aj vizuálne a chybové hlásenie jej priestor prepustí.
- **Záloha v zipe hlásila predošlú verziu.** Jej projekt sa skladal ručne a mal verziu
  zadrôtovanú, takže launcher spustený cez zálohu by donekonečna ponúkal aktualizáciu
  sám na seba — a nemohol by ju použiť, lebo viacsúborové nasadenie sa vymeniť nedá.
  `tools/build-release-package.ps1` teraz vyrobí obidva assety, obidva ostampuje verziou
  z `Directory.Build.props` a **sám overí**, že sa zhodujú.

### Changed
- **Nasadenie webu aj desktopu má jeden runbook** — [`docs/deploying.md`](docs/deploying.md).
  Nahradil `docs/releasing.md` a `docs/releasing-launcher.md`, ktoré popisovali len desktop,
  a pohltil postup nahrávania web buildu z repa Hubu. Rozdelený popis znamenal, že sa pri
  vydaní muselo pamätať, kde ktorá polovica je.
- `docs/ui-spec.md` doplnené o ponuku `⋯` a otázku na odinštalovanie a označené za
  záväzný popis okna; jazyk textov už nie je otvorené rozhodnutie.

## [0.1.1-alpha] - 2026-08-26

_Prvá vydaná verzia. Všetko nižšie vzniklo pred ňou._

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
  [postup nasadenia](docs/deploying.md) s kontrolným zoznamom a [vývoj](docs/development.md).
  README prepísané na to, čo projekt naozaj je.

- Simulácia rýchlosti sťahovania pre lokálny zdroj — `FRIWORLD_SIMULATED_BANDWIDTH`
  v bajtoch za sekundu. Bez nej sa 415 MB z priečinka skopíruje za štyri sekundy a progress
  bar ani tlačidlo Cancel sa nedajú ani pozrieť, nieto stlačiť. Zástupca za vzdialené
  úložisko má zastupovať aj to, ako dlho to trvá, nielen výsledok.

- Launcher sa po spustení hry zavrie. Jeho práca tam končí a druhé okno na paneli úloh
  je len neporiadok. Keď hra spadne v priebehu doby odkladu, okno **zostane** otvorené
  a povie to aj s návratovým kódom — to je jediná chvíľa, keď má launcher čo dodať.
  `keepOpenAfterLaunch` v `launcher.json` to vypne, čo sa hodí pri ladení samotného launchera.

- `mock-release --stub-seconds <n>` — falošná hra zostane nažive daný počet sekúnd.
  Doteraz sa vypísala a hneď skončila, čo je z pohľadu launchera build padnutý pri štarte.
  Úspešný štart sa tým nedal vyskúšať vôbec: nezapísalo sa potvrdenie, neuprataná zostala
  predošlá inštalácia a okno sa nezavrelo. Nula je stále predvolená, takže sa dá vyvolať
  aj ten pád.

- Okno má **jedno hlavné tlačidlo**, ktoré mení popis podľa stavu: Install, Update, Play,
  Retry. Vedľa neho je tichšie tlačidlo pre alternatívu, ktorá vtedy dáva zmysel — hrať
  nainštalovanú verziu namiesto aktualizácie, alebo opraviť inštaláciu.
- **Launcher už nič neinštaluje sám.** Doteraz sa pri prázdnej inštalácii pustil do
  sťahovania hneď po otvorení okna. Rozhodnutie stiahnuť stovky megabajtov nepatrí
  launcheru len preto, že niekto otvoril okno.
- Bez nainštalovanej hry nie je tlačidlo Play, ale Install. Zobrazí sa aj veľkosť
  sťahovania, aby človek vedel, do čoho ide.
- `LauncherActions` v `Core` — rozhodnutie, čo ponúknuť, je vec domény, nie okna. Vďaka tomu
  sa dá otestovať bez UI, a testy naň sú.

- `docs/ui-spec.md` — zadanie pre návrh okna. Úplný zoznam stavov, tlačidiel a textov,
  ktoré launcher zobrazuje, plus pravidlá, ktoré návrh nesmie obísť. Otvorené je v ňom
  jedno rozhodnutie: UI je po anglicky, ale cieľovka sú slovenskí žiaci.

- Okno prekreslené podľa handoffu z Claude Designera: 980 × 720, bez systémového rámu,
  vlastná hlavička s minimalizáciou a zatváraním, render FRI ako pozadie so scrimom,
  vlastný kurzor, akcentová farba `#FBB800` len na logu, hlavnom tlačidle a pruhu priebehu.
- **Texty pre hráča sú po slovensky.** Vývojárske výstupy (nápoveda CLI, `pack`) zostávajú
  anglické — delenie je podľa publika, nie podľa projektu.
- Chybový panel nesie slovo „CHYBA", nie iba farbu. Farba sama vylučuje toho, kto ju nerozlíši.
- Stred okna je zarovnaný k spodku, takže chyby a priebeh rastú nahor a akčné pásmo sa
  nehýbe pod kurzorom.
- Riadok verzie má pevnú výšku aj keď je prázdny — inak by pri prvom načítaní poskočilo logo.

- Samotná výmena launchera je otestovaná na skutočných súboroch. `LauncherDeployment` robí
  z cesty k spustiteľnému súboru a z tvaru nasadenia hodnotu, ktorá sa dá podstrčiť, takže
  `Apply` sa dá vykonať naozaj — vrátane toho najnebezpečnejšieho okna, keď je starý súbor
  premenovaný nabok a nový sa nedá presunúť. Doteraz boli pokryté len poistky okolo.

- Prvý release launchera, `v0.1.0-alpha`, s dvoma assetmi. `.exe` je jeden súbor a je to
  presne to, čo si launcher ťahá pri aktualizácii; zip navyše nesie zálohu pre prípad,
  že Smart App Control `.exe` odmietne. Adresa v manifeste ukazuje na `.exe`, nikdy na zip —
  výmena je jedna atomická operácia a rozbaľovanie by z nej spravilo niekoľko.
- Manifest hry teraz nesie aj binárku launchera, takže **self-update je zapnutý**.
  Postup vydávania je v `docs/deploying.md` vrátane kontrolného zoznamu na
  prvú ostrú skúšku.

- Odinštalovanie hry a otvorenie priečinka s hrou. Obidve sú zriedkavé akcie, tak sedia pod
  `⋯` vedľa zatvárania, nie v akčnom pásme — štvrté tlačidlo vedľa hlavného by otupilo to,
  na ktorom záleží. Zobrazia sa len keď je hra nainštalovaná.
- Odinštalovanie **sa pýta**. Zmaže stovky megabajtov a vrátiť sa to nedá, tak to nie je
  jedno kliknutie. Otázka je v okne, nie v dialógu, lebo tam je aj všetko ostatné.
- Odinštalovanie **nechá log**. Najpravdepodobnejší dôvod, prečo niekto odinštaluje, je že sa
  niečo pokazilo — a log je jediný záznam o tom. Zmazať ho spolu s hrou by zahodilo dôkaz.
  Uloženia hráča sú mimo inštalácie, takže sa ich to nedotkne vôbec.
- `launcher uninstall --yes` robí to isté bez okna. Oproti `clean`, ktorý zmaže celý koreň
  vrátane logu.

### Fixed
- **`launcher.log` ticho zahadzoval riadky, keď mal súbor otvorený iný proces.** Zapisovalo sa
  cez `File.AppendAllText`, čo súbor zakaždým otvorí so `FileShare.Read` — stačilo, že ho čítal
  antivírus alebo indexer, a zápis zlyhal. Výnimka sa prehltla, takže po sebe nenechal ani stopu.
  Zmerané na tomto stroji: **200 z 200 riadkov stratených.** Nie časť, všetky.

  Prejavilo sa to presne tam, kde to bolí najviac — celá inštalácia 745 MB neostala v logu ani
  jedným riadkom, hoci prebehla správne. Log je pritom jediná diagnostika, ktorú máš, keď to
  zlyhá u cudzieho človeka.

  Handle sa teraz otvára raz a drží so `FileShare.ReadWrite`. Keď sa zápis aj tak nepodarí,
  počíta sa a pri najbližšom úspešnom zápise log **sám prizná**, koľko riadkov chýba — log,
  ktorý vyzerá úplný a nie je, je horší než ten, čo povie, kde má dieru.
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
