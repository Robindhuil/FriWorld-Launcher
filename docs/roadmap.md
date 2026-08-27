# Plánované

**Verzia launchera:** 0.1.8-alpha · **Dátum:** 2026-08-27

Čo sa má spraviť a čo o tom už vieme. Nie zadanie — zadanie vznikne, keď sa na tom začne
robiť. Účel je, aby sa pri tom nezačínalo od nuly a nenarazilo sa na to, na čo sme prišli
už pri stavbe zvyšku.

| sekcia | čo v nej je |
|---|---|
| [Repozitár po anglicky](#repozitár-po-anglicky) | dokumentácia má byť v angličtine |
| [0.2.0-alpha](#020-alpha) | najbližšie funkcie |
| [Návrhy](#návrhy-ktoré-čakajú-na-zaradenie) | dáva zmysel, nemá termín |
| [Bez verzie](#bez-verzie) | čaká na niekoho iného alebo na rozhodnutie mimo kódu |

Hotové veci sú v [CHANGELOG.md](../CHANGELOG.md), rozhodnutia v
[decisions/](decisions/README.md).

---

## Repozitár po anglicky

Dokumentácia je celá po slovensky. Má byť po anglicky.

**Čo sa prekladá:** `README.md`, `CHANGELOG.md`, všetko v `docs/` vrátane záznamov
o rozhodnutiach, a **správy commitov odteraz**. História commitov sa neprepisuje.

**Čo zostáva po slovensky:** všetko, čo číta hráč. `tools/package/CITAJ-MA.txt`,
`Spustit-ak-exe-nejde.cmd`, texty v okne, popisy vydaní na GitHube a stránka na Hube.
Cieľovka sú slovenskí žiaci; to sa týmto nemení a je to celý dôvod, prečo sa
[preklady v okne](#preklady-slovenčina-a-angličtina) riešia zvlášť.

**Komentáre v kóde už po anglicky sú.** Nie je tam čo robiť.

### Rozsah

| | |
|---|---|
| súborov | 20 |
| slov | ~22 300 |
| najväčšie | `ui-spec.md` 3 200, `CHANGELOG.md` 3 800, `deploying.md` 2 800 |

### Na čo si dať pozor

- **Jedným prechodom, nie postupne.** Polovične preložený strom je horší než ktorýkoľvek
  z tých dvoch stavov — nikto nevie, ktorý súbor je aktuálny a ktorý zabudnutý.
- **Históriu v changelogu preložiť tiež.** Zmiešaný changelog je presne ten polovičný stav,
  len rozložený v jednom súbore.
- **Názvy súborov s rozhodnutiami sú po slovensky** (`2026-08-26-bez-podpisu-...`). Dajú sa
  premenovať v tom istom prechode; odkazy na ne sú v `decisions/README.md` a naprieč
  dokumentáciou, takže kontrola odkazov to chytí. Odkazy zo správ commitov už nie — tie
  sa premenovaním rozbijú a spraviť sa s tým nedá nič.
- **Preklad nie je prepis.** Dokumenty hovoria, **prečo** je niečo tak a nie inak; to je ich
  celá hodnota a pri prekladaní sa stráca najľahšie.
- Kontrola odkazov na konci: `docs/` má relatívne odkazy medzi súbormi a `README.md` na ne
  ukazuje z koreňa.

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

## Návrhy, ktoré čakajú na zaradenie

Nie sú v žiadnej verzii. Zoradené podľa toho, čo prinesú.

### Inštalácia v triede

Učiteľ, ktorý chce hru na tridsiatich počítačoch v učebni, dnes stiahne **30 × 415 MB cez
jedno školské pripojenie**. To je 12 GB a buď to trvá hodinu, alebo to sieť odmietne.

**Launcher to už vie.** `FRIWORLD_MANIFEST_URL` aj `manifestUrl` v `launcher.json` berú
cestu na disku, nielen adresu — viď [Konfigurácia](architecture.md#konfigurácia). A `pack`
bez `--base-url` píše holé názvy súborov, ktoré sa rátajú voči umiestneniu manifestu. Stačí
teda priečinok:

```
USB kľúč alebo sieťový disk
  manifest.json
  FriWorld-<verzia>-win-x64.zip
  FriWorldLauncher.exe
  launcher.json          manifestUrl ukazuje na manifest vedľa
```

Jedno stiahnutie, tridsať inštalácií z lokálneho zdroja.

**Chýba len postup.** Patrí do [`deploying.md`](deploying.md) ako vlastná sekcia, písaná pre
učiteľa, nie pre vývojára. Prípadne skript, ktorý ten priečinok poskladá z už vydaného
releasu. Kód sa nemení.

Otvorené je, či má launcher po takej inštalácii ostať na lokálnom zdroji, alebo sa má
prepnúť na sieťový manifest — prvé znamená, že hra sa už nikdy neaktualizuje sama, druhé, že
sa pri prvom spustení stiahne to isté, čomu sme sa práve vyhli.

### Vydanie jedným príkazom

Vydanie je dnes desať krokov v dvoch repozitároch: zdvihnúť verziu, zbuildiť balíček,
`gh release create`, `pack --launcher-only`, stiahnuť binárku z GitHubu a overiť hash,
commitnúť manifest, pushnúť, upraviť odkaz na Hube, pushnúť.

Presne táto trieda chyby už raz udrela — zip hlásil predošlú verziu.
`build-release-package.ps1` odvtedy overuje, že obidva assety nesú tú istú verziu, ale
zvyšok reťaze nekontroluje nič.

`tools/release-launcher.ps1 -Version <verzia>` by to spravil naraz, vrátane overenia hashu
proti súboru **stiahnutému z GitHubu** — nie proti lokálnej kópii. Na konci vypíše, čo
zostáva spraviť ručne na Hube.

Kontrolný zoznam pre launcher v [`deploying.md`](deploying.md#12-kontrolný-zoznam) je
zoznam toho, čo by taký skript mal robiť.

### Tri vety, ktoré okno nehovorí

| kde | čo chýba |
|---|---|
| pred inštaláciou | koľko hra zaberie **po rozbalení** (~750 MB), nie len čo sa stiahne (415 MB). Na školskom počítači rozhoduje práve to číslo, a `DiskSpace.Require` ho už počíta. |
| otázka na odinštalovanie | že **uložené pozície zostanú**. Hovorí, že hru sa dá nainštalovať znova; nehovorí to, čo hráča naozaj zaujíma. |
| nikde | ako dostať FriWorld z počítača **úplne**. Odinštalovanie zmaže hru a cache, ale nechá `launcher/`, denník a `installed.json`. CLI má `clean --yes`, okno nemá nič — a na zdieľanom školskom počítači je to reálna požiadavka. |

Je to jeden commit. Prvé dve sú texty, tretie je tlačidlo nad existujúcim príkazom.

### Drobnosti

| čo | poznámka |
|---|---|
| denník rastie donekonečna | reálne 1,6 kB za deň používania, takže to nikoho nedobehne skoro; strop je päť riadkov |
| `KeepOpenAfterLaunch` | nastavenie v `launcher.json`, o ktorom sa nikde nepíše. Buď zdokumentovať v `CITAJ-MA.txt`, alebo zahodiť |

---

## Bez verzie

Veci, ktoré čakajú na niekoho iného alebo na rozhodnutie mimo kódu.

| čo | na čom stojí |
|---|---|
| `BuildRelease.cs` do repa hry | jedno skopírovanie z [`tools/game-repo/`](../tools/game-repo/) |
| logo ako obrázok | zatiaľ je to dvojfarebný text; čaká sa na PNG |
| podpis binárky | cesta cez univerzitu ako EU organizáciu, viď [rozhodnutie](decisions/2026-08-26-bez-podpisu-launcher-je-most-k-steamu.md) |
| zrkadlo mimo GitHubu | ak by školská sieť `github.com` blokovala; najprv to treba odmerať na školskej sieti |
