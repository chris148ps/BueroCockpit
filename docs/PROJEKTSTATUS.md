# Projektstatus BüroCockpit

## Aktueller Entwicklungsstand

BüroCockpit enthält kompakte dynamische Vorgangstabellen für Aufträge und Angebote sowie eine chronologische Terminansicht. Kopf und Zeilen verwenden dieselbe lokale Ansichtskonfiguration.

## Architektur

- Drei getrennte `TableLayoutSettings` speichern Reihenfolge, Sichtbarkeit, Breiten, Sortierfeld und Sortierrichtung.
- `TableCellItem` projiziert jede sichtbare Spalte für Kopf-/Zeilenkonsistenz.
- Headerzellen können per Pointer-Geste umgeordnet werden.
- WorkflowStep bleibt zentrale Statusquelle; Kategorien bleiben kompatibel.

## Erledigte Hauptfunktionen

- kompakte Standardspalten und zusätzliche Titelspalte
- Sichtbarkeit über Kopf-Kontextmenü
- Breitenänderung über Kopf-Splitter
- Drag-and-drop-Reihenfolge der sichtbaren Spalten
- lokale Wiederherstellung je Ansicht
- Terminfilter und leere Monteurzuordnung

## Bekannte offene Punkte

- sichtbare Endabnahme und Neustartprüfung warten auf eine entsperrte macOS-Sitzung.
- produktive mutierende Status-/Monteurtests wurden nicht ausgeführt.

## Wichtige Entscheidungen

- Kunde bleibt immer sichtbar.
- Technische IDs bleiben intern.
- Layoutwerte werden ausschließlich lokal gespeichert.
