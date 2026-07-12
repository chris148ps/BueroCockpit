# Projektstatus BüroCockpit

## Aktueller Entwicklungsstand

BüroCockpit enthält kompakte dynamische Vorgangstabellen für Aufträge und Angebote sowie eine chronologische Terminansicht. Kopf und Zeilen verwenden dieselbe lokale Ansichtskonfiguration. Spaltenverschieben und Spaltenbreitenänderung sind über getrennte Bedienbereiche möglich.

## Architektur

- Drei getrennte `TableLayoutSettings` speichern Reihenfolge, Sichtbarkeit, Breiten, Sortierfeld und Sortierrichtung.
- `TableCellItem` projiziert jede sichtbare Spalte für Kopf-/Zeilenkonsistenz.
- Jeder sichtbaren Kopfspalte ist ein eigener Resize-Griff mit Live-Breitenübernahme und Mindestbreite zugeordnet.
- Normale Kopfbereiche verschieben Spalten; der rechte Randbereich startet ausschließlich Resize.
- Ein gemeinsamer horizontaler ScrollViewer hält Kopf und Zeilen synchron.
- WorkflowStep bleibt zentrale Statusquelle; Kategorien bleiben kompatibel.

## Erledigte Hauptfunktionen

- kompakte Standardspalten und zusätzliche Titelspalte
- sichtbare Titelumschaltung über die kompakte Toolbar-Aktion
- getrennte Resize-Griffe mit horizontalem Größenänderungs-Cursor
- Live-Anpassung der Kopf- und Datenzellbreiten
- Drag-and-drop-Reihenfolge der sichtbaren Spalten ohne Breitenänderung
- lokale Wiederherstellung je Ansicht nach Neustart
- Terminfilter, horizontales Scrollen und leere Monteurzuordnung

## Bekannte offene Punkte

- Produktive mutierende Status-/Monteurtests wurden nicht ausgeführt.

## Wichtige Entscheidungen

- Kunde bleibt immer sichtbar.
- Technische IDs bleiben intern.
- Layoutwerte werden ausschließlich lokal gespeichert.
