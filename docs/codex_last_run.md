# Letzter Codex-Lauf

## Datum/Uhrzeit

2026-07-14 22:31 +0200

## Letzter Auftrag

Workflowstatus, Kategorie-Badge sowie Sidebar- und Entf-Tastaturbedienung korrigieren und die vollständige Funktionsprüfung vorbereiten.

## Ergebnis

Die Statusauswahl bleibt beim Wechsel zwischen Angebotsvorgang und Direktauftrag sichtbar und persistent. Status, Vorgangstyp und fachliche Kategorie bleiben getrennt. Kategorielose Vorgänge erhalten keine erfundene Zuordnung. Die Kategorie-Spalte zeigt echte Kategoriepfade als Badge. Sidebar-Tastaturnavigation und Entf-Papierkorbpfad sind ergänzt.

## Geänderte Dateien

- `Data/BueroRepository.cs`
- `MainWindow.axaml`
- `MainWindow.axaml.cs`
- `Models/TableCellItem.cs`
- `docs/PROJEKTSTATUS.md`
- `docs/codex_last_run.md`
- `docs/NEXT_TASK.md`
- `docs/codex_journal/2026-07-14_22-31_workflowstatus-kategorien-bedienung.md`

## Prüfungen

- `git diff --check`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r win-x64`: erfolgreich, 0 Warnungen, 0 Fehler; nicht real unter Windows bedient.
- Isolierte gezielte Avalonia- und Persistenztests: Status-ComboBox, unbekannter Altstatus, Kategorieauswahl/-Badge, Kategorie-DnD und -Reihenfolge, Übersichtsnavigation, Sidebar-Tastatur, Entf-Schutz, Suche, Sortierung, Darstellung und Techniker erfolgreich.
- Isolierter Repository-Gesamtlauf: Vorgangsdetails, Kategorienverwaltung, Material, Testanhang, Schreibtischnotiz, Backup, Papierkorb, Wiederherstellung und Leeren erfolgreich.
- SQLite-Integrität nach Neustart und Gesamtläufen: `ok`.
- Lokaler Testdienst manuell gestartet und gestoppt; nur harmlose Teststatusdaten, Port `53941` danach geschlossen.
- Echte App mit temporären Pfaden gestartet. Die gesperrte macOS-Sitzung verhinderte den verpflichtenden erneuten sichtbaren Gesamtrundgang.
- Das lokal fremd geänderte `scripts/run-macos-bundle.sh` wurde nicht ausgeführt oder verändert, da seine aktuelle Fassung isolierte Startpfade nicht weitergibt.
- Keine im Testzeitraum geänderten Dateien in den read-only geprüften produktiven App-/OneDrive-Pfaden gefunden.

## Während des Gesamttests zusätzlich gefundener und behobener Fehler

Die Repository-Normalisierung ordnete einem tatsächlich kategorielosen Vorgang still die erste verfügbare Kategorie zu. Die automatische Ersatzzuordnung wurde entfernt und isoliert mit Neustartpersistenz geprüft.

## Offene Punkte

- Vollständige sichtbare Realbedienung auf entsperrtem macOS einschließlich Maus-Drag, Kontextmenüs, Dialogfokus und Entf-Bestätigung.
- Reale Windows-Bedienung.
- Der fremde lokale Diff in `scripts/run-macos-bundle.sh` bleibt unangetastet und wird nicht veröffentlicht.

## Branch
codex/work

## Commit
f03761211cc6e343ce0f1dce03a1f2a2c92787a4

## Push erfolgreich
Ja

## Empfohlener nächster Schritt

Die korrigierten Wege und den vollständigen Desktop-Rundgang auf einer entsperrten macOS-Sitzung mit ausschließlich temporären Daten- und Konfigurationspfaden sichtbar abnehmen.
