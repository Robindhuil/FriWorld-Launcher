# Obidva jazyky v jednom riadku, a Core prestáva hovoriť vetami

**Dátum:** 2026-09-18 · **Stav:** platí · **Verzia:** 0.2.0-alpha

## Kontext

Okno bolo celé po slovensky, natvrdo. Cieľovka sú slovenskí žiaci, takže to bolo správne
rozhodnutie — ale hru ukazuje fakulta aj zahraničným návštevám a na dni otvorených dverí.

Pri obhliadke sa ukázalo, že okno **už dvomi jazykmi hovorilo**, len to nikto nevolal
lokalizáciou. `FailureMessages` skladalo slovenský nadpis a za neho lepilo `e.Message`,
teda anglickú vetu z výnimky. Hráč, ktorému nezostalo miesto na disku, čítal:

> Nedostatok voľného miesta. Need about 1,7 GB free on C:\ for a 415 MB download, but only
> 300 MB is available. Treba miesto na stiahnutie aj rozbalenie naraz.

To nie je vec, ktorú pridanie angličtiny vyrieši; je to vec, ktorú pridanie angličtiny
**zdvojnásobí**, ak sa nespraví najprv.

## Zvažované možnosti

**`.resx` a satelitné zostavy.** Obvyklý tvar pre .NET a jediný, ktorý vie viac než dva
jazyky bez zásahu. Má jednu chybu, ktorá tu váži viac než všetko, čo ponúka: **súbory sa
rozídu.** Nič nenúti druhý súbor držať krok s prvým a rozdiel nie je vidieť, kým launcher
nespustí niekto v jazyku, ktorý nikto netestuje. Navrch: launcher je jednosúborová zostava
a celé riešenie sa buildí s `InvariantGlobalization`, takže hľadanie kultúry vracia
invariantnú a satelity by boli cesta, ktorá sa musí najprv sprejazdniť.

**Trieda so slovníkom na jazyk.** Menej obradu než `.resx` a presne tá istá chyba:
dva slovníky, ktoré sa rozídu.

**Abstraktná trieda a dve implementácie.** Kompilátor by chýbajúci reťazec chytil. Za cenu
troch členov na každú vetu a dvoch súborov, v ktorých sa preklad porovnáva listovaním.

**Obidve znenia v jednom člene.** `Pick(sk, en)` na jednom riadku. Reťazec, ktorý existuje
v jednom jazyku, existuje v obidvoch — inak sa to neskompiluje — a kto číta preklad, vidí
obidve vedľa seba.

## Rozhodnutie

**Jeden člen, obidva jazyky, jeden riadok.** Celý zoznam je v `Core/Localization/Texts.cs`.
Tretí jazyk je iný tvar a je to zmena, ktorá sa spraví, keď bude naozaj treba — nie teraz
a nie pre istotu.

**Core prestáva hovoriť vetami.** `UpdateStatus` nesie fázu a údaje, nie text; `Message`
vzniká až vo front ende. Ten istý kód beží pod oknom, ktoré hovorí jedným z dvoch jazykov,
a pod konzolou, ktorá je vždy anglická — veta zvolená v pipeline by bola správna pre jedno
a nesprávna pre druhé.

**Výnimky nesú údaje, nie prózu.** `InsufficientDiskSpaceException` nesie čísla,
`LauncherTooOldException` obidve verzie, `GameLaunchException` a `LauncherUpdateException`
kód problému. Hlásenie výnimky zostáva anglické a ide **do denníka**. Do okna sa nedostane
nikdy.

**Desatinný oddeľovač cestuje so slovami.** Nie je to formátovanie, je to časť prekladu:
`1,5 GB` je správne po slovensky a nesprávne po anglicky. Oddeľovače sú vypísané, nie
prevzaté z kultúry, lebo `InvariantGlobalization` zostáva zapnuté — a dva vypísané znaky sú
na školskom počítači to isté čo na vývojárskom, čo databáza kultúr nie je.

**Systémová kultúra sa nečíta.** S `InvariantGlobalization` by na slovenských Windows
odpovedala to isté čo na anglických, a stroje, na ktorých toto beží, sú školské počítače.
Zlý odhad by znamenal slovenské dieťa pred anglickým oknom.

**Poradie:** `FRIWORLD_LANGUAGE` → zapamätaná voľba z prepínača → `language`
v `launcher.json` → slovenčina. Zapamätaná voľba je nad súborom: súbor hovorí, v čom sa má
stroj spustiť, prepínač je človek, ktorý povie, čo chce.

**Manifest dostáva `notesEn`, nie `notes` ako objekt.** Manifest, v ktorom by `notes`
prestalo byť reťazcom, by neprešiel parsovaním v každom launcheri, ktorý je dnes na
školskom počítači — a launcher, ktorý nevie prečítať manifest, sa z toho ani neaktualizuje.
Pridané pole staré launchery ignorujú, čo je presne správny výsledok: ukazovali len
slovenčinu.

## Dôsledky

Prepnutie jazyka **povie celé okno znova**, nielen popisky. Polovica toho, čo je na
obrazovke, je hotová veta napísaná vtedy, keď sa niečo stalo — stavový riadok, riadok
verzie, poznámky k verzii. Prepínač, ktorý by prepísal len tlačidlá, by nechal okno hovoriť
dvomi jazykmi naraz, teda presne to, kvôli čomu toto celé vzniklo.

Zapamätaná voľba je vlastný súbor `language.txt` v inštalačnom koreni, nie riadok
v `launcher.json`. Ten súbor patrí nasadeniu a môže sedieť tam, kam launcher nesmie
zapisovať.

Anglické znenie poznámok k verzii **chýba, kým ho niekto nenapíše**. Vtedy sa ukáže
slovenské: poznámka v zlom jazyku povie viac než prázdne „ČO JE NOVÉ".

Testy majú novú triedu povinností. `LocalizationTests` prechádza `Texts` reflexiou a padá
na prázdnom reťazci, na slovenskom písmene v anglickom texte aj na vete, ktorá vyšla v
obidvoch jazykoch rovnako — teda na skopírovanom riadku. Jeden test tiež drží, že hlásenie
výnimky sa do textu pre hráča nedostane; to je ten pôvodný chybový stav, zapísaný tak, aby
sa nevrátil.
