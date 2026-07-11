# Nächste Aufgabe

## Ziel

Die neue Technikerprofilverwaltung mit einem nichtproduktiven Beispieldatensatz gezielt auf Speichern, Umbenennen und Löschen prüfen.

## Geplante Schritte

1. Isolierten, nichtproduktiven Settings-Datensatz vorbereiten.
2. Profil anlegen, bearbeiten, speichern und erneut laden.
3. Löschschutz des Standard-Profils und Löschen eines Nichtstandard-Profils prüfen.

## Vermutlich betroffene Dateien

- Services/LiveSettingsService.cs
- MainWindow.axaml
- MainWindow.axaml.cs
- docs/codex_journal/
- docs/codex_last_run.md
- docs/NEXT_TASK.md

## Bereiche, die nicht verändert werden dürfen

- Produktive Sync/live/settings.json, Aufgaben- und Kategorien-Daten, Netzwerkdienst, iPad-Code, Installer, Releases, Tags, Versionen und main.
