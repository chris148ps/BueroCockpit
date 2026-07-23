# Projektregister BüroCockpit

Diese Datei enthält ausschließlich dauerhafte technische und organisatorische Entscheidungen.

## Datenhaltung

- Jeder Desktop verwendet einen lokalen Betriebssystem-Datenordner.
- Windows verwendet `%LOCALAPPDATA%\BueroCockpit`, macOS
  `~/Library/Application Support/BueroCockpit`.
- Die Windows-Velopack-Installation verwendet den getrennten `packId`
  `BueroCockpitApp` und liegt standardmäßig unter
  `%LOCALAPPDATA%\BueroCockpitApp`. Installation, Update und Deinstallation
  dürfen `%LOCALAPPDATA%\BueroCockpit` niemals als Installationswurzel
  verwenden.
- Frühere Storage-Location-, OneDrive- und Live-Dateipfade dürfen keinen
  produktiven Datenpfad bestimmen und werden nicht automatisch zwischen
  Plattformen umgeschrieben oder migriert.
- GitHub dient ausschließlich der Quellcodeverwaltung.
- Der Datenaustausch zwischen Desktop-Geräten erfolgt manuell über vollständige Backup-/Import-ZIP-Dateien.
- Ein OneDrive-Ordner darf als Austauschablage für vollständig geschlossene ZIP-Dateien dienen.
- Es gibt keine automatische Desktop-Datenbank-Synchronisation oder Zusammenführung.
- `live.bclive` ist kein Desktop-Transport. Die iPad-Verbindung verwendet den
  bewusst gestarteten lokalen Netzwerk-Sync.

## Arbeitsweise

- Kleine Dokumentations-, Terminal-, Git- und Patchaufgaben werden möglichst direkt erledigt.
- Codex wird für größere zusammenhängende Implementierungen, Refactorings und Änderungen an vielen Dateien eingesetzt.
- Codex-Aufträge werden kompakt als Markdown-Dateien mit fortlaufender BC-ID verwaltet.
- Der reduzierte Standard-Lesestapel und die Regeln für gezieltes Lesen von
  Fachdateien, Auftragsarchiv und Journal stehen verbindlich in `AGENTS.md`.
- `PROJEKTREGISTER.md` ist die maßgebliche Quelle für dauerhafte
  Architekturentscheidungen; Status-, Auftrags- und Journaldateien beschreiben
  nur erreichten Stand oder historischen Verlauf.

## Abgrenzung

Aktueller Implementierungsstand gehört in `docs/PROJEKTSTATUS.md`.
Arbeitsregeln gehören in `AGENTS.md`.
Zukünftige Themen gehören in `docs/codex_auftraege/ROADMAP.md`.
