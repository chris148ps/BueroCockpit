# Letzter Codex-Lauf

## Datum/Uhrzeit

2026-07-14 01:05 +0200

## Letzter Auftrag

Sidebar-Auswahl beheben und Desktop-App systematisch prüfen.

## Zusammenfassung

Systembereiche, echte Benutzerkategorien, Unterkategorien und Einstellungen behalten nach Klick und Neuaufbau zuverlässig genau eine sichtbare blaue Auswahl. Einstellungen öffnet wieder über die zentrale Auswahl. Die produktiven Daten und Zuordnungen blieben unverändert.

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

## Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M docs/NEXT_TASK.md
?? docs/codex_journal/2026-07-14_01-05_sidebar-auswahl-desktop-audit.md
```

## Branch

Wird nach dem Dokumentationslauf durch den Git-Helfer ergänzt.

## Commit

Wird nach dem Dokumentationslauf durch den Git-Helfer ergänzt.

## Push erfolgreich

Nein – der reine Dokumentationslauf führt keinen Push aus.

## Offene Punkte

- macOS verweigerte Bildschirmaufnahme und Accessibility-Steuerung für `osascript`. Deshalb konnten automatisierte sichtbare Klickfolgen und Screenshots nicht durchgeführt werden. Reale Starts, Neustart, Prozesslauf, Datenbestand, Hashvergleich, Build und Quellpfade wurden geprüft.
- Kategorien erstellen, umbenennen, verschieben, verschachteln und löschen wurden wegen des Verbots von Produktivdatenänderungen nur über Handler-, Persistenz- und Datenintegritätsprüfung untersucht, nicht am aktuellen Bestand ausgeführt.

## Empfohlener nächster Schritt

Die korrigierte Sidebar-Auswahl einmal manuell mit gewährtem macOS-Hilfszugriff sichtbar abnehmen.

1. Übersicht, Systembereiche, Firma, Netzbetreiber und Unterkategorien anklicken und die blaue Auswahl prüfen.
2. Kategorien auf- und zuklappen und die unveränderte Auswahl prüfen.
3. Einstellungen öffnen und anschließend zu einer Benutzerkategorie zurückwechseln.
