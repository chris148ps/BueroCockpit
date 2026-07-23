# BüroCockpit – Entwicklungsstand

Diese Datei ist die zentrale fortlaufende Übersicht über den tatsächlich
erreichten Entwicklungsstand. Dauerhafte Architekturentscheidungen stehen in
`docs/PROJEKTREGISTER.md`.

## Aktuelle Basis

- Letzte veröffentlichte Version: `0.4.22`
- Lokaler Entwicklungs- und Releasekandidatenstand: `0.4.23`
- Hauptentwicklungsrechner: Mac mini
- Arbeitsbranch: `codex/work`
- Basis aus GitHub `main`: `cd99edb`
- Gesicherter Entwicklungsstand-Commit: `082bab4`
- Der Arbeitsstand wurde am 2026-07-23 vollständig gesichert, auf den aktuellen
  Dokumentationsstand aus `main` aufgebaut und ohne Force-Push für
  `origin/codex/work` vorbereitet.

## Sicherung und Nachvollziehbarkeit

- Externe Sicherung:
  `/Users/christian/AppProjekte/BueroCockpit-Worktree-Backups/20260723-codex-work-consolidation`
- Inhalt: vollständiges Git-Bundle, Binär-Patch, Originalstatus, Liste und
  Archiv aller unversionierten Quelldateien sowie SHA-256-Prüfsummen.
- Lokaler Sicherungsbranch: `backup/codex-work-2026-07-23`
- Zusätzlich bleibt die frühere RC-Ausgangssicherung unter
  `/Users/christian/AppProjekte/BueroCockpit-RC-Backups/20260723-0.4.23-preparation`
  erhalten.
- Produktive Daten, Datenbanken, Anhänge, Backups sowie `bin`, `obj` und
  `publish` wurden nicht in Git aufgenommen.

## Implementierter Stand

- Desktop-Fachlogik, freie Kategorien, Workflowstatus, BC-0032-Korrektur,
  Datenpfade und UI liegen vollständig im gesicherten Stand.
- Produktive Daten bleiben unter Windows benutzerbezogen in
  `%LOCALAPPDATA%\BueroCockpit`; ein Wechsel zu
  `C:\ProgramData\BueroCockpit` ist nicht vorgesehen.
- Die Velopack-Installationswurzel `%LOCALAPPDATA%\BueroCockpitApp` bleibt vom
  produktiven Datenordner getrennt.
- Manueller Backup-Austausch, sichere Importprüfung und lokaler Rückfallstand
  sind implementiert.
- Der lokale Netzwerk-Sync bleibt bewusst gestartet und manuell durch
  `Jetzt synchronisieren`; gerätebezogene Checkpoints, Deltaübertragung,
  atomare Mobile-Inbox-Ablage und sichtbare Konflikte sind implementiert.
- Die iPad-App unterstützt den manuellen inkrementellen Austausch und behält
  lokale Originale.
- Der lokale Windows-x64-Releasekandidat `0.4.23` einschließlich
  Velopack-Setup liegt unter `publish/terminalserver-0.4.23-rc`. Diese
  ignorierten lokalen Artefakte sind kein Git-Inhalt und noch nicht
  veröffentlicht.

## Übernommene GitHub-Dokumentation

- `AGENTS.md`: Pflegepflicht für diese zentrale Übersicht in das neuere lokale
  Agenten- und Auftragssystem integriert.
- `docs/ENTWICKLUNGSSTAND.md`: aus `main` übernommen und auf den tatsächlich
  weiter fortgeschrittenen lokalen Stand aktualisiert.
- `docs/NEXT_TASK.md`: das ältere GitHub-Ziel der RC-Vorbereitung nicht blind
  übernommen; der bereits erreichte RC-Stand führt korrekt zu BC-0035.
- `docs/codex_last_run.md`: ältere BC-0032-Fassung aus GitHub berücksichtigt,
  aber durch den tatsächlich späteren Sicherungs- und Konsolidierungslauf
  fortgeschrieben.

## Aktueller Prüfstand

Am 2026-07-23 auf `codex/work` erfolgreich ausgeführt:

- `git diff --check`
- `dotnet build` – 0 Warnungen, 0 Fehler
- `dotnet build -r osx-arm64` – 0 Warnungen, 0 Fehler
- `dotnet build -r win-x64` – 0 Warnungen, 0 Fehler
- Workflow-, Kategorie- und Netzwerk-Integrationstests
- Backup-Austauschtests
- iPad-Simulator-Build per `xcodebuild`

Die automatisierten Prüfungen verwendeten isolierte temporäre Daten. Produktive
Daten wurden nicht geöffnet, migriert, gelöscht oder überschrieben.

## Bekannte Grenzen und Risiken

- Der Windows-x64-Build ersetzt keinen realen Terminalserver-Test.
- Die alte Inno-Installation wurde noch nicht durch das Velopack-Setup ersetzt.
- Datenbestand, Anhänge, Neustartpersistenz, Updatekomponenten und
  Auto-Update-Weg müssen auf dem Terminalserver sichtbar geprüft werden.
- Der lokale Releasekandidat ist nicht codesigniert.
- Noch kein Tag, kein Release und kein GitHub-Release für `0.4.23`.
- Die noch offenen physischen iPad-Fälle bleiben von den erfolgreichen
  Simulator- und isolierten Integrationstests getrennt.

## Nächste Aufgabe

Genau eine nächste Aufgabe ist geplant:

`BC-0035` – Terminalserver-Installation und lokalen Auto-Update-Weg abnehmen.

Der Ablauf steht in `docs/NEXT_TASK.md`,
`docs/codex_auftraege/AKTUELL.md` und
`docs/TERMINALSERVER_RELEASEKANDIDAT.md`.
