# Manifest sa číta ako statický súbor, nie cez GitHub Releases API

**Verzia:** 0.1.0-alpha · **Dátum:** 2026-08-26

## Kontext

Pôvodný plán mal launcher volať
`https://api.github.com/repos/Robindhuil/FriWorld/releases/latest`, z poľa `assets`
nájsť `manifest.json` a stiahnuť ho.

Overené na tom repe, neautentizovane:

```
HTTP/1.1 200 OK
X-RateLimit-Limit: 60
```

**60 volaní za hodinu na IP adresu.** Sťahovanie samotného assetu sa do limitu neráta,
lebo ide cez presmerovanie na `objects.githubusercontent.com` — bolí len fetch manifestu.
Lenže launcher ho robí pri každom štarte, a za CGNAT-om, na intráku alebo v škole zdieľa
jednu adresu kopa ľudí. Prvých šesťdesiat štartov za hodinu prejde, zvyšok dostane 403.

Druhá vec: adresa je zadrôtovaná v binárke, ktorú má používateľ na disku. Keby sa repo
niekedy zavrelo alebo sa buildy presunuli inam, opraviť sa to dá len vydaním nového
launchera — čo predpokladá, že starý launcher ešte vie stiahnuť ten nový.

## Rozhodnutie

Launcher číta manifest ako **obyčajný statický JSON na pevnej URL**
(`JsonUrlReleaseSource`). URL sa dá prebiť premennou `FRIWORLD_MANIFEST_URL` alebo
prepínačom `--manifest`.

Adresy archívov v manifeste smú byť **relatívne** voči manifestu. Priečinok s releasom
sa tak dá presunúť bez prepisovania obsahu.

Prístup k obsahu ide cez `IContentClient` — `https` v ostrej prevádzke, `file://` pre
lokálny mock. Zvyšok pipeline nevie, ktorý z nich beží.

## Dôsledky

- Žiadny rate limit. Statický súbor na Hube alebo na CDN nemá strop.
- Manifest sa stal **vrstvou nepriamosti**. Presun buildov na iné úložisko je odteraz
  úprava jedného JSON súboru; launchery u ľudí sa nemenia. To je zároveň odpoveď na
  otvorenú otázku z plánu „čo keď repo hry prestane byť verejné".
- Manifest musí niekto **publikovať samostatne**, nie len priložiť ako release asset.
  Build pipeline hry teda okrem archívov nahrá manifest aj na jeho pevnú adresu.
  To je jeden krok navyše vo Fáze 1.
- Launcher stratil možnosť zistiť „aké releasy existujú". Nepotrebuje to — vždy ho
  zaujíma len ten, ktorý manifest práve menuje.
- Pre vývoj to znamená, že mock a produkcia sa líšia jedine schémou URL, takže sa
  proti mocku testuje tá istá cesta kódu.
