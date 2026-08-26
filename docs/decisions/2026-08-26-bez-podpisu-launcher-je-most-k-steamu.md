# Bez podpisu. Launcher je most k Steamu, nie produkt

**Verzia:** 0.1.0-alpha · **Dátum:** 2026-08-26

## Kontext

Hra je v alfa verzii, testuje sa. Projekt nezarába a nebude sa doňho investovať.
Cieľový domov je **Steam**, ktorý si aktualizácie, delta patche aj verzie rieši sám.
Launcher je teda dočasná vec na obdobie pred Steamom, nie niečo, čo má prežiť do plnej verzie.

Predchádzajúci zápis ([Smart App Control](2026-08-26-smart-app-control.md)) nechal otvorené,
či sa kupuje code signing certifikát. Preverené možnosti:

| možnosť | cena | verdikt |
|---|---|---|
| Azure Artifact Signing | $9.99/mesiac | platené — nie |
| Klasický OV certifikát | 300–500 €/rok + hardvérový token | platené — nie |
| Microsoft Store (MSIX) | ~$19 jednorazovo | platené, a Store aj tak nahradí launcher |
| SignPath Foundation (zadarmo pre OSS) | 0 € | **nedá sa** — viď nižšie |
| Self-signed certifikát | 0 € | Smart App Control ho neuzná |

SignPath Foundation dáva podpisovanie zadarmo, ale žiada **uznávanú open source licenciu
a verejný zdroják**. Repo hry licenciu nemá a mať ju nemôže: `Assets/ThirdParty/` obsahuje
platený obsah z Unity Asset Store (napríklad `AdvancedInputField`), ktorý sa
preposielať pod OSS licenciou nesmie. Tým možnosť padá, nezávisle od toho, či by sa
projekt otvoriť chcel.

## Rozhodnutie

**Nepodpisuje sa nič. Neplatí sa za nič.** Smart App Control a SmartScreen sa akceptujú
ako daň za dočasné riešenie.

Z toho plynie, čo sa **nesmie** stavať, lebo to Steam aj tak zahodí:

- **Žiadny self-update launchera.** Bola to najdrahšia a najrizikovejšia fáza plánu.
  Launcher len zistí, že existuje novšia verzia jeho samého, a odkáže na Hub.
- **Žiadne delta patchovanie.** Steam to robí lepšie a zadarmo.
- **Žiadne macOS.** Bez Macu sa neotestuje, Gatekeeper je horší než Smart App Control
  a je to tretina objemu každého releasu navyše.
- **Žiadne CI buildy.** Editor skript stačí.

A čo sa **musí** spraviť, lebo to je zadarmo a inak to hráča zablokuje:

- **Manifest a archívy hostovať na niečom bez rate limitu a bez faktúry.** GitHub Releases
  na verejnom repe pre archívy, statický `manifest.json` vedľa Hubu.
  Viď [manifest mimo GitHub API](2026-08-26-manifest-mimo-github-api.md).
- **Web build postaviť na Hube ako hlavnú cestu, nie ako núdzový odkaz.** Pri cieľovke
  tejto hry je to tak či tak správne poradie; viď „Komu sa čo hovorí" nižšie.
- **Nechať v build pipeline jedno miesto na podpis.** Nie podpisovať — len nerozhádzať
  kroky tak, aby sa podpis neskôr nedal doplniť jedným krokom. Stojí to nula.

## Dôsledky

### Blokuje sa hra, nie launcher

`FriWorld.exe` z Unity je nepodpísané. Smart App Control ho zastaví bez ohľadu na to,
ako sa na disk dostalo:

| cesta k hre | prejde? |
|---|---|
| cez launcher | nie |
| ručne stiahnutý a rozbalený archív | nie |
| **cez Steam** | **tiež nie** |

Steam to nerieši. Code Integrity hlási, že `Steam.exe` sa pokúsil načítať herné `.exe`,
ktoré nespĺňa požiadavky na podpis; trafilo to aj veľké tituly. Steam teda nahradí
aktualizácie, delta patche a verzie — **nenahradí podpis**.

Dôležitý dôsledok: launcher nič nezhoršuje a jeho zahodenie nič nezíska.

### Vypnutie Smart App Control je od jari 2026 vratné

Pôvodne platilo, že vypnutie je trvalé a späť sa dá len reinštaláciou Windows.
**Marcový a aprílový kumulatív 2026** (KB5079391/KB5086672, KB5083769) to zmenili —
prepína sa to v Windows Security → App & browser control → Smart App Control settings,
oboma smermi, bez resetu.

Cena tohto rozhodnutia tým klesla. Ale neklesla na nulu, viď nižšie.

### Komu sa čo hovorí

Publikum nie je jedna skupina a nedostane rovnakú inštrukciu.

**Známym testerom** — spolužiaci, vedúci práce, ľudia, s ktorými sa dá hovoriť — sa návod
na vypnutie Smart App Control dať môže. Rozumejú tomu a rozhodnú sa sami.

**Na verejnú stránku Hubu nie.** Cieľovka hry sú žiaci základných a stredných škôl a je to
náborový produkt fakulty. Inštrukcia „vypni si bezpečnostnú funkciu Windows" je presne to,
čo hovorí malware, učí zlý reflex a na školskom spravovanom počítači ju žiak aj tak nespraví.

Z toho plynie, že **pre túto hru je web build hlavný kanál, nie náhradné riešenie**. Nič sa
neinštaluje, ide na školskom počítači aj na Chromebooku. Desktop build je verzia pre
nadšenca, ktorý si ju sťahuje domov. Pri takomto rozdelení sa Smart App Control hlavnej
cesty netýka.

### Podpisovať nemusí autor

Azure Artifact Signing je dostupné **organizáciám z EU**. UNIZA organizácia je — má IČO,
doménu aj právnu entitu, a náborový produkt je v jej záujme. Buď certifikát na podpisovanie
softvéru má, alebo ho vie zaobstarať.

Toto je otvorená vec na doriešenie s fakultou. Nie je blokujúca: podpis je posledný krok
pri balení, nie vec, ktorá by ovplyvnila čokoľvek v kóde.

### Aby sa podpis dal doplniť jedným krokom

Jediná vec, ktorú treba dodržať už teraz, je **poradie krokov v build pipeline**:

```
build  →  PODPIS  →  archív  →  SHA256  →  manifest
```

Podpis mení obsah súboru, takže musí prebehnúť **pred** zabalením aj pred hashovaním.
Pipeline postavená ako `build → archív → hash → podpis` sa neskôr nedá doplniť, musí sa
prerobiť. Miesto pre podpis sa teda v skripte nechá prázdne, ale na správnom mieste.

### Uloženia hráča sú v poriadku

Hra píše cez `Application.persistentDataPath`, čo je
`%USERPROFILE%\AppData\LocalLow\Crimsoned Rose\FriWorld`, teda mimo inštalačného koreňa
launchera. Keď sa launcher raz zmaže a prejde sa na Steam, dáta zostanú. Toto je jediná
vec, ktorá by sa neskôr opravovala draho, a je už dobre. Nemeniť.

**Kedy sa toto rozhodnutie prehodnocuje:** keď fakulta poskytne certifikát (viď vyššie —
otvorené), keď hra začne zarábať, alebo keď sa ukáže, že Smart App Control blokuje viac
testerov, než je únosné. Do tej doby nie.
