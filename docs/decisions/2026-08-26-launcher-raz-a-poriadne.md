# Launcher raz a poriadne: self-update áno, ale s poistkami

**Verzia:** 0.1.0-alpha · **Dátum:** 2026-08-26

## Kontext

Predošlý zápis ([bez podpisu](2026-08-26-bez-podpisu-launcher-je-most-k-steamu.md))
self-update zamietol ako najdrahšiu a najrizikovejšiu fázu, ktorú Steam aj tak nahradí.

Pri návrhu vyšlo najavo, že to bola nesprávna úvaha, a to z jedného dôvodu:

**„Nech to netreba upravovať" a „nech sa to vie aktualizovať" sú dve odpovede na ten istý
strach.** Keď sa launcher opraviť nedá, musí byť správne na prvý raz — čo je drahšie a menej
isté než ho naučiť aktualizovať sa. A hlavne: chyba v launcheri bez self-updatu znamená
obísť každého testera zvlášť.

## Rozhodnutie

Self-update sa robí. Zdrojom je **sekcia `launcher` v manifeste hry**, nie druhý manifest
ani GitHub Releases API. Jedno stiahnutie, jeden kontrakt, žiadny rate limit.

Spolu s ním štyri veci, bez ktorých by launcher aj tak bolo treba prerábať.

### 1. `minLauncherVersion` — jediná vec, ktorú self-update nezachráni

Manifest smie niesť najnižšiu verziu launchera, ktorá s ním smie pracovať.

Tolerancia neznámych polí rieši len prípad, keď staršiemu launcheru **nevadí**, že pole
nepozná. Deň, keď manifest začne znamenať niečo, čo by starý launcher spravil zle, prichádza
skôr či neskôr, a vtedy je potrebné, aby sa **zastavil a povedal to**. Bez tohto poľa sa
formát manifestu nedá zmeniť nikdy.

Je to jediné miesto v celom launcheri, kde sa verzie **radia**. Verzia hry sa naďalej len
porovnáva na nerovnosť; radenie predvydaní by tam bola zbytočná pasca.

Nečitateľná alebo chýbajúca hodnota znamená žiadny strop. Brána, ktorá sklapne omylom,
by vypla hru, ktorá by inak fungovala.

### 2. Aktualizácia je ponuka, nie mýtna brána

Keď je nainštalovaná hrateľná verzia a vyjde nová, launcher sa **spýta**. Stiahnuť stovky
megabajtov preto, že niekto otvoril launcher, nie je rozhodnutie launchera.

Keď nie je nainštalované nič, voľba neexistuje a inštaluje sa rovno.

### 3. Oprava inštalácie

Kontrola verzií porovnáva len tagy, takže poškodenú inštaláciu nevidí. Súbory miznú —
antivírus jeden odloží do karantény, dôjde miesto uprostred rozbaľovania, niekto niečo zmaže.
Bez tlačidla Repair je jediná cesta von ručné mazanie priečinka, čo sa hráčovi nedá povedať.

### 4. Chyby ako vety, nie ako výnimky

`FailureMessages` prekladá výnimky na hlavičku, radu a príznak, či má zmysel skúsiť znova.
Jedno miesto, ktoré používa okno aj CLI, takže tú istú poruchu nemôžu opísať rozdielne.

## Ako je self-update poistený

Poradie krokov je zvolené tak, aby zlyhanie v ktoromkoľvek z nich nechalo funkčný launcher:

1. **Iba `https`.** Prísnejšie než pri archíve hry: launcher sa týmto súborom nahradí,
   takže manifest z uneseného spojenia nesmie vedieť podstrčiť spustiteľný súbor.
2. **SHA256 pred čímkoľvek iným.** Neoverený súbor sa nikdy nedostane tam, kde by sa dal spustiť.
3. **Iba jednosúborové nasadenie.** Build rozsypaný do desiatok DLL sa nedá vymeniť naraz
   a polovične vymenený launcher je horší než starý. Ostatné sa odkážu na ručné stiahnutie.
4. **Premenovanie, nie prepísanie.** Bežiaci `.exe` sa na Windows prepísať nedá, ale premenovať
   áno. Starý sa odsunie nabok a maže ho **až ďalší štart**, nie ten bežiaci.
5. **Návrat pri zlyhaní.** Keď sa nový nepodarí umiestniť, starý sa vráti späť. Keď zlyhá aj to,
   chybová hláška menuje presnú cestu k odsunutému súboru, lebo to je vtedy jediná cesta späť.
6. **Odkaz vždy zostáva.** Aj keď automatická výmena nie je možná, launcher vie povedať,
   že novšia verzia existuje, a otvoriť stránku.

## Dôsledky

Manifest hry teraz nesie aj vydania launchera. To znamená, že `pack` má prepínače
`--launcher-file` a `--launcher-base-url`, ktoré binárku zahashujú a zapíšu — rovnaká cesta
ako pri archívoch hry, takže sa kontrakt nemôže rozísť.

Verzia launchera sa musí dvíhať zámerne v `Directory.Build.props`. Bez toho self-update
nikdy nič nenájde, lebo porovnáva na nerovnosť s tým, čo je v manifeste.

Testy sa pri self-update sústredia na to, čo prežije zlyhanie, nie na šťastnú cestu.
Šťastná cesta sa dá vyskúšať; zlyhanie uprostred výmeny na cudzom stroji nie.
