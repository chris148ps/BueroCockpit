# Nächste Aufgabe

## Ziel

Konfigurierbare Spaltenlayouts für Aufträge, Angebote und Termine vollständig als lokale UI-Einstellung umsetzen.

## Geplante Schritte

1. Spaltendefinitionen mit Breite, Reihenfolge und Sichtbarkeit zentral modellieren.
2. Kopfzeilen-Splitter und Kontextmenüs an die drei Ansichten binden.
3. Layouts getrennt lokal speichern und nach Neustart wiederherstellen.

## Vermutlich betroffene Dateien

- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml`
- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml.cs`
- `/Users/christian/AppProjekte/BueroCockpit/Services/AppSettingsService.cs`

## Bereiche, die nicht verändert werden dürfen

- Produktivdaten, bestehende Kategorien und Auftragszuordnungen, Netzwerk-Sync, iPad-App, Installer, Releaseprozess, Versionen, Tags und `main`.
