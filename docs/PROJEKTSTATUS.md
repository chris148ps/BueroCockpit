# Projektstatus BüroCockpit

## Aktueller Entwicklungsstand

BüroCockpit enthält Übersicht, grobe Systemnavigation, kompakte dynamische Vorgangstabellen, rechten Detailinspektor und eine chronologische Terminprojektion.

## Architektur

- Desktop: Avalonia UI, lokale SQLite-/Dateidaten.
- Status: additive WorkflowType-/WorkflowStep-Felder; gespeicherte Schritte bleiben führend.
- Tabellen: drei getrennte lokale TableLayoutSettings mit Reihenfolge, Sichtbarkeit, Breiten, Sortierfeld und Sortierrichtung.
- Projektion: TableCellItem verbindet Kopfdefinition und Datenzeile; Kunde ist als Pflichtspalte geschützt.
- Monteure: leerer Auswahlwert speichert eine leere Zuordnung.

## Erledigte Hauptfunktionen

- dynamische Spaltenreihenfolge und Sichtbarkeit werden tatsächlich projiziert
- Titel und weitere reale Spalten im Kopf-Kontextmenü verfügbar
- Kopf-Splitter und Zeilenbreiten verbunden
- getrennte Layoutpersistenz für Aufträge, Angebote und Termine
- horizontales und vertikales Scrollen
- Status-, Termin- und Monteurkorrekturen

## Bekannte offene Punkte

- direkte Drag-and-drop-Pointergeste für die Spaltenreihenfolge fehlt noch
- visuelle Neustart- und Mausabnahme wartet auf eine entsperrte macOS-Sitzung
- Netzwerk-Sync bleibt deaktiviert

## Wichtige Entscheidungen

- StatusStep ist die zentrale sichtbare Statusquelle.
- Technische IDs bleiben intern.
- Bestehende Kategorien und Zuordnungen werden nicht destruktiv migriert.
- Layoutwerte bleiben lokal und ansichtsspezifisch.
