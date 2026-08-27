# Jeden runbook pre web aj desktop

**Dátum:** 2026-08-27 · **Stav:** platí

## Kontext

Nasadenie bolo popísané na troch miestach naraz. `docs/releasing.md` v repe launchera
hovorilo o vydaní hry, `docs/releasing-launcher.md` o vydaní launchera a
`docs/nahravanie-buildu.md` v repe Hubu o nahratí web buildu na R2. Každý popis bol sám
o sebe úplný, ale pri vydaní sa muselo pamätať, kde ktorá tretina je — a dve z nich boli
v inom repozitári než ten, z ktorého sa balí.

## Zvažované možnosti

**Nechať tri dokumenty a prepojiť ich odkazmi.** Najmenej práce a každý zostane pri svojom
repe. Lenže poradie krokov je medzi platformami previazané (manifest sa vždy zverejňuje
posledný) a to sa v troch dokumentoch dá zapísať len trikrát, teda trikrát rozísť.

**Jeden runbook v repe launchera.** Launcher je jediné miesto, ktoré manifest zapisuje aj
číta, a `pack` je jediný príkaz, ktorý sa pri desktop vydaní naozaj spúšťa. Web sa nahráva
z repa Hubu, ale príkazy sú tri a menia sa zriedka.

**Jeden runbook v repe Hubu.** Web tam patrí, desktop nie — musel by popisovať príkazy
projektu, ktorý v tom repe nie je.

## Rozhodnutie

Jeden dokument, [`docs/deploying.md`](../deploying.md) v repe launchera. Pôvodné dva
dokumenty launchera zrušené, `nahravanie-buildu.md` v Hube zredukovaný na ukazovateľ.

## Dôsledky

Runbook musí popisovať aj kroky v cudzom repe. To je prijateľné: web build sa nahráva
tromi npm príkazmi a ich znenie sa mení oveľa pomalšie, než ako často by sa dva dokumenty
rozišli.

Do runbooku pribudla sekcia o tom, čo z neho vie odviesť Claude a čo nie — zbuildiť
v Unity nevie a prihlasovacie údaje nikdy nedostane do rúk. R2 kľúče sa preto nastavujú
ako trvalé premenné prostredia.

## Pasca, ktorá si vyžiadala vlastný odstavec

V hre sú **dva rôzne súbory menom `manifest.json`**: zoznam súborov WebGL buildu
(`public/game/manifest.json` v Hube) a kontrakt o vydaní (`releases/manifest.json`
v launcheri). Nemajú spolu nič spoločné. Kým boli popísané v rôznych dokumentoch, nebolo
to vidieť; v jednom dokumente to musí byť povedané hneď na začiatku.
