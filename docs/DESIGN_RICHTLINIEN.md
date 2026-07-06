# BüroCockpit Designrichtlinien

Diese Datei ist die verbindliche Designrichtlinie fuer UI-Aenderungen in
BüroCockpit. Vor jeder UI-Aenderung muss sie gelesen und eingehalten werden.

## Grundlinie

- BüroCockpit verwendet eine ruhige, helle Kartenoptik.
- Vorhandene Design-Ressourcen, Styles, Brushes, Tokens und wiederverwendbare
  UI-Bausteine sind zu bevorzugen.
- Keine harten Farben verwenden, wenn passende Design-Ressourcen existieren.
- Harte Farben nur mit ausdruecklicher Freigabe.
- Keine weissen Rahmen verwenden.
- Keine tiefschwarzen Standardrahmen verwenden.
- Kategorie-Rahmen sollen dieselbe Rahmenlogik und denselben Stil wie normale
  Karten- und Task-Karten-Rahmen verwenden.
- Kategoriezeilen links duerfen nicht pauschal `#000000` oder tiefschwarz sein.
- Normale Kategoriezeilen nutzen einen ruhigen Hintergrund, denselben
  Kartenrahmen wie die uebrigen Karten und bleiben nicht dauerhaft sehr hell.

## Interaktion und Zustand

- Hover-Zustaende muessen deutlich sichtbar sein und auf vorhandenen Hover-
  Ressourcen aufbauen.
- Hover muss staerker als der Normalzustand sein.
- Hover verschwindet beim Mouse-Out.
- Auswahl-Zustaende muessen klar staerker als Hover-Zustaende sein und
  dauerhaft sichtbar bleiben, solange sie ausgewählt sind.
- Auswahl darf keine weisse Flaeche und keinen tiefschwarzen Sonderrahmen
  verwenden.
- Ein Akzentbalken oder ein staerkerer vorhandener Akzent-/Kartenrahmen ist
  erlaubt.
- Drag-&-Drop-Markierungen duerfen nur waehrend Drag & Drop angezeigt werden.
- Drop-Markierungen muessen nach Drop oder Abbruch zurueckgesetzt werden.
- Beim Drag & Drop gilt: oben/unten zeigt eine Einfuegelinie, mittig eine
  Zielmarkierung.
- Nach Drop oder Abbruch muessen alle temporaren Markierungen verschwinden.

## Kategorien und Navigation

- Chevrons werden ohne runden Button dargestellt.
- Der optische Chevron-Klickbereich liegt nur am Pfeil.
- Hauptkategorien sind beim Start eingeklappt.
- Ein Doppelklick auf eine Hauptkategorie klappt diese nur ein oder aus.
- Ein Klick auf eine Hauptkategorie zeigt nur direkt zugeordnete Aufgaben.
- Aufgaben aus Unterkategorien werden erst bei Klick auf die Unterkategorie
  angezeigt.

## Kategorieauswahl

- Hauptkategorien mit Unterkategorien duerfen in Kategorieauswahlen nicht als
  Ziel angeboten werden.
- Unterkategorien und echte Endkategorien sind auswaehlbar.
- Das Archiv ist keine normale Kategorie.
- Das Archiv liegt unter `Einstellungen > Aufträge`.
- Mobile Eingaenge und `Wartet auf Freigabe` duerfen nicht als normale
  Kategorien angeboten werden.
- Die iPad-Kategorieauswahl nutzt dieselbe Logik.

## Sperren

- Keine Produktivdaten loeschen.
- Keine Version, kein Tag und kein Release ohne ausdrueckliche Freigabe.
