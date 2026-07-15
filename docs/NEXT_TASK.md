# Nächste Aufgabe

## Ziel

Benutzerdefinierte Kategorien mit konfigurierbarer automatischer
Statuszuordnung implementieren, ohne bestehende Produktivdaten automatisch zu
migrieren.

## Geplante Schritte

1. Aktuelle Nutzung von `WorkflowType`, `WorkflowStep`, `CategoryId` und
   `CategoryIds` in Persistenz, Navigation, Detail, Suche, Zählern, Import und
   Export vollständig erfassen.
2. Beim Erstellen eines neuen Vorgangs verbindlich zwischen Angebotsvorgang und
   Direktauftrag wählen lassen und passende Anfangsstatus setzen.
3. Eine bestätigungspflichtige nachträgliche Änderung des Vorgangstyps ergänzen.
4. In den Einstellungen für jede zulässige Kombination aus Vorgangstyp und
   Workflowstatus genau eine Zielkategorie über ihre stabile Kategorie-ID
   konfigurierbar machen.
5. Beim Statuswechsel automatisch in die konfigurierte Zielkategorie
   verschieben; ohne Zuordnung keine beliebige Ersatzkategorie wählen.
6. Normale Kategorien weiterhin frei anlegen, umbenennen, verschieben,
   verschachteln und löschen lassen. Beim Löschen einer verwendeten Zielkategorie
   Ersatzzuordnung, bewusstes Entfernen oder Abbruch verlangen.
7. Für neue und bewusst geänderte Vorgänge genau eine Kategorie speichern;
   unveränderte Altbestände gemäß Variante A ohne stillen Schreibvorgang oder
   Massenmigration tolerant lesen.
8. Navigation, Zähler, Suche, Übersicht, Detailansicht, Import und Export auf
   dieselbe aktuelle Kategorie-ID ausrichten.
9. Alle Pflichtfälle aus `docs/TESTRICHTLINIEN.md` ausschließlich mit isolierten
   Testdaten prüfen und die Release-Blockade erst nach nachgewiesener
   Übereinstimmung aufheben.

## Vermutlich betroffene Dateien

- `MainWindow.axaml`
- `MainWindow.axaml.cs`
- `Models/TaskItem.cs`
- `Data/BueroRepository.cs`
- `Services/AppSettingsService.cs` oder eine passende zentrale
  Zuordnungs-Persistenz
- Export-/Importmodelle und zugehörige Tests beziehungsweise Testharnesses
- `docs/DESIGN_RICHTLINIEN.md` zur Beseitigung der noch starren
  Arbeitskategorien-Darstellung

## Bereiche, die nicht verändert werden dürfen

- Keine automatische Migration oder stille Neu-Zuordnung produktiver Daten.
- Keine Produktivdaten, Anhänge, Cloud-Dateien oder Backups verändern.
- Kein Release, Tag, Versionswechsel, Merge nach `main` oder Ausbau der
  Netzwerk-/Sync-Funktionen.
