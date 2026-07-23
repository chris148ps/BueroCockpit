# Codex-Auftrag BC-0034

## Status

ABGESCHLOSSEN

## Datum

2026-07-23

## Titel

Lokalen Windows-Releasekandidaten 0.4.23 für den Terminalserver vorbereiten

## Ziel

Den vollständigen uncommitteten Stand von `codex/work` verlustfrei sichern,
auf einem lokalen Releasekandidaten-Branch Version `0.4.23` vorbereiten und
frische Windows-x64-, Velopack- und Installer-Artefakte für die manuelle
Terminalserver-Abnahme erzeugen.

## Ergebnis

- Ausgangsstand extern vollständig gesichert und verifiziert.
- Branch `codex/release-0.4.23-rc` angelegt.
- Version `0.4.23` konsistent gesetzt.
- Datenpfad und Velopack-Installationswurzel sicher getrennt.
- Windows-Publish, Velopack-Setup, Full-Paket, Portable-Pakete und Manifeste
  frisch erzeugt.
- Lokaler Update-Testkanal und Terminalserver-Testablauf vorbereitet.
- Kein Commit, Push, Tag oder GitHub-Release.

## Tests

- `dotnet build`: erfolgreich.
- `dotnet build -r win-x64`: erfolgreich.
- Workflow-/Kategorie-/Netzwerk-Integrationstests: erfolgreich.
- Backup-Austauschtests: erfolgreich.
- iPad-Simulator-Build: erfolgreich.
- Artefakt-, Manifest-, PE-, Inhalt- und SHA-256-Prüfung: erfolgreich.
- Realer Windows-Test: noch offen in BC-0035.

## Beziehungen

- Ersetzt: `BC-0033`
- Nachfolger: `BC-0035`
- Zugehörige Journaldatei:
  `docs/codex_journal/2026-07-23_20-02_windows-releasekandidat-0.4.23.md`
