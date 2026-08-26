# Smart App Control blokuje nepodpísané binárky

**Verzia:** 0.1.0-alpha · **Dátum:** 2026-08-26

## Kontext

Pri prvom pokuse spustiť zbuildovaný launcher na vývojovom stroji:

```
FileLoadException: Could not load file or assembly 'FriWorld.Launcher.Core.dll'.
An Application Control policy has blocked this file. (0x800711C7)
```

Zistené:

- `HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy\VerifiedAndReputablePolicyState = 1`,
  čiže **Smart App Control je zapnutý a vynucuje**.
- Event log `Microsoft-Windows-CodeIntegrity/Operational`, udalosti 3077 a 3118
  („Smart App Control Block"), menujú konkrétny blokovaný súbor.
- Blokuje sa nekonzistentne po jednotlivých súboroch. Pri jednom builde prešlo
  35 zo 44 testov, po prebuildovaní padlo všetkých 42 na tú istú chybu — hash sa zmenil.
- Netýka sa to len vlastného kódu. Zablokovaný bol aj `Avalonia.Analyzers.CSharp.dll`
  z NuGet cache, takže build hlási `CS8034`.
- **Nepomáha** self-contained single-file publish. Výsledné `.exe` je blokované rovnako.
- `dotnet build` funguje. Blokuje sa **beh**, nie preklad.

Smart App Control nie je SmartScreen. SmartScreen ukáže dialóg s „More info → Run anyway".
Smart App Control súbor jednoducho nespustí a používateľ nemá čo kliknúť.

## Rozhodnutie

Nastavenie sa **nemení automaticky**. Je to bezpečnostné nastavenie a vypnutie je
**nevratné** — Smart App Control sa po vypnutí nedá znova zapnúť bez čistej reinštalácie
Windows. To je rozhodnutie majiteľa stroja, nie build skriptu.

Možnosti, zoradené podľa toho, čo dáva zmysel:

1. **Vypnúť Smart App Control** — Windows Security → App & browser control →
   Smart App Control settings → Off. Jednorazové, nevratné, a je to Microsoftom
   odporúčaná cesta pre vývojárske stroje.
2. **Vyvíjať v kontajneri.** Docker Desktop je na stroji nainštalovaný, ale nebeží.
   `mcr.microsoft.com/dotnet/sdk:10.0` obíde politiku úplne a navyše dá reálny Linux
   na overenie execute bitov a symlinkov.
3. **Podpísať binárky.** Self-signed certifikát nestačí — Smart App Control chce podpis,
   ktorý pozná Microsoftov reputačný systém.

Kým sa to nevyrieši, platí: `dotnet build` a `dotnet test` na riešení fungujú len potiaľ,
pokiaľ nejde o beh vlastného kódu. Launcher sa spustiť nedá.

### Dočasné obídenie pre testy

Blokuje sa **načítanie samostatnej DLL**, nie testovacia assembly ako taká. Keď sa zdroje
`Core` preložia priamo do testovacieho projektu, žiadna `FriWorld.Launcher.Core.dll` sa
nenačítava a testy prebehnú. Projekt mimo repa s týmto obsahom stačí:

```xml
<PropertyGroup>
  <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
</PropertyGroup>

<ItemGroup>
  <Compile Include="$(RepoRoot)src/FriWorld.Launcher.Core/**/*.cs"
           Exclude="$(RepoRoot)src/FriWorld.Launcher.Core/obj/**/*.cs;$(RepoRoot)src/FriWorld.Launcher.Core/bin/**/*.cs" />
  <Compile Include="$(RepoRoot)tests/FriWorld.Launcher.Core.Tests/**/*.cs"
           Exclude="$(RepoRoot)tests/FriWorld.Launcher.Core.Tests/obj/**/*.cs;$(RepoRoot)tests/FriWorld.Launcher.Core.Tests/bin/**/*.cs" />
</ItemGroup>
```

Plus rovnaké `PackageReference` ako má testovací projekt, a potom
`dotnet test -p:RepoRoot="<koreň repa>/"`. Takto prešlo 49 z 49 testov.

Je to barla, nie riešenie. Do repa sa nepridáva, lebo po vypnutí Smart App Control
je zbytočná a druhý testovací projekt by sa rozišiel s prvým.

## Dôsledky

Väčší dôsledok je pre produkt, nie pre vývoj.

Plán počítal s tým, že nepodpísaný launcher stojí používateľa jeden klik cez SmartScreen.
To platí len tam, kde Smart App Control nebeží. Na čistých inštaláciách Windows 11 je
**zapnutý predvolene** a nepodpísaný launcher tam **jednoducho nepôjde spustiť** —
žiadne „Run anyway", žiadna cesta ďalej pre bežného hráča.

Zasiahnutá je aj samotná hra: `FriWorld.exe` z Unity buildu je tiež nepodpísané a launcher
ho spúšťa ako podproces.

To mení váhu rozhodnutia „podpis je mimo rozsahu, stojí peniaze". Nie je to už kozmetika
prvého spustenia, ale otázka, či sa hra na časti strojov spustí vôbec. Treba k tomu:

- zistiť, koľko cieľových hráčov má Smart App Control zapnutý (upgradnuté Windows ho
  väčšinou zapnutý nemajú, čerstvo nainštalované áno),
- ak to nie je zanedbateľné, vrátiť sa k otázke code signing certifikátu skôr,
  než sa launcher dostane k cudzím ľuďom,
- do textu na Hube pridať aj tento prípad, nie len SmartScreen.

Nič z toho neblokuje ďalšiu prácu na launcheri. Ale je to prvá vec, ktorá sa má overiť,
skôr než sa launcher pošle prvému cudziemu človeku.
