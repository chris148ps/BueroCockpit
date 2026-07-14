# Nächste Aufgabe

## Ziel

Den Desktop-Funktionstest auf einem Windows-Testsystem mit derselben isolierten Datenkopie für die verbleibenden plattformspezifischen Bedienwege abschließen.

## Geplante Schritte

1. Isolierte Daten- und lokale Konfigurationspfade auf dem Windows-Testsystem setzen.
2. Explorer-Dateidrop, externes Öffnen, horizontales Scrollen und Windows-Fensterverhalten real prüfen.
3. Nur reproduzierbare Windows-spezifische Fehler minimal korrigieren und beide Plattform-Builds erneut ausführen.

## Vermutlich betroffene Dateien

- `MainWindow.axaml`
- `MainWindow.axaml.cs`
- `Data/AppPaths.cs`
- plattformspezifische Dienste unter `Services/`

## Bereiche, die nicht verändert werden dürfen

- Produktive Daten, zentrale Cloud-/Sync-Daten, Release, Tags, Versionen und bereits bestätigte macOS-Workflows.
