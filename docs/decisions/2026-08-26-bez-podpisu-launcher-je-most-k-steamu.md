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
- **Web build ako záchranná sieť.** Hráč, ktorému Smart App Control launcher zablokuje,
  nemá ako kliknúť ďalej — SmartScreen má „Run anyway", Smart App Control nie. Jediná
  odpoveď, ktorá preňho zostáva, je „hraj v prehliadači". Ten build už existuje, takže
  to nestojí nič, len odkaz na Hube.
- **Nechať v build pipeline jedno miesto na podpis.** Nie podpisovať — len nerozhádzať
  kroky tak, aby sa podpis neskôr nedal doplniť jedným krokom. Stojí to nula.

## Dôsledky

Malá časť hráčov launcher nespustí. Sú to majitelia čerstvo nainštalovaných Windows 11,
kde Smart App Control prežil evaluation režim. Na strojoch, kde sa bežne inštaluje
nepodpísaný softvér — čiže na herných — sa vypína sám. Presné číslo nevieme a nemá zmysel
ho odhadovať; dôležité je, že tí ľudia majú kam ísť.

Podpisu sa netýka len launcher. Unity vyrába `FriWorld.exe` tiež nepodpísané a launcher ho
spúšťa ako podproces, takže aj keby sa launcher raz podpísal, hru treba podpísať zvlášť.
Ďalší dôvod nerobiť to teraz na polovicu.

Uloženia hráča sú v poriadku a **migráciu na Steam prežijú**. Hra píše cez
`Application.persistentDataPath`, čo je `%USERPROFILE%\AppData\LocalLow\Crimsoned Rose\FriWorld`,
teda mimo inštalačného koreňa launchera. Keď sa launcher raz zmaže, dáta zostanú.
Toto je jediná vec, ktorá by sa neskôr opravovala draho, a je už dobre.

**Kedy sa toto rozhodnutie prehodnocuje:** keď hra začne zarábať, keď pribudne IČO a web
na vlastnej doméne (Artifact Signing je pre EU organizácie dostupné), alebo keď sa ukáže,
že Smart App Control blokuje viac testerov, než je únosné. Do tej doby nie.
