# Codex-Journal: Angebotsvorgänge, Direktaufträge und Fachkategorien dauerhaft entkoppeln

## Ziel

Angebotsvorgänge und Direktaufträge als dauerhafte organisatorische Typen führen, Bearbeitungsstand und Fachkategorie davon trennen, die Navigation und Kategorienauswahl vervollständigen sowie eine reine Fachkategorieverschiebung per Drag & Drop ergänzen.

## Umsetzung

- Die vorhandenen additiven Felder `WorkflowType` und `WorkflowStep` bleiben die getrennten Quellen für Vorgangstyp und Bearbeitungsstand; Statuswechsel ändern weder Typ noch Kategorie.
- Die Navigation enthält nun in dieser Reihenfolge `Alle Vorgänge`, `Angebote` und `Aufträge`. Angebote und Aufträge filtern ausschließlich nach dem dauerhaften Vorgangstyp.
- Die Übersicht öffnet einen Vorgang in der typgerechten organisatorischen Ansicht.
- Die pauschale Ausblendung vorhandener Workflow-benannter Kategorien wurde entfernt. Zulässige End- und Unterkategorien stehen vollständig zur Auswahl; Hauptkategorien mit Unterkategorien, Archiv, mobile Sperrbereiche und feste System-IDs bleiben ausgeschlossen.
- Die Vorgangslisten zeigen die fachliche Kategorie als eigene sortierbare Spalte.
- Vorgänge können auf zulässige Fachkategorien gezogen werden. Der Drop ändert nur `CategoryId` und `CategoryIds`; Typ und Bearbeitungsstand bleiben erhalten. Organisatorische Filterbereiche nehmen keinen Drop an.
- Lokale Suche, Zähler, Dashboarddaten, Detailauswahl und Tabellenprojektion werden nach Status- und Kategorieänderungen konsistent aktualisiert.
- Der stabile Altbestands-Fallback bleibt rückwärtskompatibel: eindeutige Angebotsmerkmale ergeben einen Angebotsvorgang, uneindeutige Altbestände einen Direktauftrag; ohne echte Bearbeitung erfolgt keine stille Datenumsortierung.
- Die Modellregeln nennen keine feste Codex-Version mehr. Der macOS-Bundle-Helfer reicht gesetzte Testpfade ausdrücklich an LaunchServices weiter, damit isolierte Tests nicht in produktive Pfade fallen.

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

## Ergebnis

Angebotsvorgänge bleiben unabhängig vom Bearbeitungsstand unter Angebote, Direktaufträge unter Aufträge. Fachkategorien sind davon getrennt, vollständig auswählbar und per Drag & Drop änderbar, ohne Typ oder Bearbeitungsstand zu verändern. Altbestände bleiben additiv kompatibel und produktive Daten wurden nicht für Tests verwendet.

## Bekannte offene Punkte

- Echter Maus-Drag und sichtbarer Übersichtsklick konnten wegen der gesperrten macOS-Sitzung nicht real bedient werden; der isolierte Zustands- und Persistenzpfad war erfolgreich.
- Windows-spezifische Bedienwege wurden gebaut, aber nicht real auf Windows getestet.

## Aktueller Git-Status

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
