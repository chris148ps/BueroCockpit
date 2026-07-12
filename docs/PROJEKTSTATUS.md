# Projektstatus BüroCockpit

## Aktueller Entwicklungsstand

BüroCockpit ist eine lokale Avalonia/.NET-Desktopanwendung mit Übersicht, grober Systemnavigation, kompakten Vorgangslisten, Detailinspektor und Terminprojektion. Vorgangs- und Terminlisten verwenden lokale, ansichtsspezifische Tabellenlayoutdaten.

## Architektur

- Desktop: Avalonia UI, lokale SQLite-/Dateidaten.
- Status: additive WorkflowType-/WorkflowStep-Felder mit defensiver Bestandsableitung.
- Navigation: Systemnavigation getrennt von bestehenden Benutzerkategorien.
- Tabellen: separate Orders-, Offers- und Appointments-Layouts mit Breiten, Reihenfolge-, Sichtbarkeits- und Sortiermetadaten.
- Splitter: Liste/Detail und Kopfspalten verwenden sichtbare GridSplitter-Handles; Layoutwerte bleiben lokal.

## Erledigte Hauptfunktionen

- kompakte Spalten Status, Kunde, Ort, Termin, Techniker
- ansichtsspezifische gespeicherte Spaltenbreiten
- Kopf-Splitter mit Zeilenbindung
- horizontales und vertikales Scrollen
- Terminfilter und chronologische deduplizierte Terminprojektion
- Status „Angebot gesendet“ und leere Monteurzuordnung

## Bekannte offene Punkte

- echte Drag-and-drop-Reihenfolge der Spalten fehlt noch
- vollständig dynamisches Spaltenkontextmenü mit je Ansicht eigener Sichtbarkeit fehlt noch
- visuelle Neustart- und Splitterabnahme wartet auf eine entsperrte macOS-Sitzung

## Wichtige Entscheidungen

- technische IDs bleiben intern
- gespeicherter WorkflowStep ist die gemeinsame sichtbare Statusquelle
- bestehende Kategorien, IDs und Zuordnungen werden nicht migriert oder gelöscht
- UI-Layoutwerte werden lokal und getrennt nach Ansicht gespeichert
