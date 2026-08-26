# Súbory pre repo hry

Toto **nie je súčasť launchera**. Sú to súbory, ktoré patria do repa
`Robindhuil/FriWorld`, ale píšu sa tu, lebo tvoria druhú polovicu kontraktu
a majú sa meniť spolu s ním.

| súbor | kam patrí |
|---|---|
| `BuildRelease.cs` | `Assets/_Game/Editor/BuildRelease.cs` |

Celé zadanie aj s dôvodmi je v [`../../docs/build-pipeline-spec.md`](../../docs/build-pipeline-spec.md).

---

## Nasadenie

1. Skopíruj `BuildRelease.cs` do `Assets/_Game/Editor/`. Unity si `.meta` vyrobí samo.
2. Do `.gitignore` repa hry pridaj `Build/`, ak tam ešte nie je.
3. V Unity sa objaví menu **FriWorld → Build → Release**.

Prvý build nechaj len Windows. Linux sa zapína prepínačom
**FriWorld → Build → Include Linux** a vyžaduje **Linux Build Support (IL2CPP)**
z Unity Hub → Installs → Add modules. Launcher aj balič ho podporujú a otestované to je,
takže sa dá pridať kedykoľvek.

---

## Ako to celé beží

```
Unity: FriWorld → Build → Release
        │
        ▼
Build/<bundleVersion>/win-x64/          hotový player
        │
        ▼
launcher pack --input … --version …     archív + SHA256 + manifest.json
        │
        ▼
dist/<verzia>/                          na nahratie
```

Skript na konci vypíše presný `pack` príkaz aj s cestami, netreba si ho pamätať.

---

## Čo skript zámerne nerobí

- **Nedvíha `bundleVersion`.** Číta ju. Dvíhať ju je vedomý krok človeka a je to jediný
  bod, ktorý určuje, ako sa release volá.
- **Nebalí a nepočíta hashe.** Unity beží na frameworku bez tar writera, a `tar` vyrobený
  na Windows stratí execute bit, takže by sa linuxový build nespustil. Hlavne ale manifest
  má písať len jedna strana, inak sa kontrakt časom rozíde.
- **Nenahráva nikam.** Nahratie je vedomý krok.
