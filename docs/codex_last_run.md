# Letzter Codex-Lauf

## Datum/Uhrzeit

2026-07-12 12:32 +0200

## Letzter Auftrag

Kompakte Vorgangsansicht mit verschiebbarem Detailbereich und zentralem Status

## Zusammenfassung

Die kompakte Auftragsliste und der rechte Inspektor entsprechen der Referenzstruktur. Die Detailbreite ist lokal vorbereitet, Statuswerte sind workflowbasiert und Bestandsdaten werden ohne aggressive Migration geladen.

## Geänderte Dateien

- `/Users/christian/AppProjekte/BueroCockpit/Data/BueroRepository.cs`
- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml`
- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml.cs`
- `/Users/christian/AppProjekte/BueroCockpit/Models/TaskItem.cs`
- `/Users/christian/AppProjekte/BueroCockpit/Models/WorkflowStepItem.cs`
- `/Users/christian/AppProjekte/BueroCockpit/Services/AppSettingsService.cs`
- `/Users/christian/AppProjekte/BueroCockpit/docs/CODEX_PROJEKTREGELN.md`
- `/Users/christian/AppProjekte/BueroCockpit/docs/DESIGN_RICHTLINIEN.md`

## Tests

- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `git diff --check`: erfolgreich.
- Realer macOS-Bundle-Start über `./scripts/run-macos-bundle.sh Debug`: erfolgreich; frische App-Instanz sichtbar gestartet.
- Sichtprüfung Übersicht: Übersicht bleibt Startansicht.
- Sichtprüfung Hauptnavigation: grobe Bereiche sind sichtbar und Benutzer-Unterkategorien werden nicht als tägliche Hauptnavigation angezeigt.
- Sichtprüfung Auftragsliste: Status, Kunde, Ort, Termin und Techniker sind sichtbar; keine technische Auftragsnummer in der Liste.
- Sichtprüfung Detailbereich: Kundenname ist primärer Detailkopf; Ablauf und Statusauswahl sind sichtbar.
- Sichtprüfung Splitter: sichtbarer Handle zwischen Liste und Detailbereich; beide Richtungen per Maus angesteuert; Mindestbreiten sind im Layout definiert.
- Statusänderung und Neustart nicht auf einem bestehenden Produktivauftrag ausgeführt, um keine Bestandsdaten zu verändern; Speicherpfad und identische Bindungsquelle sind statisch geprüft.

## Git-Status

```text
 M Data/BueroRepository.cs
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M Models/TaskItem.cs
 M Services/AppSettingsService.cs
 M docs/CODEX_PROJEKTREGELN.md
 M docs/DESIGN_RICHTLINIEN.md
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
?? Models/WorkflowStepItem.cs
?? docs/codex_journal/2026-07-12_12-32_kompakte-vorgaenge.md
```

## Branch
codex/work

## Commit
94918ad518693be451f4ddedfb2a46b6bfa69745

## Push erfolgreich
Ja

## Offene Punkte

- Eine mutierende Statusprobe mit anschließendem Neustart wurde aus Sicherheitsgründen nicht auf einem bestehenden Produktivauftrag ausgeführt.
- Die bestehende Desktop-Computer-Use-Umgebung erlaubt die sichtbare Prüfung, aber die exakte gespeicherte Pixelbreite ist nur über den lokalen Einstellungsweg verifizierbar.

## Empfohlener nächster Schritt

Terminansicht als eigene chronologische Vorgangsliste mit den vier Zeitfiltern fertigstellen.

1. Reale Auftrags- und Angebotstermine dedupliziert chronologisch projizieren.
2. Filter Alle, Vergangen, Heute und Zukünftig ergänzen.
3. Klicknavigation zum Originalvorgang und kompakte Leerzustände prüfen.
