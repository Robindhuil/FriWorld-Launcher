# Okno sa škáluje celé jedným faktorom

**Dátum:** 2026-08-27 · **Stav:** platí · **Verzia:** 0.1.6-alpha

## Kontext

Okno malo pevných 980 × 720. Na displeji, kde bola tá veľkosť posúdená ako správna, zaberá
47 % šírky a 61 % výšky. Na menšom displeji je to podstatne väčší podiel plochy, než
launcher na svoju jednu úlohu potrebuje.

## Zvažované možnosti

**Nechať pevnú veľkosť.** Najjednoduchšie a na veľkých monitoroch bez chyby. Na notebooku
s 1366 × 768 by okno zabralo takmer celú výšku plochy.

**Zmenšiť rám a nechať vnútro.** Prirodzená prvá myšlienka: okno je menšie, obsah zostáva.
Lenže obsah je navrhnutý na 980 × 720 — 60px logo, 52px tlačidlá, 44px okraje. V menšom
ráme by sa akčné pásmo stlačilo pod logo, ktoré by sa nezmenšilo. Rozloženie by prestalo
byť to, čo bolo navrhnuté.

**Responzívne rozloženie s bodmi zlomu.** Čo robí web. Znamená to navrhnúť okno druhýkrát,
pre každý bod zlomu, a udržiavať obidve podoby. Launcher má jednu obrazovku a beží pol
minúty; druhé rozloženie je náklad bez výnosu.

**Škálovať všetko jedným faktorom.** Vnútro zostáva navrhnuté v jednotkách 980 × 720
a `LayoutTransformControl` zmenší celok — písmo, tlačidlá, odsadenia aj render naraz.

## Rozhodnutie

Jeden faktor na celé okno. Medze sú najviac 50 % šírky a 65 % výšky **pracovnej plochy**,
najmenej 0,70 a najviac 1,00.

Čísla sú odvodené z plochy 2103 × 1183 a položené kúsok nad to, čo tá plocha potrebuje, aby
jej okno zostalo plné aj po odrátaní panela úloh. Výška má voľnejšiu medzu, lebo na
notebookoch dochádza skôr než šírka.

Aritmetika je vo `WindowFit` v `Core`, nie v okne — je to počítanie, ktoré má byť
otestované, nie kreslenie.

## Dôsledky

Pomer strán sa nikdy nemení. Dva nezávislé faktory by render aj písmo roztiahli, a to je
horšie než okno, ktoré je o niečo väčšie, než by chcelo byť.

Nikdy sa nezväčšuje nad návrhovú veľkosť. Nie je čo odhaliť a pozadie je raster, ktorý by
zmäkol.

**Spodná medza smie ustúpiť.** Keď by okno pri 0,70 pretieklo cez okraj plochy, faktor klesne
tak, aby sa zmestilo. Nečitateľné písmo je zlé, ale okno bez systémovej lišty sa nedá
pritiahnuť späť, keď raz vytečie — a to je horšie.

Počíta sa raz, pred zobrazením okna. Okno, ktoré sa zmenší až na obrazovke, je bliknutie,
ktoré si človek všimne.
