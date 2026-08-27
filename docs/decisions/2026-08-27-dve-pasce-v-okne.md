# Dve pasce v okne: kto vlastní kláves a kto vlastní stav

**Dátum:** 2026-08-27 · **Stav:** platí · **Verzie:** 0.1.5-alpha.2, 0.1.7-alpha.2

Dve chyby v okne, ktoré spolu nesúvisia, ale majú rovnaký tvar: niečo napísalo do stavu
okna neskôr než ten, komu stav patril.

---

## Enter patrí tomu, kto má fokus

Klávesnica bola pridaná tak, že si okno odchytilo Enter cez `KeyBinding` a spustilo hlavné
tlačidlo. Vyzeralo to správne — Enter spraví hlavnú vec.

Lenže Tab medzitým posúva fokus po tlačidlách a kreslí okolo nich prstenec. Ten prstenec je
sľub o tom, čo Enter spraví. Okno ho porušovalo: nech bol fokus kdekoľvek, Enter spustil
hlavné tlačidlo.

**Enter patrí tomu, kto má fokus.** Hlavné tlačidlo ho chytá cez `IsDefault`, čo v Avalonii
platí len pre Enter, ktorý si nevzal nikto iný. Nikdy nie cez odchytenie na celom okne.

Z toho plynie aj to, čo bolo treba dorobiť pri modale: keď je otázka na obrazovke, hlavné
tlačidlo musí byť **zakázané**, inak by Enter prešiel modalom rovno do inštalácie.

---

## Hlásenie o priebehu nesmie prepísať výsledok

Hlásenia o priebehu sa posielali na UI vlákno cez `Dispatcher.Post` **vždy**, aj keď ich
vyvolalo samotné UI vlákno.

Keď práca dobehla bez toho, aby sa raz uspala — čo sa pri kontrole aktualizácií stane, len
čo sa dá odpovedať z cache — poradie sa obrátilo:

```
1. kontrola ohlási „Kontrolujem aktualizácie"   →  do fronty
2. kontrola dobehne, výsledok sa zapíše         →  „Pripravené", pruh preč
3. zaradené hlásenie z kroku 1 sa vykoná        →  späť na „Kontrolujem…", pruh späť
```

Okno zostalo stáť na texte, ktorý kontrola nastavuje **pred** začiatkom, s pruhom priebehu
na obrazovke — a s akciou správne nastavenou na Hrať pod tým.

Dve zmeny, každá by stačila sama, spolu držia aj to druhé:

- hlásenie vyvolané na UI vlákne sa aplikuje **hneď**, takže poradie zostane zachované;
- hlásenie, ktoré dorazí, keď už nič nebeží, sa **zahodí**.

---

## Čo je z toho poučenie

Obidve chyby prežili každý ručný test a obidve sa objavili až v prevádzke — prvá preto, že
sa dá pracovať aj bez klávesnice, druhá preto, že je vidieť len tam, kde je kontrola rýchla.

Ani jednu sa nedalo vyčítať zo zdrojáku. Či Enter dostane zafokusované tlačidlo alebo
predvolené a či sa `Post` vykoná pred alebo po pokračovaní `await`, sú vlastnosti Avalonie
a plánovača, nie kódu, ktorý je vidieť.

**Preto existuje `tests/FriWorld.Launcher.App.Tests`** — testy, ktoré otvoria skutočné okno
cez `Avalonia.Headless` a stláčajú v ňom klávesy. Obidve chyby v ňom padnú.
