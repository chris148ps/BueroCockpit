# BüroCockpit – verbindliche Fachlogik für Kategorien und Statuszuordnungen

Diese Datei ist die verbindliche Fachquelle für Vorgangstyp, Workflowstatus,
benutzerdefinierte Kategorien und automatische Statuszuordnungen. Sie ist vor
jeder Änderung an Vorgängen, Kategorien, Navigation, Suche, Filtern, Import,
Export oder Sync vollständig zu lesen.

## Begriffe

Jeder Vorgang besitzt fachlich genau:

- einen Vorgangstyp,
- einen Workflowstatus,
- genau eine aktuell zugeordnete Kategorie.

Die Kategorien in der linken Navigation sind benutzerdefiniert. Der Endbenutzer
darf sie frei anlegen, umbenennen, verschieben, verschachteln und löschen,
sofern keine Sicherheits- oder Systemregel entgegensteht.

Fest eingebaute Navigationsziele sind ausschließlich technische Systemansichten:

- Übersicht,
- Alle Vorgänge,
- Papierkorb,
- Einstellungen,
- das unter Einstellungen geführte Archiv,
- sowie ein technisch erforderlicher mobiler Eingang.

Angebote, Aufträge, Material und Termine sind keine fest eingebauten
Systemansichten. Solche Arbeitsbereiche entstehen ausschließlich durch frei
verwaltete normale Kategorien. Vorgangstyp oder Workflowstatus erzeugen keine
zusätzliche parallele Sammelansicht.

Die automatische Zuordnung erfolgt nicht über Kategorienamen, sondern über die
stabile Kategorie-ID. Dadurch bleibt eine Statuszuordnung erhalten, wenn eine
Kategorie umbenannt oder innerhalb der Hierarchie verschoben wird.

Ein Vorgang darf in der Oberfläche niemals gleichzeitig in mehreren normalen
Kategorien erscheinen.

## Automatische Statuszuordnung

Für jede zulässige Kombination aus Vorgangstyp und Workflowstatus kann der
Benutzer genau eine vorhandene Kategorie-ID als Ziel festlegen.

Beispiele für mögliche, aber nicht fest vorgeschriebene Zuordnungen:

| Vorgangstyp | Workflowstatus | mögliche Zielkategorie |
|---|---|---|
| Angebotsvorgang | Ansicht | Angebote |
| Angebotsvorgang | Angebot | Angebote |
| Angebotsvorgang | Angebot gesendet | Angebote / Gesendet |
| Angebotsvorgang | Auftrag | Angebote / Beauftragt |
| Angebotsvorgang | Material | Material |
| Angebotsvorgang | Termin | Termine |
| Angebotsvorgang | Erledigt | Erledigt |
| Direktauftrag | Auftrag | Aufträge |
| Direktauftrag | Material | Material |
| Direktauftrag | Termin | Termine |
| Direktauftrag | Erledigt | Erledigt |

Die Namen in dieser Tabelle sind nur Beispiele. Verbindlich ist die Zuordnung
auf eine stabile Kategorie-ID, nicht ein bestimmter Kategoriename.

Bei einem Statuswechsel wird der Vorgang automatisch in die für Vorgangstyp und
Workflowstatus konfigurierte Kategorie verschoben.

Ist für die neue Kombination keine Zielkategorie konfiguriert, darf keine
beliebige Ersatzkategorie gewählt werden. Der Benutzer muss eine gültige
Zuordnung festlegen; Neuanlage, Typwechsel oder Statuswechsel werden bis dahin
mit einem klaren Hinweis blockiert.

## Vorgangstyp

Beim Anlegen eines neuen Vorgangs muss der Benutzer wählen:

- Angebotsvorgang
- Direktauftrag

Empfohlene Anfangsstatus:

- Angebotsvorgang → `Angebot`
- Direktauftrag → `Auftrag`

Der Vorgangstyp muss später bewusst änderbar sein. Vor der Änderung ist eine
Bestätigung erforderlich.

Beim Wechsel bleiben Kunde, Beschreibung, Anhänge, Material, Termine,
Techniker, Wiedervorlagen und sonstige Vorgangsdaten erhalten.

Existiert der aktuelle Workflowstatus im neuen Vorgangstyp, bleibt er erhalten.
Ist er nicht zulässig, muss der Benutzer einen passenden Zielstatus auswählen.
Es darf keine stille Typ- oder Statusänderung geben.

## Kategorieverwaltung

Der Endbenutzer darf normale Kategorien und Unterkategorien frei:

- anlegen,
- umbenennen,
- verschieben,
- verschachteln,
- in der Reihenfolge ändern,
- löschen.

Die oben genannten technischen Systemansichten sind keine normalen Kategorien
und nicht frei löschbar. Jede normale Kategorie muss dagegen unabhängig von
Name und Hierarchiestufe in der Kategorieverwaltung vollständig verwaltbar sein.

Wird eine Kategorie gelöscht, die in einer automatischen Statuszuordnung
verwendet wird, muss die App vor dem Löschen warnen. Der Benutzer muss entweder:

- eine Ersatzkategorie auswählen,
- die betroffenen Statuszuordnungen bewusst entfernen,
- oder den Löschvorgang abbrechen.

Vorgänge dürfen beim Löschen einer zugeordneten Kategorie nicht still in eine
andere Kategorie verschoben werden.

## Drag & Drop

Ein Vorgang darf per Drag & Drop in eine andere normale Kategorie verschoben
werden. Dadurch ändert sich nur seine aktuelle Kategorie, nicht automatisch
Vorgangstyp oder Workflowstatus.

Beim nächsten bewussten Statuswechsel greift wieder die konfigurierte
automatische Statuszuordnung.

Drops auf Systemansichten dürfen keine stille Typ-, Status- oder
Kategorieänderung auslösen.

## Variante A für bestehende Daten

- Es findet keine automatische oder massenhafte Migration von Produktivdaten
  statt.
- Die neue Logik gilt sofort für neue Vorgänge, bewusst geänderte Vorgänge,
  bewusst geänderte Vorgangstypen und Workflowstatus.
- Bestehende unveränderte Datensätze werden beim Start, Laden oder Anzeigen
  nicht zurückgeschrieben, umsortiert oder neu zugeordnet.
- Alte Kategorie-IDs oder Mehrfachzuordnungen dürfen aus
  Rückwärtskompatibilitätsgründen tolerant gelesen werden.
- Sobald ein bestehender Vorgang bewusst geändert und gespeichert wird, darf
  für den neuen Stand nur noch genau eine normale Kategorie fortgeschrieben
  werden.
- Alte Mehrfachzuordnungen dürfen nicht still gelöscht werden. Die Umstellung
  muss kontrolliert und ohne Datenverlust erfolgen.

## Bedien- und Darstellungsregeln

- Navigation, Listen, Zähler, Suche, Übersicht und Detailansicht verwenden
  dieselbe aktuelle Kategorie-ID.
- `Alle Vorgänge` ist die einzige technische Sammelansicht für normale
  Vorgänge. Ein Treffer dort bleibt genau seiner einen normalen Kategorie
  zugeordnet.
- Workflowstatus und Kategorie bleiben als unterschiedliche Werte sichtbar.
- Die Kategorie wird als kompaktes neutrales Badge angezeigt.
- Der Status wird weiterhin als eigenes Status-Badge angezeigt.
- Statuszuordnungen werden in den Einstellungen über verständliche
  Kategoriepfade dargestellt, intern aber ausschließlich über stabile IDs
  gespeichert.
- Eine Umbenennung oder Verschiebung einer Kategorie darf die Zuordnung nicht
  zerstören.

## Aktueller Umsetzungsstand

Die Fachlogik ist in der Desktop-App umgesetzt:

- Statuszuordnungen liegen zentral in `buerocockpit.db` und speichern stabile
  Kategorie-IDs,
- neue Vorgänge verlangen die Typauswahl und eine gültige Anfangszuordnung,
- Typ- und Statuswechsel sind bestätigt beziehungsweise kontrolliert und
  übernehmen genau die konfigurierte Kategorie,
- manuelle Kategorieänderungen lassen Typ und Status unverändert,
- Kategorie-Löschungen mit Verwendungen verlangen Ersatz, bewusstes Entfernen
  oder Abbruch,
- neue und bewusst geänderte Vorgänge werden mit genau einer Kategorie
  fortgeschrieben,
- unveränderte Legacy-Datensätze werden gemäß Variante A tolerant gelesen und
  nicht automatisch migriert,
- der additive Snapshot-Export enthält Vorgangstyp, Workflowstatus und aktuelle
  Kategorie-ID.

Die isolierten Persistenz-, Legacy- und Exportprüfungen sind nachgewiesen. Ein
vollständiger realer Bedienrundgang bleibt vor einer Releasefreigabe weiterhin
Pflicht.
