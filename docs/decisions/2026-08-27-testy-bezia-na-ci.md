# Testy bežia na CI, lebo na vývojovom stroji už nemôžu

**Dátum:** 2026-08-27 · **Stav:** platí · **Verzia:** 0.1.4-alpha a neskôr

## Kontext

Smart App Control blokuje nepodpísané binárky a jeho verdikt sa časom zhoršuje. Dlho sa
dal obísť premenovaním assembly: `FriWorldLauncher.dll` prestala fungovať, po premenovaní
išla; potom `FriWorld.Launcher.Core.dll`, znova to isté.

**27. 8. 2026 to prestalo platiť.** Nové, nikdy nepoužité meno je zablokované okamžite.

## Čo sa zmeralo

| čo | výsledok |
|---|---|
| `dotnet test` | knižnica sa nenačíta, celá sada padne |
| nové meno assembly | zablokované hneď |
| build mimo repa (`BaseOutputPath` do `%TEMP%`) | testovacia assembly sa načíta, `FriWorldLauncherCoreLib.dll` nie |
| `run-under-smart-app-control.ps1` | funguje ďalej |

Rozdiel medzi posledným riadkom a ostatnými nie je meno ani priečinok, ale **počet
assembly**. Skript zlúči zdroje `Core` a vstupného projektu do jednej a spustí ju cez
`dotnet exec`. Blokuje sa **referencovaná nepodpísaná knižnica načítaná za behu**, nie
proces ako taký.

## Zvažované možnosti

**Vypnúť Smart App Control.** Od jarných aktualizácií 2026 je to vratné. Je to voľba
používateľa stroja, nie projektu, a launcher by tým prestal byť testovaný v prostredí, kde
ho čaká väčšina hráčov.

**Kompilovať zdroje `Core` priamo do testovacieho projektu**, ako to robí skript. Fungovalo
by to a testy by sa vrátili. Cenou je podmienená vetva v build súboroch, ktorá existuje
kvôli jednému stroju a ktorá pri každom probléme pridá otázku „v ktorom režime to bežalo".

**Nechať testy bežať na CI.** GitHub Actions, Windows aj Linux, žiadny Smart App Control.

## Rozhodnutie

CI. Beží pri každom push a pull requeste a je jediné miesto, kde sa testy naozaj vykonajú.

Lokálne zostáva `run-under-smart-app-control.ps1` na skúšanie okna a CLI.

## Dôsledky

Slučka je pomalšia: chyba v teste sa ukáže o dve minúty, nie o dve sekundy. Za tú cenu sa
kúpilo to, že testy bežia na obidvoch platformách vždy, nielen keď si niekto spomenie.

Vyplatilo sa to hneď. CI našlo, že dva testy záviseli na rýchlosti `cmd.exe`, že jeden
zbieral progress do obyčajného `List` z viacerých vlákien, a že pravidlo o bežiacej hre sa
mockom overiť nedá.

**Test, ktorý nemôže bežať lokálne, musí byť napísaný tak, aby jedno zlyhanie na CI
povedalo všetko.** Beh sa nedá zopakovať krokovaním, takže v hláške má byť stav, nie len
očakávaná hodnota.
