# BüroCockpit – verbindliche Testrichtlinien

Diese Datei ist vor größeren Funktionsänderungen, vollständigen App-Tests und Releases zu lesen.

## Grundsatz

- Nicht nur Code lesen oder Screenshots vergleichen.
- Funktionen real bedienen, Eingaben speichern, App neu starten und Persistenz prüfen.
- Gefundene reproduzierbare und sicher begrenzte Fehler sofort beheben und anschließend erneut testen.
- Produktive Daten niemals für Funktionstests verändern.

## Testdaten

- Für vollständige Funktionstests immer einen isolierten temporären Datenordner verwenden.
- Eine Kopie des aktuellen Datenbestands darf ausschließlich zu Testzwecken verwendet werden.
- Testaufträge, Testkategorien, Anhänge, Backups und Änderungen nur dort erzeugen.
- Vor und nach dem Test prüfen, dass produktive Daten unverändert geblieben sind.

## Pflichtprüfungen nach Änderungen

```bash
cd "$HOME/AppProjekte/BueroCockpit"
git diff --check
dotnet build
```

Bei einem reinen Dokumentationsauftrag ohne Code-, Projekt-, Build- oder
Skriptänderung ist kein Build erforderlich. Pflicht bleiben:

```bash
cd "$HOME/AppProjekte/BueroCockpit"
git diff --check
rg -n '<betroffene Fachbegriffe und Altregeln>' AGENTS.md docs
git status --short
```

Zusätzlich je nach Bereich:

- Windows/Desktop: `dotnet build -r win-x64`
- macOS: `./scripts/run-macos-bundle.sh Debug` und realer Start
- iPad/iPhone: passendes `xcodebuild`
- Netzwerk/Sync: Endpunkte, Portzustand und Stop-Verhalten real prüfen
- Release: `docs/RELEASE_PROZESS.md` vollständig befolgen

## Konsistenzprüfung vor jedem Release

Vor jedem Release muss automatisch als erster Schritt zusätzlich die Prüfung aus
`docs/CODEX_AUFTRAGSPRUEFUNG.md` vollständig durchgeführt werden. Dabei werden
Regeldateien, Projektstatus, tatsächliche App, Releaseprozess und
Designrichtlinien gegeneinander geprüft.

Jeder gefundene Widerspruch stoppt den Release. Der Nutzer entscheidet, ob
zuerst die Regeldateien oder die Implementierung angepasst werden. Erst nach
erneuter erfolgreicher Prüfung dürfen Releasearbeiten beginnen.

## Pflichtfälle für Arbeitskategorien

Nach der Implementierung von `docs/ARBEITSKATEGORIEN.md` müssen isoliert
mindestens folgende Fälle geprüft werden:

- jeder Vorgang besitzt genau einen Typ und genau einen Workflowstatus,
- jede Kombination erscheint in genau einer Arbeitskategorie gemäß Tabelle,
- Angebotsvorgänge wechseln korrekt zwischen `Angebote`,
  `Angebote gesendet`, `Angebotsaufträge`, `Material`, `Termin` und `Erledigt`,
- Direktaufträge wechseln korrekt zwischen `Aufträge`, `Material`, `Termin`
  und `Erledigt`,
- `SH-Netz`, `Retouren`, `Lager`, `Marktstammdatenregister` und
  `Warten auf Kunde` erscheinen ausschließlich als getrennte
  Kennzeichnungen,
- Navigation, Zähler, Suche, Übersicht, Detail und Neustartpersistenz zeigen
  keine doppelte Arbeitskategorie,
- neue und bewusst geänderte Vorgänge verwenden die neue Logik,
- unveränderte Legacy-Datensätze werden nach Variante A weder migriert noch
  still zurückgeschrieben.

## Vollständiger Desktop-Funktionstest

Mindestens prüfen:

1. Start, Navigation, Auswahlmarkierung, Zähler und Fenstergrößen
2. Aufträge und Angebote erstellen, bearbeiten, speichern, löschen, wiederherstellen und archivieren
3. Arbeitskategorien automatisch ableiten sowie Kennzeichnungen getrennt anlegen, bearbeiten und anzeigen
4. Suche, Sortierung, Spaltenbreiten, Spaltenreihenfolge und Sichtbarkeit
5. Detailfelder, Termine, Wiedervorlagen, Techniker, Material und Anhänge
6. Schreibtischfunktionen und Neustartpersistenz
7. Einstellungen, Backup, Diagnose und lokale Testdienste
8. Konsolenausgaben, Exceptions, Binding-Fehler und widersprüchliche UI-Zustände

## Fehlerbehandlung

Bei jedem Fund:

1. Ursache bestimmen.
2. Minimal korrigieren.
3. Build ausführen.
4. Betroffene Funktion erneut real testen.
5. Angrenzende Funktionen auf Regression prüfen.

Ein Auftrag gilt nicht als abgeschlossen, wenn bekannte Fehler nur dokumentiert, aber nicht behoben wurden.

## Plattformgrenzen

- Nicht real getestete Windows-Funktionen müssen ausdrücklich als nicht real getestet dokumentiert werden.
- Ein erfolgreicher `win-x64`-Build ersetzt keinen vollständigen Bedienungstest unter Windows.
- Gleiches gilt für macOS- und iPad-spezifische Bedienwege.

## Dokumentation

Nach größeren Tests gemäß `AGENTS.md` aktualisieren:

- `docs/codex_journal/`
- `docs/codex_last_run.md`
- `docs/PROJEKTSTATUS.md` bei fachlichen Änderungen
- `docs/NEXT_TASK.md`
