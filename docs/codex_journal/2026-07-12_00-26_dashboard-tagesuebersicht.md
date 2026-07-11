# Codex-Journal: Übersicht als zentrale Tagesarbeitsübersicht ausbauen

## Ziel

Die Startseite Übersicht mit vier ruhigen, realen und read-only Bereichen für Termine, Wiedervorlagen, Mobile Eingänge und Synchronisation ausbauen.

## Umsetzung

Die bestehende Dashboard-Abfrage wurde um eine Heute-Gruppe und ein aktuelles deutsches Datum ergänzt. Die Übersicht zeigt Termine für Heute, Diese Woche und Nächste Woche, heutige Wiedervorlagen mit Anzahl und roter Hervorhebung nur bei echten Treffern, neue Mobile-Inbox-Aufträge/Fotos/Skizzen aus vorhandenen Entry-Daten sowie einen read-only Desktop-/Netzwerk-Sync-Status. Fehlende Daten zeigen die angeforderten freundlichen Leerzustände; erfolgreiche Synchronisation wird nicht erfunden, wenn kein Zeitpunkt vorhanden ist.

Die Übersicht wurde in `MainWindow.axaml` auf ruhige, gleichartige Windows-11-Dark-Karten mit semantischen Ressourcen, konsistenten Abständen und einer resize-fähigen Grid-Struktur umgestellt. Kategorien, Aufträge, Datenmodell, Persistenz und Netzwerk-Sync wurden nicht verändert.

## Geänderte Dateien

- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml`
- `/Users/christian/AppProjekte/BueroCockpit/MainWindow.axaml.cs`
- `/Users/christian/AppProjekte/BueroCockpit/docs/PROJEKTSTATUS.md`
- automatisch erzeugte Laufdokumentation unter `/Users/christian/AppProjekte/BueroCockpit/docs/codex_journal/`, `/Users/christian/AppProjekte/BueroCockpit/docs/codex_last_run.md` und `/Users/christian/AppProjekte/BueroCockpit/docs/NEXT_TASK.md`

## Tests

- `git diff --check`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- Realer Desktop-Start über `./scripts/run-macos-bundle.sh Debug`: erfolgreich.
- Übersicht geöffnet: Überschrift, „Heute“ und aktuelles Datum sichtbar.
- Termine geprüft: Heute, Diese Woche und Nächste Woche sichtbar; vorhandene Termine werden angezeigt; Heute zeigt bei fehlenden Daten „Keine Termine vorhanden.“.
- Wiedervorlagen geprüft: Anzahl oben und „Keine Wiedervorlagen.“ bei fehlenden heutigen Wiedervorlagen; rote Hervorhebung bleibt an echte Treffer gebunden.
- Mobile Eingänge geprüft: „Keine neuen mobilen Eingänge.“ bei fehlenden neuen Einträgen; Zählungen sind an reale Mobile-Inbox-Daten gebunden.
- Synchronisation geprüft: Desktop „verbunden“, Netzwerk-Sync „inaktiv“, „Noch keine erfolgreiche Synchronisation.“ und „Kein mobiles Gerät verbunden.“ ohne erfundene Daten.
- Vorhandene Daten geprüft: reale Termine aus dem Bestand sichtbar.
- Fenster verkleinert: alle vier Bereiche und Leerzustände sichtbar.
- Fenster vergrößert: alle vier Bereiche weiterhin sichtbar.
- Keine Produktivdaten, Kategorien, Aufträge oder Sync-Dienste verändert/gestartet.

## Ergebnis

Die Startseite Übersicht ist eine schlichte zentrale Tagesarbeitsübersicht mit ausschließlich den angeforderten Informationen und realen Daten.

## Bekannte offene Punkte

- Keine fachlichen offenen Punkte aus diesem Auftrag.
- Der bestehende Draft-PR #1 bleibt der einzige PR und wird über den vorgeschriebenen Helper aktualisiert.

## Aktueller Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
?? docs/codex_journal/2026-07-12_00-26_dashboard-tagesuebersicht.md
```
