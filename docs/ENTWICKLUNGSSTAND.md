# BüroCockpit – Entwicklungsstand

Diese Datei ist die zentrale, fortlaufend zu pflegende Übersicht über den tatsächlichen Entwicklungsstand. Sie ergänzt `docs/PROJEKTSTATUS.md`, `docs/codex_last_run.md` und `docs/NEXT_TASK.md`.

## Aktuelle Basis

- Letzte veröffentlichte Version: `0.4.22`
- Hauptentwicklungsrechner: Mac mini
- Üblicher Arbeitsbranch für größere Arbeiten: `codex/work`
- GitHub dient ausschließlich für Quellcode, Dokumentation, Releases und Update-Artefakte.
- Lokale, noch nicht veröffentlichte Änderungen können dem Stand auf GitHub voraus sein. Vor jeder Arbeit sind Branch, Commit und Arbeitsbaum zu prüfen.

## Produktivarchitektur

### Windows-Terminalserver

- BüroCockpit soll dauerhaft auf dem Firmen-Terminalserver laufen.
- Nur ein RDP-Benutzer verwendet die Anwendung.
- Interner Zugriff erfolgt direkt per RDP.
- Externer Zugriff erfolgt per VPN und anschließend RDP.
- Der produktive Windows-Datenordner darf deshalb benutzerbezogen unter `%LOCALAPPDATA%\BueroCockpit` liegen.
- Die produktive Datenbank darf nicht im Installationsordner und nicht direkt in OneDrive liegen.

### iPad

- Das iPad erhält seinen Datenstand per Direktübertragung im selben lokalen Netzwerk.
- Der Terminalserver ist künftig die führende produktive Desktop-Instanz.
- Alte OneDrive-, Cloud- oder Live-Datei-Wege dürfen nicht wieder aktiviert werden.

## Datenhaltung und Datenaustausch

- Keine gemeinsam geöffnete SQLite-Datenbank in OneDrive.
- Jedes Entwicklungsgerät arbeitet mit einem lokalen Datenordner.
- Datenaustausch erfolgt kontrolliert über manuelle Backup-/Import-Archive.
- OneDrive darf nur als zentraler Austauschordner für geschlossene Backup-ZIP-Dateien verwendet werden.
- Ein Import ersetzt den vollständigen lokalen Datenstand; parallele Änderungen werden nicht automatisch zusammengeführt.
- Vor jedem Import ist ein automatisches lokales Rückfall-Backup Pflicht.

## Letzte abgeschlossene fachliche Korrektur

### BC-0032 – Erledigte Aufträge blieben unsichtbar

- Ursache war `IsArchivedForSearch`.
- Der Workflowstatus `Erledigt` wurde fälschlich wie das technische Archiv behandelt.
- Status, Abschlusszeit und Zielkategorie waren bereits korrekt gespeichert; der Auftrag wurde anschließend nur ausgeblendet.
- Nur Status oder Kategorie `Archiv` gelten nun als archiviert.
- Regressionstest für Status, Kategorieverschiebung, Sichtbarkeit, Archivabgrenzung und Neustartpersistenz wurde ergänzt.
- macOS-, Windows-Runtime- und allgemeine Builds waren erfolgreich.

## Aktuell laufendes Ziel

Eine aktuelle Windows-x64-Installer-Version für den Terminalserver vorbereiten, damit:

- die alte Installation ersetzt beziehungsweise aktualisiert werden kann,
- die aktuelle Programmlogik aus dem vollständigen Entwicklungsstand enthalten ist,
- spätere Updates über Velopack/Auto-Update möglich werden,
- bestehende produktive Daten unangetastet bleiben.

## Release- und Installer-Vorgaben

- Keine Versionsänderung, kein Tag und kein Release ohne ausdrückliche Freigabe.
- Die nächste sinnvolle Patch-Version nach `0.4.22` ist `0.4.23`.
- Release-Arbeiten müssen die verbindlichen Prüfungen aus `docs/RELEASE_PROZESS.md` und `docs/CODEX_AUFTRAGSPRUEFUNG.md` erfüllen.
- Ein lokaler Releasekandidat darf nicht still auf einem unsauberen Arbeitsbaum veröffentlicht werden.
- Vor Veröffentlichung muss der vollständige lokale Entwicklungsstand gesichert, geprüft und nachvollziehbar in einen sauberen Release-Stand überführt werden.
- Der Installer und spätere Updates dürfen `%LOCALAPPDATA%\BueroCockpit` niemals löschen oder überschreiben.

## Bekannte Risiken

- Der lokale Stand auf dem Mac mini kann umfangreiche uncommittete Änderungen enthalten, die auf GitHub noch nicht sichtbar sind.
- Ein neuer Chat oder ein anderer Rechner sieht nur veröffentlichte GitHub-Inhalte und die Projektdokumentation, nicht automatisch den lokalen Arbeitsbaum.
- Vor Release oder Branchwechsel dürfen keine bestehenden Änderungen verworfen, zurückgesetzt oder überschrieben werden.
- Die alte Terminalserver-Installation besitzt noch keine verlässlich nutzbare Auto-Update-Basis; der erste aktuelle Installer muss die Velopack-fähige Installation herstellen.

## Verbindliche Prüfung vor jeder neuen Aufgabe

1. `AGENTS.md` lesen.
2. Diese Datei vollständig lesen.
3. `docs/PROJEKTSTATUS.md` lesen.
4. `docs/codex_last_run.md` lesen.
5. `docs/NEXT_TASK.md` lesen.
6. `git status --short --branch` prüfen.
7. Branch, Commit, Versionsstand und Abweichung zu GitHub prüfen.
8. Widersprüche zwischen Auftrag, Regeldateien und tatsächlichem Projektstand vor jeder Änderung melden.

## Pflegepflicht

Nach jedem größeren Codex-Auftrag muss diese Datei aktualisiert werden. Sie muss mindestens enthalten:

- aktuelle Version,
- aktueller Arbeitsbranch,
- letzte abgeschlossene Aufgabe,
- laufendes Ziel,
- maßgebliche Architekturentscheidungen,
- bekannte Risiken,
- Build- und Teststatus,
- nächste geplante Version beziehungsweise nächste sinnvolle Aufgabe.

Die Angaben müssen dem tatsächlich geprüften Stand entsprechen. Keine Vermutungen als Tatsachen dokumentieren.
