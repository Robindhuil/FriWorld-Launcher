# Manifest — kontrakt medzi hrou a launcherom

**Verzia:** 0.1.0-alpha · **Dátum:** 2026-08-26

Jediný súbor, ktorý launcher číta. Nečíta názvy súborov v úložisku, nečíta žiadne API —
len toto. Preto sa dá úložisko vymeniť bez toho, aby sa u ľudí menil launcher.

Zapisuje ho `launcher pack`, číta ho `JsonUrlReleaseSource`. **Obidve strany sú ten istý
kód**, takže sa nemôžu rozísť — to je celý dôvod, prečo balenie nerobí Unity.

---

## Celý tvar

```json
{
  "version": "0.1.2-alpha",
  "released": "2026-08-26T10:00:00Z",
  "notes": "Krátky text do okna launchera.",
  "minLauncherVersion": "0.3.0",

  "platforms": {
    "win-x64": {
      "url": "FriWorld-0.1.2-alpha-win-x64.zip",
      "sha256": "…64 hex znakov…",
      "size": 435666845,
      "exec": "FriWorld.exe",
      "format": "zip"
    },
    "linux-x64": {
      "url": "FriWorld-0.1.2-alpha-linux-x64.tar.gz",
      "sha256": "…",
      "size": 441000000,
      "exec": "FriWorld.x86_64",
      "format": "tarGz"
    }
  },

  "launcher": {
    "version": "0.2.0-alpha",
    "downloadUrl": "https://friworld.example/stiahnut",
    "notes": "Rýchlejšie sťahovanie.",
    "platforms": {
      "win-x64": {
        "url": "https://friworld.example/FriWorldLauncher.exe",
        "sha256": "…",
        "size": 49568808
      }
    }
  }
}
```

Povinné je len `version` a aspoň jedna platforma. Všetko ostatné je voliteľné.

---

## Pole po poli

### Koreň

| pole | povinné | čo |
|---|---|---|
| `version` | áno | tag hry, obvykle `bundleVersion` |
| `released` | nie | kedy, len na informáciu |
| `notes` | nie | jedna až tri vety do okna |
| `minLauncherVersion` | nie | najnižší launcher, ktorý s týmto manifestom smie pracovať |
| `platforms` | áno | aspoň jedna |
| `launcher` | nie | novšia verzia launchera |

### `platforms.<kľúč>`

Kľúče sú `win-x64`, `linux-x64`, `osx-arm64`, `osx-x64`. Sú súčasťou kontraktu a `pack`
ich číta priamo z názvov priečinkov Unity výstupu.

| pole | povinné | čo |
|---|---|---|
| `url` | áno | absolútna, alebo relatívna voči manifestu |
| `sha256` | áno | presne 64 hex znakov |
| `size` | áno | bajty, väčšie než nula |
| `exec` | áno | cesta k **binárke** vnútri archívu |
| `format` | nie | `zip` alebo `tarGz`; bez neho sa odvodí z prípony |

### `launcher`

| pole | povinné | čo |
|---|---|---|
| `version` | áno | tag launchera |
| `downloadUrl` | áno | **stránka pre človeka**, nie súbor; http alebo https |
| `notes` | nie | jedna veta |
| `launcher.platforms.<kľúč>` | nie | binárka: `url` (iba **https**), `sha256`, `size` |

Bez `platforms` je aktualizácia launchera len odkaz. S ňou sa launcher vymení sám.

---

## Pravidlá, ktoré nie sú vidieť na tvare

### `exec` musí ukazovať na binárku, nie na priečinok

Na macOS to znamená `FriWorld.app/Contents/MacOS/FriWorld`. Samotné `FriWorld.app` je
**priečinok** a spustiť sa nedá. Launcher si binárku v bundli dohľadá, ale ohlási to ako
varovanie — manifest ju má menovať priamo.

Na Linuxe Unity vyrába `FriWorld.x86_64`, nie `FriWorld`.

### Relatívne `url` sa počítajú voči manifestu

`pack` bez `--base-url` zapisuje holé názvy súborov. Priečinok s releasom sa potom dá
presunúť alebo nahrať kamkoľvek bez prepisovania obsahu.

### Neznáme polia sa ignorujú

Build pipeline môže pridávať polia bez toho, aby rozbila launchery, ktoré sú už u ľudí.

**Toto ale platí len dovtedy, kým ich ignorovanie dáva správny výsledok.** Na deň, keď
prestane, je `minLauncherVersion`.

### `minLauncherVersion` sa nastavuje len keď treba

Je to jediné miesto, kde sa verzie **radia**. Nastavením sa zamkne von každý launcher
v obehu pod tou verziou, takže sa nastavuje výhradne vtedy, keď je alternatívou to, že
tie launchery spravia niečo zle.

Nečitateľná alebo chýbajúca hodnota znamená žiadny strop. Brána, ktorá sklapne omylom,
by vypla hru, ktorá by inak fungovala.

### Verzia hry sa neporovnáva na poradie

`0.1.1` po `0.1.2` je platný krok späť a launcher ho spraví. Manifest je autorita.

---

## Ako vzniká

```bash
launcher pack --input Build/0.1.2-alpha --version 0.1.2-alpha --notes "Čo je nové."
```

S binárkou launchera a stropom:

```bash
launcher pack \
  --input Build/0.1.2-alpha \
  --version 0.1.2-alpha \
  --base-url https://ulozisko.example/friworld/0.1.2-alpha \
  --launcher-version 0.2.0-alpha \
  --launcher-url https://friworld.example/stiahnut \
  --launcher-file win-x64=dist/FriWorldLauncher.exe \
  --launcher-base-url https://friworld.example/launcher \
  --min-launcher 0.2.0-alpha
```

`pack` po zápise manifest **hneď aj prečíta späť**, takže sa nevydá súbor, ktorý by
launcher odmietol.

---

## Čo launcher pri čítaní odmietne

- prázdna `version`
- žiadne platformy
- `sha256`, ktorý nemá 64 znakov
- `size` nula alebo menej
- prázdny `exec`
- `url`, ktoré sa nedá rozrátať voči manifestu

Odmietnutie je hlásené ako „release information could not be read" a launcher pri ňom
nechá nainštalovanú hru hrateľnú.
