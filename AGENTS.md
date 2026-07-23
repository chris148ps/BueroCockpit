# BüroCockpit – Agentenregeln

Diese Datei ist der verbindliche Einstieg für Codex und andere Agenten.

## Vor jeder Aufgabe lesen

Immer:

- `docs/CODEX_PROJEKTREGELN.md`
- `docs/CODEX_AUFTRAGSPRUEFUNG.md`
- `docs/ENTWICKLUNGSSTAND.md`

Zusätzlich je nach Thema:

- Vorgangstyp, Workflowstatus, Kategorien, Navigation oder Statuszuordnungen: `docs/ARBEITSKATEGORIEN.md`
- UI/Design: `docs/DESIGN_RICHTLINIEN.md`
- iPad, iPhone, Fotos, Netzwerk oder Sync: `docs/LOCAL_NETWORK_SYNC.md`
- vollständige Funktionsprüfung oder Release: `docs/TESTRICHTLINIEN.md`
- Release, Version, Tag, GitHub Upload oder Auto-Update: `docs/RELEASE_PROZESS.md`

Vor jedem Codex-Auftrag und zusätzlich vor jedem Release müssen alle relevanten Regel- und Fachdateien automatisch als erster Arbeitsschritt auf widersprüchliche Aussagen, Verbote und veraltete Vorgaben geprüft werden. Dabei ist außerdem zu prüfen, ob die Dokumentation der tatsächlichen App entspricht und ob Releaseprozess, Agentenregeln und Designrichtlinien noch mit der Implementierung übereinstimmen. Bei einem Widerspruch darf der Auftrag beziehungsweise Release nicht gestartet werden. Der Widerspruch ist dem Nutzer konkret mit Datei, Regel, notwendiger Änderung und Auswirkung zu nennen. Der Nutzer entscheidet, ob zuerst die Regeldateien oder die Implementierung angepasst werden. Eine betroffene Regeldatei darf erst nach ausdrücklicher Freigabe angepasst werden.

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
git fetch origin main codex/work
git pull --ff-only origin "$(git branch --show-current)"
```

Der Pull muss den aktuellen Branch nur per Fast-Forward aktualisieren. Auf `codex/work` darf dabei `main` nicht automatisch gemergt werden.

Nach Änderungen:

```bash
git diff --check
dotnet build
git status --short
```

Bei reinen Dokumentationsänderungen ohne Code-, Projekt-, Build- oder Skriptänderung ist kein Build erforderlich. Pflicht bleiben `git diff --check`, eine dokumentierte Konsistenzsuche und `git status --short`.

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
- Vor jedem Release ist die Konsistenzprüfung aus `docs/CODEX_AUFTRAGSPRUEFUNG.md` zwingend durchzuführen und zu dokumentieren. Jeder gefundene Widerspruch stoppt den Release vor Versionsänderung, Build, Tag oder Upload.
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
- `docs/ENTWICKLUNGSSTAND.md` auf den tatsächlich geprüften Gesamtstand aktualisieren

`docs/ENTWICKLUNGSSTAND.md` ist die zentrale fortlaufende Übersicht über den tatsächlichen Entwicklungsstand. Sie muss unabhängig davon gepflegt werden, ob Änderungen bereits committed oder nach GitHub gepusht wurden. Mindestens festzuhalten sind aktuelle Version, Arbeitsbranch, letzte abgeschlossene Aufgabe, laufendes Ziel, maßgebliche Architekturentscheidungen, bekannte Risiken, Build- und Teststatus sowie die nächste geplante Version beziehungsweise Aufgabe.

Die Dokumentation muss dem tatsächlich geprüften Stand entsprechen. Keine erfundenen Tests oder Ergebnisse.
Einträge unter `docs/codex_journal/` sind historische Laufprotokolle und keine aktuelle Regelquelle; bei Abweichungen gelten die aktuellen Regeldateien, `docs/ENTWICKLUNGSSTAND.md` und `docs/PROJEKTSTATUS.md`.