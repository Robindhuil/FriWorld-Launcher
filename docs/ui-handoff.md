# Handoff: FriWorld Launcher (0.1.0-alpha)

> **Stav:** návrh je zapracovaný a okno z neho vychádza. Dokument sa nemení — je to
> prevzatý podklad, a **v niekoľkých veciach už neplatí**:
>
> | čo hovorí | čo platí |
> |---|---|
> | po spustení hry sa okno zavrie | skryje sa a po zatvorení hry vráti |
> | pevných 980 × 720 | návrhová veľkosť; skutočná sa škáluje podľa obrazovky |
> | otázky v strede okna | modal so zatieneným a zablokovaným pozadím |
> | akcie na hre pod `⋯` | v akčnom pásme; pod `⋯` sú akcie launchera |
>
> **Záväzný popis okna je [`ui-spec.md`](ui-spec.md)**, nie tento súbor.

## Overview
Desktop launcher pre hru **FriWorld** — 3D prehliadka Fakulty riadenia a informatiky ŽU.
Launcher stiahne hru, overí ju, nainštaluje a spustí; po spustení hry sa zavrie.
Beží pár desiatok sekúnd, takže musí byť čitateľný na prvý pohľad.

Cieľová implementácia podľa zadania: **Avalonia 12 / .NET 10**, Windows aj Linux,
`SystemDecorations="None"`, vlastné ťahanie okna, vlastný kurzor.

## About the Design Files
Súbory v tomto balíku sú **dizajnová referencia napísaná v HTML** — prototyp, ktorý ukazuje
zamýšľaný vzhľad a správanie, nie produkčný kód na skopírovanie. Úloha je **znovu vytvoriť
tento dizajn v cieľovom prostredí** (Avalonia XAML: Grid + StackPanel, žiadny CSS grid ani
flexbox) s použitím jeho vlastných vzorov a knižníc. HTML slúži len ako presný zdroj rozmerov,
farieb, typografie a stavov.

## Fidelity
**High-fidelity.** Farby, typografia, rozostupy aj stavy sú finálne. Rekreovať čo najpresnejšie.
Jediná výnimka: logo je v prototype vysadené pixelovým fontom (Press Start 2P) — v aplikácii to
má byť **obrázok loga**, ako hovorí zadanie.

## Screens / Views
Launcher je **jedno okno, 980 × 720, pevná veľkosť, bez systémového rámu**, vycentrované pri štarte.
Okno sa dá ťahať za pozadie. Obsah je vždy rovnaký skelet, mení sa len stredné pásmo a akčné pásmo.

### Skelet okna
- Root: 980 × 720, `border-radius: 10px`, 1px vnútorný okraj `rgba(255,255,255,.10)`.
- Vrstva 1 — pozadie: `assets/fri-render.png`, cover, pozícia `center 55%`, `saturate(.92) brightness(.92)`.
- Vrstva 2 — scrim, lineárny gradient zhora nadol:
  `rgba(8,9,14,.80) 0%` → `rgba(8,9,14,.32) 26%` → `rgba(8,9,14,.28) 44%` → `rgba(6,7,11,.86) 72%` → `rgba(5,6,9,.97) 100%`.
  Účel: konštantný kontrast textu nad svetlou oblohou aj tmavou dlažbou (min. 4.5:1).
- Vrstva 3 — obsah, padding `38px 44px 0`, tri pásma pod sebou (hlavička / stred / akčné pásmo).
- Kurzor v celom okne: `assets/cursor.png`, hotspot 2,2.

### Hlavička (hore)
- Logo „FriWorld": 30px, riadkovanie 1, tieň `0 3px 16px rgba(0,0,0,.75)`.
  „Fri" = **#FBB800**, „World" = **#FFFFFF**.
- Riadok verzie pod logom, medzera 12px: 14px / 500, `rgba(255,255,255,.74)`, tieň `0 1px 8px rgba(0,0,0,.7)`,
  max. šírka 560px, **jeden riadok, orezanie s „…"** (verzia môže byť `0.2.0-rc.3+build.77`).
  Rezervovaná výška 19px aj keď je prázdny — layout nesmie skákať.
- Vpravo hore: minimalizácia „—" a zatvorenie „✕", každé **34 × 34 px**, radius 6px,
  podklad `rgba(10,11,16,.35)`. Hover: minimalizácia → `rgba(255,255,255,.16)` + biela;
  zatvorenie → pozadie `#C0392B` + biela. Medzera medzi nimi 6px.

### Stred
Väčšinu času prázdny. Obsah je zarovnaný **k spodku** stredného pásma (justify-content: space-between,
banner hore, obsah dole), aby sa chyby a priebeh rozťahovali nahor a akčné pásmo sa nehýbalo.
Max. šírka obsahu 660px. Naraz je aktívny najviac jeden blok:

1. **Poznámky k verzii** — nadpis 11px / 700 / letter-spacing .14em / UPPERCASE / `rgba(255,255,255,.48)`;
   telo 15px / 1.6 / `rgba(255,255,255,.86)`, **max. 3 riadky, potom „…"** (line-clamp);
   voliteľný spodný riadok 14px / `rgba(255,255,255,.62)` (napr. „Na stiahnutie 415,48 MB.").
2. **Chyba** — panel, padding 16px 18px, radius 8px, pozadie `rgba(60,14,12,.62)`, okraj `rgba(240,120,110,.42)`.
   Vľavo štítok **„CHYBA"** 11px / 700 / .12em / `#FF9A90` — chyba **nikdy nie je len farbou** (farbosleposť).
   Vpravo dva riadky: čo sa stalo (15px / 600 / `#FFD9D4`) a čo s tým (14px / 1.55 / `rgba(255,255,255,.76)`).
   Musí zniesť 3–4 riadky bez roztlačenia akčného pásma.
3. **Priebeh** — max. šírka 600px:
   - riadok: názov fázy (15px / 600 / `rgba(255,255,255,.92)`) + percentá vpravo (15px / 600 / **#FBB800**, tabular-nums)
   - pruh: výška 8px, radius 4px, koľajnica `rgba(255,255,255,.14)`, výplň **#FBB800**, `transition: width .3s ease`
   - neurčitý variant: bežec šírky 34%, keyframes `indet` (left −34% → 100%), 1.25s ease-in-out infinite
   - pod pruhom: detail vľavo (13.5px / `rgba(255,255,255,.62)`, tabular-nums) a **Zrušiť** vpravo
     (13px / 600 / `rgba(255,255,255,.68)`, hover `rgba(255,255,255,.12)`) — zrušenie patrí k pruhu, nie do akčného pásma
4. **Jednoduchý text** — 15px / 1.6 / `rgba(255,255,255,.78)`, max. 520px (stav Zrušené).

### Panel „nový launcher"
Samostatný panel **hore v strednom pásme**, môže sa objaviť súbežne s ktorýmkoľvek stavom.
Padding 14px 16px 14px 18px, radius 8px, okraj `rgba(255,255,255,.16)`, pozadie `rgba(10,11,16,.60)`, blur 6px.
Vľavo titulok 14px / 600 a poznámka 13px / `rgba(255,255,255,.60)`; vpravo **obrysové** tlačidlo
(9px 16px, radius 6px, okraj `rgba(255,255,255,.28)`, 13px / 600) — text „Aktualizovať a reštartovať"
alebo „Otvoriť stránku so stiahnutím". Musí byť zreteľne tichší než hlavné tlačidlo — nikdy nie žltý.

### Akčné pásmo (dole)
Horná linka `1px rgba(255,255,255,.14)`, padding `22px 0 30px`.
- Vľavo: bodka 8px (farba podľa stavu) + stavový text 15px / 600 / `rgba(255,255,255,.88)`, orezanie s „…".
- Vpravo: vedľajšie tlačidlo a hlavné tlačidlo, medzera 12px.
  - **Vedľajšie**: výška 52px, padding 0 22px, radius 7px, obrys `rgba(255,255,255,.26)`, 15px / 600 /
    `rgba(255,255,255,.86)`; hover `rgba(255,255,255,.12)` + okraj `rgba(255,255,255,.42)`.
    Šírka sa prispôsobuje textu („Opraviť" vs „Hrať 0.1.1-alpha").
  - **Hlavné**: výška 52px, **min-width 180px**, padding 0 34px, radius 7px, 17px / 700,
    pozadie **#FBB800**, text **#1A1400**, tieň `0 8px 26px rgba(251,184,0,.28)`.
    Zakázané: pozadie `rgba(255,255,255,.12)`, text `rgba(255,255,255,.42)`, bez tieňa.
    Min-width musí zniesť „Aktualizovať" aj „Skúsiť znova".

## Interactions & Behavior
- Ťahanie okna za pozadie (nie za tlačidlá).
- Hover stavy sú definované vyššie pri každom prvku. Žiadne iné animácie okrem pruhu priebehu.
- Prechody medzi fázami (Sťahujem → Overujem → Rozbaľujem → Inštalujem) sú **jeden proces, nie štyri obrazovky**:
  mení sa len názov fázy a detail, pruh zostáva na mieste. Pri rýchlom pripojení nesmie blikať —
  odporúčam minimálne trvanie zobrazenia fázy ~400 ms.
- Po spustení hry sa okno zavrie. Výnimka: ak hra spadne do niekoľkých sekúnd, okno zostane a zobrazí chybu.
- Zrušenie je dostupné počas celého sťahovania.
- Nič sa nesťahuje samo — otvorenie launchera len skontroluje server a čaká.
- Nainštalovaná hra zostáva hrateľná pri novej verzii, nedostupnom serveri aj po zlyhaní sťahovania.

## State Management
Jeden stavový enum, deväť hodnôt. Presné texty (slovenčina — cieľovka sú slovenskí žiaci ZŠ/SŠ;
desatinná **čiarka**, nie bodka):

| stav | riadok verzie | stred | stav vľavo | hlavné | vedľajšie |
|---|---|---|---|---|---|
| `checking` | (prázdne) | neurčitý pruh, „Kontrolujem aktualizácie" / „Zisťujem, čo je na serveri." | Kontrolujem aktualizácie | „Počkaj chvíľu" (zakázané) | — |
| `notinstalled` | Verzia 0.1.1-alpha k dispozícii | poznámky + „Na stiahnutie 415,48 MB." | Nenainštalované | Inštalovať | — |
| `downloading` | Verzia 0.1.1-alpha | pruh s %, „234,10 MB z 415,48 MB · zostáva 02:14", Zrušiť | Sťahujem 0.1.1-alpha | zakázané | — |
| `verifying` | Verzia 0.1.1-alpha | neurčitý pruh, „Kontrolujem kontrolný súčet · 415,48 MB" | Overujem stiahnuté | zakázané | — |
| `installing` | Verzia 0.1.1-alpha | pruh s %, „1 284 z 1 566 súborov" (fáza „Rozbaľujem") | Inštalujem 0.1.1-alpha | zakázané | — |
| `ready` | Verzia 0.1.1-alpha | poznámky | Pripravené | Hrať | Opraviť |
| `update` | Verzia 0.1.1-alpha nainštalovaná · 0.1.2-alpha k dispozícii | poznámky k novej verzii + „Zatiaľ môžeš hrať 0.1.1-alpha." | Aktualizovať na 0.1.2-alpha? | Aktualizovať | Hrať 0.1.1-alpha |
| `error` | Verzia 0.1.1-alpha k dispozícii | panel chyby | Sťahovanie zlyhalo | Skúsiť znova / Inštalovať / Hrať | podľa stavu |
| `cancelled` | Verzia 0.1.1-alpha k dispozícii | „Čiastočne stiahnuté súbory sme nechali, nabudúce sa bude pokračovať tam, kde si prestal." | Zrušené | Inštalovať / Aktualizovať | — |

Nezávislý boolean: **panel nového launchera** — môže byť zapnutý pri ktoromkoľvek stave.
Nezávislý boolean: **tlačidlo minimalizácie** (v prototype prop `showMinimize`, default zapnuté).

Ďalšie chybové hlásenia (rovnaký panel, dva riadky — čo sa stalo + čo s tým):
- Hra už beží. — Zavri ju a skús to znova.
- Nedostatok voľného miesta. — Treba miesto na stiahnutie aj rozbalenie naraz.
- Nepodarilo sa spojiť so serverom. — Skontroluj pripojenie a skús to znova.
- Hra sa hneď zavrela. — Skončila s kódom N pár sekúnd po spustení.
- Tento launcher je príliš starý. — Vydanie X potrebuje launcher Y alebo novší.

## Design Tokens
**Farby**
- Akcent (trademark): `#FBB800` / `rgb(251, 184, 0)` — **len** logo, hlavné tlačidlo, pruh priebehu, percentá. Nikde inde.
- Text na akcente: `#1A1400`
- Text primárny: `rgba(255,255,255,.88)` – `.92`; sekundárny `rgba(255,255,255,.74)`; terciárny `rgba(255,255,255,.62)`; tlmený `rgba(255,255,255,.48)`
- Obrysy: `rgba(255,255,255,.14)` (linky), `.26` (tlačidlo), `.42` (tlačidlo hover)
- Hover plocha: `rgba(255,255,255,.12)`
- Zatvorenie hover: `#C0392B`
- Chyba: pozadie `rgba(60,14,12,.62)`, okraj `rgba(240,120,110,.42)`, štítok `#FF9A90`, titulok `#FFD9D4`, bodka `#FF7A6E`
- Neutrálna bodka stavu: `rgba(255,255,255,.45)`
- Plátno okolo okna (len v prototype): `#14120E`

**Typografia** — Archivo (400/500/600/700). Logo: Press Start 2P (v appke obrázok).
30 / 17 / 15 / 14 / 13.5 / 13 / 11 px. Riadkovania 1.0 / 1.45 / 1.55 / 1.6.
Slovenské texty sú o 10–20 % dlhšie než anglické — tlačidlá a stavový riadok to musia zniesť.

**Rozostupy** 6 / 8 / 12 / 14 / 18 / 22 / 26 / 30 / 38 / 44 px · **Radius** 4 (pruh) / 6 (malé) / 7 (tlačidlá) / 8 (panely) / 10 (okno) / 999 (chipy)
**Tiene** tlačidlo `0 8px 26px rgba(251,184,0,.28)` · text `0 1px 8px rgba(0,0,0,.7)` a `0 3px 16px rgba(0,0,0,.75)`

## Assets
- `assets/fri-render.png` — render budovy FRI, pozadie celého okna (dodal používateľ)
- `assets/cursor.png` — vlastný kurzor, šípka v trademark farbe (dodal používateľ)
- `assets/icon.png` — ikona aplikácie a v paneli úloh (dodal používateľ; v prototype sa nepoužíva)
- Logo „FriWorld" — v prototype nahradené fontom Press Start 2P; **do appky dodať obrázok loga**
- Fonty: Archivo (Google Fonts, OFL); Press Start 2P len ako náhrada loga

## Files
- `FriWorld Launcher.dc.html` — prototyp všetkých deviatich stavov. Pod oknom je prepínač stavov
  a prepínač panela nového launchera — **je to len nástroj na prezeranie, do aplikácie nepatrí**.
- `uispec.md` — pôvodné zadanie (pravidlá, ktoré návrh nesmie obísť, sekcia 10).
- `assets/` — pozadie, kurzor, ikona.
