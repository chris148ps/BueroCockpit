# Codex-Journal: Sichtbare Desktop-Abnahme auf regulär startbarem macOS-App-Pfad

## Ziel

Die im vorherigen Auftrag umgesetzten Korrekturen sichtbar in einer regulär startbaren macOS-App prüfen und den Bundle-Startfehler minimal beheben.

## Umsetzung

Der reguläre Start über `bin/Debug/BueroCockpit.app` schlug zunächst mit `RBSRequestErrorDomain Code=5` und `NSPOSIXErrorDomain Code=162` fehl. Ursache war der unvollständige macOS-App-Wrapper des Laufskripts. `scripts/run-macos-bundle.sh` legt nun `Contents/Resources` an und signiert das erzeugte Bundle ad hoc mit `codesign --force --deep --sign -`. Produktivdaten, Kategorien, Aufträge und Backups wurden nicht verändert; eine Wiederherstellung wurde nicht ausgelöst.

## Geänderte Dateien

- `/Users/christian/AppProjekte/BueroCockpit/scripts/run-macos-bundle.sh`
- automatisch erzeugte Laufdokumentation unter `/Users/christian/AppProjekte/BueroCockpit/docs/codex_journal/`, `/Users/christian/AppProjekte/BueroCockpit/docs/codex_last_run.md` und `/Users/christian/AppProjekte/BueroCockpit/docs/NEXT_TASK.md`

## Tests

- Regulärer Bundle-Lauf `./scripts/run-macos-bundle.sh Debug`: erfolgreich; Publish ohne Warnungen/Fehler, ad-hoc-Signierung erfolgreich, `open` ohne RBS/launchd-Fehler.
- Sichtbarer vollständiger Start: Übersicht aktiv, Übersichtsinhalt sichtbar.
- Kategorie geöffnet, App vollständig beendet, Bundle neu gestartet: wieder Übersicht.
- Einstellungen geöffnet, App vollständig beendet, Bundle neu gestartet: wieder Übersicht.
- Darstellung: kein Backup-Bereich sichtbar.
- Daten & Pfade: vollständiger Backup-Bereich mit Erstellen-, Liste- und Wiederherstellen-Bedienelementen sichtbar; keine Wiederherstellung ausgelöst.
- Kategorienverwaltung: kein leerer Eintrag; Übersicht in der Navigation funktionsfähig; vorhandene gültige Kategorien und Unterkategorien sichtbar.
- Whitespace-Kategoriename im Eingabefeld ohne Speichern hinzugefügt: defensiv mit `Bitte einen Kategorienamen eingeben.` abgewiesen.
- Bestehende Auftragszuordnungen read-only stichprobenartig über die Unterkategorie `terminiert` geprüft; Auftragskarten mit Status und Monteur sichtbar.
- `git diff --check`: erfolgreich.
- `dotnet build`: nach dem Fix erfolgreich, 0 Warnungen, 0 Fehler.

## Ergebnis

Die sichtbare Desktop-Abnahme ist erfolgreich. Der reguläre macOS-App-Pfad ist startfähig; die drei Korrekturbereiche verhalten sich wie erwartet.

## Bekannte offene Punkte

- Keine fachlichen offenen Punkte aus dieser Abnahme.
- Der bestehende Draft-PR #1 bleibt der einzige PR und muss weiterhin manuell geprüft bzw. freigegeben werden.

## Aktueller Git-Status

```text
 M docs/NEXT_TASK.md
 M scripts/run-macos-bundle.sh
?? docs/codex_journal/2026-07-11_23-44_desktop-abnahme-macos.md
```
