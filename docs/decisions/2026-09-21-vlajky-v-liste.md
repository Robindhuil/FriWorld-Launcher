# Jazyk sa prepína vlajkou v lište, nie položkou v ponuke

**Dátum:** 2026-09-21 · **Stav:** platí · **Verzia:** 0.2.0-alpha

## Kontext

Angličtina prišla do okna vo verzii 0.2.0-alpha a prepínala sa položkou v ponuke `⋯` vľavo
hore. Položka bola pomenovaná jazykom, **do ktorého** prepína: v slovenskom okne stálo
„English", v anglickom „Slovenčina". Rozumné, kým sa na to niekto pozerá ako vývojár.

Pri skúške pred vydaním sa ukázalo, čo je na tom zlé. Prepínač jazyka je jediné nastavenie
v okne, ktoré potrebuje človek, ktorý **okno nevie prečítať**. Ten nevie, že tri bodky
niečo skrývajú, a keby vedel, tak slovo „English" musí najprv nájsť medzi dvomi slovenskými
riadkami. Všetko ostatné v tej ponuke — kontrola znova, denník — sú veci pre niekoho, kto
už rozumie tomu, čo číta.

## Zvažované možnosti

**Nechať to v ponuke `⋯`.** Nič nestojí a lišta zostane úplne tichá. Ale nechá nájdenie
prepínača na tom, kto ho najmenej vie nájsť.

**Dve tlačidlá vedľa seba, vlajka na každé.** Bez rozbaľovania, jeden klik. Pri dvoch
jazykoch najrýchlejšie — a pri treťom je lišta plná a celé sa to prerába.

**Rozbaľovací zoznam s vlajkami v lište.** Jedno tlačidlo, ktoré ukazuje, čím okno práve
hovorí, a po kliknutí ponúkne obidva jazyky. O klik viac než dve tlačidlá, ale znesie
tretí jazyk bez prestavby a v lište zaberá jedno miesto.

**Len text `SK` / `EN` namiesto vlajok.** Vyhne sa známej námietke, že vlajka nie je jazyk.
Lenže tá námietka bolí tam, kde jeden jazyk má veľa krajín; tu sú to dva jazyky, jedna
krajina na každý, a obecenstvo sú deti.

## Rozhodnutie

**Rozbaľovací zoznam s vlajkami, vpravo hore naľavo od minimalizácie.** Zatvorený nesie
vlajku jazyka, ktorým okno práve hovorí, a šípku. Rozbalený má dva riadky s vlajkou
a menom jazyka, pri zapnutom je `✓`.

**Z ponuky `⋯` položka mizne.** Jedno nastavenie na dvoch miestach je horšie než na
jednom — a ten, kto by ho hľadal v ponuke, ho teraz vidí bez hľadania.

**Každý jazyk je napísaný sám v sebe** — `Slovenčina`, `English` — a nie preložený. Zoznam
ukazuje obidva naraz, takže „Angličtina" by bola nečitateľná presne pre toho, komu ten
riadok patrí. Mená prichádzajú z `Languages.NativeName`, nie z `Texts`: `Texts` má po
jednom člene na vetu v obidvoch jazykoch a meno jazyka do toho tvaru nepatrí.

**Voľba jazyka, ktorý je už zapnutý, nespraví nič.** Zoznam ukazuje obidva, takže kliknúť
na ten zapnutý je vec, ktorú spraví ktokoľvek; prepísať pri tom zapamätanú voľbu a napísať
riadok do denníka by bolo zbytočné.

**Vlajky sa kreslia, nie sťahujú.** `tools/draw-flags.py` ich vyrobí do
`src/FriWorld.Launcher.App/Assets/`. Do `Assets/` tak neprichádza nič zo zdroja, ktorý
sa nedá znovu overiť, a keď treba inú, prekreslí sa a nehľadá sa, odkiaľ pôvodná bola.
Obidve sú zjednodušené: pri 24 × 16 px je counterchange saltiru na Union Jacku aj detail
slovenského znaku pod rozlíšením jedného pixelu a kresliť ich verne by len pridalo
spôsoby, ako to spraviť zle.

**Slovenská vlajka musí mať znak.** Bez neho sú tri pruhy biela-modrá-červená ruská
vlajka; to nie je kozmetika, to je iná krajina.

## Dôsledky

`RelayCommand<T>` pribudol vedľa `RelayCommand`. Dovtedajší príkaz zahadzoval `parameter`,
takže `CommandParameter` nemal ako doraziť, a prepínač je prvá vec v okne, ktorá jeden
potrebuje.

`Languages.Other()` zmizol. Bol to pomocník pre prepínač ako preklápadlo a pri zozname
nemá čo robiť; to isté platí pre `Texts.SwitchLanguageTo`, ktorý bol jediný člen `Texts`
nesúci meno jazyka namiesto prekladu — a jediná výnimka v `LocalizationTests`. Test je
odteraz bez výnimiek.

Pri skúške sa našla **staršia chyba**, ktorú toto vydanie opravuje pri tom: keď sa nedalo
spojiť so serverom a hra bola na disku, riadok verzie po prepnutí zostal v starom jazyku.
`Fail` ho píše raz, mimo uzáveru, ktorý vie chybu povedať znova. Bolo to presne to
„okno hovorí dvomi jazykmi naraz", ktoré má
[rozhodnutie z 18. 9.](2026-09-18-obidva-jazyky-v-jednom-riadku.md) vylúčiť. Test to
nechytil, lebo zlyhanie púšťal nad prázdnym inštalačným koreňom, kde riadok verzie nevznikne.
