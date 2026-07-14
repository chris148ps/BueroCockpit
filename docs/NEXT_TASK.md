# Nächste Aufgabe

## Ziel

Den vollständigen sichtbaren Desktop-Funktionstest auf einer entsperrten macOS-Sitzung mit einem isolierten Testprofil abschließen.

## Geplante Schritte

1. Vor dem Start die fremde lokale Änderung an `scripts/run-macos-bundle.sh` klären oder einen nachweislich isolierten Bundle-Start verwenden, ohne den fremden Diff zu überschreiben.
2. Angebots- und Direktauftragsstatus, vollständige Kategorieauswahl, Kategorie-Badges, Übersichtsnavigation, echten Vorgangs- und Kategorie-Maus-Drag sowie die Neustartpersistenz sichtbar prüfen.
3. Sidebar vollständig per Pfeiltasten und Enter bedienen; Entf im normalen Vorgangskontext bestätigen und in Text-, Auswahl-, Datums- und Zahlenfeldern als wirkungslos prüfen.
4. Den vollständigen Rundgang nach `docs/TESTRICHTLINIEN.md` einschließlich Kontextmenüs, Dialogen, Leerzuständen, Archiv, Papierkorb, Backup, Diagnose und manuellem Testdienst real abschließen.

## Vermutlich betroffene Dateien

- keine Codeänderung bei erfolgreicher Abnahme
- bei einem reproduzierbaren Fehler nur der unmittelbar betroffene Code und die Abschlussdokumentation

## Bereiche, die nicht verändert werden dürfen

- Produktive Daten, Anhänge, Cloud-Dateien, iPad-Code, Netzwerk-/Sync-Architektur, Version, Release, Tags und `main`.
