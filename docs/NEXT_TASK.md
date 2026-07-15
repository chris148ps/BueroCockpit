# Nächste Aufgabe

## Ziel

Die verbindliche Arbeitskategorienlogik aus `docs/ARBEITSKATEGORIEN.md`
minimal-invasiv implementieren, ohne bestehende Produktivdaten automatisch zu
migrieren.

## Geplante Schritte

1. Aktuelle Nutzung von `WorkflowType`, `WorkflowStep`, `CategoryId` und
   `CategoryIds` in Persistenz, Navigation, Detail, Suche, Zählern, Import und
   Export vollständig erfassen.
2. Eine zentrale Ableitung für genau eine sichtbare Arbeitskategorie gemäß der
   verbindlichen Zuordnungstabelle einführen.
3. `SH-Netz`, `Retouren`, `Lager`, `Marktstammdatenregister` und
   `Warten auf Kunde` als getrennte Kennzeichnungen behandeln und die alte
   manuelle Arbeitskategorieauswahl ablösen.
4. Variante A umsetzen: neue und bewusst geänderte Vorgänge nach neuer Logik,
   unveränderte Altbestände ohne stillen Schreibvorgang oder Massenmigration.
5. Alle Pflichtfälle aus `docs/TESTRICHTLINIEN.md` ausschließlich mit
   isolierten Testdaten prüfen und die Release-Blockade erst nach nachgewiesener
   Übereinstimmung aufheben.

## Vermutlich betroffene Dateien

- `MainWindow.axaml`
- `MainWindow.axaml.cs`
- `Models/TaskItem.cs`
- `Data/BueroRepository.cs`
- Export-/Importmodelle und zugehörige Tests beziehungsweise Testharnesses

## Bereiche, die nicht verändert werden dürfen

- Keine automatische Migration oder stille Neu-Zuordnung produktiver Daten.
- Keine Produktivdaten, Anhänge, Cloud-Dateien oder Backups verändern.
- Kein Release, Tag, Versionswechsel, Merge nach `main` oder Ausbau der
  Netzwerk-/Sync-Funktionen.
