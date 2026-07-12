# Projektstatus BüroCockpit

## Aktueller Entwicklungsstand

BüroCockpit enthält kompakte dynamische Vorgangstabellen für Aufträge und Angebote sowie eine chronologische Terminansicht. Kopf und Zeilen verwenden dieselbe lokale Ansichtskonfiguration. Spaltenverschieben, Spaltenbreitenänderung und typgerechte Kopfklick-Sortierung sind getrennt bedienbar.

## Architektur

- Drei getrennte TableLayoutSettings speichern Reihenfolge, Sichtbarkeit, Breiten, Sortierfeld und Sortierrichtung.
- TableCellItem projiziert jede sichtbare Spalte für Kopf-/Zeilenkonsistenz.
- Kopfklicks werden nach der Bewegungsprüfung als Sortierklick behandelt; Drag und Resize bleiben davon getrennt.
- Sortierung verwendet eigene fachliche Sortierfelder für Status, Text, Datum und Uhrzeit.
- WorkflowStep bleibt zentrale Statusquelle; Kategorien bleiben kompatibel.

## Erledigte Hauptfunktionen

- kompakte Standardspalten und zusätzliche Titelspalte
- getrennte Resize-Griffe mit Live-Breitenübernahme
- Drag-and-drop-Reihenfolge der sichtbaren Spalten ohne Sortiernebenwirkung
- Kopfklick-Sortierung mit Auf-/Abwärtspfeil
- fachliche Statusreihenfolge für direkte Aufträge und Angebotsvorgänge
- alphabetische Textsortierung ohne Groß-/Kleinschreibung
- chronologische Termin-/Datums-/Uhrzeitsortierung mit leeren Werten am Ende
- lokale Wiederherstellung je Ansicht nach Neustart
- horizontales Scrollen von Kopf und Zeilen

## Bekannte offene Punkte

- Produktive mutierende Status-/Monteurtests wurden nicht ausgeführt.

## Wichtige Entscheidungen

- Kunde bleibt immer sichtbar.
- Technische IDs bleiben intern.
- Layout- und Sortierwerte werden ausschließlich lokal gespeichert.
