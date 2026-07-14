# Workflowstatus, Kategorienanzeige und Tastaturbedienung stabilisiert

## Anlass

Nach der bisherigen Trennung von Vorgangstyp, Workflowstatus und fachlicher Kategorie blieben mehrere Bedienfehler reproduzierbar: Der Status war nach einem Wechsel zwischen Angebotsvorgang und Direktauftrag teilweise leer, kategorielose Vorgänge erhielten beim Speichern still eine beliebige Kategorie, die Kategorie erschien in der Tabelle nicht als Badge, und Sidebar- sowie Entf-Tastaturbedienung waren unvollständig.

## Ursachen

- Die dynamische Kombination aus `ItemsSource` und verschachteltem `SelectedItem`-Binding der Status-ComboBox verlor beim Wechsel zwischen den typabhängigen Wertelisten die sichtbare Auswahl.
- Die Repository-Normalisierung ergänzte bei leerer Zuordnung die erste bekannte Kategorie und erzeugte damit eine fachlich falsche Zuordnung.
- Tabellenzellen unterschieden nur Status und normalen Text; für Kategorien gab es keine eigene Darstellung.
- Die drei getrennten Sidebar-Listen hatten keine gemeinsame Baum- und Übergangsnavigation. Die Entf-Taste war nicht mit dem vorhandenen abgesicherten Papierkorbpfad verbunden.

## Änderungen

- Die Status-ComboBox wird aus einer zentralen, gegen Ereignisrückkopplung geschützten Synchronisierung befüllt und ausgewählt. Klick auf Workflowstufe und ComboBox-Auswahl verwenden denselben Statuspfad.
- Bekannte Altstatuswerte werden kanonisch zugeordnet; unbekannte Werte bleiben sichtbar und unverändert erhalten.
- Statusänderungen verändern weder `WorkflowType` noch `CategoryId`/`CategoryIds`.
- Ein Vorgang ohne Kategorie bleibt beim Speichern kategorielos; das Repository erfindet keine Ersatzkategorie mehr.
- Die Kategorie-Spalte zeigt vorhandene vollständige Kategoriepfade als kompaktes neutrales Badge und bleibt bei fehlender Kategorie leer.
- Die Sidebar unterstützt Pfeil hoch/runter über alle drei Listen, Pfeil rechts/links zum Auf- und Zuklappen beziehungsweise Eltern-/Kindwechsel sowie Enter zum Öffnen. Der Tastaturfokus ist sichtbar.
- Entf verwendet denselben Bestätigungs-, Backup- und Papierkorbpfad wie der Löschbutton und bleibt in Texteingaben, ComboBoxen, Datums- und Zahlenfeldern wirkungslos.

## Rückwärtskompatibilität

- Keine Migration und keine Änderung bestehender Vorgangstypen oder Kategorien.
- Unbekannte historische Workflowwerte bleiben lesbar, auswählbar und persistent.
- Das bestehende additive Schema mit `WorkflowType` und `WorkflowStep` bleibt unverändert.
- Die Änderung an der Kategorie-Normalisierung betrifft nur Vorgänge, die tatsächlich keine gültige Kategoriezuordnung besitzen.

## Prüfungen

- Isolierter Avalonia-Lauf mit echtem `MainWindow`: Statuswechsel Angebot/Direktauftrag, unbekannter Altstatus, Workflowanzeige, Kategorie-Badge, fehlende Kategorie, typgerechte Übersichtsnavigation, Sidebar-Tastatur, Vorgangs-Drop und Kategorie-Reihenfolge erfolgreich.
- Neustart gegen dieselbe isolierte SQLite-Datei: `WorkflowType`, `WorkflowStep`, Kategorie, leere Kategorie und Reihenfolge persistent.
- Ergänzender isolierter UI-Lauf: Dark/Light-Umschaltung, Suche nach Workflowstatus und Kategorie, Sortierung mehrerer Standardfelder, Techniker anlegen/bearbeiten/löschen sowie Papierkorb-Kernpfad erfolgreich.
- Repository-Gesamtlauf: Vorgangsdetails, Kategorie anlegen/umbenennen/verschachteln/ausblenden, Material, Testanhang, Schreibtischnotiz, Backup, Papierkorb, Wiederherstellung und Leeren mit isolierten Testdaten erfolgreich.
- Manueller lokaler Testdienst: kein Autostart, manueller Start, harmlose Statusendpunkte, manueller Stopp; Port `53941` danach geschlossen.
- `PRAGMA integrity_check`: `ok`.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r win-x64`: erfolgreich, 0 Warnungen, 0 Fehler; keine reale Bedienung unter Windows.
- Die echte App wurde mit temporären Daten- und Konfigurationspfaden gestartet. Die macOS-Sitzung war gesperrt; sichtbare Maus-/Tastaturbedienung und damit der verpflichtende vollständige reale Rundgang konnten nicht erneut durchgeführt werden.
- `./scripts/run-macos-bundle.sh Debug` wurde nicht verwendet, weil die vorhandene fremde lokale Änderung am Skript die Weitergabe der isolierten Pfade entfernt. Eine Nutzung hätte den Produktivdatenschutz gefährdet; die Änderung blieb unangetastet.
- Read-only-Zeitfensterprüfung der bekannten produktiven App- und OneDrive-Pfade ergab keine während dieses Auftrags geänderten Dateien.

## Offene Abnahme

Auf einer entsperrten macOS-Sitzung müssen der vollständige sichtbare Rundgang, echter Maus-Drag, Status-ComboBox, Sidebar-Tastatur, Entf-Bestätigungsdialog, Kontextmenüs und Dialogfokus mit dem isolierten Testprofil nochmals real bedient werden.

## Abgrenzung

Keine Produktivdaten, iPad-Komponenten, Netzwerk-/Sync-Architektur, Version, Release, Tags oder `main` geändert.
