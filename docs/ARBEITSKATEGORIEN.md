# BüroCockpit – verbindliche Fachlogik für Arbeitskategorien

Diese Datei ist die verbindliche Fachquelle für Vorgangstyp, Workflowstatus,
sichtbare Arbeitskategorie und davon getrennte Kennzeichnungen. Sie ist vor
jeder Änderung an Vorgängen, Kategorien, Navigation, Suche, Filtern, Import,
Export oder Sync vollständig zu lesen.

## Begriffe

Jeder Vorgang besitzt fachlich genau:

- einen Vorgangstyp,
- einen Workflowstatus,
- eine daraus automatisch abgeleitete sichtbare Arbeitskategorie.

Die sichtbare Arbeitskategorie ist keine frei wählbare zweite Zuordnung. Sie
wird ausschließlich aus Vorgangstyp und Workflowstatus bestimmt. Ein Vorgang
darf in der Oberfläche niemals gleichzeitig in mehreren sichtbaren
Arbeitskategorien erscheinen.

Kennzeichnungen sind davon getrennte Metadaten. `SH-Netz`, `Retouren`, `Lager`,
`Marktstammdatenregister` und `Warten auf Kunde` sind keine
Arbeitskategorien. Sie werden in einem eigenen Bereich geführt und können zum
Beispiel als kompakte Badges sichtbar sein. Eine Kennzeichnung darf die
Arbeitskategorie weder ersetzen noch zusätzlich als zweite Arbeitskategorie
erscheinen lassen.

## Verbindliche Ableitung

| Vorgangstyp | Workflowstatus | sichtbare Arbeitskategorie |
|---|---|---|
| Angebotsvorgang | Ansicht | Angebote |
| Angebotsvorgang | Angebot | Angebote |
| Angebotsvorgang | Angebot gesendet | Angebote gesendet |
| Angebotsvorgang | Auftrag | Angebotsaufträge |
| Angebotsvorgang | Material | Material |
| Angebotsvorgang | Termin | Termin |
| Angebotsvorgang | Erledigt | Erledigt |
| Direktauftrag | Auftrag | Aufträge |
| Direktauftrag | Material | Material |
| Direktauftrag | Termin | Termin |
| Direktauftrag | Erledigt | Erledigt |

Der vorhandene Angebotsstatus `Ansicht` bleibt als kompatibler Status erhalten
und wird der Arbeitskategorie `Angebote` zugeordnet. Unbekannte Altstatuswerte
dürfen nicht still umgeschrieben werden. Sie müssen als Legacy-Zustand sichtbar
und kontrolliert behandelbar bleiben, bis eine ausdrücklich freigegebene
Zuordnung festgelegt ist.

## Variante A für bestehende Daten

- Es findet keine automatische oder massenhafte Migration von Produktivdaten
  statt.
- Die neue Logik gilt sofort für neue Vorgänge, bewusst geänderte Vorgänge und
  bewusst geänderte Vorgangstypen oder Workflowstatus.
- Bestehende unveränderte Datensätze werden beim Start, Laden oder Anzeigen
  nicht zurückgeschrieben, umsortiert oder neu zugeordnet.
- Die Oberfläche darf die eindeutige Arbeitskategorie aus Vorgangstyp und
  Workflowstatus ableiten, ohne dadurch den gespeicherten Altbestand zu
  verändern.
- Alte Kategorie-IDs oder Mehrfachzuordnungen dürfen aus
  Rückwärtskompatibilitätsgründen tolerant gelesen werden. Sie sind keine
  zweite Arbeitskategorie und dürfen bei neuen oder geänderten Vorgängen nicht
  als solche fortgeschrieben werden.
- Sobald ein bestehender Vorgang bewusst geändert und gespeichert wird, muss
  für seinen neuen Stand genau eine Arbeitskategorie nach obiger Tabelle
  gelten. Erhalt oder Umwandlung alter Kennzeichnungen muss ohne stille
  Datenverluste erfolgen.

## Bedien- und Darstellungsregeln

- Die Arbeitskategorie wird nicht manuell gewählt und nicht durch Drag & Drop
  unabhängig vom Status verändert.
- Ein Status- oder Typwechsel aktualisiert die sichtbare Arbeitskategorie
  deterministisch nach der Tabelle.
- Listen, Navigation, Zähler, Suche, Übersicht und Detailansicht verwenden
  dieselbe Ableitung.
- Workflowstatus und Arbeitskategorie bleiben als zwei unterschiedliche Werte
  sichtbar; die Arbeitskategorie ist eine Projektion des Status, kein Ersatz
  für die Statusbezeichnung.
- Kennzeichnungen werden optisch und fachlich von Arbeitskategorien getrennt,
  beispielsweise über neutrale Badges und einen eigenen Bearbeitungsbereich.

## Aktueller Umsetzungsstand

Diese Fachlogik ist seit dem Dokumentationsauftrag vom 15. Juli 2026
verbindlich, aber noch nicht in der App implementiert. Bis zur Umsetzung ist
die Abweichung zwischen Dokumentation und Anwendung ein Release-Blocker. Der
vorliegende Dokumentationsauftrag selbst darf keine Implementierung oder
Produktivdatenmigration enthalten.
