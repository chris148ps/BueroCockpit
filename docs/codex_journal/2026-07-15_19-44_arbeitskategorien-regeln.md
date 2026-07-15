# Codex-Journal: Verbindliche Arbeitskategorien- und Release-Prüfregeln

## Ziel

Die Projektdokumentation vor jeder Implementierung auf die neue eindeutige
Arbeitskategorienlogik umstellen und eine zusätzliche verbindliche
Konsistenzprüfung vor jedem Release festlegen.

## Geprüfte Regel- und Fachdateien

Vollständig gelesen wurden `AGENTS.md`, `docs/CODEX_PROJEKTREGELN.md`,
`docs/CODEX_AUFTRAGSPRUEFUNG.md`, `docs/DESIGN_RICHTLINIEN.md`,
`docs/PROJEKTSTATUS.md`, `docs/TESTRICHTLINIEN.md` und der laut `AGENTS.md`
zusätzlich relevante `docs/RELEASE_PROZESS.md`. Weitere aktive Dokumente
wurden per gezielter Suche auf Kategorie-, Workflow-, Branch- und
Releaseaussagen geprüft.

## Ersetzte Regeln

- Frei wählbare oder mehrere sichtbare fachliche Kategorien werden durch
  genau eine aus Vorgangstyp und Workflowstatus abgeleitete Arbeitskategorie
  ersetzt.
- `SH-Netz`, `Retouren`, `Lager`, `Marktstammdatenregister` und
  `Warten auf Kunde` werden als getrennte Kennzeichnungen definiert.
- Unabhängiger Kategorien-Drag und manuelle Arbeitskategorieauswahl sind kein
  verbindliches Zielbild mehr.
- Variante A verbietet die automatische Migration unveränderter
  Produktivdaten; die neue Logik gilt für neue und bewusst geänderte Vorgänge.
- Reine Dokumentationsänderungen benötigen keinen Build, solange weder Code,
  Projektdateien noch Build- oder Skriptlogik verändert wurden.
- Normale Codex-Veröffentlichung erfolgt über `codex/work` statt durch einen
  direkten Push nach `main`.
- Die Grundprüfung aktualisiert den aktuellen Branch nur per Fast-Forward und
  mischt `main` nicht mehr automatisch in `codex/work` ein.
- Die Dokumentationsvorlage verlangt künftig den Nachweis der vorab gelesenen
  Regeldateien und freigegebenen Widerspruchsauflösung.

## Neue Release-Regel

Vor jedem Release werden Regeldateien, veraltete Projektregeln,
Projektdokumentation gegen tatsächliche App, Releaseprozess gegen `AGENTS.md`
und Designrichtlinien gegen Implementierung geprüft. Jeder Fund stoppt den
Release vor Versionsänderung, Build, Tag oder Upload. Der Nutzer entscheidet,
ob zuerst Regeln oder Implementierung angepasst werden.

## Tatsächlicher App-Stand

Die Codeprüfung zeigte, dass die App weiterhin manuelle `CategoryId`-/
`CategoryIds`-Zuordnungen, Mehrfachauswahl und statusunabhängigen
Kategorie-Drag unterstützt. Die neue Fachlogik ist daher dokumentiert, aber
noch nicht implementiert und bleibt bis zur nächsten Aufgabe ein
Release-Blocker.

## Prüfungen

- Nur Dokumentationsdateien geändert.
- Keine Anwendung implementiert, kein Build ausgeführt und keine
  Produktivdaten verwendet.
- `git diff --check`: erfolgreich.
- Konsistenzsuche nach widersprüchlichen Kategorie-, Workflow-, Release-,
  Branch- und Buildregeln: erfolgreich; keine aktive Gegenregel gefunden.
- Dateiumfang: ausschließlich `AGENTS.md` und Markdown-Dokumentation.
- `git status --short`: ausgeführt.

## Abgrenzung

Kein Merge, Release, Tag, Versionswechsel, Build, Anwendungstest oder Eingriff
in Produktivdaten.
