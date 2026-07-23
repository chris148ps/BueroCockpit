# Letzter Codex-Lauf

## Auftrag

BC-0032: Fehler beheben, durch den Vorgänge nach dem Workflowstatus `Erledigt` aus Kategorien, Zählern und Suche verschwanden.

## Ergebnis

BC-0032 ist abgeschlossen.

Ursache war der Filter `IsArchivedForSearch`: Er behandelte den Workflowstatus `Erledigt` fälschlich wie das technische Archiv. Status, Abschlusszeit und Zielkategorie wurden bereits korrekt gespeichert; der Auftrag wurde anschließend jedoch ausgeblendet.

## Geänderte Bereiche

- `Services/CategoryHierarchyFilter.cs`: Nur Status oder Kategorie `Archiv` gelten als archiviert.
- `MainWindow.axaml.cs`: Kategorien, Zähler, Suche und Archivdialog verwenden die korrigierte Prüfung.
- `tests/BueroCockpit.WorkflowTests/Program.cs`: Regressionstest für Status, Kategorieverschiebung, Sichtbarkeit, Archivabgrenzung und Neustartpersistenz.
- Auftragsarchiv, Journal, Projektstatus und Zeiger auf die nächste Aufgabe wurden aktualisiert.

## Prüfung

- Reale macOS-Reproduktion vor der Korrektur: Kategorie `Erledigt` zeigte trotz gespeichertem Auftrag Zähler `0` und `0 Aufgaben`.
- Nach der Korrektur mit demselben isolierten Datenstand: Zähler `1`, Auftrag sichtbar, Status und Kategoriepfad korrekt.
- Nach vollständigem App-Neustart weiterhin sichtbar.
- Workflow-, Kategorie- und Netzwerk-Integrationstests: erfolgreich.
- `git diff --check`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r osx-arm64`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r win-x64`: erfolgreich, 0 Warnungen, 0 Fehler.

## Grenzen

- Kein visueller Windows-Test; der Windows-Runtime-Build war erfolgreich.
- Kein Commit, Push, Merge, Tag, Release oder Versionswechsel wurde im BC-0032-Lauf durchgeführt.
- Der lokale Branch `codex/work` kann weiterhin umfangreiche uncommittete Änderungen enthalten; diese wurden erhalten.

## Aktuelle Architekturentscheidung

- Produktivbetrieb künftig auf dem Windows-Terminalserver.
- Nur ein RDP-Benutzer; produktive Daten dürfen unter `%LOCALAPPDATA%\BueroCockpit` liegen.
- Keine direkt in OneDrive geöffnete produktive SQLite-Datenbank.
- OneDrive nur als Austauschort für geschlossene Backup-Archive.
- iPad-Datenaustausch weiterhin per Direktübertragung im lokalen Netzwerk.

## Nächster Schritt

Den vollständigen Entwicklungsstand sicher in einen Windows-x64-Releasekandidaten für den Terminalserver überführen und einen Erstinstaller mit Velopack-fähiger Auto-Update-Basis vorbereiten. Kein Versionswechsel oder Release ohne ausdrückliche Freigabe.
