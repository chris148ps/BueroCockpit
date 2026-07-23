# BüroCockpit – Agentenregeln

Diese Datei ist der verbindliche Einstieg für Codex und andere Agenten.

## Standard-Lesestapel vor jeder Aufgabe

Bei jedem normalen neuen oder fortgesetzten Codex-Auftrag zuerst nur diese
Dateien lesen:

1. `AGENTS.md`
2. `docs/PROJEKTREGISTER.md`
3. `docs/ENTWICKLUNGSSTAND.md`
4. `docs/PROJEKTSTATUS.md`
5. `docs/codex_last_run.md`
6. `docs/NEXT_TASK.md`
7. `docs/codex_auftraege/LETZTER_AUFTRAG.md`
8. `docs/codex_auftraege/AKTUELL.md`

Weitere Regel- und Fachdateien nur lesen, wenn das Thema oder `AGENTS.md` sie
konkret verlangt.


Informationen, die bereits in einer der acht Standarddateien enthalten sind,
dürfen nicht dauerhaft zusätzlich in anderen Regel-, Status-, Auftrags- oder
Journaldateien geführt werden. Stattdessen ist die bestehende maßgebliche
Quelle zu aktualisieren.

Zusätzlich je nach Thema lesen:

- Vorgangstyp, Workflowstatus, Kategorien, Navigation oder Statuszuordnungen: `docs/ARBEITSKATEGORIEN.md`
- UI/Design: `docs/DESIGN_RICHTLINIEN.md`
- iPad, iPhone, Fotos, Netzwerk oder Sync: `docs/LOCAL_NETWORK_SYNC.md`
- vollständige Funktionsprüfung oder Release: `docs/TESTRICHTLINIEN.md`
- Release, Version, Tag, GitHub Upload oder Auto-Update: `docs/RELEASE_PROZESS.md`
- Änderung von Projekt-, Codex-, Sicherheits- oder Arbeitsregeln:
  `docs/CODEX_PROJEKTREGELN.md`
- Auftragsprüfung oder Release:
  `docs/CODEX_AUFTRAGSPRUEFUNG.md`


Es dürfen nicht routinemäßig alle Dateien unter `docs/` durchsucht oder
eingelesen werden. Standardmäßig werden ausschließlich die Dateien des
Standard-Lesestapels gelesen. Weitere Dokumente dürfen nur geöffnet werden,
wenn sie durch diese Datei ausdrücklich gefordert werden oder für den
konkreten Auftrag fachlich erforderlich sind.


`docs/codex_auftraege/INDEX.md` nur lesen, wenn der Auftragszeiger unklar ist
oder ein historischer Bezug benötigt wird.

Dateien unter `docs/codex_auftraege/ARCHIV/` und `docs/codex_journal/` niemals
pauschal einlesen. Sie dürfen nur gezielt gelesen werden, wenn
`AKTUELL.md`, `INDEX.md` oder eine konkrete fachliche Prüfung darauf verweist.

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

- `docs/codex_auftraege/AKTUELL.md` aktualisieren
- `docs/codex_auftraege/LETZTER_AUFTRAG.md` aktualisieren
- `docs/codex_auftraege/INDEX.md` aktualisieren
- neuer Eintrag unter `docs/codex_journal/`
- `docs/codex_last_run.md` aktualisieren
- `docs/PROJEKTSTATUS.md` bei fachlichen Änderungen aktualisieren
- `docs/NEXT_TASK.md` auf genau eine nächste Aufgabe setzen
- `docs/ENTWICKLUNGSSTAND.md` auf den tatsächlich geprüften Gesamtstand aktualisieren

`docs/ENTWICKLUNGSSTAND.md` ist die zentrale fortlaufende Übersicht über den
tatsächlichen Entwicklungsstand. Sie muss unabhängig davon gepflegt werden, ob
Änderungen bereits committed oder nach GitHub gepusht wurden. Mindestens
festzuhalten sind aktuelle Version, Arbeitsbranch, letzte abgeschlossene
Aufgabe, laufendes Ziel, maßgebliche Architekturentscheidungen, bekannte
Risiken, Build- und Teststatus sowie die nächste geplante Version
beziehungsweise Aufgabe. Sie ergänzt die dauerhaften Entscheidungen aus
`docs/PROJEKTREGISTER.md`, ersetzt sie aber nicht.

Die Dokumentation muss dem tatsächlich geprüften Stand entsprechen. Keine erfundenen Tests oder Ergebnisse.
Einträge unter `docs/codex_journal/` sind historische Laufprotokolle und keine
aktuelle Regelquelle; bei Abweichungen gelten `AGENTS.md`,
`docs/PROJEKTREGISTER.md` und die gezielt einschlägigen aktuellen Fach- und
Regeldateien.

`docs/PROJEKTREGISTER.md` ist die maßgebliche Quelle für dauerhafte
Architekturentscheidungen. Dauerhafte Entscheidungen nicht zusätzlich als
zweite maßgebliche Regel in Status-, Auftrags- oder Journaldateien festlegen;
diese Dateien dürfen nur den erreichten Stand beziehungsweise den historischen
Arbeitsnachweis beschreiben.

## Codex-Auftragssystem

Aktuelle und historische Arbeitsaufträge liegen unter `docs/codex_auftraege/`.

Historische Aufträge und Journale sind keine aktuelle Regelquelle.

Zulässige Auftragsstatus:

- `GEPLANT`
- `OFFEN`
- `IN_ARBEIT`
- `ABGESCHLOSSEN`
- `ERSETZT`
- `ABGEBROCHEN`

Nach erfolgreichem Abschluss eines größeren Auftrags:

1. Status des aktuellen Auftrags auf `ABGESCHLOSSEN` setzen.
2. Auftrag mit BC-ID und kurzem Titel nach `docs/codex_auftraege/ARCHIV/` verschieben.
3. `INDEX.md` und `LETZTER_AUFTRAG.md` aktualisieren.
4. `AKTUELL.md` aus `VORLAGE.md` mit der nächsten freien BC-ID und Status `GEPLANT` vorbereiten.
5. Neuen Eintrag unter `docs/codex_journal/` erstellen.
6. `docs/codex_last_run.md` aktualisieren.
7. `docs/PROJEKTSTATUS.md` nur bei tatsächlichen fachlichen oder technischen Änderungen aktualisieren.
8. `docs/NEXT_TASK.md` auf genau eine nächste sinnvolle Aufgabe setzen.

Bei Teilabschluss bleibt der Auftrag in `AKTUELL.md`. Ein offener Auftrag darf niemals still überschrieben werden.
