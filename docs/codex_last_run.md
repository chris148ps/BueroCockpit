# Letzter Codex-Lauf

## Datum/Uhrzeit

2026-07-14 20:31 +0200

## Letzter Auftrag

Angebotsvorgänge, Direktaufträge und Fachkategorien dauerhaft entkoppeln

## Zusammenfassung

Angebotsvorgänge bleiben unabhängig vom Bearbeitungsstand unter Angebote, Direktaufträge unter Aufträge. Fachkategorien sind davon getrennt, vollständig auswählbar und per Drag & Drop änderbar, ohne Typ oder Bearbeitungsstand zu verändern. Altbestände bleiben additiv kompatibel und produktive Daten wurden nicht für Tests verwendet.

## Geänderte Dateien

- `AGENTS.md`
- `MainWindow.axaml`
- `MainWindow.axaml.cs`
- `Services/AppSettingsService.cs`
- `docs/CODEX_PROJEKTREGELN.md`
- `docs/UEBERGABE_CODEX_APP_2026-07-04.md`
- `docs/PROJEKTSTATUS.md`
- `docs/codex_last_run.md`
- `docs/NEXT_TASK.md`
- neuer Eintrag unter `docs/codex_journal/`
- `scripts/publish-codex-work.sh`
- `scripts/run-macos-bundle.sh`

## Tests

- Ausgangs-`dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- Isolierter Repository-Integrationstest im temporären Datenordner: Angebotsfolge Angebot gesendet, Auftrag, Material, Termin und Erledigt sowie Direktauftragsfolge Material, Termin und Erledigt behielten jeweils Typ und Fachkategorie; Neustartpersistenz erfolgreich.
- Isolierter Altbestandstest mit einer Datenbank ohne `WorkflowType`/`WorkflowStep`: additive Spaltenmigration, Kategorienzuordnung und `PRAGMA integrity_check = ok` erfolgreich; der alte Datensatz wurde nicht still umsortiert.
- Isolierter Fallbacktest: Sendedatum wurde eindeutig als Angebotsvorgang/Angebot gesendet erkannt; uneindeutiger Altbestand stabil als Direktauftrag/Auftrag.
- Isolierter Avalonia-Zustandstest: Navigation `Übersicht, Alle Vorgänge, Angebote, Aufträge, Material, Termine`, typreine Filter, 13 zulässige Endkategorien, Neuanlage beider Typen, sämtliche Statusfolgen, lokale Suche und DnD-Zielmethode erfolgreich.
- DnD-Persistenztest: Fachkategorie geändert, `WorkflowType` und `WorkflowStep` unverändert gespeichert.
- `./scripts/run-macos-bundle.sh Debug`: Bundle gebaut, signiert und mit expliziten temporären Daten-/Konfigurationspfaden gestartet; Prozessstart und SQLite-Neustartpersistenz erfolgreich.
- Die macOS-Sitzung war gesperrt. Echte Mausbedienung des Drag & Drop und sichtbarer Übersichtsklick konnten deshalb nicht durchgeführt werden und werden nicht als real bedient ausgewiesen.
- `dotnet build -r win-x64`: erfolgreich, 0 Warnungen, 0 Fehler. Keine reale Bedienung unter Windows.
- `bash -n scripts/run-macos-bundle.sh scripts/publish-codex-work.sh`: erfolgreich.
- Suche in `AGENTS.md`, `docs` und `scripts`: keine festen Codex-Modellnamen und kein fester Modellparameter mehr.

## Git-Status

```text
 M AGENTS.md
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M Services/AppSettingsService.cs
 M docs/CODEX_PROJEKTREGELN.md
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
 M docs/UEBERGABE_CODEX_APP_2026-07-04.md
 M scripts/publish-codex-work.sh
 M scripts/run-macos-bundle.sh
?? docs/codex_journal/2026-07-14_20-31_vorgangstyp-kategorien-workflow.md
```

## Branch

Wird nach dem Dokumentationslauf durch den Git-Helfer ergänzt.

## Commit

Wird nach dem Dokumentationslauf durch den Git-Helfer ergänzt.

## Push erfolgreich

Nein – der reine Dokumentationslauf führt keinen Push aus.

## Offene Punkte

- Echter Maus-Drag und sichtbarer Übersichtsklick konnten wegen der gesperrten macOS-Sitzung nicht real bedient werden; der isolierte Zustands- und Persistenzpfad war erfolgreich.
- Windows-spezifische Bedienwege wurden gebaut, aber nicht real auf Windows getestet.

## Empfohlener nächster Schritt

Die neue Vorgangstyp-, Kategorien- und Drag-&-Drop-Bedienung auf einer entsperrten macOS-Sitzung mit einem isolierten Testprofil sichtbar abnehmen.

1. App mit `BUEROCOCKPIT_DATA_DIRECTORY` und `BUEROCOCKPIT_LOCAL_CONFIG_DIRECTORY` auf temporäre Ordner starten.
2. Angebots- und Direktauftragsfolgen sichtbar anklicken, Übersichtseinträge öffnen und Vorgänge per Maus zwischen zulässigen Fachkategorien ziehen.
3. Nach Neustart Typ, Bearbeitungsstand, Kategorie, Filter, Zähler und Suchergebnis sichtbar sowie per SQLite prüfen.
