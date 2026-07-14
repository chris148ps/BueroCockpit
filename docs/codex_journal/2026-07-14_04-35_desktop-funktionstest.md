# Codex-Journal: Vollständiger funktionaler Desktop-Test mit isoliertem Datenbestand

## Ziel

Die macOS-Desktop-App mit einer vollständigen Kopie des aktuellen Datenbestands real bedienen, die vorgegebenen Auftrags- und Kategorienfehler zuerst beheben, weitere reproduzierbare Fehler minimal korrigieren und Persistenz, Bundle-Start sowie Schutz der produktiven Datenbank prüfen.

## Umsetzung

- Daten- und lokale Konfigurationspfade können für sichere Testläufe über `BUEROCOCKPIT_DATA_DIRECTORY` und `BUEROCOCKPIT_LOCAL_CONFIG_DIRECTORY` isoliert werden.
- Neue Vorgänge werden in Aufträge als Direktauftrag und in Angebote als Angebotsworkflow angelegt.
- Kategorien lassen sich ohne vorausgesetzte Workflowkategorien bearbeiten, verschieben, verschachteln und löschen; beim Löschen bleibt der Auftrag samt weiterer Zuordnungen erhalten. Nur echte feste System-IDs sind gesperrt.
- Der obere Titel-Button wurde entfernt, der globale Speicherbutton heißt `Alles speichern`, und im Detail ist `Duplizieren` erreichbar.
- Papierkorb, Wiederherstellung, Archiv-Rückholung und Kategoriedarstellung wurden fachlich konsistent korrigiert.
- Die Kategorienverwaltung verwendet dieselbe hierarchische Reihenfolge wie Navigation und Detailauswahl.
- Materialpositionen erhielten ein schmales, bedienbares Detail-Layout. Nullable Materialwerte werden robust gespeichert, und Entfernen bleibt nicht mehr durch ein spätes Binding-Ereignis unbemerkt in der Datenbank bestehen.
- Speicherfehler werden zusätzlich mit der konkreten Ausnahme auf stderr protokolliert.
- Alle Testaufträge, Anhänge, Backups, Schreibtischobjekte und lokalen Einstellungen entstanden im temporären Testprofil.

## Geänderte Dateien

- `Data/AppPaths.cs`
- `Data/BueroRepository.cs`
- `MainWindow.axaml`
- `MainWindow.axaml.cs`
- `docs/PROJEKTSTATUS.md`
- `docs/codex_journal/`
- `docs/codex_last_run.md`
- `docs/NEXT_TASK.md`

## Tests

- Reale Bedienung auf macOS: Start, Navigation, Auswahl, Auf-/Zuklappen, Zähler, Fenstergrößen, Darstellung, Schreibtisch-Schalter, Direktauftrag, Angebotsworkflow, Bearbeiten, Speichern, Neustart, Duplizieren, Löschen, Papierkorb, Wiederherstellen, Archivieren/Rückholen, Kategorien, Suche, globale Suche, Sortierung, Spaltenreihenfolge/-breite/-sichtbarkeit, Termine, Material, Anhänge, Vorschau, externes Öffnen, Entfernen, Rückgängig, Schreibtischnotiz, Techniker, Backup/Restore, Diagnose und lokaler Sync-Testdienst.
- Materialfehler jeweils nach Build real erneut geprüft: Eingabe/Menge gespeichert, Neustartpersistenz, sichtbares und tatsächliches Entfernen aus SQLite.
- Lokaler Testdienst real auf Port 53941 gestartet; `/health`, `/pairing/status` und `/local-sync/status` lieferten HTTP 200; danach kein Listener mehr vorhanden.
- Backup im temporären Datenordner erstellt und real wiederhergestellt.
- `git diff --check`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r win-x64`: erfolgreich, 0 Warnungen, 0 Fehler.
- `./scripts/run-macos-bundle.sh Debug`: erfolgreich; signiertes Bundle real mit temporären Umgebungsvariablen gestartet.
- Zweiter Bedienrundgang und Neustartpersistenz: erfolgreich für Aufträge, Angebote, Termine, Kategorien, Tabellenlayout und Materiallöschung.
- Produktive SQLite-Datenbank: identischer SHA-256 vor/nach Test und `pragma integrity_check` = `ok`.
- Produktiver Hauptdatenordner: identisches Manifest aus 426 Dateien vor/nach Test.

## Ergebnis

Die bekannten Auftrags-, Kategorien- und Buttonfehler sowie zusätzlich gefundene Persistenz- und Layoutfehler bei Löschen, Archiv-Rückholung und Material sind korrigiert und real gegen den isolierten Datenbestand nachgetestet. Build, Windows-RID-Build, macOS-Bundle-Start und Neustartpersistenz sind erfolgreich. Die produktive Hauptdatenbank und ihr Datenordner blieben bytegenau unverändert.

## Bekannte offene Punkte

- Windows-spezifische Funktionen wurden auf macOS nur per Codepfad und erfolgreichem `win-x64`-Build, nicht real unter Windows bedient.
- Nativer Finder-Drop einer Datei auf den Schreibtisch, horizontales Trackpad-Scrollen und ein leerer Uhrzeitwert wurden nicht als eigener zweiter Realtest wiederholt; die angrenzenden Datei-, Tabellen- und Datumswege wurden real geprüft.
- Der ältere zentrale Pfad `BueroCockpit_Daten/Sync/live/settings.json` wurde in der frühen Testphase vor der vollständigen Umleitung einmal gespeichert. Er enthält wieder ausschließlich die ursprünglichen sieben Techniker; ohne Vorab-Hash ist dort jedoch kein bytegenauer Vergleich möglich. Hauptdatenbank und produktiver Hauptdatenordner sind nachweislich unverändert.

## Aktueller Git-Status

```text
 M Data/AppPaths.cs
 M Data/BueroRepository.cs
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
?? docs/codex_journal/2026-07-14_04-35_desktop-funktionstest.md
```
