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

## Pflichtfälle für Kategorien und Statuszuordnungen

Nach der Implementierung von `docs/ARBEITSKATEGORIEN.md` müssen isoliert
mindestens folgende Fälle geprüft werden:

- jeder Vorgang besitzt genau einen Typ und genau einen Workflowstatus,
- jede zulässige Kombination aus Vorgangstyp und Workflowstatus lässt sich
  getrennt auf eine vorhandene normale Kategorie-ID konfigurieren,
- frei gewählte Kategorienamen und vollständige Kategoriepfade werden korrekt
  angezeigt,
- Umbenennen und Verschieben einer Kategorie erhält die Statuszuordnung,
- fehlende oder gelöschte Zielkategorien werden als ungültig angezeigt und
  niemals still ersetzt,
- jeder bewusste Statuswechsel verschiebt in genau die konfigurierte Kategorie,
- Navigation, Zähler, Suche, Übersicht, Detail und Neustartpersistenz zeigen
  keine doppelte normale Kategorie,
- Angebote, Aufträge, Material und Termine erscheinen nicht als fest
  eingebaute Systemnavigation oder parallele Typ-/Status-Sammelansicht,
- `Alle Vorgänge` bleibt die einzige technische Sammelansicht; dortige Treffer
  behalten genau eine normale Kategorie,
- jede normale Haupt- und Unterkategorie ist in Navigation,
  Kategorieauswahl, Statuszuordnung und Kategorieverwaltung erreichbar,
- Drag & Drop und bewusster Statuswechsel navigieren in die Zielkategorie und
  erhalten Auswahl sowie Detailansicht; dies gilt auch für einen noch nicht
  gespeicherten neuen Vorgang,
- neue und bewusst geänderte Vorgänge verwenden die neue Logik,
- unveränderte Legacy-Datensätze werden nach Variante A weder migriert noch
  still zurückgeschrieben.

## Vollständiger Desktop-Funktionstest

Mindestens prüfen:

1. Start, Navigation, Auswahlmarkierung, Zähler und Fenstergrößen
2. Vorgänge beider Typen erstellen, bearbeiten, speichern, löschen, wiederherstellen und archivieren
3. Alle normalen Haupt- und Unterkategorien frei verwalten, auswählen, verschieben, als Drop-Ziel verwenden, Statuszuordnungen konfigurieren und Kategoriepfade korrekt anzeigen
4. Suche, Sortierung, Spaltenbreiten, Spaltenreihenfolge, Sichtbarkeit und gemeinsame beziehungsweise kategoriebewusste Layoutpersistenz ohne neue feste fachliche Ansichten
5. Festen Detailkopf, Bereichsreihenfolge, verbundenen responsiven Workflow-Stepper, Detailfelder, Termine, Wiedervorlagen, Techniker, Material und Anhänge
6. Schreibtischfunktionen und Neustartpersistenz
7. Einstellungen, Backup, Diagnose und lokale Sync-Dienste
8. Konsolenausgaben, Exceptions, Binding-Fehler und widersprüchliche UI-Zustände

Zusätzlich ist auf der Übersicht zu prüfen, dass überfällige Wiedervorlagen
eine neutrale Fläche mit dünnem semantischem Fehlerrahmen oder -akzent besitzen
und ihre Bedeutung nicht ausschließlich durch Farbe vermitteln.

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
