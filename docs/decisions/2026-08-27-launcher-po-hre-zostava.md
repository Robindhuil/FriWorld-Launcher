# Launcher sa po spustení hry neuzavrie, len skryje

**Dátum:** 2026-08-27 · **Stav:** platí · **Verzia:** 0.1.7-alpha ·
**Nahrádza:** pravidlo „po spustení hry sa okno zavrie"

## Kontext

Pôvodné pravidlo znelo: launcher spustí hru a zavrie sa. Dávalo zmysel — jeho úloha je
hotová a okno navyše je len prekážka.

V praxi to znamenalo, že po dohratí človek nemá kde nič spraviť. Čo chce najskôr po
zatvorení hry — aktualizovať, opraviť, odinštalovať, hrať znova — je práve launcher, a ten
musí hľadať druhýkrát.

## Zvažované možnosti

**Zavrieť, ako doteraz.** Najčistejšie z pohľadu procesov: po spustení hry nezostáva bežať
nič zbytočné. Ale odpoveď na „čo teraz" je vždy „nájdi launcher znova".

**Minimalizovať.** Okno zostane v paneli úloh. Lenže kým hra beží, sa v ňom nedá nič robiť
— hlavné tlačidlo je zakázané a druhá kópia hry sa aj tak nespustí. Položka v paneli, ktorá
nič nerobí, je jedna vec navyše medzi človekom a hrou.

**Skryť a vrátiť.** Launcher zmizne úplne, aj z panela úloh, a objaví sa, keď hra skončí.

## Rozhodnutie

Skryť a vrátiť. Keď sa vráti, **skontroluje aktualizácie znova** — sedenie môže trvať
hodinu a vracať sa s tým, čo platilo pred hraním, by bolo horšie než nevracať sa vôbec.

Výnimka zostáva: hra, ktorá spadne do ochrannej lehoty piatich sekúnd, sa za spustenú
nepovažuje. Vtedy okno nezmizne a povie, čo sa stalo — je to jediná chvíľa, keď má launcher
niečo užitočné na povedanie.

## Dôsledky

Launcher beží celý čas, čo beží hra. To je proces navyše; jeho pamäťová stopa je oproti
Unity hre zanedbateľná.

**Návrat okna musí byť vo `finally`.** Čokoľvek vyhodené po skrytí by inak nechalo launcher
bežať bez okna a bez spôsobu, ako sa k nemu dostať — neviditeľný proces, ktorý drží zámok
jednej inštancie a bráni spusteniu ďalšieho.

Kým je okno skryté, na Hrať sa kliknúť nedá. To samo osebe bráni druhej kópii, ale
spoľahnúť sa na to nestačí: pravidlo je v `LaunchAsync`, takže platí aj pre `launcher play`
a aj vtedy, keď hru niekto spustil mimo launchera.
