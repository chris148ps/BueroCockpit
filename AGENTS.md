# BueroCockpit - Agentenregeln

Die zentralen Projektregeln stehen in `docs/CODEX_PROJEKTREGELN.md`.

Start fuer Codex-Aufgaben:

```bash
cd "$HOME/AppProjekte/BueroCockpit"
codex -m gpt-5.5
```

Vor jeder Aufgabe immer zuerst lesen:

- `docs/CODEX_PROJEKTREGELN.md`
- bei UI- und Design-Aenderungen zusaetzlich `docs/DESIGN_RICHTLINIEN.md`
- bei iPad-, Sync-, Foto- oder Netzwerk-Themen zusaetzlich `docs/LOCAL_NETWORK_SYNC.md`

Arbeitsweise:

- Kleine Terminal-, Such- und Patch-Aufgaben moeglichst ohne Codex erledigen.
- Codex fuer groessere zusammenhaengende UI-, Datenmodell- oder Architekturarbeiten verwenden.
- Vor Aenderungen `git status` und `git pull origin main` pruefen.
- Nach Aenderungen `git diff --check` und `dotnet build` pruefen.
- Bei iPad-Code zusaetzlich `xcodebuild` pruefen.

Release-Regel:

- Kein Release ohne ausdrueckliche Freigabe.
- Wenn der Nutzer ausdruecklich `Release erstellen` sagt, bedeutet das immer der komplette Release-Ablauf: Version setzen, Release-Skript ausfuehren, Release-Commit, Tag, `git push origin main`, `git push origin v<version>`, GitHub Release erstellen, Artefakte hochladen und `gh release view` pruefen.
- Aktueller Release-Befehl: `./scripts/release.sh <version>`.

# Verbindliche Projektdokumentation

Diese Regeln gelten für jeden größeren Codex-Auftrag und müssen automatisch eingehalten werden.

Nach Abschluss eines größeren Auftrags ist die Projektdokumentation zu aktualisieren.

Pflicht:

1. `docs/codex_journal/`
   - Für jeden größeren abgeschlossenen Auftrag eine neue Journaldatei anlegen.
   - Dateiname: `YYYY-MM-DD_HH-MM_kurzer-name.md`

   Inhalt:
   - Ziel
   - Umsetzung
   - geänderte Dateien
   - Tests
   - Ergebnis
   - bekannte offene Punkte
   - aktueller Git-Status

2. `docs/codex_last_run.md`

   Immer überschreiben.

   Enthält:
   - Datum/Uhrzeit
   - letzter Auftrag
   - Zusammenfassung
   - geänderte Dateien
   - Tests
   - Git-Status
   - offene Punkte
   - empfohlener nächster Schritt

3. `docs/PROJEKTSTATUS.md`

   Nur aktualisieren, wenn sich der fachliche Projektstand geändert hat.

   Enthält:
   - aktueller Entwicklungsstand
   - Architektur
   - erledigte Hauptfunktionen
   - bekannte offene Punkte
   - wichtige Entscheidungen

4. `docs/NEXT_TASK.md`

   Immer genau eine nächste sinnvolle Aufgabe.

   Enthält:
   - Ziel
   - geplante Schritte
   - vermutlich betroffene Dateien
   - Bereiche, die nicht verändert werden dürfen

Grundregeln:

- Die Dokumentation muss dem tatsächlichen Projektstand entsprechen.
- Keine erfundenen Ergebnisse oder Tests dokumentieren.
- Keine Dokumentation überspringen.
- Erst wenn die Dokumentation aktualisiert wurde, gilt der Auftrag als abgeschlossen.

## Automatisierter Dokumentationslauf

Für einen abgeschlossenen größeren Auftrag wird die Vorlage
`docs/codex_run_template.md` kopiert und mit den tatsächlichen Ergebnissen
gefüllt. Danach aus dem Repository-Stamm ausführen:

```bash
./scripts/update-codex-documentation.sh \
  --input /tmp/codex-run.md \
  --name kurzer-name
```

Vor dem Schreiben kann der Lauf mit `--dry-run` geprüft werden. Wenn sich der
fachliche Projektstand geändert hat, wird zusätzlich eine vollständige,
geprüfte Statusdatei übergeben:

```bash
./scripts/update-codex-documentation.sh \
  --input /tmp/codex-run.md \
  --name kurzer-name \
  --project-status-file /tmp/PROJEKTSTATUS.md
```

Der Runner legt Journale kollisionsgeschützt neu an, überschreibt nur die
vorgeschriebenen Dateien `docs/codex_last_run.md` und `docs/NEXT_TASK.md`,
aktualisiert `docs/PROJEKTSTATUS.md` nur mit dem ausdrücklichen Parameter und
führt selbst keine Git-Schreibaktion aus. `docs/NEXT_TASK.md` wird aus genau
einem Zielblock erzeugt.

## Verbindlicher Arbeitsbranch- und Push-Ablauf

Nach erfolgreicher Dokumentationsaktualisierung darf der Arbeitsstand nur über
den separaten Git-Helfer veröffentlicht werden:

```bash
./scripts/publish-codex-work.sh \
  --name kurzer-name \
  --description "kurze Beschreibung" \
  --include AGENTS.md \
  --include docs/codex_journal \
  --include docs/codex_last_run.md \
  --include docs/NEXT_TASK.md \
  --include scripts/update-codex-documentation.sh \
  --include scripts/publish-codex-work.sh
```

Der Helfer erzeugt den Branch `codex/work-YYYY-MM-DD-kurzer-name`, commitet
nur die ausdrücklich mit `--include` angegebenen Pfade und pusht ausschließlich
diesen Branch. Vorhandene nicht ausgewählte Änderungen bleiben erhalten. Ein
bereits vorgemerkter Index wird aus Sicherheitsgründen abgelehnt.

`docs/codex_last_run.md` enthält nach dem Git-Lauf zusätzlich die Felder
`Branch`, `Commit` und `Push erfolgreich`. Der Helper dokumentiert den ersten
Arbeitscommit und legt danach einen separaten Metadatencommit an.

Der Helper darf niemals nach `main` pushen oder mergen und führt niemals Tags,
Releases oder Versionsänderungen aus. Mit `--dry-run` kann der Branch-, Commit-
und Push-Plan ohne Zustandsänderung geprüft werden.
