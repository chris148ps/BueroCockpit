# Projektstatus BüroCockpit

## Verbindliches fachliches Zielbild

BüroCockpit unterscheidet dauerhaft:

1. genau einen Vorgangstyp,
2. genau einen Workflowstatus,
3. genau eine aktuell zugeordnete normale Kategorie.

Die normalen Kategorien in der linken Navigation sind benutzerdefiniert. Der
Endbenutzer darf sie frei anlegen, umbenennen, verschieben, verschachteln und
löschen, soweit keine System- oder Sicherheitsregel entgegensteht.

Als feste Navigation bleiben nur Übersicht, Alle Vorgänge, Papierkorb,
Einstellungen, das unter Einstellungen geführte Archiv und ein technisch
erforderlicher mobiler Eingang. Angebote, Aufträge, Material und Termine sind
normale frei verwaltete Kategorien und keine zusätzlichen Systemansichten.

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

Die Desktop-App setzt die konfigurierbare Fachlogik um:

- `WorkflowType` und `WorkflowStep` bleiben getrennte Quellen für Vorgangstyp
  und Workflowstatus.
- `WorkflowCategoryMappings` speichert die zentrale Zielkategorie pro
  Kombination ausschließlich über die stabile Kategorie-ID.
- Unter `Einstellungen > Kategorien` sind sämtliche Angebots- und
  Direktauftragsstatus einzeln konfigurierbar; fehlende oder ausgeblendete
  Ziele werden sichtbar ungültig.
- Neue Vorgänge verlangen die Auswahl `Angebotsvorgang` oder `Direktauftrag`
  und werden ohne gültige Anfangszuordnung nicht angelegt.
- Die nachträgliche Typänderung verlangt eine Bestätigung und bei
  inkompatiblem Status eine ausdrückliche Statusauswahl.
- Ein Statuswechsel übernimmt genau eine konfigurierte Kategorie. Ohne gültige
  Zuordnung wird die Änderung blockiert und auf die Einstellungen verwiesen.
- Manuelle Kategorieauswahl und Drag & Drop ändern ausschließlich die aktuelle
  Kategorie; Haupt- und Unterkategorien sind gleichermaßen auswählbar.
- Das Löschen einer verwendeten Kategorie verlangt eine ausdrückliche
  Ersatz-/Entfernungsentscheidung oder Abbruch; Vorgänge werden nicht still
  verschoben.
- Neue und bewusst geänderte Vorgänge schreiben genau eine Kategorie fort.
  Unveränderte Legacy-Mehrfachzuordnungen bleiben beim Laden und bei reinen
  Konfigurationsänderungen unverändert.
- Navigation, Zähler, Suche, Übersicht und Detail verwenden die aktuelle
  Kategorie-ID; Status- und Kategorie-Badges bleiben getrennt und zeigen beim
  Kategorie-Badge den Pfad.
- Neue mobile Eingänge und Duplikate verwenden die Statuszuordnung. Der
  additive Snapshot-Export enthält `currentCategoryId`, `workflowType`,
  `workflowStep` und `status`; alte Leser tolerieren die zusätzlichen Felder.
- Die feste Navigation enthält keine Angebots-, Auftrags-, Material- oder
  Terminansicht mehr. Normale Vorgänge werden ausschließlich über ihre eine
  stabile Kategorie-ID oder die technische Gesamtansicht `Alle Vorgänge`
  angezeigt.
- Statuswechsel navigieren in die konfigurierte Zielkategorie und halten den
  bearbeiteten Vorgang samt Detailansicht ausgewählt; verschachtelte Ziele
  werden dafür aufgeklappt.
- Der Detailkopf bleibt beim Scrollen sichtbar, die Termine folgen direkt auf
  Aufgabe und der Workflow wird als verbundener, zugänglicher Stepper aus der
  gemeinsamen `WorkflowStep`-Quelle dargestellt.

## Übergang für bestehende Daten – Variante A

- Keine automatische oder massenhafte Migration von Produktivdaten.
- Keine stillen Schreibvorgänge beim Start, Laden oder Anzeigen.
- Neue und bewusst geänderte Vorgänge verwenden sofort die neue Logik.
- Unveränderte Altbestände dürfen technisch unverändert bleiben und werden
  tolerant gelesen.
- Alte Mehrfachzuordnungen dürfen nicht still gelöscht werden.
- Sobald ein bestehender Vorgang bewusst geändert und gespeichert wird, darf
  für seinen neuen Stand nur noch genau eine normale Kategorie fortgeschrieben
  werden.

## Weiterhin gültiger technischer Stand

- `AppPaths` unterstützt explizite temporäre Daten- und lokale
  Konfigurationsverzeichnisse für isolierte Tests.
- Tabellenlayouts bleiben über eine gemeinsame beziehungsweise
  kategoriebewusste Struktur lokal persistent; bestehende feste Layoutschlüssel
  dürfen nur tolerant weitergelesen und nicht migriert werden.
- Status-ComboBox, Workflowanzeige und Status-Badges verwenden
  `WorkflowStep` als gemeinsame Statusquelle.
- Papierkorb steht im festen Navigationsfuß direkt über Einstellungen.
- Speichern, Duplizieren, Löschen, Wiederherstellen, Archivieren, Material,
  Anhänge, Schreibtisch, Backup, Diagnose und manueller lokaler Testdienst sind
  vorhandene Funktionen.
- Produktive Tests dürfen ausschließlich mit explizit isolierten Testpfaden
  erfolgen.

## Release-Blocker und offene Punkte

- Die isolierten Repository-, Workflow-, Legacy- und Snapshot-Exporttests sind
  erfolgreich. Das reale macOS-Bundle wurde mit isolierten Pfaden sichtbar
  bedient; Neuanlage, Status- und Typwechsel, Haupt- und Unterkategorieauswahl,
  Vorgangs- und Kategorie-Drag-and-drop, Löschschutz und Neustartpersistenz
  wurden erfolgreich geprüft.
- Zwei im sichtbaren Rundgang gefundene Auswahlfehler wurden behoben und erneut
  erfolgreich geprüft: initiale ComboBox-Ereignisse verändern keine
  Statuszuordnung, und ein noch neuer Vorgang bleibt beim programmgesteuerten
  Statuskategorienwechsel erhalten.
- Windows-spezifische Bedienwege müssen real unter Windows geprüft werden.

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
