# Nächste Aufgabe

## Ziel

Isolierten, nicht-produktiven UI-Testdatenbestand für mutierende Status- und Techniker-Speichertests bereitstellen.

## Geplante Schritte

1. Einen temporären lokalen Datenordner ausschließlich für den Abnahmetest verwenden.
2. Direkten Auftrag und Angebotsvorgang anlegen oder vorhandene Testdaten verwenden.
3. Status und Techniker ändern, speichern, neu starten und die identische Listenanzeige prüfen.

## Vermutlich betroffene Dateien

- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml.cs`
- `/Users/christian/AppProjekte/BueroCockpit/Services/AppSettingsService.cs`
- `/Users/christian/AppProjekte/BueroCockpit/Data/AppPaths.cs`

## Bereiche, die nicht verändert werden dürfen

- Produktivdaten, Kategorien, IDs, Auftragszuordnungen, Netzwerk-Sync, iPad-App, Installer, Releaseprozess, Versionen, Tags und `main`.
