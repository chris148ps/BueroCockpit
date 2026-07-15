# BüroCockpit Designrichtlinien

Diese Datei ist die verbindliche Designrichtlinie für UI-Änderungen in
BüroCockpit. Vor jeder UI-Änderung muss sie gelesen und eingehalten werden.

## Geltungsbereich und Designprinzipien

- Die Desktop-App verwendet eine kompakte, ruhige Windows-11-Dark-Optik auf
  Basis des vorhandenen Avalonia-`FluentTheme`.
- Der Dark-Modus ist der Standard und die maßgebliche Desktop-Gestaltung. Der
  vorhandene Light-Modus bleibt als kompatible Benutzereinstellung erhalten
  und verwendet dieselben semantischen Ressourcennamen.
- Die iPad-App hat eine eigenständige SwiftUI-Designsprache. Desktop-Ressourcen
  und Desktop-Styles dürfen nicht in das iPad-Projekt übertragen werden.
- Informationsdichte und Arbeitsabläufe haben Vorrang vor dekorativer Größe.
  Keine übergroßen Überschriften, breiten Leerflächen oder pillenförmigen
  Standardkomponenten einführen.
- Farben, Rahmen, Rundungen und wiederkehrende Abstände werden über die
  zentralen Ressourcen aus `App.axaml.cs` und gemeinsame Styles in
  `MainWindow.axaml` verwendet. Neue harte Farbwerte sind nur für fachlich
  eigenständige Inhalte erlaubt und müssen begründet werden.

## Semantische Flächen

Die Dark-Palette verwendet abgestufte, nicht tiefschwarze Flächen:

- `WindowBackgroundBrush`: äußerster Fensterhintergrund.
- `NavigationBackgroundBrush` und `SidebarBackgroundBrush`: Navigation.
- `SidebarPanelBackgroundBrush`: eingebettete Navigationsfläche.
- `ContentBackgroundBrush`: primäre Arbeitsfläche.
- `ContentSecondaryBackgroundBrush`: abgesetzte Arbeits- und Gruppenfläche.
- `SurfaceBackgroundBrush` und `CardBackgroundBrush`: Karten.
- `SurfaceElevatedBrush`: Dialoge, Menüs und erhöhte Komponenten.
- `InputBackgroundBrush`: Eingabefelder und kompakte Innenflächen.
- `HoverBackgroundBrush`, `SelectedBackgroundBrush` und
  `DisabledBackgroundBrush`: Interaktionszustände.

Flächen werden durch kleine Helligkeitsstufen und dünne Rahmen getrennt. Kein
reines oder nahezu reines Schwarz als dominanten Hintergrund verwenden.

## Text und Typografie

- `TextPrimaryBrush`: Überschriften, Werte und primärer Inhalt.
- `TextSecondaryBrush`: Beschriftungen, Metadaten und unterstützender Text.
- `TextTertiaryBrush`: zurückhaltende Hinweise.
- `TextDisabledBrush`: deaktivierter Text.
- `TextOnAccentBrush`: Text auf der hellen Akzentfläche.
- Standardtext bleibt kompakt bei 13 px. Abschnittstitel sind 12 px und
  halb-fett; sichtbare Seitenüberschriften dürfen größer sein, ohne zusätzliche
  Leerflächen zu erzwingen.
- Sekundärtext darf nicht so kontrastarm werden, dass tägliches längeres Lesen
  erschwert wird.

Die hellen Notizzettel und Dateikarten auf dem Schreibtisch sind fachlich
eigenständige Dokumentflächen. Dafür existieren ausschließlich die zentralen
Kontrastressourcen `DeskInkBrush`, `DeskStrongInkBrush` und
`DeskSelectionBrush`; diese Ausnahme ist nicht auf normale App-Flächen zu
übertragen.

## Rahmen, Trennlinien und Elevation

- `BorderBrush` und `BorderBrushDark`: ruhiger 1-px-Standardrahmen.
- `BorderBrushStrong`: Hover oder stärkere Abgrenzung.
- `FocusBorderBrush`: klarer Fokusrahmen.
- `AccentSoftBorderBrush`: ausgewählte Elemente.
- Keine weißen oder tiefschwarzen Standardrahmen, keine Doppelrahmen und keine
  flächendeckenden 2-px-Konturen verwenden.
- Karten, Kategorien, Listenzeilen und Dialogflächen nutzen in Ruhe 1 px.
  Sichtbarer Tastaturfokus nutzt 2 px.
- Elevation entsteht in der bestehenden Architektur durch
  `SurfaceElevatedBrush` plus Rahmen; keine zusätzliche Schattenbibliothek
  einführen.

## Rundungen und Abstände

- `CornerRadiusSmall` = 4 px für sehr kleine Elemente und Tooltips.
- `CornerRadiusMedium` = 6 px für Eingaben, Buttons und kompakte Zeilen.
- `CornerRadiusLarge` = 8 px für Karten, Menüs, Dialoge und große Flächen.
- `SpacingSmall`, `SpacingMedium`, `SpacingLarge` entsprechen 4, 8 und 12 px.
- `ControlPadding` = 12 × 6 px, `CardPadding` = 12 px und
  `DialogPadding` = 18 px.
- Abweichungen sind nur für bestehende dichte Speziallayouts zulässig. Normale
  Karten, Eingaben und Buttons werden nicht pillenförmig gestaltet.

## Akzent und Interaktionszustände

- `AccentBrush` ist die sparsam verwendete Windows-11-Akzentfarbe.
- `AccentHoverBrush` und `AccentPressedBrush` bilden Hover und gedrückt ab.
- `AccentSoftBrush` und `AccentSoftBorderBrush` markieren Auswahl ohne massive
  Akzentfläche.
- Normale Buttons verwenden neutrale Flächen. Primäre Buttons verwenden die
  Akzentfläche; `Secondary`, `Tertiary`, `Icon` und `Danger` sind die
  vorgesehenen Varianten.
- Normal: ruhige Fläche und Standardrahmen.
- Hover: `HoverBackgroundBrush` und `BorderBrushStrong`; Hover ist klarer als
  normal, aber schwächer als Auswahl.
- Gedrückt: kompakte dunklere Fläche mit erkennbarem Akzent-/Fokusrahmen.
- Aktiv/ausgewählt: `SelectedBackgroundBrush` und
  `AccentSoftBorderBrush`; der Zustand bleibt sichtbar.
- Tastaturfokus: 2-px-`FocusBorderBrush`, nicht nur eine minimale Farbänderung.
- Deaktiviert: `DisabledBackgroundBrush`, `TextDisabledBrush`, Standardrahmen
  und reduzierte Deckkraft.
- Drag-over: `AccentBrush` für Einfügelinien und `AccentSoftBrush` für die
  innere Zielmarkierung. Zustände werden nach Drop oder Abbruch entfernt.
- Ungültige Eingaben und fachliche Fehler verwenden die Fehlerressourcen und
  dürfen nicht wie normale Auswahl aussehen.

## Semantische Statusfarben

Folgende Ressourcen dürfen nicht durch die normale Akzentfarbe ersetzt werden:

- Information: `InformationBrush`, `InformationBackgroundBrush`.
- Erfolg: `SuccessBrush`, `SuccessBackgroundBrush`.
- Warnung: `WarningBrush`, `WarningBackgroundBrush`.
- Fehler: `DangerBrush`, `DangerBackgroundBrush`.
- Ausstehend: `PendingBrush`.
- Verbunden: `ConnectedBrush`.
- Getrennt: `DisconnectedBrush`.
- Überfällig: `OverdueBrush`.
- Bestätigt: `ConfirmedBrush`.
- Deaktiviert: `DisabledBackgroundBrush`, `TextDisabledBrush`.

Status darf nie allein durch Farbe vermittelt werden; sichtbarer Text, Symbol
oder Tooltip bleibt erforderlich. Bestehende fachliche Bedeutungen bleiben
unverändert.

## Komponentenregeln

### Karten, Kategorien und Navigation

- `Border.Card` ist die normale Kartenbasis; `Border.InlineCard` ist die
  kompakte eingebettete Variante.
- Auftragskarten verwenden dieselbe Rahmenlogik. Hover und Auswahl werden über
  zentrale Ressourcen und nicht durch schwarze Konturen dargestellt.
- Kategoriezeilen folgen der Kartensprache. Hover bleibt neutral, Auswahl nutzt
  die weiche Akzentfläche, Einfügepositionen nutzen dünne Linien.
- Arbeitskategorien und Kennzeichnungen sind visuell und fachlich getrennt.
  Arbeitskategorien bilden den aktuellen Bearbeitungsbereich ab;
  Kennzeichnungen erscheinen beispielsweise als kompakte neutrale Badges.
- Chevrons werden ohne runden Button dargestellt; der optische Klickbereich
  liegt nur am Pfeil.
- Soweit Kennzeichnungen hierarchisch dargestellt werden, bleiben Einrückung,
  Auf- und Zuklappen sowie sichtbarer Tastaturfokus erhalten. Diese Hierarchie
  darf keine zweite Arbeitskategorie erzeugen.

### Schaltflächen und Eingaben

- Primäre Aktionen: `Button.Primary`.
- Normale Alternativen: `Button.Secondary`.
- Zurückhaltende Aktionen: `Button.Tertiary`.
- Gefährliche Aktionen: `Button.Danger`.
- Kompakte Symbolaktionen: `Button.Icon` oder ein bestehender spezialisierter
  Stil mit denselben Zustandsressourcen.
- `TextBox`, `ComboBox`, `DatePicker` und `NumericUpDown` teilen Fläche, Rahmen,
  Rundung, Hover, sichtbaren Fokus und deaktivierten Zustand.
- Mehrzeilige Textfelder folgen denselben Regeln. Schreibtisch-Editoren sind
  nur innerhalb ihrer Dokumentfläche rahmenlos.

### Listen, Tabellen und leere Zustände

- Listencontainer bleiben ruhig und transparent; die sichtbare Zeile oder
  Karte trägt Fläche und Zustand.
- Tabellenähnliche Material- und Verwaltungszeilen nutzen Karten- oder
  Inline-Kartenrahmen statt harter Gitterlinien.
- Leere Zustände bleiben kompakt: klare Aussage plus optionaler Sekundärtext in
  einer normalen Karte. Keine übergroßen Illustrationen oder Leerflächen.

### Kompakte Vorgangsansicht

- Zwischen der mittleren Vorgangsliste und dem rechten Detailbereich liegt ein
  sichtbarer, per Maus verschiebbarer Splitter. Beide Bereiche behalten eine
  sinnvolle Mindestbreite; bei kleinen Fenstern darf die Liste dichter werden,
  aber Inhalte dürfen nicht überlappen oder abgeschnitten werden.
- Der Kundenname ist die primäre sichtbare Bezeichnung eines Vorgangs. Eine
  technische ID oder neu erzeugte Auftragsnummer darf nicht als primäre
  Listenbezeichnung hervorgehoben werden.
- Die kompakten Standardspalten der Auftragsliste sind: Status, Kunde, Ort,
  Termin und Techniker. Zusätzliche Spalten brauchen eine fachliche Begründung.
- Stepper, Detailansicht, Listen-Badge und Terminansicht verwenden dieselbe
  zentrale Statusquelle und zeigen exakt dieselbe aktuelle Statusbezeichnung.
  Zusätzlich zeigen Navigation und Arbeitsbereich genau eine aus Vorgangstyp
  und Status abgeleitete Arbeitskategorie. Die Arbeitskategorie ersetzt oder
  verfälscht die sichtbare Statusbezeichnung nicht.
- Der Angebotsablauf verwendet die sichtbaren Schritte Ansicht, Angebot,
  Angebot gesendet, Auftrag, Material, Termin und Erledigt. Der Direktauftrag
  verwendet Auftrag, Material, Termin und Erledigt.
- Die sichtbare Arbeitskategorie folgt verbindlich der Tabelle in
  `docs/ARBEITSKATEGORIEN.md`. `Ansicht` und `Angebot` erscheinen unter
  `Angebote`, `Angebot gesendet` unter `Angebote gesendet` und `Auftrag` eines
  Angebotsvorgangs unter `Angebotsaufträge`. Gemeinsame Statusbereiche sind
  `Material`, `Termin` und `Erledigt`; ein Direktauftrag mit Status `Auftrag`
  erscheint unter `Aufträge`.
- Die Standardspalten der Terminansicht sind Datum, Uhrzeit, Status, Kunde, Ort
  und Techniker. Termine werden chronologisch und dedupliziert dargestellt;
  fehlende Monteurzuordnungen bleiben vollständig leer.
- Tabellenkopf-Kontextmenüs dürfen optionale reale Zusatzfelder wie Titel
  ein- und ausblenden und müssen eine Rückkehr zu den Standardspalten anbieten.
  Änderungen an Spaltenkonfigurationen bleiben lokale UI-Einstellungen.
- Spaltenbreiten werden über dezente Kopf-Splitter verändert und müssen in den
  Datenzeilen unmittelbar mitgeführt werden. Die Layoutwerte werden getrennt
  für Aufträge, Angebote und Termine gespeichert.
- Ein kurzer Klick auf einen normalen Spaltentitel sortiert die aktuelle
  Tabellenansicht; ein erneuter Klick wechselt die Richtung. Die aktive
  Sortierspalte zeigt einen dezenten Pfeil. Ein Kopf-Drag ab der definierten
  Bewegungsgrenze verschiebt die Spalte, der rechte Resize-Griff verändert
  ausschließlich die Breite und löst keine Sortierung aus.
- Sortierungen verwenden den fachlichen Datentyp, stellen leere Werte
  einheitlich ans Ende und bleiben stabil. Status folgt der jeweiligen
  Workflowreihenfolge; Text, Datum und Uhrzeit werden nicht alphabetisch
  beziehungsweise nicht als formatierten Anzeigetext verglichen.

### Dialoge, Kontextmenüs und Tooltips

- Dialogfenster verwenden `WindowBackgroundBrush`; ihr Inhalt verwendet
  `SurfaceElevatedBrush`, 1-px-Standardrahmen, große Rundung und
  `DialogPadding`.
- Programmgesteuerte Dialoge beziehen Brushes über die zentrale
  `ResourceBrush`-Abfrage und dürfen keine separaten hellen Farbschemata
  definieren.
- Kontextmenüs verwenden erhöhte Fläche, Standardrahmen, große Rundung und
  neutralen Hover.
- Tooltips verwenden erhöhte Fläche, starken Rahmen, kleine Rundung und
  kompaktes Padding.

### Scrollbereiche, Hinweise und Meldungen

- ScrollViewer und Scrollleisten verwenden das vorhandene Avalonia-Fluent-
  Verhalten. Umgebende Flächen und Karten dürfen keine zusätzliche harte
  Scrollrahmung erzeugen.
- Hinweise verwenden Information, Erfolg, Warnung oder Fehler passend zur
  fachlichen Bedeutung. Fehlermeldungen müssen lesbaren Text und einen klaren
  semantischen Rahmen besitzen.
- Lade- und Fortschrittszustände verwenden Akzent nur als Aktivitätsanzeige;
  der übrige Container bleibt neutral.

## Arbeitskategorien und Kennzeichnungen

- Eine Arbeitskategorie wird niemals manuell ausgewählt. Sie ergibt sich
  ausschließlich aus Vorgangstyp und Workflowstatus.
- Ein Vorgang darf an keiner Stelle gleichzeitig in mehreren sichtbaren
  Arbeitskategorien erscheinen. Navigation, Listen, Übersicht, Suche und
  Zähler müssen dieselbe eindeutige Ableitung verwenden.
- `SH-Netz`, `Retouren`, `Lager`, `Marktstammdatenregister` und
  `Warten auf Kunde` sind Kennzeichnungen. Sie werden in einem eigenen Bereich
  bearbeitet und beispielsweise als kompakte neutrale Badges angezeigt.
- Ein Vorgang darf mehrere Kennzeichnungen tragen, sofern die spätere
  Implementierung dies fachlich benötigt; sie dürfen jedoch weder wie
  Arbeitskategorien aussehen noch die eindeutige Arbeitskategorie verändern.
- Drag & Drop eines Vorgangs darf die Arbeitskategorie nicht unabhängig von
  Vorgangstyp oder Workflowstatus verändern. Ein Statuswechsel aktualisiert
  die Arbeitskategorie automatisch.
- Das Archiv ist keine normale Kategorie und liegt unter
  `Einstellungen > Aufträge`.
- Mobile Eingänge und `Wartet auf Freigabe` dürfen nicht als normale
  Arbeitskategorien oder Kennzeichnungen angeboten werden. Spätere iPad-Views
  müssen dieselbe Trennung verwenden.
- Drag-&-Drop-Markierungen dürfen nur während Drag & Drop sichtbar sein. Oben
  und unten zeigen eine Einfügelinie, mittig eine dezente Zielmarkierung. Nach
  Drop oder Abbruch verschwinden alle temporären Markierungen. Markierungen für
  Kennzeichnungen müssen sich optisch von Arbeitskategorien unterscheiden.

## Regeln für neue Desktop-Views

1. Zuerst vorhandene semantische Ressourcen und Komponentenstyles verwenden.
2. Neue wiederkehrende Werte zentral ergänzen und für Dark sowie kompatiblen
   Light-Modus definieren.
3. Normal, Hover, gedrückt, ausgewählt, Fokus und deaktiviert prüfen.
4. Information, Erfolg, Warnung und Fehler fachlich korrekt unterscheiden.
5. Mit 1-px-Rahmen, moderaten Rundungen und dem kompakten Abstandssystem
   beginnen.
6. Kontextmenü, Tooltip, Leerzustand und Fehlerfall mitgestalten.
7. Keine Daten-, Sync-, Navigations- oder Bedienlogik aus optischen Gründen
   verändern.

Ausdrücklich zu vermeiden sind harte schwarze oder weiße Konturen, nahezu
schwarze dominante Flächen, lokale Kopien zentraler Styles, beliebige
Akzentfarben, übermäßige Rundungen, normale pillenförmige Controls, unnötige
Schatten und eine Verringerung der Informationsdichte.

## Desktop-/iPad-Abgrenzung und Sperren

- `App.axaml`, `App.axaml.cs`, `MainWindow.axaml` und die programmgesteuerten
  Desktop-Dialoge gehören ausschließlich zur Avalonia-Desktop-App.
- Das SwiftUI-Projekt unter `iPad/` teilt diese Ressourcen nicht. Fachlich
  gemeinsame Daten und Snapshot-Formate werden durch Designänderungen nicht
  verändert.
- Keine Produktivdaten löschen. Keine Version, kein Tag und kein Release ohne
  ausdrückliche Freigabe.
