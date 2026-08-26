# Vydanie launchera

**Verzia launchera:** 0.1.0-alpha · **Dátum:** 2026-08-26

Launcher nemá vlastný manifest. Vydáva sa ako release v tomto repe a **oznamuje sa cez
manifest hry** — sekciou `launcher`. Jedno stiahnutie, jeden kontrakt, žiadny druhý súbor
na udržiavanie.

---

## Dva assety, nie jeden

| asset | na čo |
|---|---|
| `FriWorldLauncher.exe` | jeden súbor. Toto si launcher **ťahá sám** pri aktualizácii. |
| `FriWorld-Launcher-<verzia>-win-x64.zip` | celý balíček aj so zálohou pre Smart App Control |

Rozdelenie nie je kozmetické. **Self-update vymieňa jeden súbor** — premenuje bežiaci
`.exe` nabok a na jeho miesto presunie nový. Zip by sa musel najprv rozbaliť, čo z jednej
atomickej operácie robí niekoľko, z ktorých ktorákoľvek môže zlyhať v polovici.

Adresa v manifeste preto ukazuje **na `.exe`**, nikdy na zip.

Zip je pre človeka, ktorému Smart App Control `.exe` odmietne — potrebuje `zaloha\`
a `Spustit-ak-exe-nejde.cmd`, a tie sa do jedného súboru nezmestia.

---

## Postup

### 1. Zdvihni verziu

`<Version>` v `Directory.Build.props`. **Bez toho self-update nikdy nič nenájde** —
porovnáva sa na nerovnosť s tým, čo je v manifeste.

Riadok do `CHANGELOG.md`, `[Unreleased]` premenovať.

### 2. Postav obidva assety

```powershell
./tools/build-release-package.ps1
```

Vyrobí `.exe` aj zip do `dist/launcher/<verzia>/` a **overí, že obidva nesú verziu
z `Directory.Build.props`**. Ručné skladanie raz vydalo zip, ktorého záloha hlásila predošlú
verziu — taký launcher by donekonečna ponúkal aktualizáciu sám na seba. Preto to robí skript
a preto si to sám kontroluje.

Súbory do zipu (`Spustit-ak-exe-nejde.cmd`, `CITAJ-MA.txt`, `launcher.json`) sú v
`tools/package/`.

<details>
<summary>Čo skript robí ručne</summary>

### 2a. Jednosúborový build

```bash
dotnet publish src/FriWorld.Launcher.App/FriWorld.Launcher.App.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -p:DebugType=none \
  -o dist/launcher
```

**Musí to byť jeden súbor.** Self-update odmieta vymeniť build rozsypaný do desiatok DLL,
lebo polovične vymenený launcher je horší než starý.

### 2b. Zip so zálohou

Zip obsahuje: `FriWorldLauncher.exe`, `Spustit-ak-exe-nejde.cmd`, `launcher.json`,
`CITAJ-MA.txt` a priečinok `zaloha\` — zlúčený build s konzolovým subsystémom, ktorý
Smart App Control púšťa tam, kde apphost zastaví.

Záloha sa stavia rovnako ako v `tools/run-under-smart-app-control.ps1`: zdroje `Core`
a `App` preložené do jednej assembly, `OutputType=Exe`, framework-dependent.

</details>

### 3. Vydaj

```bash
gh release create v<verzia> \
  dist/launcher/FriWorldLauncher.exe \
  dist/FriWorld-Launcher-<verzia>-win-x64.zip \
  --repo Robindhuil/FriWorld-Launcher \
  --title "<verzia>" --notes-file <poznamky.md> --prerelease
```

### 4. Oznám to v manifeste hry

Až teraz, keď je binárka naozaj na svojom mieste:

```bash
dotnet run --project src/FriWorld.Launcher.Cli -- pack \
  --input "<Build hry>" --version <verzia hry> \
  --base-url "<kde su archivy hry>" \
  --launcher-version <verzia launchera> \
  --launcher-url "https://github.com/Robindhuil/FriWorld-Launcher/releases/latest" \
  --launcher-file win-x64=dist/launcher/FriWorldLauncher.exe \
  --launcher-base-url "https://github.com/Robindhuil/FriWorld-Launcher/releases/download/v<verzia>"
```

Potom skopíruj vygenerovaný `manifest.json` do `releases/` a pushni.

`--launcher-base-url` **musí byť https**. Launcher sa tým súborom nahradí, takže tu je
pravidlo prísnejšie než pri archíve hry: manifest zo zneužitého spojenia nesmie vedieť
podstrčiť spustiteľný súbor.

---

## Poradie je záväzné

```
build  →  release s binárkou  →  až potom manifest
```

Kým manifest o novej verzii nevie, nikto o nej nevie — čo je správne. Naopak by launchery
u ľudí ukazovali na súbor, ktorý ešte neexistuje, a self-update by zlyhával na stiahnutí.

---

## Keď sa vydanie pokazí

Prepíš `releases/manifest.json` späť — buď na predošlú verziu launchera, alebo sekciu
`launcher` úplne odstráň. Bez nej launcher len ukáže odkaz na stiahnutie, čo je bezpečný
stav.

Binárku predošlej verzie preto **nemaž**.

---

## Čo overiť pred prvým ostrým self-updatom

Výmena je otestovaná na skutočných súboroch (`SelfUpdateSwapTests`), ale **nikdy nebežala
v skutočnom nasadení** — vyžaduje jednosúborový build, ktorý Smart App Control na
vývojovom stroji blokuje.

Pred tým, než sa naň spoľahneš u cudzích ľudí, sprav skúšku nanečisto na stroji, kde sa
jednosúborový build spustiť dá:

```
[ ] stará verzia sa spustí a ohlási novú
[ ] Aktualizovať a reštartovať stiahne, overí a vymení
[ ] nová verzia nabehne sama, bez hlásenia „beží iný launcher"
[ ] pri druhom štarte zmizne súbor .superseded
[ ] hra zostane nainštalovaná a hrateľná
```

Ten tretí bod je ten, na ktorom to raz už padlo: launcher štartoval nástupcu skôr, než
uvoľnil zámok jednej inštancie.
