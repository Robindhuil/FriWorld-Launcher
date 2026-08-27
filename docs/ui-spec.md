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
┌──────────────────────────────────────────── ⋯ — ✕ ┐
│                                                     │  hlavička
│   [ LOGO ]                                          │  logo + verzia
│   Verzia 0.1.1-alpha                                │
│                                                     │
│                                                     │
│                  (pozadie: render FRI)              │  stred
│                                                     │  poznámky k verzii,
│                                                     │  priebeh, chyby,
│                                                     │  upozornenie na launcher
│                                                     │
│  ─────────────────────────────────────────────────  │
│  Ready to play          [ Repair ]   [   PLAY   ]   │  akčné pásmo
└─────────────────────────────────────────────────────┘
```

**Hlavička** — logo, pod ním riadok o verzii. Vpravo hore ponuka `⋯`, minimalizácia
a zatváranie.

**Stred** — najviac priestoru, väčšinou prázdny. Sem prichádzajú poznámky k verzii, pruh
priebehu, chybové hlásenie, otázka na odinštalovanie a upozornenie na novší launcher.
Naraz je aktívne najviac jedno — okrem upozornenia na launcher, ktoré má vlastný pruh nad
zvyškom a smie sa objaviť súbežne s čímkoľvek.

**Akčné pásmo** — vľavo stavový riadok, vpravo tlačidlá. Toto je jediné miesto, kam sa dá
kliknúť, a musí byť jednoznačné.

---

## 6. Tlačidlá

### Hlavné tlačidlo

**Jedno tlačidlo, ktoré mení význam.** Tak, ako to robí bežný herný launcher — nie tri
tlačidlá vedľa seba, z ktorých dve sú zakázané.

| stav | popis |
|---|---|
| nič nenainštalované | **Install** |
| je novšia verzia | **Update** |
| nainštalované a aktuálne | **Play** |
| zlyhalo pred prvou kontrolou | **Retry** |
| práve pracuje | zakázané, popis „Please wait" |

Vizuálne najvýraznejší prvok okna po logu. Nesie hlavnú farbu.

### Vedľajšie tlačidlo

Tichšie, vľavo od hlavného. Zobrazí sa **len v dvoch stavoch**:

| stav | popis | čo robí |
|---|---|---|
| je novšia verzia | **Play 0.1.1-alpha** | spustí verziu, ktorú človek už má |
| nainštalované a aktuálne | **Repair** | preinštaluje pri poškodených súboroch |

To prvé je podstatné: **nová verzia nesmie zablokovať hranie tej, ktorú človek už má.**

### Zrušenie

Objaví sa **len počas sťahovania**, pri pruhu priebehu, nie v akčnom pásme. Tichšie než
obidve predošlé.

### Zatvorenie okna

Vlastné, vpravo hore. Bežné správanie: zvýraznenie pri prejdení myšou, jasný cieľ na
kliknutie (najmenej 32 × 32 px). Vedľa neho tichšia minimalizácia.

### Ponuka `⋯` — zriedkavé akcie

Vľavo od minimalizácie, rovnaká veľkosť ako ostatné tlačidlá hlavičky. **Zobrazí sa len
vtedy, keď je hra nainštalovaná.** Rozbalí sa nadol zarovnaná doprava.

| položka | čo robí |
|---|---|
| **Otvoriť priečinok s hrou** | otvorí správcu súborov na nainštalovanej hre |
| **Odinštalovať hru** | tichá červená; vypýta si potvrdenie, nemaže hneď |

Sú tu zámerne, nie v akčnom pásme. Štvrté tlačidlo vedľa hlavného by otupilo to, na ktoré
sa má kliknúť — a obidve akcie človek za celý život launchera použije nanajvýš raz.

Keď sa priečinok nepodarí otvoriť, launcher to povie vetou v strede okna. Nič sa neotvorí
potichu a nič nezlyhá potichu.

---

## 7. Stavy a čo je v nich vidieť

Toto je úplný zoznam. Nič iné launcher nezobrazuje.

### 7.1 Kontrolujem

Hneď po otvorení okna, trvá zlomok sekundy až pár sekúnd.

```
verzia     (prázdne)
stred      neurčitý pruh priebehu
stav       Checking for updates
hlavné     zakázané
```

### 7.2 Nič nenainštalované

```
verzia     Version 0.1.1-alpha available
stred      poznámky k verzii
           415.48 MB to download.
stav       Not installed
hlavné     Install
vedľajšie  (žiadne)
```

**Nič sa nesťahuje, kým človek neklikne.** Toto je pravidlo, nie detail.

### 7.3 Sťahujem

```
stred      pruh s percentami
           415.48 MB of 415.48 MB · 02:14 left        [ Cancel ]
stav       Downloading 0.1.1-alpha
hlavné     zakázané
```

Ďalej rovnakým spôsobom: **Verifying download**, **Unpacking**, **Installing**.
Sú to štyri po sebe idúce fázy jedného procesu, nie štyri rôzne obrazovky.

### 7.4 Pripravené

```
verzia     Version 0.1.1-alpha
stred      poznámky k verzii
stav       Ready to play
hlavné     Play
vedľajšie  Repair
```

### 7.5 Je novšia verzia

```
verzia     Version 0.1.1-alpha installed · 0.1.2-alpha available
stred      poznámky k novej verzii
           You can keep playing 0.1.1-alpha for now.
stav       Update to 0.1.2-alpha?
hlavné     Update
vedľajšie  Play 0.1.1-alpha
```

### 7.6 Chyba

```
stred      (červeno) The download was damaged.
           The file did not match its checksum and was deleted.
           Trying again usually fixes it.
stav       hlavička chyby
hlavné     Retry, Install alebo Play — podľa toho, čo sa dá
```

Chybové hlásenie má **dva riadky**: čo sa stalo, a čo s tým človek môže spraviť. Návrh
musí počítať s tromi až štyrmi riadkami textu.

Ďalšie hlásenia, ktoré sa reálne zobrazujú:

- *The game is already running.* — Close it and try again.
- *Not enough free space.* — potrebuje miesto na stiahnutie aj rozbalenie naraz
- *Could not reach the download server.* — Check the connection and try again.
- *The game closed straight away.* — It exited with code N moments after starting.
- *This launcher is too old.* — Release X needs launcher Y or newer.

### 7.7 Zrušené

```
stred      A partial download is kept and will continue next time.
stav       Cancelled
hlavné     Install alebo Update
```

### 7.8 Otázka na odinštalovanie

Po kliknutí na **Odinštalovať hru** v ponuke `⋯`. Nahradí obsah stredu, aj prípadnú chybu.

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
hlavné     Install
ponuka ⋯   zmizne
```

Uložené pozície v hre ležia mimo inštalácie, takže dole ide naozaj len hra.

### 7.9 Upozornenie na novší launcher

Samostatný panel v strede, môže sa objaviť **súbežne s ktorýmkoľvek stavom**.

```
┌─────────────────────────────────────────────────────┐
│ Launcher 0.2.0-alpha is available.  [ Update and    │
│ Rýchlejšie sťahovanie.                 restart ]    │
└─────────────────────────────────────────────────────┘
```

Tlačidlo má dva možné popisy: **Update and restart**, keď sa launcher vie vymeniť sám,
alebo **Open download page**, keď nie.

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
| vedľajšie tlačidlo nesie verziu | „Play 0.1.1-alpha" je dlhšie než „Repair" — šírka sa mení |
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
3. **Jedno hlavné tlačidlo.** Nie štyri, z ktorých sú tri zakázané.
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
