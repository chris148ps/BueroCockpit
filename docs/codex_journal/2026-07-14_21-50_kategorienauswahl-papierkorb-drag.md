# Kategorienauswahl, Papierkorb und Kategorie-Drag korrigiert

## Anlass

Nach der Trennung von Vorgangstyp, Bearbeitungsstand und fachlicher Kategorie waren die vollständige Kategorienstruktur und die Einschränkung auf Endkategorien in der Detailauswahl nicht verständlich sichtbar. Außerdem stand der Papierkorb noch in der scrollenden Systemnavigation und das Sortieren des Kategorienbaums hatte keinen eindeutigen Drag-Griff.

## Änderungen

- Der Papierkorb wird zusammen mit den Einstellungen im festen Fußbereich geführt und steht dort direkt darüber.
- Die Detailauswahl zeigt die vollständige fachliche Kategorienstruktur. Hauptkategorien mit Unterkategorien sind sichtbar, aber gemäß Designrichtlinie deaktiviert; Endkategorien bleiben auswählbar.
- Ein kurzer Hinweis erklärt die Endkategorien-Regel.
- Die Kategorienverwaltung hat einen sichtbaren Drag-Griff zum Sortieren und Unterordnen.
- `Angebote` und `Aufträge` bleiben organisatorische Filter. Vorgänge werden weiterhin nur auf zulässige fachliche Endkategorien gezogen; ein Drop ändert weder Vorgangstyp noch Bearbeitungsstand.

## Prüfungen

- `git diff --check`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r win-x64`: erfolgreich, 0 Warnungen, 0 Fehler; keine reale Bedienung unter Windows.
- Isolierter Avalonia-Zustandslauf: vollständige Kategorienstruktur, 13 auswählbare Endkategorien sowie feste Fußreihenfolge `Papierkorb`, `Einstellungen` bestätigt.
- Isolierter Sortier- und Persistenztest: `bestellt` vor `bestellen` verschoben, gespeichert und aus SQLite neu gelesen; anschließend Ursprungsreihenfolge wiederhergestellt und erneut gespeichert.
- Isolierter Vorgangs-Drop-Pfad: fachliche Kategorie geändert; Vorgangstyp und Bearbeitungsstand blieben unverändert.
- Der echte Maus-Drag wurde in der weiterhin gesperrten macOS-Sitzung nicht real bedient.
- Die produktive Datenbank wurde nicht verwendet. Nach einer Diagnoseausgabe aus einer alten temporären Testkonfiguration wurde read-only geprüft, dass im produktiven Sync-Verzeichnis keine Datei des Testzeitraums verändert worden war; anschließend wurde die Testkonfiguration auf einen ausschließlich temporären Zielpfad korrigiert.

## Abgrenzung

Kein Release, Tag, Versionswechsel, Merge nach `main` sowie keine Änderung an Netzwerk-/Sync-Funktionen oder produktiven Daten.
