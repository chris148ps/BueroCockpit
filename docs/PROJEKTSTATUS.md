# Projektstatus BüroCockpit

## Aktueller Entwicklungsstand

BüroCockpit führt Angebotsvorgänge und Direktaufträge dauerhaft als getrennte organisatorische Vorgangstypen. Bearbeitungsstand und fachliche Kategorien bleiben davon unabhängig. Kategorien werden hierarchisch aus einer gemeinsamen Quelle dargestellt, und Tabellenlayouts bleiben je Ansicht lokal persistent.

## Architektur

- `AppPaths` akzeptiert optionale Umgebungsvariablen für isolierte Daten- und lokale Konfigurationsverzeichnisse; ohne Variablen bleiben die bisherigen produktiven Pfade unverändert.
- Drei getrennte `TableLayoutSettings` speichern Reihenfolge, Sichtbarkeit, Breiten, Sortierfeld und Sortierrichtung lokal.
- `WorkflowType` ist die dauerhafte Quelle für Angebotsvorgang oder Direktauftrag; `WorkflowStep` speichert ausschließlich den Bearbeitungsstand.
- Uneindeutige Altbestände ohne Typfeld erhalten zur Laufzeit den stabilen Fallback Direktauftrag. Nur eindeutige Angebotsmerkmale wie vorhandene Angebotskategorien oder ein Sendedatum ergeben einen Angebotsvorgang; die Ableitung schreibt ohne spätere echte Bearbeitung keine Produktivdaten um.
- Kategorien werden als eigenständige gemeinsame Hierarchie für Navigation, Einstellungen, Detailauswahl und Drag & Drop aufgebaut.
- Repository-Speicherung normalisiert nullable Materialwerte über die vorhandene Parameterhilfe.

## Erledigte Hauptfunktionen

- organisatorische Navigation `Alle Vorgänge`, `Angebote` und `Aufträge` mit ausschließlich typbasierter Filterung
- Direktauftrag unter Aufträge und Angebotsvorgang unter Angebote neu anlegen und über alle Bearbeitungsstände im ursprünglichen Bereich halten
- vorhandene End- und Unterkategorien vollständig auswählen; Hauptkategorien mit Unterkategorien bleiben gesperrt
- Vorgänge per Drag & Drop ausschließlich in eine zulässige fachliche Kategorie verschieben
- fachliche Kategorie als sichtbare und sortierbare Spalte in den Vorgangslisten
- Übersichtsklick führt typgerecht zu Angebote oder Aufträge
- eindeutige globale Aktion `Alles speichern` und rechte Aktion `Speichern und prüfen`
- Duplizieren, Löschen, Papierkorb, Wiederherstellen, Archivieren und Rückholen
- Kategorien erstellen, umbenennen, verschieben, verschachteln, auf Hauptebene ziehen und zuordnungsschonend löschen
- ausschließlich echte feste System-IDs sperren
- getrennte persistente Tabellenlayouts für Aufträge, Angebote und Termine
- responsive Materialpositionen im schmalen Detailbereich
- robustes Speichern leerer optionaler Materialwerte
- tatsächliches Löschen von Materialpositionen trotz spätem UI-Binding-Ereignis
- isolierbarer funktionaler Testbetrieb ohne Zugriff auf die produktive Hauptdatenbank
- real geprüfte Anhänge, Backup/Restore, Diagnose, Schreibtischnotiz und manueller lokaler Sync-Testdienst

## Bekannte offene Punkte

- Windows-spezifische Bedienwege sind erfolgreich gebaut, aber noch nicht real auf Windows geprüft.
- Der echte Maus-Drag zwischen Vorgangsliste und Fachkategorie sowie der sichtbare Übersichtsklick müssen auf einer entsperrten macOS-Sitzung noch real bedient werden; die zugehörigen Zustands- und Persistenzpfade wurden isoliert ausgeführt.
- Finder-Dateidrop auf den Schreibtisch und horizontales Trackpad-Scrollen benötigen noch einen gezielten Plattform-Nachtest.
- Der ältere zentrale Live-Settings-Pfad sollte bei künftigen isolierten Tests bereits vor dem ersten Start explizit auf den temporären Datenordner zeigen.

## Wichtige Entscheidungen

- Produktive Tests werden ausschließlich über explizite temporäre Daten- und lokale Konfigurationspfade ausgeführt.
- Vorgangstyp, Bearbeitungsstand und fachliche Kategorie werden getrennt gespeichert und dürfen sich nicht gegenseitig still ändern.
- Drops auf organisatorische Filterbereiche sind keine Typänderung; ein Vorgangstyp wird nicht per Drag & Drop geändert.
- Kategorie-Löschen entfernt nur die gewählte Zuordnung; Aufträge und weitere Kategorien bleiben erhalten.
- Fachliche Kategorien mit technischen `__`-IDs bleiben editierbar, sofern ihre ID nicht in der kleinen festen System-ID-Menge steht.
- Archiv-Rückholung erhält vorhandene Kategorien und setzt Workflow und Status konsistent zurück.
- Layout- und Sortierwerte bleiben lokal; fachliche Daten bleiben im gemeinsamen Datenbestand.
