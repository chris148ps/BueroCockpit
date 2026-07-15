# Projektstatus BüroCockpit

## Verbindliches fachliches Zielbild

BüroCockpit unterscheidet dauerhaft:

1. genau einen Vorgangstyp,
2. genau einen Workflowstatus,
3. genau eine aktuell zugeordnete normale Kategorie.

Die normalen Kategorien in der linken Navigation sind benutzerdefiniert. Der
Endbenutzer darf sie frei anlegen, umbenennen, verschieben, verschachteln und
löschen, soweit keine System- oder Sicherheitsregel entgegensteht.

Für jede zulässige Kombination aus Vorgangstyp und Workflowstatus kann der
Benutzer eine Zielkategorie konfigurieren. Die Zuordnung wird intern über die
stabile Kategorie-ID gespeichert und bleibt deshalb bei Umbenennung oder
Verschiebung der Kategorie erhalten.

Beim Statuswechsel wird der Vorgang automatisch in die konfigurierte
Zielkategorie verschoben. Ein Vorgang darf niemals gleichzeitig in mehreren
normalen Kategorien erscheinen.

Die verbindliche Fachlogik steht in `docs/ARBEITSKATEGORIEN.md`.

## Vorgangstypen

Beim Erstellen eines neuen Vorgangs muss zwischen folgenden Typen gewählt werden:

- Angebotsvorgang
- Direktauftrag

Empfohlene Anfangsstatus:

- Angebotsvorgang → `Angebot`
- Direktauftrag → `Auftrag`

Der Vorgangstyp muss später bewusst und nach Bestätigung änderbar sein. Daten
wie Kunde, Beschreibung, Anhänge, Material, Termine, Techniker und
Wiedervorlagen bleiben dabei erhalten.

## Automatische Statuszuordnungen

Die Kategorienamen sind nicht fest vorgeschrieben. Folgende Zuordnungen sind
nur Beispiele:

- Angebotsvorgang / Angebot → Angebote
- Angebotsvorgang / Angebot gesendet → Angebote / Gesendet
- Angebotsvorgang / Auftrag → Angebote / Beauftragt
- Direktauftrag / Auftrag → Aufträge
- Material → Material
- Termin → Termine
- Erledigt → Erledigt

Verbindlich ist immer die vom Benutzer gewählte stabile Kategorie-ID.

## Tatsächlicher Implementierungsstand

Die App speichert Vorgangstyp und Workflowstatus bereits getrennt in
`WorkflowType` und `WorkflowStep`. Kategorien werden bereits über IDs geführt,
die vollständige konfigurierbare Zuordnung von Vorgangstyp und Status zu einer
Zielkategorie ist jedoch noch nicht umgesetzt.

Die App muss noch ergänzen:

- Auswahl des Vorgangstyps beim Anlegen,
- nachträgliche bewusste Änderung des Vorgangstyps,
- Konfiguration der Statuszuordnungen in den Einstellungen,
- automatische Verschiebung beim Statuswechsel,
- Schutz beim Löschen einer zugeordneten Kategorie,
- genau eine Kategorie pro neuem oder bewusst geändertem Vorgang,
- konsistente Verwendung in Navigation, Suche, Zählern, Übersicht, Import und
  Export.

Bis diese Abweichung implementiert und vollständig geprüft ist, blockiert sie
einen Release.

## Übergang für bestehende Daten – Variante A

- Keine automatische oder massenhafte Migration von Produktivdaten.
- Keine stillen Schreibvorgänge beim Start, Laden oder Anzeigen.
- Neue und bewusst geänderte Vorgänge verwenden nach der Umsetzung sofort die
  neue Logik.
- Unveränderte Altbestände dürfen technisch unverändert bleiben und werden
  tolerant gelesen.
- Alte Mehrfachzuordnungen dürfen nicht still gelöscht werden.
- Sobald ein bestehender Vorgang bewusst geändert und gespeichert wird, darf
  für seinen neuen Stand nur noch genau eine normale Kategorie fortgeschrieben
  werden.

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

- Frei konfigurierbare Statuszuordnungen sind noch nicht implementiert.
- Die Auswahl des Vorgangstyps beim Erstellen fehlt.
- Die nachträgliche Typänderung fehlt.
- Navigation, Zähler, Suche, Übersicht, Detailansicht, Import/Export und spätere
  Sync-Formate müssen dieselbe eindeutige Kategorie-ID verwenden.
- Variante A und der unveränderte Produktivdatenbestand müssen mit isolierten
  Legacy-Testdaten nachgewiesen werden.
- Windows-spezifische Bedienwege müssen nach der Implementierung real unter
  Windows geprüft werden.

## Verbindliche Projektentscheidungen

- `docs/ARBEITSKATEGORIEN.md` ist die Fachquelle für Vorgangstyp,
  Workflowstatus, benutzerdefinierte Kategorien und Statuszuordnungen.
- Historische Journal-Einträge beschreiben frühere Stände und setzen das neue
  Zielbild nicht außer Kraft.
- Vor jedem Codex-Auftrag und jedem Release ist die Konsistenzprüfung aus
  `docs/CODEX_AUFTRAGSPRUEFUNG.md` Pflicht.
- Jeder ungeklärte Widerspruch zwischen Regeln, Dokumentation, Design und App
  stoppt einen Release.
- Kein Release, Tag, Versionswechsel oder Eingriff in Produktivdaten ohne
  ausdrückliche Freigabe.
