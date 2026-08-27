# FriWorld Launcher — zadanie pre návrh UI

**Verzia launchera:** 0.1.1-alpha · **Dátum:** 2026-08-26 · **Stav:** navrhnuté a postavené

Pôvodne zadanie pre návrh, dnes **záväzný popis okna**. Hovorí, čo okno obsahuje, kedy sa
čo zobrazí a čo sa nesmie zmeniť — bez znalosti kódu.

Návrh, ktorý z neho vznikol, je v [`ui-handoff.md`](ui-handoff.md); okno v aplikácii ho
sleduje. Texty v tomto dokumente sú miestami po anglicky, ako boli v zadaní —
**v aplikácii sú všetky po slovensky**, viď sekciu 8.

---

## 1. Čo to je

Launcher k hre **FriWorld** — interaktívnej 3D prehliadke Fakulty riadenia a informatiky
Žilinskej univerzity. Hra je náborový produkt fakulty, cieľovka sú **žiaci základných
a stredných škôl**.

Launcher robí jedinú vec: stiahne hru, overí ju, nainštaluje a spustí. Počas hry sa skryje
a po jej zatvorení sa vráti. Beží pár desiatok sekúnd na jedno použitie, takže **musí byť čitateľný na prvý
pohľad, nie objavovateľný**.

---

## 2. Okno

| vlastnosť | hodnota |
|---|---|
| návrhová veľkosť | **980 × 720** |
| skutočná veľkosť | podľa obrazovky, viac nižšie |
| zmena veľkosti užívateľom | **nie** — pevná |
| systémový rám | **žiadny** |
| systémová lišta s názvom | **žiadna** |
| zatváranie | **vlastné tlačidlo** |
| minimalizácia | zvážiť, viď nižšie |
| pozícia pri štarte | stred obrazovky |

Okno musí ísť **ťahať za pozadie** — bez systémovej lišty inak niet za čo chytiť.

### Veľkosť podľa obrazovky

**Všetko v okne je navrhnuté v jednotkách 980 × 720** — veľkosti písma, výšky tlačidiel,
odsadenia. Menšia obrazovka preto neznamená iné rozloženie, ale **celé okno zmenšené jedným
faktorom**. Pomer strán sa nikdy nemení.

Faktor sa ráta z pracovnej plochy obrazovky, teda bez panela úloh:

| medza | hodnota | prečo |
|---|---|---|
| najviac šírky | 50 % | širšie a okno prestáva pôsobiť ako okno |
| najviac výšky | 65 % | výška je na notebookoch tá, ktorá dochádza skôr |
| najmenej | 0,70 | nižšie už stavový riadok klesá pod 10,5 px |
| najviac | 1,00 | väčšie nemá čo ukázať a render by zmäkol |

Medze sú odvodené z plochy **2103 × 1183**, kde bola návrhová veľkosť posúdená ako správna.
Ležia kúsok nad tým, čo tá plocha potrebuje, aby jej okno zostalo plné aj po odrátaní panela.

| pracovná plocha | okno |
|---|---|
| 2103 × 1183 a viac | 980 × 720 |
| 1920 × 1032 | 913 × 671 |
| 1600 × 852 | 754 × 554 |
| 1366 × 728 | 686 × 504 |
| 1024 × 728 | 686 × 504 |

Spodná medza smie žiadať viac, než obrazovka má; vtedy ustúpi, aby sa okno na plochu zmestilo.
Okno bez systémovej lišty sa nedá pritiahnuť späť, keď raz pretečie cez okraj.

Počíta sa raz, **ešte pred zobrazením okna** — okno, ktoré sa zmenší až keď už je na
obrazovke, je bliknutie, ktoré si človek všimne.

**Otvorené:** má byť aj tlačidlo minimalizácie? Pri sťahovaní 400 MB si človek pravdepodobne
bude chcieť medzitým robiť niečo iné. Odporúčam áno, vľavo od zatvárania, tichšie.

---

## 3. Dodané podklady

| podklad | čo s ním |
|---|---|
| **logo** | „Fri" v hlavnej farbe, „World" biele; pixelové/hranaté písmo |
| | *zatiaľ je to text, nie obrázok — PNG ešte nedorazilo* |
| **farba** | vzorka hlavnej farby — presný odtieň vziať zo vzorky, neodhadovať |
| **ikona** | ikona aplikácie a v paneli úloh |
| **kurzor** | vlastný kurzor v okne, šípka v hlavnej farbe |
| **pozadie** | render budovy FRI — pozadie celého okna |

Farba je približne jantárová/zlatá. **Presnú hodnotu zober zo vzorky**, nie z tohto textu.

---

## 4. Farby a čitateľnosť

Hlavná farba je **akcent, nie plocha**. Nesie ju logo, hlavné tlačidlo a priebeh sťahovania.
Nič iné.

Pozadie je fotografický render s **veľkým jasovým rozsahom** — svetlá obloha hore, tmavšia
dlažba a zeleň dole. Biely text sa na oblohe stratí.

Preto:

- pod textom **tmavé zjemnenie** (scrim/gradient), aby bol kontrast konštantný
- kontrast textu voči podkladu **najmenej 4.5:1**, pri drobnom texte viac
- hlavná farba na tmavom podklade funguje; **na svetlej oblohe nie**
- chybový text nesmie byť len farbou — v hre je aj farbosleposť, treba aj slovo

---

## 5. Rozloženie

Tri pásma zhora nadol:

```
┌ ⋯ ───────────────────────────────────────────── — ✕ ┐
│                                                      │  hlavička
│   FriWorld                                           │  logo + verzia
│   Verzia 0.1.2-alpha                                 │
│                                                      │
│                  (pozadie: render FRI)               │  stred
│                                                      │  poznámky k verzii,
│                                                      │  priebeh, chyby,
│                                                      │  otázka na odinštalovanie
│                                                      │
│  ──────────────────────────────────────────────────  │
│  Pripravené   [🔧] [📁] [Odinštalovať]  [   HRAŤ   ]  │  akčné pásmo
└──────────────────────────────────────────────────────┘
```

**Hlavička** — vľavo hore ponuka `⋯` s akciami launchera, vpravo hore minimalizácia
a zatváranie. Pod nimi logo a riadok o verzii.

**Stred** — najviac priestoru, väčšinou prázdny. Sem prichádzajú poznámky k verzii, pruh
priebehu, chybové hlásenie, otázka na odinštalovanie a upozornenie na novší launcher.
Naraz je aktívne najviac jedno — okrem upozornenia na launcher, ktoré má vlastný pruh nad
zvyškom a smie sa objaviť súbežne s čímkoľvek.

**Akčné pásmo** — vľavo stavový riadok, vpravo tlačidlá. Sú zoradené tak, že každé ďalšie
doprava je hlasnejšie, a hlavné je najširšie. Toto je jediné miesto, kam sa pri bežnom
použití dá kliknúť, a musí byť jednoznačné.

---

## 6. Tlačidlá

### Hlavné tlačidlo

**Jedno tlačidlo, ktoré mení význam.** Tak, ako to robí bežný herný launcher — nie tri
tlačidlá vedľa seba, z ktorých dve sú zakázané.

| stav | popis |
|---|---|
| nič nenainštalované | **Inštalovať** |
| je novšia verzia | **Aktualizovať** |
| nainštalované a aktuálne | **Hrať** |
| zlyhalo pred prvou kontrolou | **Skúsiť znova** |
| práve pracuje | zakázané, popis „Počkaj chvíľu" |

Vizuálne najvýraznejší prvok okna po logu. Nesie hlavnú farbu.

### Vedľajšie tlačidlo

Tichšie, vľavo od hlavného. Zobrazí sa **v jedinom stave** — keď je k dispozícii novšia
verzia — a nesie popis **Hrať 0.1.1-alpha**.

Je podstatné: **nová verzia nesmie zablokovať hranie tej, ktorú človek už má.**

### Tlačidlá pre nainštalovanú hru

Naľavo od vedľajšieho, a **len keď je hra nainštalovaná** — na prázdnom disku by neznamenali
nič, tak tam ani nie sú.

| tlačidlo | tvar | čo robí |
|---|---|---|
| 🔧 | štvorec 52 × 52, ikona kľúča | preinštaluje verziu, ktorá je na disku, cez poškodené súbory |
| 📁 | štvorec 52 × 52, ikona priečinka | otvorí správcu súborov na nainštalovanej hre |
| **Odinštalovať** | obrysové, užšie než hlavné | vypýta si potvrdenie, nemaže hneď |

Štvorce sú bez textu zámerne: sú to ikony, ktoré sa spoznajú, nie popisy, ktoré sa čítajú.
Majú rovnakú výšku ako ostatné tlačidlá, aby pásmo čítalo ako jedna línia, a majú
`ToolTip` aj meno pre čítačku obrazovky.

Odinštalovanie stojí vedľa hrania, tak **nesmie vyzerať ako jeho rovnocenný sused**: má ten
istý obrys ako každé vedľajšie tlačidlo a červené je až pri prejdení myšou. Hlavnú farbu
nenesie nikdy.

Opraviť bolo pôvodne popis vedľajšieho tlačidla. Je vo štvorci preto, že dáva zmysel vždy,
keď je hra na disku — nielen vtedy, keď sa práve nedeje nič iné.

### Zrušenie

Objaví sa **len počas sťahovania**, pri pruhu priebehu, nie v akčnom pásme. Tichšie než
obidve predošlé.

### Zatvorenie okna

Vlastné, vpravo hore. Bežné správanie: zvýraznenie pri prejdení myšou, jasný cieľ na
kliknutie (najmenej 32 × 32 px). Vedľa neho tichšia minimalizácia.

### Klávesnica

Vlastný rám okna znamená, že systém neponúke žiadnu klávesovú skratku sám. Dve, ktoré každý
čaká, sú preto zapojené ručne.

**Tab** posúva fokus po tlačidlách a **Enter stlačí to, na ktorom fokus stojí.** Toto je
pravidlo, nie detail: fokusový prstenec je sľub o tom, čo Enter spraví, a launcher ho nesmie
porušiť.

Keď fokus na tlačidle nestojí, Enter stlačí hlavné tlačidlo — cez `IsDefault`, ktoré chytá
len tie Entery, ktoré si nevzal nikto iný. Nikdy nie cez odchytenie Enteru na celom okne;
tak to bolo v 0.1.5-alpha a Tab tým prestal mať zmysel.

**Escape** ustupuje od toho, čo je najviac vpredu:

| stav | čo Escape spraví |
|---|---|
| otázka na odinštalovanie | odpovie **Ponechať** |
| otázka na zavretie | odpovie **Späť** |
| beží sťahovanie | zruší ho, tak ako tlačidlo Zrušiť |
| rozbaľuje sa alebo inštaluje | **nič** |
| čokoľvek iné | **spýta sa**, či zavrieť |

Escape sám nezavrie okno nikdy — len sa spýta. Ten predposledný riadok je zámerný rovnako:
Escape je reflex, a reflex nesmie zabiť proces uprostred výmeny priečinkov ani vyvolať
otázku, na ktorú sa vtedy ľahko odpovie áno.

Poradie je [otestované](../tests/FriWorld.Launcher.Core.Tests/DismissChoiceTests.cs), samotné
smerovanie klávesov [na skutočnom okne](../tests/FriWorld.Launcher.App.Tests/KeyboardTests.cs).

Fokus musí byť vidieť. Predvolený čiarkovaný rámček sa na fotografickom pozadí stratí, takže
tlačidlá dostávajú biely dvojpixelový obrys — len pri `:focus-visible`, aby sa neobjavoval po
kliknutí myšou.

### Ponuka `⋯` — akcie launchera

**Vľavo hore**, rovnaká veľkosť ako tlačidlá hlavičky vpravo. Rozbalí sa nadol zarovnaná
doľava. Je vidieť vždy.

| položka | čo robí |
|---|---|
| **Skontrolovať znova** | znovu sa opýta manifestu; zakázané, kým launcher pracuje |
| **Otvoriť denník launchera** | otvorí `launcher.log` v správcovi súborov |

Delenie je zámerné a drží sa ho aj to, čo pribudne: **v ponuke sú akcie na launcheri,
v akčnom pásme akcie na hre.** Ponuka je vľavo hore práve preto, aby nebola po ceste
k tomu, na čo sa naozaj klikáva.

Denník je jediná vec, ktorá je na niečo, keď niekto hlási problém — je v ňom aj to, čo okno
nestihlo ukázať.

Keď sa priečinok alebo denník nepodarí otvoriť, launcher to povie vetou v strede okna. Nič
sa neotvorí potichu a nič nezlyhá potichu.

---

## 7. Stavy a čo je v nich vidieť

Toto je úplný zoznam. Nič iné launcher nezobrazuje.

### 7.1 Kontrolujem

Hneď po otvorení okna, trvá zlomok sekundy až pár sekúnd.

```
verzia     (prázdne)
stred      neurčitý pruh priebehu
stav       Kontrolujem aktualizácie
hlavné     zakázané
```

### 7.2 Nič nenainštalované

```
verzia     Verzia 0.1.1-alpha k dispozícii
stred      poznámky k verzii
           Na stiahnutie 415,48 MB.
stav       Nenainštalované
pásmo      [ INŠTALOVAŤ ]   — nič iné, na disku nie je čo opravovať ani mazať
```

**Nič sa nesťahuje, kým človek neklikne.** Toto je pravidlo, nie detail.

### 7.3 Sťahujem

```
stred      pruh s percentami
           415,48 MB z 415,48 MB · zostáva 02:14        [ Zrušiť ]
stav       Sťahujem 0.1.1-alpha
hlavné     zakázané
```

Ďalej rovnakým spôsobom: **Overujem stiahnuté**, **Rozbaľujem**, **Inštalujem**.
Sú to štyri po sebe idúce fázy jedného procesu, nie štyri rôzne obrazovky.

### 7.4 Pripravené

```
verzia     Verzia 0.1.1-alpha
stred      poznámky k verzii
stav       Pripravené
pásmo      [🔧] [📁] [Odinštalovať]   [ HRAŤ ]
```

### 7.5 Je novšia verzia

```
verzia     Verzia 0.1.1-alpha nainštalovaná · 0.1.2-alpha k dispozícii
stred      poznámky k novej verzii
           Zatiaľ môžeš hrať 0.1.1-alpha.
stav       Aktualizovať na 0.1.2-alpha?
pásmo      [🔧] [📁] [Odinštalovať] [Hrať 0.1.1-alpha]   [ AKTUALIZOVAŤ ]
```

Toto je najplnší stav pásma. Päť tlačidiel je veľa, ale dve z nich sú štvorce a jediné
výrazné je to posledné.

### 7.6 Chyba

```
stred      CHYBA  Stiahnutý súbor bol poškodený.
           Nesedel s kontrolným súčtom a bol zmazaný.
           Zvyčajne pomôže skúsiť to znova.
stav       hlavička chyby
hlavné     Skúsiť znova, Inštalovať alebo Hrať — podľa toho, čo sa dá
```

Chybové hlásenie má **dva riadky**: čo sa stalo, a čo s tým človek môže spraviť. Návrh
musí počítať s tromi až štyrmi riadkami textu.

Ďalšie hlásenia, ktoré sa reálne zobrazujú:

- *Hra už beží.* — zavri ju a skús to znova
- *Nie je dosť miesta.* — potrebuje miesto na stiahnutie aj rozbalenie naraz
- *Server sa nedá dosiahnuť.* — skontroluj pripojenie a skús to znova
- *Hra sa hneď zavrela.* — skončila s kódom N pár sekúnd po spustení; môže pomôcť oprava
- *Tento launcher je príliš starý.* — vydanie potrebuje launcher Y alebo novší
- *Už beží iný launcher.*

### 7.7 Zrušené

```
stred      Čiastočne stiahnuté súbory sme nechali, nabudúce sa bude
           pokračovať tam, kde si prestal.
stav       Zrušené
hlavné     Inštalovať alebo Aktualizovať
```

### 7.8 Otázka na odinštalovanie

Po kliknutí na **Odinštalovať** v akčnom pásme. Nahradí obsah stredu, aj prípadnú chybu.

```
stred      Odinštalovať hru?
           Stiahnuté súbory hry sa vymažú. Vrátiť sa to nedá, ale hru
           sa dá kedykoľvek nainštalovať znova.
           [ Odinštalovať ]   [ Ponechať ]
stav       nemení sa
hlavné     nemení sa
```

**Ničivá odpoveď je tá tichšia** a stojí vľavo; bezpečná je vpravo, kam ide ruka. Hlavnú
farbu nenesie ani jedna. Otázka je v okne, nie v systémovom dialógu — všetko ostatné, čo
launcher hovorí, je tiež tu. Ako presne vyzerá, je v sekcii 7.11.

Po odinštalovaní:

```
verzia     Verzia 0.1.1-alpha k dispozícii
stred      poznámky k verzii
           Hra bola odstránená. Na stiahnutie 415.48 MB.
stav       Odinštalované
pásmo      [ INŠTALOVAŤ ]   — štvorce aj Odinštalovať zmizli
```

Uložené pozície v hre ležia mimo inštalácie, takže dole ide naozaj len hra.

### 7.9 Otázka na zavretie

Po kliknutí na `✕` alebo po Escape. Nahradí obsah stredu.

```
stred      Zavrieť launcher?
           <čo to stojí — závisí od toho, čo beží>
           [ Zavrieť ]   [ Späť ]
stav       nemení sa
```

Druhý riadok hovorí pravdu o tom, čo sa stratí, a tá sa líši:

| stav | veta |
|---|---|
| beží sťahovanie | Sťahovanie sa zastaví. Stiahnuté súbory zostanú a nabudúce sa bude pokračovať tam, kde prestalo. |
| pracuje sa, nedá sa zrušiť | Launcher práve pracuje. Zavretie teraz nechá rozrobenú prácu, ktorú bude treba spraviť znova. |
| nič nebeží | Hra zostane nainštalovaná. |

Tvrdiť, že sa stratí sťahovanie, keď sa v skutočnosti obnoví, je rovnako zlé ako mlčať
o rozrobenej inštalácii.

**Okno, ktoré sa zatvára samo po spustení hry, sa nepýta.** To nie je nikto, kto sa pýta.

### 7.10 Hra beží

```
stav       Beží
okno       skryté — ani v paneli úloh
```

Skryté, nie minimalizované: v launcheri sa medzitým nedá nič robiť a položka v paneli úloh,
ktorá nič nerobí, je len ďalšia vec medzi človekom a hrou.

Keď hra skončí, okno sa vráti a **skontroluje aktualizácie znova** — sedenie môže trvať
hodinu, tak sa vracia s tým, čo platí teraz, nie s tým, čo platilo pred hraním.

Keď je nastavené `keepOpenAfterLaunch`, okno zostane vidieť so stavom **Beží** a hlavné
tlačidlo je zakázané, kým hra beží.

### 7.11 Ako otázka vyzerá

Obidve otázky — odinštalovanie aj zavretie — kreslí **jeden modal**. Naraz je na obrazovke
najviac jeden; dve karty by ukazovali nadpis jednej s tlačidlami druhej.

```
┌──────────────────────────────────────────────────────┐
│░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│
│░░░░░░░┌────────────────────────────────────┐░░░░░░░░░│
│░░░░░░░│ Zavrieť launcher?                  │░░░░░░░░░│
│░░░░░░░│ Hra zostane nainštalovaná.         │░░░░░░░░░│
│░░░░░░░│                                    │░░░░░░░░░│
│░░░░░░░│            [ Zavrieť ]  [ Späť ]   │░░░░░░░░░│
│░░░░░░░└────────────────────────────────────┘░░░░░░░░░│
│░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░│
└──────────────────────────────────────────────────────┘
```

Karta je v strede okna, najviac 470 široká, nad všetkým ostatným. Pozadie za ňou stmavne.

**Modal sú tri sľuby, nie jeden:**

| sľub | čo ho drží |
|---|---|
| je navrchu | poradie v strome — je posledný |
| nič za ním sa nedá kliknúť | zatienenie má pozadie, takže kliknutia pohíta |
| nič za ním sa nedá dosiahnuť klávesnicou | obsah pod ním je **zakázaný** — zakázané prvky tabulátor preskočí |

Ten tretí sa najľahšie zabudne. Hlavné tlačidlo je predvolené, takže bez neho by Enter
prešiel modalom rovno do inštalácie.

Fokus sa pri otvorení presunie na **bezpečnú odpoveď** — obsah, ktorý ho mal, oň práve
zakázaním prišiel, a Tab z ničoho je horšie miesto na začiatok.

Escape otázku zruší, nikdy na ňu neodpovie áno.

### 7.12 Upozornenie na novší launcher

Samostatný panel v strede, môže sa objaviť **súbežne s ktorýmkoľvek stavom**.

```
┌─────────────────────────────────────────────────────┐
│ Launcher 0.2.0-alpha je k dispozícii.  [ Aktualizovať│
│ Rýchlejšie sťahovanie.                a reštartovať ]│
└─────────────────────────────────────────────────────┘
```

Tlačidlo má dva možné popisy: **Aktualizovať a reštartovať**, keď sa launcher vie vymeniť
sám, alebo **Otvoriť stránku so stiahnutím**, keď nie.

Musí byť **zreteľne tichší** než hlavné tlačidlo — je to poznámka, nie hlavná úloha.

---

## 8. Texty

**Rozhodnuté: slovenčina.** Cieľovka sú slovenskí žiaci základných a stredných škôl,
anglické texty tam nesedeli. Okno je dnes celé po slovensky; anglické podoby nižšie sú
pôvodné znenia zo zadania a slúžia už len ako mapovanie.

| teraz | po slovensky |
|---|---|
| Checking for updates | Kontrolujem aktualizácie |
| Not installed | Nenainštalované |
| Install | Inštalovať |
| Downloading 0.1.1-alpha | Sťahujem 0.1.1-alpha |
| Verifying download | Overujem stiahnuté |
| Unpacking | Rozbaľujem |
| Installing | Inštalujem |
| Ready to play | Pripravené |
| Play | Hrať |
| Update | Aktualizovať |
| Update to 0.1.2-alpha? | Aktualizovať na 0.1.2-alpha? |
| You can keep playing 0.1.1-alpha for now. | Zatiaľ môžeš hrať 0.1.1-alpha. |
| Repair | Opraviť |
| Retry | Skúsiť znova |
| Cancel | Zrušiť |
| 415.48 MB to download. | Na stiahnutie 415,48 MB. |
| Uninstall the game? | Odinštalovať hru? |
| Open the game folder | Otvoriť priečinok s hrou |
| Uninstalled | Odinštalované |

Slovenské texty sú **dlhšie než anglické**, typicky o 10–20 %. Tlačidlá musia zniesť
„Aktualizovať" aj „Skúsiť znova", nielen „Play".

Desatinná čiarka, nie bodka.

---

## 9. Okrajové prípady, s ktorými treba počítať

| prípad | dôsledok pre návrh |
|---|---|
| dlhé číslo verzie | `0.1.1-alpha` aj `0.2.0-rc.3+build.77` — riadok verzie musí skracovať |
| dlhé poznámky k verzii | zalomiť, najviac tri riadky, potom „…" |
| dlhá chybová hláška | tri až štyri riadky, nesmie roztlačiť akčné pásmo |
| vedľajšie tlačidlo nesie verziu | „Hrať 0.2.0-rc.3+build.77" je oveľa širšie než „Hrať 0.1.1-alpha" |
| plné akčné pásmo | päť tlačidiel naraz; stavový riadok naľavo od nich musí skracovať |
| pomalé pripojenie | pruh priebehu je na obrazovke aj desiatky minút, musí byť znesiteľný |
| rýchle pripojenie | fázy preblesknú za sekundu — prechody nesmú blikať |
| bez pripojenia | okno musí ukázať chybu **a zároveň** nechať hrateľné, čo je nainštalované |

---

## 10. Čo sa nesmie zmeniť

Funkčné pravidlá, ktoré návrh nesmie obísť:

1. **Nič veľké sa nedeje samo.** Otvorenie launchera skontroluje, čo je vonku, a čaká.
   Žiadne automatické sťahovanie.
2. **Nainštalovaná hra zostáva hrateľná** — pri novej verzii, pri nedostupnom serveri
   aj po zlyhaní sťahovania.
3. **Jedno hlavné tlačidlo.** Ostatné sú tichšie a užšie a nikdy nenesú hlavnú farbu.
4. **Zrušenie je dostupné počas celého sťahovania.**
5. **Po spustení hry sa okno skryje a po jej zatvorení sa vráti.** Nezatvára sa: to, čo
   človek chce najskôr po dohratí, je zvyčajne launcher. Výnimka je hra, ktorá spadne do
   niekoľkých sekúnd — vtedy okno zostane a povie to.
6. **Naraz beží jedna hra.** Druhé spustenie sa odmietne.

---

## 11. Technické mantinely

Okno je **Avalonia 12** (.NET 10), nie web. Z toho plynie:

- rozloženie je Grid a StackPanel; nie CSS grid, nie flexbox
- animácie sú možné, ale striedmo — launcher beží pol minúty
- vlastný rám okna znamená `SystemDecorations="None"` a vlastné ťahanie
- vlastný kurzor sa nastavuje na okno, funguje
- font: pixelový len v logu; texty bežným čitateľným písmom, logo je obrázok
- pozadie ako obrázok cez celé okno, obsah nad ním

Cieľová platforma je **Windows**, kód beží aj na Linuxe. Návrh nesmie stáť na niečom,
čo je len windowsové.

---

## 12. Na čo sa sústrediť

Podľa dôležitosti:

1. **Akčné pásmo.** Človek musí do dvoch sekúnd vedieť, čo má stlačiť.
2. **Priebeh sťahovania.** Je to najdlhšia časť zážitku a jediná, kde človek čaká.
3. **Chybové stavy.** Rozhodujú o tom, či sa niekto dostane do hry, alebo to vzdá.
4. **Hlavička.** Tu si človek prvýkrát overí, že spustil správnu vec.
5. Zvyšok.

Stred okna je väčšinu času prázdny. To je v poriadku — je tam preto, aby bolo vidieť
pozadie a aby mali chyby a priebeh kam expandovať bez skákania rozloženia.
