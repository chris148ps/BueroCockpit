# Projektstatus BüroCockpit

## Aktueller Entwicklungsstand

BüroCockpit ist auf macOS mit einer isolierten Kopie des aktuellen Datenbestands funktional durchgetestet. Aufträge und Angebote besitzen getrennte Erstellungsworkflows, Kategorien werden hierarchisch aus einer gemeinsamen Quelle dargestellt, und Tabellenlayouts bleiben je Ansicht lokal persistent. Material- und Löschvorgänge speichern sichtbar und tatsächlich konsistent.

## Architektur

- `AppPaths` akzeptiert optionale Umgebungsvariablen für isolierte Daten- und lokale Konfigurationsverzeichnisse; ohne Variablen bleiben die bisherigen produktiven Pfade unverändert.
- Drei getrennte `TableLayoutSettings` speichern Reihenfolge, Sichtbarkeit, Breiten, Sortierfeld und Sortierrichtung lokal.
- `WorkflowType` und `WorkflowStep` bleiben die fachliche Quelle für Direktauftrag, Angebot, Material, Termin und Erledigt.
- Kategorien werden als gemeinsame Hierarchie für Navigation, Einstellungen und Detailauswahl aufgebaut.
- Repository-Speicherung normalisiert nullable Materialwerte über die vorhandene Parameterhilfe.

## Erledigte Hauptfunktionen

- Direktauftrag unter Aufträge und Angebotsvorgang unter Angebote neu anlegen
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
- Finder-Dateidrop auf den Schreibtisch und horizontales Trackpad-Scrollen benötigen noch einen gezielten Plattform-Nachtest.
- Der ältere zentrale Live-Settings-Pfad sollte bei künftigen isolierten Tests bereits vor dem ersten Start explizit auf den temporären Datenordner zeigen.

## Wichtige Entscheidungen

- Produktive Tests werden ausschließlich über explizite temporäre Daten- und lokale Konfigurationspfade ausgeführt.
- Kategorie-Löschen entfernt nur die gewählte Zuordnung; Aufträge und weitere Kategorien bleiben erhalten.
- Fachliche Kategorien mit technischen `__`-IDs bleiben editierbar, sofern ihre ID nicht in der kleinen festen System-ID-Menge steht.
- Archiv-Rückholung erhält vorhandene Kategorien und setzt Workflow und Status konsistent zurück.
- Layout- und Sortierwerte bleiben lokal; fachliche Daten bleiben im gemeinsamen Datenbestand.
