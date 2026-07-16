# Release-Prüfung 0.4.20

## Auftrag und Freigabe

Der Nutzer hat nach dem gemeldeten Vorprüfungsstopp ausdrücklich die Erstellung
eines vollständigen Auto-Update-Releases freigegeben. Ziel ist die Installation
auf dem Firmenrechner über die bestehende Windows-Auto-Update-Funktion.

## Geprüfte aktuelle Regel- und Fachdateien

- `AGENTS.md`
- `docs/CODEX_PROJEKTREGELN.md`
- `docs/CODEX_AUFTRAGSPRUEFUNG.md`
- `docs/ARBEITSKATEGORIEN.md`
- `docs/DESIGN_RICHTLINIEN.md`
- `docs/PROJEKTSTATUS.md`
- `docs/TESTRICHTLINIEN.md`
- `docs/RELEASE_PROZESS.md`
- `docs/NEXT_TASK.md`
- `docs/codex_last_run.md`

## Ergebnis der Konsistenzprüfung

- Die Fachregeln zu genau einem Vorgangstyp, einem Workflowstatus und einer
  normalen Kategorie stimmen mit der Implementierung und den automatisierten
  Tests überein.
- Die Designrichtlinien stimmen mit der sichtbaren macOS-Prüfung von
  Navigation, Wiedervorlage, Detailkopf, Bereichsreihenfolge und Stepper
  überein.
- `AGENTS.md` und `docs/RELEASE_PROZESS.md` verlangen übereinstimmend frische
  Windows-/Velopack-Artefakte, Release-Commit auf `main`, Tag, Push, GitHub
  Release, Upload aller Pflichtdateien und abschließendes `gh release view`.
- `scripts/check-release-artifacts.sh` behandelte den laut Releaseprozess
  optionalen Inno-Installer noch als Pflichtdatei. Der Prüfer wurde vor der
  Artefakterzeugung minimal korrigiert; die Velopack-Pflichtdateien bleiben
  unverändert zwingend.
- Eine veraltete Aussage in `docs/PROJEKTSTATUS.md` schrieb dem letzten Lauf
  einen sichtbaren Kategoriezeilen-Drag zu, obwohl `docs/codex_last_run.md`
  diese Geste ausdrücklich als nicht zuverlässig geprüft ausweist. Die
  Statusdokumentation wurde auf den tatsächlichen Stand korrigiert.
- Die reale Windows-Bedienung und die praktische Installation des Auto-Updates
  sind noch nicht ausgeführt. Aufgrund der ausdrücklichen Releasefreigabe werden
  sie als nachgelagerte Abnahme auf dem Firmenrechner geführt und nicht als
  bereits bestanden behauptet.

Nach dieser Korrektur besteht kein ungeklärter Widerspruch zwischen den aktiven
Regeln, der Statusdokumentation, dem Releaseprozess und dem tatsächlich
geprüften App-Stand. Version, Artefakte, Tag und Upload dürfen erst nach
Übernahme des geprüften Arbeitsstands nach `main` erzeugt werden.

## Sicherheitsgrenzen

- Keine Produktivdaten, Kategorien, Anhänge, Backups oder Cloud-Dateien ändern.
- Keine Netzwerk-/Sync-Erweiterung aktivieren.
- Keine früheren Release-Artefakte wiederverwenden.
- Nur den ausdrücklich freigegebenen Release nach `main`, als Tag und als
  GitHub Release veröffentlichen.
