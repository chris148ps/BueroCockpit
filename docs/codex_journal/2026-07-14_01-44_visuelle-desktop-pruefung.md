# Codex-Journal: Desktop-App visuell prüfen und gefundene UI-Ungereimtheiten beheben.

## Ziel

Die laufende BüroCockpit-Desktop-App mit freigegebener macOS-Bildschirmaufnahme und Bedienungshilfe systematisch sichtbar prüfen, klar reproduzierbare UI-Fehler minimal-invasiv beheben und die korrigierten Ansichten real verifizieren.

## Umsetzung

Die Hauptnavigation mit Übersicht, Aufträge, Angebote, Material, Termine, Schreibtisch, Benutzerkategorien und Einstellungen wurde in der real gestarteten macOS-App durchgeklickt. Die Auswahl blieb beim Wechsel zwischen den drei Sidebar-Listen sowie beim Auf- und Zuklappen der Kategorie Firma eindeutig sichtbar. Auf hellen Schreibtisch-Notizzetteln wurden die kontrastarmen Texte Ziehen und Wichtig sowie der Resize-Hinweis auf die vorhandenen Desk-Ink-Ressourcen umgestellt. Die Backup-Initialmeldung berücksichtigt nun die tatsächlich geladenen Sicherungen und meldet bei vorhandenem Bestand nicht mehr fälschlich, dass noch kein Backup erstellt worden sei. Netzwerk-Sync wurde nur angesehen; der Testdienst blieb gestoppt.

## Geänderte Dateien

- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml`
- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml.cs`

## Tests

- `git pull origin main`: erfolgreich, bereits aktuell.
- Ausgangs- und Abschlussprüfung mit `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `git diff --check`: erfolgreich.
- `./scripts/run-macos-bundle.sh Debug`: nach beiden Korrekturen erfolgreich gebaut, signiert und real geöffnet.
- Visueller macOS-Rundgang: Übersicht, Aufträge, Angebote, Material, Termine, Schreibtisch, Firma, Retouren, Einstellungen, Daten und Pfade sowie Sync sichtbar geprüft.
- Sidebar: Firma ausgewählt, aufgeklappt, Retouren ausgewählt, Firma erneut ausgewählt, wieder zugeklappt und nach Einstellungen erneut angewählt; die blaue Auswahl blieb korrekt eindeutig.
- Schreibtisch: Ziehen, Wichtig und Resize-Hinweis nach Neustart dunkel und lesbar auf dem gelben Notizzettel bestätigt.
- Daten und Pfade: nach Neustart korrekte Meldung `20 Backups verfügbar.` bei sichtbarer Backupliste bestätigt.
- Sync: Status `Testdienst gestoppt` sichtbar bestätigt; Start- oder Stop-Aktionen wurden nicht ausgelöst.
- `lsof -nP -iTCP -sTCP:LISTEN | rg 'BueroCockpit|dotnet'`: keine BüroCockpit- oder dotnet-Listener gefunden.
- Produktive Aufgaben, Kategorien, Anhänge, Backups und Sync-Daten wurden nicht bearbeitet.

## Ergebnis

Die zuvor offene visuelle Abnahme der Sidebar-Auswahl ist erfolgreich. Zwei reproduzierbare UI-Ungereimtheiten wurden behoben und in der neu gebauten App sichtbar bestätigt: der Notizzettel-Kontrast und die widersprüchliche Backup-Initialmeldung.

## Bekannte offene Punkte

- Der kompatible Light Mode wurde in diesem Lauf nicht umgeschaltet; die geänderten Desk-Ink-Ressourcen sind jedoch modusunabhängig definiert.

## Aktueller Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M docs/NEXT_TASK.md
?? docs/codex_journal/2026-07-14_01-44_visuelle-desktop-pruefung.md
```
