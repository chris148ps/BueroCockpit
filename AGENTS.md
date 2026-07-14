# BüroCockpit – Agentenregeln

Diese Datei ist der verbindliche Einstieg für Codex und andere Agenten.

## Vor jeder Aufgabe lesen

Immer:

- `docs/CODEX_PROJEKTREGELN.md`

Zusätzlich je nach Thema:

- UI/Design: `docs/DESIGN_RICHTLINIEN.md`
- iPad, iPhone, Fotos, Netzwerk oder Sync: `docs/LOCAL_NETWORK_SYNC.md`
- vollständige Funktionsprüfung oder Release: `docs/TESTRICHTLINIEN.md`
- Release, Version, Tag, GitHub Upload oder Auto-Update: `docs/RELEASE_PROZESS.md`

## Werkzeug- und Modellwahl

- Kleine Terminal-, Git-, Such-, Build-, Release- und Patchaufgaben direkt im Terminal erledigen.
- Codex nur für größere zusammenhängende Code-, UI-, Datenmodell-, Netzwerk- oder Architekturarbeiten verwenden.
- Das Codex-Modell abhängig vom Aufgabentyp wählen.
- Für kleine und mittlere Aufgaben ein effizientes Modell verwenden.
- Für komplexe Architektur-, Refactoring- oder schwierige Fehlersuchaufgaben ein leistungsfähigeres Modell verwenden.
- Eine deutliche Abweichung vom üblichen Modellstandard kurz begründen.

## Grundprüfung

Vor Änderungen:

```bash
cd "$HOME/AppProjekte/BueroCockpit"
git status --short
git pull origin main
```

Nach Änderungen:

```bash
git diff --check
dotnet build
git status --short
```

Bei iPad-Code zusätzlich `xcodebuild`; bei Windows-Code mindestens `dotnet build -r win-x64`.

## Sicherheit

- Keine produktiven Daten, Kategorien, IDs, Zuordnungen oder Anhänge löschen oder still verändern.
- Keine riskante Datenmigration ohne ausdrückliche Freigabe.
- Keine Echtdaten, Datenbanken, Anhänge, Backups oder `publish`-Ordner committen.
- Keine Cloud-, iCloud- oder OneDrive-Dateien verändern, außer der Nutzer beauftragt dies ausdrücklich.
- Keine alten Cloud-, Live-Datei- oder Pairing-Code-Wege wieder aktivieren.
- Kein Netzwerkdienst, Polling, FileSystemWatcher, Portscan oder UDP-Broadcast ohne ausdrückliche Freigabe.

## Testpflicht

- Relevante Funktionen real bedienen, nicht nur Code lesen.
- Für vollständige Tests isolierte Testdaten verwenden.
- Reproduzierbare, sicher begrenzte Fehler sofort beheben und erneut testen.
- Details stehen in `docs/TESTRICHTLINIEN.md`.

## Release-Regel

- Kein Release, Tag oder Versionswechsel ohne ausdrückliche Freigabe.
- „Release erstellen“ bedeutet immer den vollständigen GitHub-Release für die Windows-Auto-Update-Funktion.
- Pflicht sind frische Velopack-Artefakte, Tag, Push, GitHub Release, Upload und `gh release view`.
- Der optionale Inno-Installer ist nicht Voraussetzung für das Velopack-Auto-Update.
- Release-Arbeiten direkt im Terminal ausführen.
- Der vollständige Ablauf steht ausschließlich in `docs/RELEASE_PROZESS.md`.

## Arbeitsbranch für Codex

- Größere Codex-Arbeiten erfolgen auf `codex/work`.
- Veröffentlichung über `scripts/publish-codex-work.sh`.
- Der Helfer darf niemals nach `main` pushen, mergen, taggen oder Releases erstellen.
- Der offene Draft-PR `codex/work` → `main` bleibt der nachvollziehbare Arbeitsstand.

## Projektdokumentation nach größeren Aufträgen

Pflicht:

- neuer Eintrag unter `docs/codex_journal/`
- `docs/codex_last_run.md` aktualisieren
- `docs/PROJEKTSTATUS.md` bei fachlichen Änderungen aktualisieren
- `docs/NEXT_TASK.md` auf genau eine nächste Aufgabe setzen

Die Dokumentation muss dem tatsächlich geprüften Stand entsprechen. Keine erfundenen Tests oder Ergebnisse.
