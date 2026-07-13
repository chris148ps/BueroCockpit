# Codex-Journal: Sidebar-Auswahl beheben und Desktop-App systematisch prüfen.

## Ziel

Die drei Sidebar-Listen ohne konkurrierende `SelectedCategory`-Bindings synchronisieren, die Auswahl dauerhaft sichtbar halten und die wesentlichen Desktop-Funktionsbereiche auf sichere lokal begrenzte Fehler prüfen.

## Umsetzung

Die bidirektionalen `SelectedItem`-Bindings von `CategoryList`, `UserCategoryList` und `SettingsCategoryList` wurden entfernt. Ein Klick übernimmt nun ausschließlich die tatsächlich ausgewählte Kategorie per ID, leert die beiden anderen Listen unter der vorhandenen Suppress-Sperre und lässt `SelectedCategory` niemals durch ein Abwahlereignis auf `null` setzen. Alle drei Listen verwenden denselben lokalen Auswahlstil. Start, Collection-Neuaufbau, Auf-/Zuklappen, Kategorie-Löschen, programmgesteuerte Schreibtisch-Navigation und Daten-Neuladen stellen die sichtbare Auswahl per ID wieder her. Der `SelectedCategory`-Setter akzeptiert nach einem Reload eine neue Kategorieinstanz mit gleicher ID und behält kein veraltetes Objekt.

Systematisch geprüft wurden Navigation, Kategorienverwaltung, Kategorieauswahl im Auftragsdetail, Aufträge/Angebote/Material/Termine, Tabelleninteraktionen und lokale Tabellenpersistenz, Detail/Speichern, Schreibtisch, Einstellungen, Daten & Pfade, Backups, Diagnose, leere bzw. doppelte Bedienelemente, Zähler, Statuswerte, XAML-Handler und offensichtliche Ausnahmepfade. Weitere sicher reproduzierbare und lokal begrenzte Fehler wurden nicht gefunden.

## Geänderte Dateien

- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml`
- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml.cs`

## Tests

- `git pull --ff-only origin main`: erfolgreich, bereits aktuell.
- `git diff --check`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `./scripts/run-macos-bundle.sh Debug`: erfolgreich; Bundle gebaut, signiert und real geöffnet.
- Reales Beenden und erneutes Öffnen des Bundles: erfolgreich; neuer Prozess lief nach dem Neustart.
- Aktuelle Datenbank read-only geprüft: `PRAGMA integrity_check` meldet `ok`; keine verwaisten TaskCategories, keine Aufträge ohne Kategoriezuordnung und keine leeren sichtbaren Kategorienamen.
- Aktueller Kategorienbestand read-only geprüft: `Firma / Retouren`, `Firma / Lager`, `Netzbetreiber / SH-Netz` und `Netzbetreiber / Marktstammdatenregister` sind vorhanden; Branch-Zähler ergeben Firma 2 und Netzbetreiber 9 ohne Mehrfachzählung.
- Produktivdaten und lokale Einstellungen vor/nach Start und Neustart per SHA-256 verglichen: unverändert.
- XAML-Eventhandler und relevante Bindings statisch geprüft; Checkboxen verwenden `Category.SelectionName`, Diagnose liegt ausschließlich unter `Daten & Pfade`, Tabellenlayout wird getrennt gespeichert.

## Ergebnis

Systembereiche, echte Benutzerkategorien, Unterkategorien und Einstellungen behalten nach Klick und Neuaufbau zuverlässig genau eine sichtbare blaue Auswahl. Einstellungen öffnet wieder über die zentrale Auswahl. Die produktiven Daten und Zuordnungen blieben unverändert.

## Bekannte offene Punkte

- macOS verweigerte Bildschirmaufnahme und Accessibility-Steuerung für `osascript`. Deshalb konnten automatisierte sichtbare Klickfolgen und Screenshots nicht durchgeführt werden. Reale Starts, Neustart, Prozesslauf, Datenbestand, Hashvergleich, Build und Quellpfade wurden geprüft.
- Kategorien erstellen, umbenennen, verschieben, verschachteln und löschen wurden wegen des Verbots von Produktivdatenänderungen nur über Handler-, Persistenz- und Datenintegritätsprüfung untersucht, nicht am aktuellen Bestand ausgeführt.

## Aktueller Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M docs/NEXT_TASK.md
?? docs/codex_journal/2026-07-14_01-05_sidebar-auswahl-desktop-audit.md
```
