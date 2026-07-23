# Codex-Journal: iPad-Revisionsmodell und Desktop-Konfliktprüfung

## Auftrag

Die in `docs/NEXT_TASK.md` festgelegten Schritte für Offline-Bearbeitung
bestehender Desktopvorgänge, versionierten Rücktransport und sichtbare
Feldkonfliktentscheidung fortsetzen.

## Vorprüfung

Gelesen und gegeneinander geprüft wurden `AGENTS.md`,
`docs/CODEX_PROJEKTREGELN.md`, `docs/CODEX_AUFTRAGSPRUEFUNG.md`,
`docs/ARBEITSKATEGORIEN.md`, `docs/DESIGN_RICHTLINIEN.md`,
`docs/LOCAL_NETWORK_SYNC.md`, `docs/TESTRICHTLINIEN.md`,
`docs/IPAD_FUNKTIONSMATRIX.md` und `docs/NEXT_TASK.md`.

Es bestand kein ungeklärter Widerspruch. Der Auftrag bleibt beim manuell
gestarteten Dienst, beim bewusst ausgelösten iPad-Sync und beim sichtbaren
Desktop-Inbox-Import. Es wurde kein direkter Netzwerk-Upsert aktiviert.

## Umsetzung

- Snapshot-Leser übernimmt aktuelle Kategorie-ID, Vorgangstyp und
  Workflowstatus als bearbeitbaren Basisstand.
- Das iPad-Formular bearbeitet Notiz, stabile Kategorie-ID,
  Vorgangstyp/Status, Termin, Wiedervorlage, Wiedervorlagegrund und Monteur.
- `local-sync-inbox-v2` enthält Paket-ID, Desktopvorgangs-ID, Basisrevision,
  bestätigte Snapshotrevision und Basiswerte. `v1` bleibt kompatibel.
- Nach bestätigtem oder idempotent übersprungenem Upload wird nur der lokale
  JSON-Transportstatus atomar angepasst; Originaldateien bleiben erhalten.
- Der Desktop prüft Paket und Anhänge vor der fachlichen Übernahme und zeigt
  Basis-, Desktop- und iPad-Wert je geändertem Feld.
- Bei parallelen Änderungen sind Konfliktfelder standardmäßig abgewählt.
  Statuszuordnung und stabile Kategorie-ID werden erneut gegen den aktuellen
  Desktopstand validiert.
- Neue mobile Vorgänge verwenden dasselbe `v2`-Modell und übernehmen die
  zusätzlichen Termin-, Wiedervorlage- und Monteurfelder erst nach bewusster
  Desktopaktion.

## Prüfungen

- `dotnet run --project tests/BueroCockpit.WorkflowTests/BueroCockpit.WorkflowTests.csproj`: erfolgreich.
- Automatisiert geprüft: reine iPad-Änderung, paralleler Feldkonflikt,
  unveränderte Kategorie-ID, identische/abweichende Revision, `v2`-Annahme und
  Loader-Basiswerte, bestehende Upload-/Idempotenz-/Konflikt-/Recovery-Fälle.
- `dotnet build`: erfolgreich.
- `dotnet build -r win-x64`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet restore -r osx-arm64` und
  `dotnet build -r osx-arm64 --no-restore`: erfolgreich, 0 Warnungen,
  0 Fehler.
- iOS-Simulator-`xcodebuild` mit DerivedData unter `/private/tmp`: erfolgreich.
- Ein physischer iPad-/Windows-Bedienlauf wurde nicht durchgeführt und bleibt
  die genau eine nächste Aufgabe.

## Grenzen

- Keine Produktivdaten, Cloud-Dateien oder produktiven Datenbanken verändert.
- Kein Hintergrundsync, kein automatischer Dienststart und keine Lösch- oder
  Archiv-Synchronisation.
- Kein Commit, Push, Merge, Pull Request, Release, Tag oder Versionswechsel.
