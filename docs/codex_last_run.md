# Letzter Codex-Lauf

## Datum/Uhrzeit

2026-07-14 04:35 +0200

## Letzter Auftrag

Vollständiger funktionaler Desktop-Test mit isoliertem Datenbestand

## Zusammenfassung

Die bekannten Auftrags-, Kategorien- und Buttonfehler sowie zusätzlich gefundene Persistenz- und Layoutfehler bei Löschen, Archiv-Rückholung und Material sind korrigiert und real gegen den isolierten Datenbestand nachgetestet. Build, Windows-RID-Build, macOS-Bundle-Start und Neustartpersistenz sind erfolgreich. Die produktive Hauptdatenbank und ihr Datenordner blieben bytegenau unverändert.

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

## Git-Status

```text
 M Data/AppPaths.cs
 M Data/BueroRepository.cs
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
?? docs/codex_journal/2026-07-14_04-35_desktop-funktionstest.md
```

## Branch
codex/work

## Commit
90bb2eedc313b54f1f92ac0c36fcf98408d0dc3b

## Push erfolgreich
Ja

## Offene Punkte

- Windows-spezifische Funktionen wurden auf macOS nur per Codepfad und erfolgreichem `win-x64`-Build, nicht real unter Windows bedient.
- Nativer Finder-Drop einer Datei auf den Schreibtisch, horizontales Trackpad-Scrollen und ein leerer Uhrzeitwert wurden nicht als eigener zweiter Realtest wiederholt; die angrenzenden Datei-, Tabellen- und Datumswege wurden real geprüft.
- Der ältere zentrale Pfad `BueroCockpit_Daten/Sync/live/settings.json` wurde in der frühen Testphase vor der vollständigen Umleitung einmal gespeichert. Er enthält wieder ausschließlich die ursprünglichen sieben Techniker; ohne Vorab-Hash ist dort jedoch kein bytegenauer Vergleich möglich. Hauptdatenbank und produktiver Hauptdatenordner sind nachweislich unverändert.

## Empfohlener nächster Schritt

Den Desktop-Funktionstest auf einem Windows-Testsystem mit derselben isolierten Datenkopie für die verbleibenden plattformspezifischen Bedienwege abschließen.

1. Isolierte Daten- und lokale Konfigurationspfade auf dem Windows-Testsystem setzen.
2. Explorer-Dateidrop, externes Öffnen, horizontales Scrollen und Windows-Fensterverhalten real prüfen.
3. Nur reproduzierbare Windows-spezifische Fehler minimal korrigieren und beide Plattform-Builds erneut ausführen.
