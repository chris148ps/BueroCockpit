# Codex-Auftrag BC-0032

## Status

ABGESCHLOSSEN

## Datum

2026-07-20

## Titel

Status „Erledigt“ in normaler Zielkategorie sichtbar halten

## Ziel

Den reproduzierbaren Fehler beheben, dass ein bewusst auf `Erledigt`
gesetzter Auftrag trotz gültiger Statuszuordnung nicht in der zugeordneten
normalen Kategorie erscheint. Zuordnung, Sichtbarkeit und Persistenz wurden
isoliert geprüft.

## Ursache und Ergebnis

- Status, Abschlusszeit und stabile Kategorie-ID wurden bereits korrekt
  gespeichert.
- Der normale Kategorien-, Zähler- und Suchfilter behandelte jedoch jeden
  Status `Erledigt` wie das technische Archiv und blendete den Vorgang aus.
- Die Archivprüfung erkennt jetzt ausschließlich Status `Archiv` oder die
  tatsächliche Kategorie `Archiv`.
- `Erledigt` bleibt als normaler Workflowstatus in seiner frei konfigurierten
  Zielkategorie sichtbar.
- Übersicht und Fälligkeitsansichten schließen abgeschlossene Vorgänge
  weiterhin über ihre eigene bestehende Regel aus.

## Tests

- Reproduktion im echten macOS-Bundle mit isolierten Pfaden: nach dem
  Statuswechsel zeigte `Erledigt` fälschlich Zähler 0 und `0 Aufgaben`.
- Automatisierter Regressionstest: Status, Abschlusszeit, genau eine
  Zielkategorie, normaler Kategorienfilter, technisches Archiv und Reload über
  eine frisch initialisierte Repository-Instanz erfolgreich.
- Sichtbarer macOS-Nachtest mit demselben isolierten Datenstand: Kategorie
  `Erledigt` zeigte Zähler 1 und den Auftrag; nach App-Neustart unverändert.
- Workflow-/Kategorie-/Netzwerk-Integrationstests: erfolgreich.
- `git diff --check`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r osx-arm64`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r win-x64`: erfolgreich, 0 Warnungen, 0 Fehler.

## Grenzen

- Keine Produktivdaten oder produktiven Kategorien verändert.
- Keine allgemeine Workflow- oder UI-Neugestaltung.
- Kein Commit, Push, Merge, Tag, Release oder Versionswechsel.

## Beziehungen

- Zugehörige Journaldatei:
  `docs/codex_journal/2026-07-20_01-54_status-erledigt-kategorie-sichtbar.md`
- Vorgänger: `BC-0031`
- Nachfolger: `BC-0033`
