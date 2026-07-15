# Projektstatus BüroCockpit

## Verbindliches fachliches Zielbild

BüroCockpit unterscheidet dauerhaft drei Ebenen:

1. genau einen Vorgangstyp,
2. genau einen Workflowstatus,
3. genau eine daraus automatisch abgeleitete sichtbare Arbeitskategorie.

Die verbindliche Zuordnung steht in `docs/ARBEITSKATEGORIEN.md`. Ein Vorgang
darf niemals gleichzeitig in mehreren sichtbaren Arbeitskategorien erscheinen.
`SH-Netz`, `Retouren`, `Lager`, `Marktstammdatenregister` und
`Warten auf Kunde` sind davon getrennte Kennzeichnungen und keine zweite
Arbeitskategorie.

## Tatsächlicher Implementierungsstand

Die App speichert Vorgangstyp und Workflowstatus bereits getrennt in
`WorkflowType` und `WorkflowStep`. Der aktuelle Code verwendet daneben jedoch
noch `CategoryId` und `CategoryIds` als manuell auswählbare fachliche
Kategorien, unterstützt Mehrfachzuordnungen und kann Vorgänge unabhängig vom
Workflowstatus per Drag & Drop in solche Kategorien verschieben.

Damit entspricht die aktuelle Implementierung noch nicht dem neuen
verbindlichen Zielbild. In diesem Dokumentationsauftrag wurde bewusst kein
Anwendungscode geändert. Bis die Abweichung implementiert und geprüft ist,
blockiert sie jeden Release.

## Verbindliche Arbeitskategorien

Für Angebotsvorgänge gilt:

- `Ansicht` oder `Angebot` → `Angebote`
- `Angebot gesendet` → `Angebote gesendet`
- `Auftrag` → `Angebotsaufträge`
- `Material` → `Material`
- `Termin` → `Termin`
- `Erledigt` → `Erledigt`

Für Direktaufträge gilt:

- `Auftrag` → `Aufträge`
- `Material` → `Material`
- `Termin` → `Termin`
- `Erledigt` → `Erledigt`

## Übergang für bestehende Daten – Variante A

- Keine automatische oder massenhafte Migration von Produktivdaten.
- Keine stillen Schreibvorgänge beim Start, Laden oder Anzeigen.
- Neue und bewusst geänderte Vorgänge verwenden nach der Umsetzung sofort die
  neue Ableitung.
- Unveränderte Altbestände dürfen technisch unverändert bleiben und werden
  tolerant gelesen.
- Alte Kategorie-IDs und Mehrfachzuordnungen sind Legacy-Daten. Sie dürfen
  nicht als zweite sichtbare Arbeitskategorie fortgeführt werden.
- Die Umwandlung geeigneter Altkategorien in Kennzeichnungen muss später
  verlustfrei und ohne ungefragte Produktivdatenmigration umgesetzt werden.

## Weiterhin gültiger technischer Stand

- `AppPaths` unterstützt explizite temporäre Daten- und lokale
  Konfigurationsverzeichnisse für isolierte Tests.
- Tabellenlayouts bleiben je Ansicht lokal persistent.
- Status-ComboBox, Workflowanzeige und Status-Badges verwenden
  `WorkflowStep` als gemeinsame Statusquelle.
- Papierkorb steht im festen Navigationsfuß direkt über Einstellungen.
- Speichern, Duplizieren, Löschen, Wiederherstellen, Archivieren, Material,
  Anhänge, Schreibtisch, Backup, Diagnose und manueller lokaler Testdienst sind
  vorhandene Funktionen.
- Produktive Tests dürfen ausschließlich mit explizit isolierten Testpfaden
  erfolgen.

## Release-Blocker und offene Punkte

- Die neue Arbeitskategorienlogik ist noch nicht implementiert.
- Kennzeichnungen sind noch nicht vollständig als eigener Bereich vom alten
  Kategorienmodell getrennt.
- Navigation, Zähler, Suche, Übersicht, Detailansicht, Import/Export und
  spätere Sync-Formate müssen auf dieselbe eindeutige Ableitung umgestellt
  werden.
- Variante A und der unveränderte Produktivdatenbestand müssen mit isolierten
  Legacy-Testdaten nachgewiesen werden.
- Windows-spezifische Bedienwege müssen nach der Implementierung real unter
  Windows geprüft werden.

## Verbindliche Projektentscheidungen

- `docs/ARBEITSKATEGORIEN.md` ist die Fachquelle für Vorgangstyp,
  Workflowstatus, Arbeitskategorie und Kennzeichnungen.
- Historische Journal-Einträge beschreiben frühere Stände und setzen das neue
  Zielbild nicht außer Kraft.
- Vor jedem Codex-Auftrag und jedem Release ist die Konsistenzprüfung aus
  `docs/CODEX_AUFTRAGSPRUEFUNG.md` Pflicht.
- Jeder ungeklärte Widerspruch zwischen Regeln, Dokumentation, Design und App
  stoppt einen Release. Der Nutzer entscheidet, ob zuerst Regeln oder
  Implementierung angepasst werden.
- Kein Release, Tag, Versionswechsel oder Eingriff in Produktivdaten ohne
  ausdrückliche Freigabe.
