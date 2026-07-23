# Codex-Journal: Status „Erledigt“ bleibt in Zielkategorie sichtbar

## Zeitpunkt

2026-07-20 01:54 +0200

## Auftrag

BC-0032 – Den macOS-Fehler beheben, durch den ein korrekt auf `Erledigt`
gesetzter und verschobener Auftrag in der normalen Zielkategorie nicht mehr
angezeigt wurde.

## Ausgangslage und Grenzen

- Der Arbeitsbranch `codex/work` enthielt bereits umfangreiche uncommittete
  Änderungen; sie wurden vollständig erhalten.
- Produktive Daten, Kategorien, Zuordnungen und Anhänge durften nicht verändert
  werden.
- Keine Workflow- oder UI-Neugestaltung.
- Kein Commit, Push, Merge, Tag, Release oder Versionswechsel.

## Reproduktion und Ursache

- Das echte macOS-Bundle wurde mit separatem Daten- und
  Konfigurationsverzeichnis unter `/private/tmp` gestartet.
- Eine normale Kategorie `Erledigt` wurde dem Direktauftragsstatus `Erledigt`
  zugeordnet und ein isolierter Auftrag aus `Offene Aufgaben` abgeschlossen.
- Status, Abschlusszeit und Zielkategorie wurden korrekt gespeichert.
- Danach zeigte die Zielkategorie trotzdem Zähler 0 und `0 Aufgaben`.
- Ursache war `IsArchivedForSearch`: Die Methode fasste `Erledigt` und
  `Archiv` zusammen und wurde zugleich für normale Kategorien, Zähler, Suche
  und den Archivdialog verwendet.

## Umsetzung

- Die zentrale Kategorienfilterlogik unterscheidet jetzt die technische
  Archivzugehörigkeit von einem fachlich abgeschlossenen Vorgang.
- Als archiviert gelten nur Status `Archiv` oder die tatsächliche Kategorie
  `Archiv`.
- Der vorhandene Abschlussfilter für Übersicht und Fälligkeiten bleibt
  unverändert.
- Der Regressionstest prüft Statuswechsel, Abschlusszeit, genau eine
  Zielkategorie, normale Sichtbarkeit, Archivabgrenzung und Persistenz nach
  erneuter Repository-Initialisierung.

## Prüfungen

- Workflow-/Kategorie-/Netzwerk-Integrationstests: erfolgreich.
- `git diff --check`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r osx-arm64`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r win-x64`: erfolgreich, 0 Warnungen, 0 Fehler.
- Sichtbarer macOS-Nachtest mit demselben isolierten Datenstand: `Erledigt`
  zeigte Zähler 1, `1 Aufgabe`, den Status `Erledigt` und den gespeicherten
  Kategoriepfad.
- Nach vollständigem Beenden und Neustart blieb derselbe Auftrag sichtbar.

## Nicht ausgeführt

- Kein Windows-UI-Test; der Windows-Runtime-Build war erfolgreich.
- Kein iPad-Test und kein `xcodebuild`, da keine iPad-Datei betroffen war.
