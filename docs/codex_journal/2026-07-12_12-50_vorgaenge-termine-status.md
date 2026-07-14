# Codex-Journal: Vorgangsstatus, Terminansicht und Monteurzuordnung vervollständigen

## Ziel

Die kompakte Auftrags-, Angebots- und Terminansicht um den vollständigen Angebotsstatus, die chronologische Terminprojektion, die neutrale Monteurauswahl und dauerhafte Designvorgaben ergänzen.

## Umsetzung

Der Angebotsablauf enthält nun Ansicht, Angebot, Angebot gesendet, Auftrag, Material, Termin und Erledigt. Alte Bestandskategorien mit `gesendet` werden defensiv als Angebot gesendet abgeleitet. Neue und kopierte Vorgänge starten mit dem Direktauftragsstatus Auftrag. Listen- und Termin-Badges verwenden WorkflowStep als sichtbare Statusquelle. Die Termin-Navigation projiziert reale Termine dedupliziert und chronologisch und bietet Alle, Vergangen, Heute und Zukünftig. Die Detailauswahl enthält Kein Monteur als UI-Sentinel; gespeichert wird dafür eine leere Technikerzuordnung, ohne ein Profil anzulegen. Fehlende Techniker erscheinen neutral als —. Titel kann über das Tabellenkopf-Kontextmenü ein- und ausgeblendet werden; Standard wiederherstellen ist vorhanden. Die Designrichtlinie wurde um die neuen dauerhaften Vorgaben ergänzt.

## Geänderte Dateien

- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml`
- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml.cs`
- `/Users/christian/AppProjekte/BueroCockpit/Models/TaskItem.cs`
- `/Users/christian/AppProjekte/BueroCockpit/Services/AppSettingsService.cs`
- `/Users/christian/AppProjekte/BueroCockpit/docs/DESIGN_RICHTLINIEN.md`

## Tests

- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `git diff --check`: erfolgreich.
- macOS-Bundle-Erzeugung über `./scripts/run-macos-bundle.sh Debug`: erfolgreich.
- Sichtprüfung vor der aktuellen Mac-Sperre: Übersicht startet ohne Splitter; Hauptnavigation und kompakte Auftragsliste mit Status, Kunde, Ort, Termin und Techniker sichtbar; Detailkopf zeigt Kundenname; Splitter sichtbar.
- Sichtprüfung Terminansicht: Zeitraum Alle sichtbar, reale Termine chronologisch und ohne doppelte Task-IDs sichtbar; Spalten Datum, Uhrzeit, Status, Kunde, Ort, Techniker sichtbar; fehlender Monteur zeigt —.
- Sichtprüfung Filtermenü: Alle, Vergangen, Heute und Zukünftig werden angeboten.
- Mutierende Status- und Monteur-Neustartprobe auf bestehenden Produktivdaten nicht ausgeführt, um Bestandsdaten unverändert zu lassen.
- Weitere Computer-Use-Aktionen wurden durch die automatische macOS-Sperre verhindert.

## Ergebnis

Status- und Terminprojektion sowie die neutrale Monteurzuordnung sind kompatibel ergänzt. Die Anwendung bleibt kompilierbar und der bestehende Datenbestand wird nicht destruktiv migriert.

## Bekannte offene Punkte

- Vollständige per-Maus-Spaltenbreitenänderung, frei speicherbare Spaltenreihenfolge und getrennte Breitenmodelle für Aufträge, Angebote und Termine sind noch nicht vollständig umgesetzt.
- Die mutierende End-to-End-Prüfung von Status- und Monteurspeicherung nach Neustart steht wegen der Sicherheitsgrenze für Produktivdaten aus.
- Die visuelle Abnahme kleiner und großer Fenster war wegen der gesperrten macOS-Sitzung nicht erneut möglich.

## Aktueller Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M Models/TaskItem.cs
 M Services/AppSettingsService.cs
 M docs/DESIGN_RICHTLINIEN.md
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
?? docs/codex_journal/2026-07-12_12-50_vorgaenge-termine-status.md
```
