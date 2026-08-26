# Publikovaný manifest

`manifest.json` je to, čo launchery u ľudí čítajú. **Adresa sa nikdy nemení** — mení sa
obsah súboru. To je celý zmysel: keď sa archívy raz presunú na iné úložisko, prepíše sa
tento súbor a launchery, ktoré už sú u ľudí, ostanú fungovať.

## Vydanie novej verzie hry

1. `launcher pack …` s `--base-url` adresy, kde archív naozaj bude
2. nahraj archív (release v repe hry, alebo iné úložisko)
3. **až potom** skopíruj vygenerovaný `manifest.json` sem a pushni

Poradie nie je jedno. Kým tento súbor nie je aktualizovaný, hráči vidia predošlú verziu —
čo je správne. Naopak by launcher ukazoval na archív, ktorý ešte neexistuje.

## Návrat po pokazenom vydaní

Prepíš tento súbor späť na predošlú verziu a pushni. Launcher verzie neporovnáva na
poradie, len na rozdiel, takže krok späť je preň bežná aktualizácia a ľudia sa vrátia sami.

Preto sa archívy predošlej verzie nemažú hneď.
