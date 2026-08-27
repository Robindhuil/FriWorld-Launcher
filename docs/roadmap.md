# Plánované

**Verzia launchera:** 0.1.8-alpha · **Dátum:** 2026-08-27

Čo sa má spraviť a čo o tom už vieme. Nie zadanie — zadanie vznikne, keď sa na tom začne
robiť. Účel je, aby sa pri tom nezačínalo od nuly a nenarazilo sa na to, na čo sme prišli
už pri stavbe zvyšku.

Hotové veci sú v [CHANGELOG.md](../CHANGELOG.md), rozhodnutia v
[decisions/](decisions/README.md).

---

## 0.2.0-alpha

### Preklady: slovenčina a angličtina

Okno je dnes celé po slovensky, natvrdo. Cieľovka sú slovenskí žiaci, takže slovenčina
zostáva predvolená; angličtina je pre kohokoľvek mimo, a pre fakultu, keď hru ukazuje
zahraničným návštevám.

**Čo treba rozhodnúť skôr, než sa začne písať:**

| otázka | prečo je otvorená |
|---|---|
| kto vyberá jazyk | systémová kultúra, `launcher.json`, alebo prepínač v okne — a či sa voľba pamätá |
| čo s `notes` v manifeste | poznámky k verzii píše človek pri vydaní, launcher ich prekladať nevie; buď jeden text, alebo pole na jazyk v manifeste |
| kde žijú texty | `.resx`, alebo obyčajná trieda so slovníkom |

To druhé je zásah do [manifestu](manifest.md), teda do kontraktu medzi repami. Ak sa
pridáva, tak radšej hneď — neznáme polia staré launchery ignorujú, ale len dovtedy, kým to
dáva správny výsledok.

**Na čo si dať pozor:**

- **Core dnes nesie slovenské texty**, čo pri jednom jazyku nikomu neprekážalo:
  `FailureMessage`, `UpdateStatus`, `UpdateOrchestrator`, `WindowFit`. Buď sa `Core` očistí
  na kódy a preklad ostane v `App`, alebo nesie zdroje aj on. Prvé je čistejšie a znamená
  prejsť každé miesto, kde `Core` dnes hovorí vetou.
- **Formátovanie čísel je dnes hack.** `Size()` vo view modeli nahrádza bodku čiarkou, lebo
  slovenčina píše desatinnú čiarku. S dvomi jazykmi to musí ísť cez kultúru — a `Core` má
  `InvariantGlobalization`, čo treba vypnúť alebo obísť vedome.
- **Slovenské texty sú o 10–20 % dlhšie.** Tlačidlá musia zniesť „Aktualizovať" aj „Update"
  bez toho, aby sa akčné pásmo pretrhlo; pri piatich tlačidlách naraz je to najtesnejšie.
- Chybové hlásenia majú dva riadky — čo sa stalo a čo s tým — a **obidva sa prekladajú**.
  Preložiť len prvý je horšie než nepreložiť nič.

---

### Pozadie ako slideshow

Dnes je pozadím jeden render budovy FRI. Viac obrázkov, ktoré sa striedajú, by ukázalo viac
z fakulty práve vtedy, keď sa aj tak čaká na sťahovanie.

**Čo treba rozhodnúť:**

| otázka | prečo je otvorená |
|---|---|
| odkiaľ obrázky | zabalené v launcheri, alebo sťahované — druhé je nový spôsob, ako môže launcher zlyhať |
| koľko a ako často | launcher beží desiatky sekúnd; pri 8 s na obrázok sú to nanajvýš štyri |
| čo počas sťahovania | vtedy okno stojí aj desiatky minút a slideshow je jediné, čo sa hýbe |

**Na čo si dať pozor:**

- **Každý obrázok je v jednosúborovom `.exe`.** Súčasný render má 2,7 MB pri celkových
  50 MB. Päť obrázkov je +11 MB na každom stiahnutí launchera aj na každom self-update.
  Ak to má rásť, patria skôr vedľa hry než do launchera.
- **Zatienenie je naladené na jeden obrázok.** Existuje preto, že render má svetlú oblohu
  hore a tmavú dlažbu dole, a drží kontrast textu konštantný. Každý ďalší obrázok musí pod
  tým istým zatienením fungovať, inak sa zatienenie musí prispôsobovať obrázku.
- **Prechod nesmie súperiť s textom.** V strede okna sa objavujú poznámky, priebeh, chyby
  aj modal; pohyb za nimi musí byť pomalý a tichý.
- Systémové nastavenie **obmedzeného pohybu** (reduced motion) má slideshow zastaviť. Je to
  pár riadkov a bez toho je to prvok, ktorý časti ľudí prekáža.
- Obrázky sa načítavajú do pamäte naraz alebo lenivo. Pri 2,7 MB PNG je rozdiel citeľný
  a launcher má bežať aj na školskom počítači.

---

## Bez verzie

Veci, ktoré čakajú na niekoho iného alebo na rozhodnutie mimo kódu.

| čo | na čom stojí |
|---|---|
| `BuildRelease.cs` do repa hry | jedno skopírovanie z [`tools/game-repo/`](../tools/game-repo/) |
| logo ako obrázok | zatiaľ je to dvojfarebný text; čaká sa na PNG |
| podpis binárky | cesta cez univerzitu ako EU organizáciu, viď [rozhodnutie](decisions/2026-08-26-bez-podpisu-launcher-je-most-k-steamu.md) |
| zrkadlo mimo GitHubu | ak by školská sieť `github.com` blokovala; najprv to treba odmerať na školskej sieti |
