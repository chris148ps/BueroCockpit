# Lokale Produktivdaten und manueller Backup-Austausch

## Auftrag

Die direkte Nutzung einer produktiven SQLite-Datenbank in OneDrive beenden.
Jeder Desktop soll ausschließlich lokal arbeiten und vollständige Datenstände
nur über bewusst exportierte und importierte ZIP-Archive austauschen.

## Regelprüfung und Freigabe

`DATENORDNER.md`, `SETTINGS_KONZEPT.md` und `IPAD_SYNC_KONZEPT.md` enthielten
noch Aussagen zu einem gemeinsamen oder frei wählbaren Datenordner. Der Auftrag
wurde deshalb vor Änderungen gestoppt. Nach ausdrücklicher Zustimmung wurden
diese Dokumente auf den neuen lokalen Grundsatz angepasst.

## Umsetzung

- `AppPaths` verwendet unter Windows `LocalApplicationData` und unter macOS
  `ApplicationData`; `storage-location*.json` wird nicht mehr gelesen.
- Die frühere OneDrive-Erkennung, Pfadübersetzung, Datenordnerauswahl und das
  Migrationsskript wurden aus dem aktiven Weg entfernt.
- `StorageLocationService` blockiert Symlink- oder Junction-Umleitungen vor dem
  Datenbankzugriff. Die App zeigt dabei Sollpfad und Ziel, verändert aber nichts.
- Ohne lokale Datenbank bietet die App beim Start sichtbar den bewussten
  Backup-Import oder das ausdrückliche leere lokale Beginnen an.
- `BackupService` sichert SQLite nun über `BackupDatabase`.
- `BackupExchangeService` erzeugt vollständige ZIPs mit Manifest, SHA-256,
  Abstammung und atomarer Veröffentlichung im Austauschordner.
- Der Import prüft Archivpfade, Manifest, vollständige Dateiliste, Hashes,
  Datenbankgröße, `integrity_check` und Schema, erstellt ein vollständiges
  lokales Rückfall-ZIP und aktiviert den Stand mit Rückstellung bei Fehlern.
- Lokaler Zustand und Journal liegen im gerätelokalen Konfigurationspfad und
  werden niemals exportiert.
- Unter Windows teilen lokale Konfiguration und Produktivdaten denselben
  OS-Stammordner; Gerätelokaldateien werden deshalb ausdrücklich vom Export
  ausgeschlossen und beim Import erhalten.
- Die Einstellungen erklären den sequenziellen Bedienablauf und bieten
  Auswahl, Öffnen, Export und Import des Austauschordners. Konflikte verlangen
  eine zusätzliche eindeutige Bestätigung.
- Die bestehende lokale Backup-Liste und Wiederherstellung bleiben getrennt
  erhalten.

## Reale Pfadfeststellung

Der aktuelle Pfad
`~/Library/Application Support/BueroCockpit` ist ein Symlink auf
`~/Library/CloudStorage/OneDrive-ElektroSchweim/Dokumente/BueroCockpit_gemeinsame_Daten`.
Weder Symlink noch Ziel wurden verändert. Die neue Startprüfung blockiert
diesen Zustand, bis der Sollpfad bewusst lokal eingerichtet wurde.

## Prüfung

- Neue automatisierte Backup-Austauschtests: vollständig erfolgreich.
- Bestehende Workflow-/Kategorie-/Netzwerk-Integrationstests: vollständig
  erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r win-x64`: erfolgreich, 0 Warnungen, 0 Fehler.
- Ausstehend: sichtbarer Zwei-Geräte-Desktop-Test.
- Eine ausschließlich mit `/private/tmp`-Daten gestartete macOS-Testinstanz
  konnte wegen des gesperrten Macs nicht über die Bedienhilfe bedient werden
  und wurde wieder beendet.

## Git

Kein Commit, Push, Tag, Versionswechsel oder Release.
