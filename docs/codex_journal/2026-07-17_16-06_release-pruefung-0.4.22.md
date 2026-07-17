# Codex-Journal: Release-Prüfung 0.4.22

Datum: 2026-07-17 16:06 +0200

Ausgangsstand: `4209cbb6f9a0db076086434a1f7b76d5d444d601`

Arbeitsbranch der Fehlerkorrektur: `codex/work`

## Auftrag

Die nach Veröffentlichung von `0.4.21` behobene Absturzursache bei nicht
verfügbaren Cloud-Miniaturen soll als neues vollständiges
Windows-/Velopack-Auto-Update veröffentlicht werden. Die nächste freie
Patchversion ist `0.4.22`.

## Verbindliche Konsistenzprüfung vor Versionsänderung

Vollständig gegeneinander geprüft wurden `AGENTS.md`,
`docs/CODEX_PROJEKTREGELN.md`, `docs/CODEX_AUFTRAGSPRUEFUNG.md`,
`docs/TESTRICHTLINIEN.md`, `docs/DESIGN_RICHTLINIEN.md`,
`docs/RELEASE_PROZESS.md`, `docs/PROJEKTSTATUS.md`, `docs/NEXT_TASK.md` und
die tatsächliche Implementierung einschließlich `UpdateService`,
`ThumbnailBitmapCache`, `scripts/release.sh` und
`scripts/check-release-artifacts.sh`.

- Der Nutzer hat Version, Tag und vollständigen Auto-Update-Release
  ausdrücklich freigegeben.
- `AGENTS.md` und `docs/RELEASE_PROZESS.md` verlangen übereinstimmend frische
  Velopack-Artefakte, Windows-ZIP, Commit, Tag, Push, GitHub Release und
  abschließendes `gh release view`.
- Der Standard-Updatekanal zeigt weiterhin auf
  `https://github.com/chris148ps/BueroCockpit`.
- Der von Velopack erzeugte `BueroCockpit-win-x64-Setup.exe` bleibt
  verpflichtend. Der separate Inno-Installer bleibt optional und wird nicht
  als Auto-Update-Artefakt hochgeladen.
- Die Fehlerkorrektur verändert keine Fachlogik, Navigation, Datenmodelle,
  produktiven Daten oder Sync-Wege und steht nicht im Widerspruch zu den
  Designrichtlinien.
- Die Korrektur wurde im echten macOS-Bundle mit isolierten Daten vor und nach
  der Änderung reproduzierbar geprüft. Workflow-Test, Standardbuild,
  Windows-x64- und macOS-arm64-Build waren erfolgreich.
- Die noch offene physische Windows-/iPad-Zielgeräte-Abnahme bleibt offen und
  wird nicht als Releaseprüfung ausgegeben.
- `docs/NEXT_TASK.md` wird auf die neue Zielversion `0.4.22` aktualisiert.
- Es besteht kein ungeklärter Regel-, Fach-, Design-, Sicherheits- oder
  Releasewiderspruch.

Damit ist die Release-Prüfung erfolgreich. Erst anschließend dürfen
Fehlerkorrektur und Dokumentation übernommen, Version `0.4.22` gesetzt und
frische Artefakte erzeugt werden.
