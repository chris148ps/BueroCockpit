# Codex-Journal: Standard-Techniker vollständig entfernen

## Ziel

Alle Technikerprofile sollen gleichberechtigt bearbeitbar und löschbar sein; es darf keinen Standard-Techniker geben.

## Umsetzung

- Standard-Kennzeichnung, Löschsperre und UI-Text entfernt.
- Alte isStandard-Werte werden beim Lesen toleriert und bei einem späteren Speichern nicht erneut ausgegeben.
- Jede Technikerzeile zeigt das gleiche transparente Lösch-X mit Tooltip.

## Geänderte Dateien

- MainWindow.axaml
- MainWindow.axaml.cs
- Services/LiveSettingsService.cs
- docs/PROJEKTSTATUS.md
- docs/codex_last_run.md
- docs/NEXT_TASK.md
- docs/codex_journal/

## Tests

- git diff --check erfolgreich.
- dotnet build erfolgreich.
- Reale Start- und Sichtprüfung der Technikerliste erfolgreich; jede sichtbare Zeile enthält ein Lösch-X ohne Standardtext.

## Ergebnis

Technikerprofile sind nun vollständig gleichberechtigt. Bestehende Profil- und Namensdaten bleiben kompatibel.

## Bekannte offene Punkte

- Produktive Technikerprofile wurden nicht verändert oder gelöscht.
- Der echte lokale Netzwerk-Sync bleibt unverändert vorbereitet und deaktiviert.

## Aktueller Git-Status

```text
 M MainWindow.axaml
 M MainWindow.axaml.cs
 M Services/LiveSettingsService.cs
 M docs/NEXT_TASK.md
 M docs/PROJEKTSTATUS.md
?? docs/codex_journal/2026-07-11_21-29_standard-techniker-entfernt.md
```
