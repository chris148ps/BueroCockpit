# Letzter Codex-Lauf

## Datum/Uhrzeit

2026-07-16 01:59 +0200

## Letzter Auftrag

Feste Angebots-, Auftrags-, Material- und Terminansichten entfernen, freie Kategorien als einzige normale Arbeitsbereiche verwenden und Detail- sowie Wiedervorlagen-UI vereinheitlichen.

## Zusammenfassung

Normale Arbeitsbereiche sind jetzt ausschließlich frei verwaltete Kategorien. Die früheren festen Angebots-, Auftrags-, Material- und Terminansichten erzeugen weder zusätzliche Navigation noch parallele Treffer. Statuswechsel führen genau in die konfigurierte stabile Kategorie und erhalten die Auswahl. Tabellenlayout, Wiedervorlagen, Detailkopf, Bereichsreihenfolge und Workflow-Stepper entsprechen den aktualisierten Regeln. Die Tests liefen ausschließlich gegen temporäre Pfade; die produktive Standarddatenbank behielt vor und nach dem Test unverändert den Änderungszeitpunkt 16.07.2026 00:00:56. Produktive Kategorien, Anhänge, Backups, Cloud-Dateien, Netzwerkdienste, Version, Release und Tags wurden nicht verändert.

## Geänderte Dateien

- MainWindow.axaml
- MainWindow.axaml.cs
- Models/TaskItem.cs
- Models/WorkflowStepItem.cs
- Services/AppSettingsService.cs
- Services/NavigationCategoryPolicy.cs
- scripts/run-macos-bundle.sh
- tests/BueroCockpit.WorkflowTests/Program.cs
- docs/ARBEITSKATEGORIEN.md
- docs/CODEX_PROJEKTREGELN.md
- docs/DESIGN_RICHTLINIEN.md
- docs/PROJEKTSTATUS.md
- docs/TESTRICHTLINIEN.md
- docs/NEXT_TASK.md
- docs/codex_last_run.md
- neuer Eintrag unter docs/codex_journal/

## Tests

- Baseline `dotnet build` vor der Implementierung: erfolgreich, 0 Warnungen, 0 Fehler.
- Abschließendes `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- Abschließendes `dotnet build -r win-x64`: erfolgreich, 0 Warnungen, 0 Fehler; keine reale Windows-Bedienung.
- `dotnet run --project tests/BueroCockpit.WorkflowTests/BueroCockpit.WorkflowTests.csproj`: erfolgreich. Geprüft wurden stabile ID-Zuordnungen, Umbenennen/Verschieben, Ersatz/Entfernen/fehlende Ziele, Variant A, genau eine Kategorie nach bewusster Änderung, sämtliche Status beider Vorgangstypen, genau ein Treffer in der Gesamtmenge, technische Navigation, Legacy-IDs, Layout-Fallback ohne Migration, Stepper-Zustände, textliche Wiedervorlagenwarnung und Snapshot-Felder.
- `bash -n scripts/run-macos-bundle.sh`: erfolgreich.
- `bash ./scripts/run-macos-bundle.sh Debug` mit explizit isolierten Daten- und lokalen Konfigurationspfaden: Bundle erfolgreich gebaut, signiert und real gestartet; der zuvor reproduzierte leere-Array-Fehler trat nicht erneut auf.
- Sichtbarer macOS-Rundgang mit isolierten Daten: nur Übersicht, Alle Vorgänge, Schreibtisch sowie normale Benutzerkategorien in der oberen Navigation; Papierkorb direkt über Einstellungen; sämtliche normalen Haupt- und Unterkategorien in Verwaltung und Statusauswahl; Direktauftrag angelegt; Kategorie manuell von Material / bestellen nach Angebot / erstellen verschoben; Statuswechsel Auftrag/Material mit korrekter Zielnavigation, aufgeklapptem Unterpfad, Auswahl und offenem Detail; genau ein Vorgang und eine Kategoriezuordnung nach Neustart; Alle Vorgänge zeigt genau einen Treffer.
- Sichtbar geprüft: fester Detailkopf beim Scrollen, Reihenfolge Status/Aufgabe/Termine, Duplizieren außerhalb des Kopfes, verbundener Stepper einschließlich Automation-Texten sowie Dark- und Light-Darstellung.
- Sichtbar geprüft: überfällige Wiedervorlage mit neutraler Fläche, schmalem Warnrahmen und Text `Überfällig` in Dark und Light; Klick öffnet den richtigen Vorgang.
- Sichtbar geprüft: Testkategorien anlegen, umbenennen, geordnet schließen, nach Neustart wiederfinden, löschen, erneut starten und kein Wiedererzeugen. Der native Kategoriezeilen-Drag ließ sich über die macOS-Bedienhilfe in diesem Lauf nicht zuverlässig auslösen; Vorgangs-Drop, stabile Parent-/ID-Persistenz und der unveränderte Kategorie-DnD-Pfad wurden angrenzend geprüft, aber kein sichtbarer erfolgreicher Kategorie-Drag behauptet.
- Isolierte SQLite-Prüfung: `integrity_check = ok`, ein Testvorgang, genau eine `TaskCategories`-Zeile, keine sichtbare Codex-Testkategorie nach dem Löschen.
- Dokumentations-Konsistenzsuche nach festen Altansichten, Alt-IDs und `Alle Aufträge`: keine widersprüchliche aktive Regel gefunden; historische Journale blieben unverändert.
- `git diff --check`: erfolgreich.
- Kein `xcodebuild`, weil kein iPad-Code geändert wurde.

## Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M Models/TaskItem.cs
 M Models/WorkflowStepItem.cs
 M Services/AppSettingsService.cs
 M docs/ARBEITSKATEGORIEN.md
 M docs/CODEX_PROJEKTREGELN.md
 M docs/DESIGN_RICHTLINIEN.md
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
 M docs/TESTRICHTLINIEN.md
 M scripts/run-macos-bundle.sh
 M tests/BueroCockpit.WorkflowTests/Program.cs
?? Services/NavigationCategoryPolicy.cs
?? docs/codex_journal/2026-07-16_01-59_freie-kategorien-navigation-detail.md
```

## Branch
codex/work

## Commit
7c3dd75c2f819538ece4611378d6df4ba598ffe4

## Push erfolgreich
Ja

## Offene Punkte

- Die Windows-spezifische Bedienung wurde nicht real unter Windows geprüft; der erfolgreiche win-x64-Build ersetzt diese Abnahme nicht.
- Der native Kategoriezeilen-Drag sollte bei der nächsten realen Plattformabnahme nochmals manuell bestätigt werden, weil die macOS-Bedienhilfe die Drag-Geste in diesem Lauf nicht zuverlässig an Avalonia übergeben hat.

## Empfohlener nächster Schritt

Die neue Navigation, Kategorieverwaltung und Workflow-Detailansicht mit isolierten Daten real unter Windows abnehmen.

1. Temporäre Daten- und lokale Konfigurationspfade unter Windows setzen und die App sichtbar starten.
2. Beide Vorgangstypen und alle Statuswechsel, genau eine Kategorie, Alle Vorgänge, Suche, Zähler sowie verschachtelte Zielnavigation bedienen.
3. Kategoriezeilen-Drag, Vorgangs-Drop, Umbenennen, Löschen und Neustartpersistenz manuell prüfen.
4. Detailkopf, Stepper, Termine, Wiedervorlagen, Tabellenlayout sowie Dark und Light prüfen.
5. Angrenzende Desktopfunktionen einschließlich Papierkorb, Archiv, Schreibtisch, Anhänge, Backup und Diagnose regressiv bedienen und nur tatsächlich ausgeführte Ergebnisse dokumentieren.
