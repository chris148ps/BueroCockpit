# Codex-Journal: Fester codex/work-Branch mit automatischem Draft-Pull-Request

## Ziel

Nach jedem größeren Auftrag Dokumentation, Commit und Push dauerhaft auf codex/work bereitstellen und genau einen Draft-PR nach main erstellen oder aktualisieren.

## Umsetzung

Den bestehenden Git-Helfer auf den festen Branch codex/work umgestellt. Sicherheitsprüfungen für gh, Dokumentationsdateien, Branchname, vorgemerkte Indexänderungen und Pushstatus ergänzt. Nach dem Push wird ein vorhandener Draft-PR aktualisiert oder einmalig ein Draft-PR mit festem Titel und automatisch erzeugter Beschreibung erstellt. AGENTS.md dokumentiert den Ablauf und die Schutzregeln.

## Geänderte Dateien

- AGENTS.md
- scripts/publish-codex-work.sh
- scripts/update-codex-documentation.sh
- docs/codex_journal/2026-07-11_13-45_draft-pr-workflow.md
- docs/codex_last_run.md
- docs/NEXT_TASK.md

## Tests

- AGENTS.md vollständig gelesen.
- git status --short und Branch geprüft.
- gh-Version und Authentifizierung geprüft.
- bash -n für beide Skripte erfolgreich.
- Git-Helfer-Dry-Run erfolgreich; kein Branch, Commit, Push oder PR erzeugt.
- Offene Draft-PR-Suche für codex/work nach main erfolgreich; vor dem Lauf kein PR vorhanden.
- Echter Commit-, Push- und Draft-PR-Lauf erfolgreich: Branch codex/work, Arbeitscommit 5d2f96f, Metadatencommit ef38235, Draft-PR #1 erstellt und zu origin gepusht.

## Ergebnis

Der Workflow kann den festen Branch codex/work veröffentlichen und genau einen Draft-PR nach main verwalten, ohne main selbst zu verändern.

## Bekannte offene Punkte

- Der Helfer kann fachliche Änderungen nicht automatisch von anderen lokalen Änderungen unterscheiden; zu veröffentlichende Pfade müssen mit --include angegeben werden.
- PROJEKTSTATUS.md wird nur bei tatsächlichen fachlichen Änderungen aktualisiert.
- Ein bereits auf „Ready for review“ gesetzter PR wird aus Sicherheitsgründen nicht automatisch zurückgestuft.

## Aktueller Git-Status

```text
 M App.axaml
 M App.axaml.cs
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M Services/AppSettingsService.cs
 M docs/DESIGN_RICHTLINIEN.md
 M iPad/BueroCockpitSnapshotReader/BueroCockpitSnapshotReader/SnapshotRootView.swift
 M scripts/publish-codex-work.sh
 ?? macOS/
?? scripts/run-macos-bundle.sh
```
