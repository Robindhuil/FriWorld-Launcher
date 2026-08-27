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

Launcher robí jedinú vec: stiahne hru, overí ju, nainštaluje a spustí. Po spustení hry sa
sám zavrie. Beží pár desiatok sekúnd na jedno použitie, takže **musí byť čitateľný na prvý
pohľad, nie objavovateľný**.

---

## 2. Okno

| vlastnosť | hodnota |
|---|---|
| veľkosť | **980 × 720** |
| zmena veľkosti | **nie** — pevná |
| systémový rám | **žiadny** |
| systémová lišta s názvom | **žiadna** |
| zatváranie | **vlastné tlačidlo** |
| minimalizácia | zvážiť, viď nižšie |
| pozícia pri štarte | stred obrazovky |

Okno musí ísť **ťahať za pozadie** — bez systémovej lišty inak niet za čo chytiť.

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

**Ničivá odpoveď je tá tichšia.** „Odinštalovať" je obrysové tlačidlo, „Ponechať" textové;
hlavnú farbu nenesie ani jedno. Otázka je v okne, nie v systémovom dialógu — všetko ostatné,
čo launcher hovorí, je tiež tu.

Po odinštalovaní:

```
verzia     Verzia 0.1.1-alpha k dispozícii
stred      poznámky k verzii
           Hra bola odstránená. Na stiahnutie 415.48 MB.
stav       Odinštalované
pásmo      [ INŠTALOVAŤ ]   — štvorce aj Odinštalovať zmizli
```

Uložené pozície v hre ležia mimo inštalácie, takže dole ide naozaj len hra.

### 7.9 Upozornenie na novší launcher

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
5. **Po spustení hry sa okno zavrie.** Výnimka: keď hra spadne do niekoľkých sekúnd,
   okno zostane a povie to.

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
