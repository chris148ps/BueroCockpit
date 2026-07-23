using System.Net;
using System.Net.Sockets;
using System.Net.Http.Json;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using BueroCockpit.Data;
using BueroCockpit.Models;
using BueroCockpit.Services;
using BueroCockpit.Services.LocalSync;
using Microsoft.Data.Sqlite;

var tempRoot = Path.Combine(Path.GetTempPath(), $"BueroCockpit-WorkflowTests-{Guid.NewGuid():N}");
Directory.CreateDirectory(tempRoot);

try
{
    var workflowDatabasePath = Path.Combine(tempRoot, "data", "workflow-tests.db");
    var repository = new BueroRepository(workflowDatabasePath);
    repository.Initialize();
    using (var migrationConnection = new SqliteConnection(
               $"Data Source={Path.Combine(tempRoot, "data", "workflow-tests.db")}"))
    {
        migrationConnection.Open();
        using var removeFollowUpReason = migrationConnection.CreateCommand();
        removeFollowUpReason.CommandText = "ALTER TABLE Tasks DROP COLUMN FollowUpReason;";
        removeFollowUpReason.ExecuteNonQuery();
    }
    repository.Initialize();
    using (var migrationConnection = new SqliteConnection(
               $"Data Source={Path.Combine(tempRoot, "data", "workflow-tests.db")}"))
    {
        migrationConnection.Open();
        using var schemaCommand = migrationConnection.CreateCommand();
        schemaCommand.CommandText = "PRAGMA table_info(Tasks);";
        using var schemaReader = schemaCommand.ExecuteReader();
        var hasFollowUpReason = false;
        while (schemaReader.Read())
        {
            hasFollowUpReason |= string.Equals(schemaReader.GetString(1), "FollowUpReason", StringComparison.Ordinal);
        }
        Assert(hasFollowUpReason, "Eine bestehende Datenbank wird additiv um FollowUpReason migriert.");
    }

    var categories = repository.GetCategories();
    Assert(categories.Count >= 3, "Die isolierte Testdatenbank enthält Testkategorien.");
    var first = categories[0];
    var second = categories[1];
    var third = categories[2];

    repository.SaveWorkflowCategoryMapping(
        WorkflowCategoryService.DirectWorkflowType,
        "Auftrag",
        first.Id);
    AssertMapping(repository, WorkflowCategoryService.DirectWorkflowType, "Auftrag", first.Id);

    first.Name = "Frei benannte Kategorie";
    first.ParentId = second.Id;
    repository.SaveCategory(first);
    AssertMapping(repository, WorkflowCategoryService.DirectWorkflowType, "Auftrag", first.Id);

    repository.SaveWorkflowCategoryMapping(
        WorkflowCategoryService.OfferWorkflowType,
        "Angebot",
        first.Id);
    var replaced = repository.ReplaceWorkflowCategoryMappings(first.Id, second.Id);
    Assert(replaced == 2, "Alle Zuordnungen der ersetzten Kategorie wurden aktualisiert.");
    AssertMapping(repository, WorkflowCategoryService.DirectWorkflowType, "Auftrag", second.Id);
    AssertMapping(repository, WorkflowCategoryService.OfferWorkflowType, "Angebot", second.Id);

    var removed = repository.DeleteWorkflowCategoryMappingsForCategory(second.Id);
    Assert(removed == 2, "Zuordnungen lassen sich ausdrücklich ohne Ersatz entfernen.");
    Assert(repository.GetWorkflowCategoryMappings().Count == 0, "Fehlende Zuordnungen bleiben tatsächlich fehlend.");

    var legacyTask = new TaskItem
    {
        Id = Guid.NewGuid().ToString("N"),
        Title = "Unveränderter Altvorgang",
        CustomerName = "Testkunde",
        Description = string.Empty,
        CategoryId = first.Id,
        CategoryIds = [first.Id, second.Id],
        Status = "Auftrag",
        WorkflowType = WorkflowCategoryService.DirectWorkflowType,
        WorkflowStep = "Auftrag",
        Priority = "Normal",
        AssignedTo = string.Empty,
        CreatedAt = DateTime.Now.AddDays(-1),
        UpdatedAt = DateTime.Now.AddDays(-1)
    };
    repository.SaveTask(legacyTask);
    var legacyBeforeMappingChange = GetTask(repository, legacyTask.Id);
    Assert(legacyBeforeMappingChange.CategoryIds.Count == 2, "Legacy-Mehrfachzuordnungen werden tolerant gelesen.");

    repository.SaveWorkflowCategoryMapping(
        WorkflowCategoryService.DirectWorkflowType,
        "Auftrag",
        third.Id);
    var legacyAfterMappingChange = GetTask(repository, legacyTask.Id);
    Assert(
        legacyAfterMappingChange.CategoryIds.Count == 2 &&
        legacyAfterMappingChange.CategoryIds.Contains(first.Id) &&
        legacyAfterMappingChange.CategoryIds.Contains(second.Id),
        "Das Konfigurieren einer Zuordnung migriert unveränderte Altvorgänge nicht.");

    WorkflowCategoryService.ApplyCategory(legacyAfterMappingChange, third.Id);
    repository.SaveTask(legacyAfterMappingChange);
    var changedTask = GetTask(repository, legacyTask.Id);
    Assert(changedTask.CategoryId == third.Id, "Ein geänderter Vorgang speichert die aktuelle Kategorie-ID.");
    Assert(changedTask.CategoryIds.SequenceEqual([third.Id]), "Ein geänderter Vorgang besitzt genau eine Kategorie.");

    third.Name = "Umbenannt und verschoben";
    third.ParentId = first.Id;
    repository.SaveCategory(third);
    AssertMapping(repository, WorkflowCategoryService.DirectWorkflowType, "Auftrag", third.Id);

    third.IsVisible = false;
    repository.SaveCategory(third);
    AssertMapping(repository, WorkflowCategoryService.DirectWorkflowType, "Auftrag", third.Id);
    Assert(
        repository.GetCategories().All(category => category.Id != third.Id),
        "Eine ausgeblendete Zielkategorie ist nicht mehr als sichtbare Kategorie gültig.");

    Assert(
        WorkflowCategoryService.IsValidStep(WorkflowCategoryService.OfferWorkflowType, "Material"),
        "Gemeinsame Status bleiben beim Typwechsel kompatibel.");
    Assert(
        !WorkflowCategoryService.IsValidStep(WorkflowCategoryService.DirectWorkflowType, "Angebot gesendet"),
        "Nicht kompatible Status werden erkannt.");

    foreach (var workflowType in new[] { WorkflowCategoryService.OfferWorkflowType, WorkflowCategoryService.DirectWorkflowType })
    {
        var workflowTask = new TaskItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = $"Statusfolge {workflowType}",
            CustomerName = "Workflow-Test",
            WorkflowType = workflowType,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        foreach (var workflowStep in WorkflowCategoryService.GetSteps(workflowType))
        {
            var stepCategory = new CategoryItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = $"{workflowType} - {workflowStep}",
                SortOrder = 1000,
                SortMode = "Erstellt am",
                Color = "#F2F3F5",
                IsVisible = true
            };
            repository.SaveCategory(stepCategory);
            repository.SaveWorkflowCategoryMapping(workflowType, workflowStep, stepCategory.Id);

            workflowTask.WorkflowStep = workflowStep;
            workflowTask.Status = workflowStep;
            WorkflowCategoryService.ApplyCategory(workflowTask, stepCategory.Id);
            repository.SaveTask(workflowTask);

            var persistedStep = GetTask(repository, workflowTask.Id);
            Assert(
                persistedStep.WorkflowStep == workflowStep &&
                persistedStep.Status == workflowStep &&
                persistedStep.CategoryId == stepCategory.Id &&
                persistedStep.CategoryIds.SequenceEqual([stepCategory.Id]),
                $"{workflowType} / {workflowStep} bleibt genau einer stabilen Kategorie zugeordnet.");
        }

        Assert(
            repository.GetTasks().Count(task => task.Id == workflowTask.Id) == 1,
            $"{workflowType} erscheint in der technischen Gesamtmenge genau einmal.");
    }

    var openRegressionCategory = new CategoryItem
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = "Offen Regression",
        SortOrder = 1100,
        SortMode = "Erstellt am",
        Color = "#F2F3F5",
        IsVisible = true
    };
    var completedRegressionCategory = new CategoryItem
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = "Erledigt Regression",
        SortOrder = 1101,
        SortMode = "Erstellt am",
        Color = "#F2F3F5",
        IsVisible = true
    };
    repository.SaveCategory(openRegressionCategory);
    repository.SaveCategory(completedRegressionCategory);
    repository.SaveWorkflowCategoryMapping(
        WorkflowCategoryService.DirectWorkflowType,
        "Erledigt",
        completedRegressionCategory.Id);
    var completedRegressionTask = new TaskItem
    {
        Id = Guid.NewGuid().ToString("N"),
        Title = "Erledigt bleibt sichtbar",
        CustomerName = "Regression",
        CategoryId = openRegressionCategory.Id,
        CategoryIds = [openRegressionCategory.Id],
        Status = "Auftrag",
        WorkflowType = WorkflowCategoryService.DirectWorkflowType,
        WorkflowStep = "Auftrag",
        Priority = "Normal",
        CreatedAt = DateTime.Now,
        UpdatedAt = DateTime.Now
    };
    repository.SaveTask(completedRegressionTask);

    completedRegressionTask.WorkflowStep = "Erledigt";
    completedRegressionTask.Status = "Erledigt";
    completedRegressionTask.CompletedAt = DateTime.Now;
    WorkflowCategoryService.ApplyCategory(completedRegressionTask, completedRegressionCategory.Id);
    repository.SaveTask(completedRegressionTask);

    var regressionCategories = repository.GetCategories();
    var persistedCompletedTask = GetTask(repository, completedRegressionTask.Id);
    Assert(
        repository.GetWorkflowCategoryMappings().Single(mapping =>
            mapping.WorkflowType == WorkflowCategoryService.DirectWorkflowType &&
            mapping.WorkflowStep == "Erledigt").CategoryId == completedRegressionCategory.Id,
        "Der Status Erledigt bleibt der konfigurierten normalen Zielkategorie zugeordnet.");
    Assert(
        persistedCompletedTask.Status == "Erledigt" &&
        persistedCompletedTask.WorkflowStep == "Erledigt" &&
        persistedCompletedTask.CategoryId == completedRegressionCategory.Id &&
        persistedCompletedTask.CategoryIds.SequenceEqual([completedRegressionCategory.Id]) &&
        persistedCompletedTask.CompletedAt.HasValue,
        "Statuswechsel, Abschlusszeit und automatische Kategorieverschiebung werden gespeichert.");
    Assert(
        CategoryHierarchyFilter.IsVisibleInNormalCategory(
            persistedCompletedTask,
            regressionCategories,
            completedRegressionCategory.Id) &&
        !CategoryHierarchyFilter.IsArchived(persistedCompletedTask, regressionCategories),
        "Ein erledigter Vorgang bleibt in seiner normalen Zielkategorie sichtbar und ist kein technischer Archiveintrag.");

    var restartedRepository = new BueroRepository(workflowDatabasePath);
    restartedRepository.Initialize();
    var restartedCategories = restartedRepository.GetCategories();
    var restartedCompletedTask = GetTask(restartedRepository, completedRegressionTask.Id);
    Assert(
        restartedCompletedTask.Status == "Erledigt" &&
        restartedCompletedTask.CategoryId == completedRegressionCategory.Id &&
        CategoryHierarchyFilter.IsVisibleInNormalCategory(
            restartedCompletedTask,
            restartedCategories,
            completedRegressionCategory.Id),
        "Nach einem Repository-Neustart bleibt der erledigte Vorgang gespeichert und in der Zielkategorie sichtbar.");

    var archiveRegressionCategory = new CategoryItem
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = "Archiv",
        IsVisible = true
    };
    var archiveRegressionTask = new TaskItem
    {
        CategoryId = archiveRegressionCategory.Id,
        CategoryIds = [archiveRegressionCategory.Id],
        Status = "Erledigt"
    };
    Assert(
        CategoryHierarchyFilter.IsArchived(
            archiveRegressionTask,
            [completedRegressionCategory, archiveRegressionCategory]) &&
        !CategoryHierarchyFilter.IsVisibleInNormalCategory(
            archiveRegressionTask,
            [completedRegressionCategory, archiveRegressionCategory],
            completedRegressionCategory.Id),
        "Die bestehende technische Archivkategorie bleibt vom normalen Kategorienfilter ausgeschlossen.");

    Assert(
        NavigationCategoryPolicy.PrimaryNavigation.Select(item => item.Id)
            .SequenceEqual([NavigationCategoryPolicy.OverviewId, NavigationCategoryPolicy.AllTasksId]),
        "Die feste Hauptnavigation enthält nur Übersicht und Alle Vorgänge.");
    Assert(
        NavigationCategoryPolicy.PrimaryNavigation.Select(item => item.Name)
            .SequenceEqual(["Übersicht", "Alle Vorgänge"]),
        "Die technischen Hauptansichten tragen keine fachlich festgelegten Arbeitskategorienamen.");
    foreach (var legacyNavigationId in new[] { "__offers", "__orders", "__materials", "__appointments" })
    {
        Assert(
            NavigationCategoryPolicy.IsLegacyWorkId(legacyNavigationId) &&
            !NavigationCategoryPolicy.IsTechnicalId(legacyNavigationId),
            $"{legacyNavigationId} wird nur tolerant als Alt-ID erkannt und nicht als technische Systemansicht geführt.");
    }
    Assert(
        !NavigationCategoryPolicy.IsTechnicalId(first.Id) &&
        !NavigationCategoryPolicy.IsLegacyWorkId(first.Id),
        "Eine frei vergebene stabile Kategorie-ID bleibt eine normale Kategorie.");

    var legacyLayout = TableLayoutSettings.CreateOrdersDefault();
    legacyLayout.ColumnWidths["Kunde"] = 333;
    var localSettings = new AppSettings
    {
        TaskTableLayout = null,
        OrdersTableLayout = legacyLayout,
        OffersTableLayout = TableLayoutSettings.CreateOffersDefault(),
        AppointmentsTableLayout = TableLayoutSettings.CreateAppointmentsDefault()
    };
    var resolvedLegacyLayout = localSettings.ResolveTaskTableLayout();
    Assert(
        ReferenceEquals(resolvedLegacyLayout, legacyLayout) && localSettings.TaskTableLayout is null,
        "Ein altes Layout wird tolerant gelesen, ohne es automatisch zu migrieren.");
    var writableLayout = localSettings.GetWritableTaskTableLayout();
    Assert(
        !ReferenceEquals(writableLayout, legacyLayout) && writableLayout.ColumnWidths["Kunde"] == 333,
        "Erst eine bewusste Layoutänderung erhält eine gemeinsame, unabhängige Layoutkopie.");
    writableLayout.ColumnWidths["Kunde"] = 280;
    Assert(
        legacyLayout.ColumnWidths["Kunde"] == 333,
        "Das alte Layout bleibt bei Änderungen am gemeinsamen Layout unverändert.");

    var completedStep = new WorkflowStepItem("Angebot", 2, true, false);
    var currentStep = new WorkflowStepItem("Auftrag", 3, false, true);
    var futureStep = new WorkflowStepItem("Material", 4, false, false);
    Assert(completedStep.Glyph == "✓" && completedStep.IsConnectorActive, "Der Stepper kennzeichnet abgeschlossene Schritte eindeutig.");
    Assert(currentStep.Glyph == "●" && currentStep.StateText == "aktuell", "Der Stepper kennzeichnet den aktuellen Schritt mit Text und Symbol.");
    Assert(futureStep.IsFuture && futureStep.StateText == "zukünftig", "Der Stepper kennzeichnet zukünftige Schritte unabhängig von Farbe.");

    var overdueFollowUp = new TaskItem { FollowUpDate = DateTime.Today.AddDays(-1) };
    var dueTodayFollowUp = new TaskItem { FollowUpDate = DateTime.Today };
    Assert(overdueFollowUp.FollowUpAlertText == "Überfällig", "Eine überfällige Wiedervorlage hat eine textliche Warnkennzeichnung.");
    Assert(dueTodayFollowUp.FollowUpAlertText == "Heute fällig", "Eine heute fällige Wiedervorlage hat eine textliche Warnkennzeichnung.");
    var todayDueDate = new TaskItem { DueDate = DateTime.Today.AddHours(8) };
    var tomorrowDueDate = new TaskItem { DueDate = DateTime.Today.AddDays(1) };
    var overdueDueDate = new TaskItem { DueDate = DateTime.Today.AddDays(-1) };
    Assert(todayDueDate.DueDateFollowUpOverviewText == "Heute, 08:00 Uhr" && todayDueDate.IsDueDateToday,
        "Die Wiedervorlagenkarte hebt einen heutigen Auftragstermin anhand des Auftragstermins hervor.");
    Assert(tomorrowDueDate.DueDateFollowUpOverviewText == "Morgen" && tomorrowDueDate.IsDueDateFuture,
        "Ein morgiger Auftragstermin ohne Uhrzeit wird ohne erfundene Uhrzeit angezeigt.");
    Assert(overdueDueDate.IsDueDateOverdue,
        "Nur ein vergangener Auftragstermin steuert die Überfällig-Markierung der Terminzeile.");

    var hierarchyParent = new CategoryItem { Id = "hierarchy-parent", Name = "Angebot" };
    var hierarchyChild = new CategoryItem { Id = "hierarchy-child", Name = "Erstellen", ParentId = hierarchyParent.Id };
    var hierarchyGrandchild = new CategoryItem
    {
        Id = "hierarchy-grandchild",
        Name = "Prüfen",
        ParentId = hierarchyChild.Id,
        IsVisible = false
    };
    var hierarchyCategories = new[] { hierarchyParent, hierarchyChild, hierarchyGrandchild };
    var directHierarchyTask = new TaskItem { CategoryId = hierarchyParent.Id, CategoryIds = [hierarchyParent.Id] };
    var nestedHierarchyTask = new TaskItem
    {
        CategoryId = hierarchyGrandchild.Id,
        CategoryIds = [hierarchyGrandchild.Id, hierarchyChild.Id]
    };
    var unrelatedHierarchyTask = new TaskItem { CategoryId = "other", CategoryIds = ["other"] };
    var hierarchyTasks = new[] { directHierarchyTask, nestedHierarchyTask, unrelatedHierarchyTask };
    var hierarchyIds = CategoryHierarchyFilter.GetCategoryAndDescendantIds(hierarchyCategories, hierarchyParent.Id);
    Assert(hierarchyIds.SetEquals([hierarchyParent.Id, hierarchyChild.Id, hierarchyGrandchild.Id]),
        "Die zentrale Kategorienlogik enthält die gewählte Kategorie und beliebig tiefe Nachfolger ohne Doppelungen.");
    Assert(hierarchyTasks.Count(task => CategoryHierarchyFilter.Matches(task, hierarchyCategories, hierarchyParent.Id)) == 2,
        "Oberkategorien zählen direkte und rekursive Vorgänge jeweils genau einmal.");
    Assert(CategoryHierarchyFilter.Matches(nestedHierarchyTask, hierarchyCategories, hierarchyChild.Id),
        "Eine Unterkategorie aggregiert ebenfalls ihre tiefer verschachtelten Nachfolger.");

    var normalizeSortField = typeof(BueroCockpit.MainWindow).GetMethod(
        "NormalizeSortField",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
        ?? throw new InvalidOperationException("Sortiernormalisierung nicht gefunden.");
    Assert((string?)normalizeSortField.Invoke(null, ["Status"]) == "Status" &&
           (string?)normalizeSortField.Invoke(null, ["Kategorie"]) == "Kategorie",
        "Entfernte Dropdownoptionen bleiben als alte gespeicherte Header-Sortierungen tolerant lesbar.");
    Assert((string?)normalizeSortField.Invoke(null, ["nicht-mehr-vorhanden"]) == "Erstellt am",
        "Eine unbekannte alte Sortiereinstellung fällt sicher auf ‚Erstellt am‘ zurück.");

    third.IsVisible = true;
    repository.SaveCategory(third);
    changedTask.FollowUpReason = "Kundenrückmeldung abwarten";
    repository.SaveTask(changedTask);
    Assert(GetTask(repository, changedTask.Id).FollowUpReason == changedTask.FollowUpReason,
        "Der optionale Wiedervorlagegrund wird ohne Kürzung dauerhaft gespeichert.");
    changedTask.FollowUpReason = string.Empty;
    repository.SaveTask(changedTask);
    Assert(GetTask(repository, changedTask.Id).FollowUpReason == string.Empty,
        "Der Wiedervorlagegrund kann vollständig entfernt werden.");
    changedTask.FollowUpReason = "Materiallieferung prüfen";
    repository.SaveTask(changedTask);
    var networkSnapshotPath = Path.Combine(tempRoot, "workflow-network-snapshot.bcsnapshot");
    var exportResult = await new IpadSnapshotExportService().ExportNetworkSnapshotToFileAsync(
        repository,
        networkSnapshotPath,
        "test",
        "isolated-test");
    Assert(exportResult.Success, "Der isolierte Snapshot-Export war erfolgreich.");
    using var workflowArchive = ZipFile.OpenRead(networkSnapshotPath);
    using var tasksStream = workflowArchive.GetEntry("tasks.json")!.Open();
    using var tasksDocument = JsonDocument.Parse(tasksStream);
    var exportedTask = tasksDocument.RootElement.EnumerateArray().Single(item =>
        item.GetProperty("id").GetString() == legacyTask.Id);
    Assert(exportedTask.GetProperty("currentCategoryId").GetString() == third.Id, "Der Export enthält currentCategoryId.");
    Assert(exportedTask.GetProperty("workflowType").GetString() == WorkflowCategoryService.DirectWorkflowType, "Der Export enthält workflowType.");
    Assert(exportedTask.GetProperty("workflowStep").GetString() == "Auftrag", "Der Export enthält workflowStep.");
    Assert(exportedTask.GetProperty("status").GetString() == "Auftrag", "Der Export enthält status.");
    Assert(exportedTask.GetProperty("followUpReason").GetString() == changedTask.FollowUpReason,
        "Der Desktop-iPad-Export enthält den Wiedervorlagegrund.");

    var localNetworkPackagePath = Path.Combine(tempRoot, "local-network-technicians.bcsnapshot");
    var technicianProfiles = new[]
    {
        new TechnicianProfile { Id = "tech-1", Name = "Monteur Eins" },
        new TechnicianProfile { Id = "tech-2", Name = "Monteur Zwei" },
        new TechnicianProfile { Id = "tech-3", Name = "Monteur Drei" },
        new TechnicianProfile { Id = "tech-4", Name = "Monteur Vier" }
    };
    var localNetworkExport = await new IpadSnapshotExportService().ExportNetworkSnapshotToFileAsync(
        repository,
        localNetworkPackagePath,
        "test",
        "isolated-test",
        CancellationToken.None,
        technicianProfiles);
    Assert(localNetworkExport.Success, "Das lokale Netzwerkpaket mit zentraler Monteurliste wird erstellt.");
    using (var technicianArchive = ZipFile.OpenRead(localNetworkPackagePath))
    using (var technicianStream = technicianArchive.GetEntry("technicians.json")!.Open())
    using (var technicianDocument = JsonDocument.Parse(technicianStream))
    {
        var exportedTechnicians = technicianDocument.RootElement.EnumerateArray().ToList();
        Assert(exportedTechnicians.Count == 4 &&
               exportedTechnicians.Select(item => item.GetProperty("id").GetString()).Distinct().Count() == 4,
            "Die iPad-Monteurauswahl erhält alle zentral konfigurierten Monteure mit stabilen IDs, nicht nur bereits zugewiesene Namen.");
    }

    AssertMobileTaskRevisionModel(first.Id);
    AssertLocalSyncDeltaCheckpoints(tempRoot);
    await AssertLocalSyncTransferAsync(tempRoot);

    Console.WriteLine("Workflow-/Kategorie-Integrationstests erfolgreich.");
}
finally
{
    try
    {
        Directory.Delete(tempRoot, recursive: true);
    }
    catch
    {
        // Testdaten liegen ausschließlich im temporären Testordner.
    }
}

static TaskItem GetTask(BueroRepository repository, string taskId) =>
    repository.GetTasks().Single(task => task.Id == taskId);

static void AssertMapping(BueroRepository repository, string workflowType, string workflowStep, string categoryId)
{
    var mapping = repository.GetWorkflowCategoryMappings().Single(item =>
        string.Equals(item.WorkflowType, workflowType, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(item.WorkflowStep, workflowStep, StringComparison.OrdinalIgnoreCase));
    Assert(mapping.CategoryId == categoryId, $"Zuordnung {workflowType} / {workflowStep} verweist auf die erwartete stabile ID.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Test fehlgeschlagen: {message}");
    }

    Console.WriteLine($"OK: {message}");
}

static void AssertMobileTaskRevisionModel(string categoryId)
{
    var revision = DateTime.Now.AddMinutes(-5);
    var desktopTask = new TaskItem
    {
        Id = "desktop-revision-test",
        Description = "Desktop wurde geändert",
        CategoryId = categoryId,
        CategoryIds = [categoryId],
        WorkflowType = WorkflowCategoryService.DirectWorkflowType,
        WorkflowStep = "Auftrag",
        Status = "Auftrag",
        DueDate = new DateTime(2026, 7, 20),
        FollowUpDate = new DateTime(2026, 7, 21),
        FollowUpReason = "Desktopgrund",
        Technician = "Desktopmonteur",
        UpdatedAt = revision
    };
    var entry = new MobileInboxEntry
    {
        Id = "mobile-revision-test",
        SchemaVersion = 2,
        Operation = "update",
        DesktopTaskId = desktopTask.Id,
        BaseRevision = revision.AddMinutes(-1).ToString("O"),
        Notes = "iPad-Notiz",
        CategoryId = categoryId,
        WorkflowType = WorkflowCategoryService.DirectWorkflowType,
        WorkflowStep = "Auftrag",
        DueDate = new DateTime(2026, 7, 22),
        FollowUpDate = new DateTime(2026, 7, 21),
        FollowUpReason = "iPad-Grund",
        Technician = "iPad-Monteur",
        BaseValues = new MobileTaskRevisionValues
        {
            Notes = "Basisnotiz",
            CategoryId = categoryId,
            WorkflowType = WorkflowCategoryService.DirectWorkflowType,
            WorkflowStep = "Auftrag",
            DueDate = new DateTime(2026, 7, 20),
            FollowUpDate = new DateTime(2026, 7, 21),
            FollowUpReason = "Basisgrund",
            Technician = "Basismonteur"
        }
    };

    var changes = MobileTaskRevisionService.BuildChanges(entry, desktopTask);
    Assert(changes.Single(change => change.Field == MobileTaskRevisionService.NotesField).HasConflict,
        "Abweichende Desktop- und iPad-Notizen werden als sichtbarer Feldkonflikt erkannt.");
    Assert(!changes.Single(change => change.Field == MobileTaskRevisionService.DueDateField).HasConflict,
        "Eine nur auf dem iPad geänderte Terminangabe ist kein Feldkonflikt.");
    Assert(changes.All(change => change.Field != MobileTaskRevisionService.CategoryField),
        "Eine unveränderte stabile Kategorie-ID erzeugt keine mobile Änderung.");
    Assert(!MobileTaskRevisionService.RevisionMatches(entry.BaseRevision, desktopTask.UpdatedAt),
        "Eine abweichende Desktoprevision wird vor der Übernahme erkannt.");
    Assert(MobileTaskRevisionService.RevisionMatches(desktopTask.UpdatedAt.ToString("O"), desktopTask.UpdatedAt),
        "Die identische Desktoprevision wird tolerant im ISO-Format erkannt.");
}

static void AssertLocalSyncDeltaCheckpoints(string tempRoot)
{
    var deltaRoot = Path.Combine(tempRoot, "local-sync-delta");
    Directory.CreateDirectory(deltaRoot);
    var statePath = Path.Combine(deltaRoot, "state.json");
    var store = new LocalSyncDeltaStore(statePath);
    var firstPackage = CreateDeltaSnapshotPackage(
        deltaRoot,
        "first",
        taskTitle: "Unverändert",
        categoryName: "Montage",
        technicianName: "Monteur A",
        attachmentBytes: [0x01, 0x02, 0x03],
        additionalUnchangedTasks: 99);

    var missingCheckpoint = store.BuildDelta("ipad-a", null, firstPackage);
    Assert(missingCheckpoint.RequiresFullSync,
        "Ein neues Gerät ohne bestätigten Checkpoint erhält einen sicheren Erstabgleich.");

    var prepared = store.PrepareFullSync("ipad-a", firstPackage);
    var rejected = store.Confirm("ipad-a", new LocalSyncAckRequest("wrong-token", prepared.ServerRevision));
    Assert(rejected.Status == "rejected",
        "Eine falsche oder veraltete Bestätigung verschiebt den Geräte-Checkpoint nicht.");
    Assert(store.BuildDelta("ipad-a", prepared.ServerRevision, firstPackage).RequiresFullSync,
        "Nach abgebrochener Erstübertragung bleibt der alte leere Checkpoint wirksam.");

    var confirmed = store.Confirm(
        "ipad-a",
        new LocalSyncAckRequest(prepared.AckToken, prepared.ServerRevision, 3));
    Assert(confirmed.Status == "confirmed" && confirmed.LastConfirmedClientSequence == 3,
        "Erst die passende Abschlussquittung bestätigt Server- und Client-Sequenz.");

    var noChanges = new LocalSyncDeltaStore(statePath).BuildDelta("ipad-a", confirmed.ServerRevision, firstPackage);
    Assert(!noChanges.RequiresFullSync &&
           noChanges.Counts.Tasks == 0 &&
           noChanges.Counts.Files == 0 &&
           noChanges.AckToken is null,
        "Nach App- und Store-Neustart überträgt ein zweiter Sync ohne Änderung keine Objekte oder Dateien.");

    var changedPackage = CreateDeltaSnapshotPackage(
        deltaRoot,
        "changed",
        taskTitle: "Nur dieser Auftrag ist geändert",
        categoryName: "Montage",
        technicianName: "Monteur A",
        attachmentBytes: [0x01, 0x02, 0x04],
        additionalUnchangedTasks: 99);
    var interruptedDelta = store.BuildDelta("ipad-a", confirmed.ServerRevision, changedPackage);
    Assert(interruptedDelta.Counts.Tasks == 1 &&
           interruptedDelta.Counts.Attachments == 1 &&
           interruptedDelta.Counts.Files == 2,
        "Nur der geänderte Auftrag sowie Original und Vorschau der geänderten Anhangsversion werden als Delta geliefert.");
    Assert(interruptedDelta.Tasks.Single().GetProperty("id").GetString() == "task-1",
        "Stabile Auftrag-IDs bleiben im Delta erhalten.");
    var transferredFile = interruptedDelta.Files.Single(file => file.RelativePath == "attachments/foto.jpg");
    var transferredBytes = Convert.FromBase64String(transferredFile.DataBase64);
    Assert(transferredBytes.SequenceEqual(new byte[] { 0x01, 0x02, 0x04 }) &&
           transferredFile.Sha256 == Convert.ToHexString(SHA256.HashData(transferredBytes)).ToLowerInvariant(),
        "Eine geänderte Datei wird vollständig und mit passender SHA-256-Prüfsumme übertragen.");

    var retriedDelta = new LocalSyncDeltaStore(statePath)
        .BuildDelta("ipad-a", confirmed.ServerRevision, changedPackage);
    Assert(retriedDelta.Counts.Tasks == 1 && retriedDelta.Counts.Files == 2,
        "Ein Abbruch vor der Bestätigung lässt dasselbe Delta sicher erneut abrufbar.");
    var retryConfirmed = store.Confirm(
        "ipad-a",
        new LocalSyncAckRequest(
            retriedDelta.AckToken ?? throw new InvalidOperationException("Ack-Token fehlt."),
            retriedDelta.ToRevision,
            4));
    Assert(retryConfirmed.Status == "confirmed",
        "Der wiederholte Delta-Abruf kann ohne Duplikat mit seinem aktuellen Token bestätigt werden.");
    Assert(store.BuildDelta("ipad-a", retryConfirmed.ServerRevision, changedPackage).Counts is
           { Tasks: 0, Attachments: 0, Files: 0, Tombstones: 0 },
        "Bereits bestätigte Aufträge und Anhänge werden nicht erneut übertragen.");

    var referencePackage = CreateDeltaSnapshotPackage(
        deltaRoot,
        "references",
        taskTitle: "Nur dieser Auftrag ist geändert",
        categoryName: "Montage geändert",
        technicianName: "Monteur B",
        attachmentBytes: [0x01, 0x02, 0x04],
        additionalUnchangedTasks: 99);
    var referenceDelta = store.BuildDelta("ipad-a", retryConfirmed.ServerRevision, referencePackage);
    Assert(referenceDelta.Counts.Tasks == 0 &&
           referenceDelta.Counts.Categories == 1 &&
           referenceDelta.Counts.Technicians == 1,
        "Geänderte Kategorien und Monteure werden unabhängig von unveränderten Aufträgen inkrementell geliefert.");
    var referenceConfirmed = store.Confirm(
        "ipad-a",
        new LocalSyncAckRequest(referenceDelta.AckToken!, referenceDelta.ToRevision));
    Assert(referenceConfirmed.Status == "confirmed",
        "Auch ein reines Referenzdaten-Delta besitzt einen bestätigten Geräte-Checkpoint.");

    var deletedPackage = CreateDeltaSnapshotPackage(
        deltaRoot,
        "deleted",
        taskTitle: null,
        categoryName: "Montage geändert",
        technicianName: "Monteur B",
        attachmentBytes: null,
        additionalUnchangedTasks: 99);
    var tombstoneDelta = store.BuildDelta("ipad-a", referenceConfirmed.ServerRevision, deletedPackage);
    Assert(tombstoneDelta.Tombstones.TaskIds.SequenceEqual(["task-1"]) &&
           tombstoneDelta.Tombstones.AttachmentIds.SequenceEqual(["attachment-1"]),
        "Unterstützte Löschungen werden als stabile Auftrag- und Anhangs-Tombstones übertragen.");

    Assert(store.BuildDelta("ipad-b", null, changedPackage).RequiresFullSync,
        "Jedes gekoppelte Gerät besitzt einen eigenen Checkpoint.");
    Assert(store.BuildDelta("ipad-a", "verloren", changedPackage).RequiresFullSync,
        "Ein verlorener oder ungültiger Checkpoint löst einen sicheren Neuaufbau statt stiller Überschreibung aus.");

    var otherPrepared = store.PrepareFullSync("ipad-b", changedPackage);
    var otherConfirmed = store.Confirm(
        "ipad-b",
        new LocalSyncAckRequest(otherPrepared.AckToken, otherPrepared.ServerRevision));
    Assert(otherConfirmed.Status == "confirmed",
        "Ein zweites Gerät kann unabhängig einen eigenen bestätigten Checkpoint erhalten.");
    Assert(store.DeleteDeviceCheckpoint("ipad-a") &&
           !store.DeleteDeviceCheckpoint("ipad-a"),
        "Der Checkpoint eines gelöschten Geräts wird genau einmal und idempotent entfernt.");
    Assert(store.BuildDelta("ipad-a", referenceConfirmed.ServerRevision, changedPackage).RequiresFullSync,
        "Ein erneut gekoppeltes Gerät kann nach dem Löschen nicht mit seinem alten Checkpoint fortfahren.");
    Assert(store.BuildDelta("ipad-b", otherConfirmed.ServerRevision, changedPackage).Counts is
           { Tasks: 0, Attachments: 0, Files: 0, Tombstones: 0 },
        "Das Löschen eines Geräts lässt den bestätigten Checkpoint anderer Geräte unverändert.");

    var fullBytes = new FileInfo(changedPackage.FilePath).Length;
    var deltaBytes = JsonSerializer.SerializeToUtf8Bytes(interruptedDelta).LongLength;
    Console.WriteLine(
        $"MESSUNG Delta-Test: Aufträge vorhanden=100, geändert=1, übertragen=1; Anhänge vorhanden=1, geändert=1, übertragen=1; Vollpaket={fullBytes} Byte, Deltaantwort={deltaBytes} Byte.");
}

static LocalSyncSnapshotPackage CreateDeltaSnapshotPackage(
    string root,
    string name,
    string? taskTitle,
    string categoryName,
    string technicianName,
    byte[]? attachmentBytes,
    int additionalUnchangedTasks = 0)
{
    var path = Path.Combine(root, $"{name}.bcsnapshot");
    using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
    using var archive = new ZipArchive(file, ZipArchiveMode.Create);

    WriteZipJson(archive, "metadata.json", new
    {
        schemaVersion = 1,
        createdAt = DateTimeOffset.UtcNow,
        appVersion = "isolated-test",
        deviceName = "Test"
    });
    WriteZipJson(archive, "categories.json", new[]
    {
        new { id = "category-1", name = categoryName, parentId = (string?)null, sortOrder = 1, isVisible = true }
    });
    WriteZipJson(archive, "technicians.json", new[]
    {
        new { id = "technician-1", name = technicianName, details = "" }
    });
    var tasks = new List<object>();
    if (taskTitle is not null)
    {
        tasks.Add(new
        {
            id = "task-1",
            title = taskTitle,
            currentCategoryId = "category-1",
            status = "Auftrag",
            notes = "Gezielt geänderter Auftrag",
            updatedAt = "2026-07-18T00:00:00Z"
        });
    }
    for (var index = 2; index <= additionalUnchangedTasks + 1; index++)
    {
        tasks.Add(new
        {
            id = $"task-{index}",
            title = $"Unveränderter Auftrag {index:000}",
            currentCategoryId = "category-1",
            status = "Auftrag",
            notes = $"Unveränderter isolierter Testinhalt für Auftrag {index:000}.",
            updatedAt = "2026-07-17T00:00:00Z"
        });
    }
    WriteZipJson(archive, "tasks.json", tasks);

    if (attachmentBytes is null)
    {
        WriteZipJson(archive, "attachments-index.json", Array.Empty<object>());
    }
    else
    {
        var attachmentHash = Convert.ToHexString(SHA256.HashData(attachmentBytes)).ToLowerInvariant();
        WriteZipJson(archive, "attachments-index.json", new[]
        {
            new
            {
                id = "attachment-1",
                taskId = "task-1",
                fileName = "foto.jpg",
                packagePath = "attachments/foto.jpg",
                previewPath = "previews/foto.jpg",
                contentHash = attachmentHash
            }
        });
        WriteZipBytes(archive, "attachments/foto.jpg", attachmentBytes);
        WriteZipBytes(archive, "previews/foto.jpg", attachmentBytes);
    }

    return new LocalSyncSnapshotPackage(path, Path.GetFileName(path), name, DateTimeOffset.UtcNow);
}

static void WriteZipJson<T>(ZipArchive archive, string path, T value)
{
    var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
    using var stream = entry.Open();
    JsonSerializer.Serialize(stream, value);
}

static void WriteZipBytes(ZipArchive archive, string path, byte[] value)
{
    var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
    using var stream = entry.Open();
    stream.Write(value);
}

static async Task AssertLocalSyncTransferAsync(string tempRoot)
{
    var dataRoot = Path.Combine(tempRoot, "local-sync-store");
    Directory.CreateDirectory(dataRoot);
    var store = new LocalSyncInboxStore(dataRoot);
    var request = CreateUploadRequest("mobile-test-001", "ipad-test", "Erster Inhalt");

    var accepted = store.Accept(request);
    Assert(accepted.Status == "accepted" && accepted.TransferredObjects == 1 && accepted.TransferredPhotos == 1,
        "Ein vollständiges mobiles Paket wird genau einmal angenommen und bestätigt.");
    var originalPath = Path.Combine(dataRoot, "Sync", "inbox", "mobile-test-001", "originals", "foto.jpg");
    var expectedOriginal = request.Files.Single(file => file.RelativePath == "originals/foto.jpg").Data;
    Assert(File.ReadAllBytes(originalPath).SequenceEqual(expectedOriginal),
        "Das Desktop-Ziel enthält das unveränderte Foto-Original mit bestätigter Prüfsumme.");
    Assert(new MobileInboxLoader().Load(dataRoot).Single().Id == request.UploadId,
        "Der angenommene Netzwerkeingang ist im vorhandenen Desktop-Mobile-Inbox-Loader sichtbar.");

    var versionedRequest = CreateVersionedUpdateRequest(
        "mobile-update-001",
        request.DeviceId,
        "desktop-task-001",
        DateTimeOffset.UtcNow.AddMinutes(-10));
    var versionedAccepted = store.Accept(versionedRequest);
    Assert(versionedAccepted.Status == "accepted",
        "Ein vollständiges versioniertes Änderungspaket wird über denselben konservativen Inbox-Weg angenommen.");
    var loadedVersioned = new MobileInboxLoader().Load(dataRoot).Single(entry => entry.Id == versionedRequest.UploadId);
    Assert(loadedVersioned.IsDesktopUpdate &&
           loadedVersioned.DesktopTaskId == "desktop-task-001" &&
           loadedVersioned.BaseValues?.Notes == "Basisnotiz",
        "Der Desktop-Loader erhält Desktop-ID, Basisrevision und Basiswerte des iPad-Pakets.");

    var skipped = store.Accept(request);
    Assert(skipped.Status == "skipped" && skipped.SkippedObjects == 1,
        "Die identische stabile Objekt-ID mit identischem Inhalt wird idempotent übersprungen.");
    Assert(Directory.EnumerateDirectories(Path.Combine(dataRoot, "Sync", "inbox"), "mobile-*").Count() == 2,
        "Eine Wiederholung erzeugt neben den zwei fachlich verschiedenen Testpaketen keinen weiteren Eingangsordner.");

    var conflict = store.Accept(CreateUploadRequest(request.UploadId, request.DeviceId, "Geänderter Inhalt"));
    Assert(conflict.Status == "conflict" && conflict.FailedObjects == 1,
        "Geänderter Inhalt unter derselben stabilen ID wird als Konflikt erhalten und nicht überschrieben.");
    var persistedTaskJson = await File.ReadAllTextAsync(Path.Combine(dataRoot, "Sync", "inbox", "mobile-test-001", "aufgabe.json"));
    Assert(persistedTaskJson.Contains("Erster Inhalt", StringComparison.Ordinal),
        "Der konservative Konfliktpfad lässt den zuerst bestätigten Desktop-Inhalt unverändert.");
    Assert(Directory.EnumerateDirectories(Path.Combine(dataRoot, "Sync", "conflicts")).Count() == 1,
        "Der abweichende Konfliktinhalt bleibt separat zur manuellen Prüfung erhalten.");

    var badChecksumFiles = request.Files.Select(file => file.RelativePath == "originals/foto.jpg"
        ? file with { Sha256 = new string('0', 64) }
        : file).ToList();
    var invalidChecksum = store.Accept(request with { UploadId = "mobile-test-002", Files = badChecksumFiles });
    Assert(invalidChecksum.Status == "invalid" &&
           invalidChecksum.Messages.Any(message => message.Contains("Prüfsumme", StringComparison.Ordinal)) &&
           !Directory.Exists(Path.Combine(dataRoot, "Sync", "inbox", "mobile-test-002")),
        "Ein Paket mit falscher Prüfsumme wird ohne sichtbaren Teilimport abgelehnt.");

    var missingTask = store.Accept(request with
    {
        UploadId = "mobile-test-003",
        Files = request.Files.Where(file => file.RelativePath != "aufgabe.json").ToList()
    });
    Assert(missingTask.Status == "invalid" && !Directory.EnumerateDirectories(Path.Combine(dataRoot, "Sync", "inbox"), ".staging-*").Any(),
        "Ein unvollständiges Paket hinterlässt keinen Staging- oder Eingangsordner.");

    var richRequest = CreateUploadRequest("mobile-test-rich", request.DeviceId, "Mehrere Anlagen");
    var richFiles = richRequest.Files.Concat(new[]
    {
        UploadFile("originals/foto-2.jpg", "image/jpeg", "original-photo", new byte[] { 0xFF, 0xD8, 0xFF, 0xE1, 0x10, 0x11, 0xFF, 0xD9 }),
        UploadFile("previews/foto-2.jpg", "image/jpeg", "photo-preview", new byte[] { 0xFF, 0xD8, 0xFF, 0xE1, 0x12, 0x13, 0xFF, 0xD9 }),
        UploadFile("sketches/skizze.png", "image/png", "sketch", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01 }),
        UploadFile("sketches/skizze.pkdrawing", "application/x-pkdrawing", "editable-sketch", new byte[] { 0x01, 0x02, 0x03 }),
        UploadFile("files/notiz.pdf", "application/pdf", "attachment", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37 })
    }).ToList();
    var richResult = store.Accept(richRequest with { Files = richFiles });
    Assert(richResult.Status == "accepted" && richResult.TransferredPhotos == 2 && richResult.TransferredFiles == richFiles.Count,
        "Ein Paket mit mehreren Fotos, Vorschauen, bearbeitbarer Skizze und Datei wird vollständig gezählt und abgelegt.");

    var recoveryRequest = CreateUploadRequest("mobile-test-recovery", request.DeviceId, "Wiederaufnahme");
    Assert(store.Accept(recoveryRequest).Status == "accepted", "Das Wiederaufnahme-Testpaket wird zunächst vollständig abgelegt.");
    File.Delete(Path.Combine(dataRoot, "Sync", "receipts", "mobile-test-recovery.json"));
    var recovered = new LocalSyncInboxStore(dataRoot).Accept(recoveryRequest);
    Assert(recovered.Status == "skipped" && recovered.SkippedObjects == 1,
        "Nach Abbruch zwischen atomarer Ablage und Bestätigungsbeleg wird das vollständige Paket wiedererkannt.");
    Assert(Directory.EnumerateDirectories(Path.Combine(dataRoot, "Sync", "inbox"), "mobile-test-recovery").Count() == 1,
        "Die Wiederaufnahme nach Bestätigungsabbruch erzeugt kein Duplikat.");

    await AssertAuthenticatedEndpointAsync(tempRoot);
}

static async Task AssertAuthenticatedEndpointAsync(string tempRoot)
{
    var dataRoot = Path.Combine(tempRoot, "local-sync-http");
    Directory.CreateDirectory(dataRoot);
    var devicePath = Path.Combine(tempRoot, "local-sync-local", "devices.json");
    var deviceStore = new LocalNetworkDeviceStore(devicePath);
    var snapshotPackage = CreateDeltaSnapshotPackage(
        tempRoot,
        "local-sync-snapshot-test",
        taskTitle: "HTTP-Test",
        categoryName: "HTTP-Kategorie",
        technicianName: "HTTP-Monteur",
        attachmentBytes: [0x10, 0x11, 0x12]);
    var snapshotPath = snapshotPackage.FilePath;
    var snapshotBytes = await File.ReadAllBytesAsync(snapshotPath);
    var snapshotProviderCallCount = 0;
    var port = ReserveFreePort();
    var deltaStore = new LocalSyncDeltaStore(
        Path.Combine(tempRoot, "local-sync-local", "sync-state.json"));
    var service = new LocalSyncService(
        new LocalSyncOptions
        {
            Enabled = true,
            Port = port,
            DeviceName = "Isolierter Test-Desktop",
            DeviceId = "desktop-test",
            AppVersion = "test",
            DataFolderPath = dataRoot,
            AnnounceBonjour = false
        },
        deviceStore,
        _ =>
        {
            Interlocked.Increment(ref snapshotProviderCallCount);
            return Task.FromResult(new LocalSyncSnapshotPackage(
                snapshotPath,
                "BueroCockpit.bcsnapshot",
                "test-change-1",
                DateTimeOffset.UtcNow));
        },
        deltaStore);

    var started = await service.StartAsync();
    Assert(started.Message?.Contains("Running", StringComparison.OrdinalIgnoreCase) == true,
        "Der isolierte lokale HTTP-Testdienst startet ohne Bonjour-Ankündigung.");

    using var http = new HttpClient(new SocketsHttpHandler { UseProxy = false })
    {
        BaseAddress = new Uri($"http://127.0.0.1:{port}"),
        Timeout = TimeSpan.FromSeconds(10)
    };
    try
    {
        var request = CreateUploadRequest("mobile-http-001", "ipad-http", "Netzwerktest");
        var unauthorized = await http.PostAsJsonAsync("/local-sync/mobile-inbox", request);
        Assert(unauthorized.StatusCode == HttpStatusCode.Forbidden,
            "Ohne gültigen Kopplungsnachweis ist kein Mobile-Inbox-Upload möglich.");
        Assert(!Directory.Exists(Path.Combine(dataRoot, "Sync", "inbox")),
            "Ein unautorisierter Versuch schreibt keine produktnahen Eingangsdaten.");
        var unauthorizedSnapshot = await http.GetAsync("/local-sync/snapshot");
        Assert(unauthorizedSnapshot.StatusCode == HttpStatusCode.Forbidden && snapshotProviderCallCount == 0,
            "Ohne gültigen Kopplungsnachweis werden weder Desktopdaten noch der Snapshot-Provider geöffnet.");

        const string trustKey = "isolated-secret-that-remains-on-the-ipad";
        var remembered = await http.PostAsJsonAsync(
            "/local-sync/devices/remember",
            new LocalNetworkDeviceRememberRequest("ipad-http", "Test-iPad", "iPadOS", "test", SharedSecret: trustKey));
        Assert(remembered.IsSuccessStatusCode && deviceStore.GetPairingState("ipad-http", trustKey, out _) == LocalNetworkPairingState.Pending,
            "Ein neues iPad wird nur vorgemerkt und wartet auf die ausdrückliche Desktop-Freigabe.");
        var storedDeviceJson = await File.ReadAllTextAsync(devicePath);
        Assert(!storedDeviceJson.Contains(trustKey, StringComparison.Ordinal),
            "Der Desktop speichert nur den Hash und nicht den geheimen Kopplungsnachweis.");

        Assert(deviceStore.SetTrusted("ipad-http", true), "Die Desktop-Freigabe setzt den vorgemerkten Nachweis auf vertrauenswürdig.");
        using (var invalidPairing = new HttpRequestMessage(HttpMethod.Get, "/local-sync/pairing/status"))
        {
            invalidPairing.Headers.Add("X-BueroCockpit-Device-Id", "ipad-http");
            invalidPairing.Headers.Add("X-BueroCockpit-Trust-Key", "wrong-secret");
            var invalidPairingResponse = await http.SendAsync(invalidPairing);
            Assert(invalidPairingResponse.StatusCode == HttpStatusCode.Forbidden,
                "Ein falscher Kopplungsnachweis wird auch für die Statusprüfung abgelehnt.");
        }
        using (var validPairing = new HttpRequestMessage(HttpMethod.Get, "/local-sync/pairing/status"))
        {
            validPairing.Headers.Add("X-BueroCockpit-Device-Id", "ipad-http");
            validPairing.Headers.Add("X-BueroCockpit-Trust-Key", trustKey);
            var validPairingResponse = await http.SendAsync(validPairing);
            Assert(validPairingResponse.IsSuccessStatusCode,
                "Der gültige, manuell freigegebene Kopplungsnachweis wird bestätigt.");
        }
        using var authorizedRequest = new HttpRequestMessage(HttpMethod.Post, "/local-sync/mobile-inbox")
        {
            Content = JsonContent.Create(request)
        };
        authorizedRequest.Headers.Add("X-BueroCockpit-Device-Id", "ipad-http");
        authorizedRequest.Headers.Add("X-BueroCockpit-Trust-Key", trustKey);
        var authorized = await http.SendAsync(authorizedRequest);
        var authorizedBody = await authorized.Content.ReadAsStringAsync();
        Assert(authorized.IsSuccessStatusCode,
            $"Ein manuell freigegebenes iPad kann ein geprüftes Paket an den Desktop übertragen. HTTP {(int)authorized.StatusCode}: {authorizedBody}");

        using (var snapshotRequest = new HttpRequestMessage(HttpMethod.Get, "/local-sync/snapshot"))
        {
            snapshotRequest.Headers.Add("X-BueroCockpit-Device-Id", "ipad-http");
            snapshotRequest.Headers.Add("X-BueroCockpit-Trust-Key", trustKey);
            var fullTransferTimer = System.Diagnostics.Stopwatch.StartNew();
            var snapshotResponse = await http.SendAsync(snapshotRequest);
            var receivedSnapshot = await snapshotResponse.Content.ReadAsByteArrayAsync();
            fullTransferTimer.Stop();
            Assert(snapshotResponse.IsSuccessStatusCode &&
                   receivedSnapshot.SequenceEqual(snapshotBytes) &&
                   snapshotProviderCallCount == 1 &&
                   snapshotResponse.Headers.TryGetValues("X-BueroCockpit-Snapshot-Schema", out var schemaValues) &&
                   schemaValues.Single() == "local-sync-snapshot-v1",
                "Ein freigegebenes iPad erhält den versionierten Desktop-Snapshot unverändert.");

            var ackToken = snapshotResponse.Headers.GetValues("X-BueroCockpit-Ack-Token").Single();
            var revision = snapshotResponse.Headers.GetValues("X-BueroCockpit-Change-Version").Single();
            using var ackRequest = new HttpRequestMessage(HttpMethod.Post, "/local-sync/ack")
            {
                Content = JsonContent.Create(new LocalSyncAckRequest(ackToken, revision))
            };
            ackRequest.Headers.Add("X-BueroCockpit-Device-Id", "ipad-http");
            ackRequest.Headers.Add("X-BueroCockpit-Trust-Key", trustKey);
            var ackResponse = await http.SendAsync(ackRequest);
            Assert(ackResponse.IsSuccessStatusCode,
                "Der vollständige Erstabgleich setzt seinen Geräte-Checkpoint erst nach passender Quittierung.");

            using var deltaRequest = new HttpRequestMessage(
                HttpMethod.Get,
                $"/local-sync/changes?since={Uri.EscapeDataString(revision)}");
            deltaRequest.Headers.Add("X-BueroCockpit-Device-Id", "ipad-http");
            deltaRequest.Headers.Add("X-BueroCockpit-Trust-Key", trustKey);
            var deltaTransferTimer = System.Diagnostics.Stopwatch.StartNew();
            var deltaResponse = await http.SendAsync(deltaRequest);
            var deltaPayload = await deltaResponse.Content.ReadAsByteArrayAsync();
            deltaTransferTimer.Stop();
            var decodedDelta = JsonSerializer.Deserialize<LocalSyncDeltaResponse>(
                deltaPayload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert(deltaResponse.IsSuccessStatusCode &&
                   decodedDelta is { RequiresFullSync: false, Counts.Tasks: 0, Counts.Files: 0 },
                "Der HTTP-Delta-Endpunkt liefert nach dem bestätigten Erstabgleich keine unveränderten Objekte.");
            Console.WriteLine(
                $"MESSUNG HTTP-Loopback: Vollabgleich={receivedSnapshot.LongLength} Byte/{fullTransferTimer.Elapsed.TotalMilliseconds:F2} ms; unverändertes Delta={deltaPayload.LongLength} Byte/{deltaTransferTimer.Elapsed.TotalMilliseconds:F2} ms.");
        }

        using var interrupted = new HttpRequestMessage(HttpMethod.Post, "/local-sync/mobile-inbox")
        {
            Content = new StringContent("{\"uploadId\":\"mobile-http-broken\"", System.Text.Encoding.UTF8, "application/json")
        };
        interrupted.Headers.Add("X-BueroCockpit-Device-Id", "ipad-http");
        interrupted.Headers.Add("X-BueroCockpit-Trust-Key", trustKey);
        var interruptedResponse = await http.SendAsync(interrupted);
        Assert(interruptedResponse.StatusCode == HttpStatusCode.BadRequest &&
               !Directory.Exists(Path.Combine(dataRoot, "Sync", "inbox", "mobile-http-broken")),
            "Eine abgebrochene oder unvollständige JSON-Übertragung wird ohne Teilimport zurückgewiesen.");

        Assert(deltaStore.DeleteDeviceCheckpoint("ipad-http") &&
               deviceStore.Delete("ipad-http"),
            "Beim Löschen eines gekoppelten Geräts werden nur lokale Freigabe und Geräte-Checkpoint entfernt.");
        Assert(deviceStore.GetPairingState("ipad-http", trustKey, out _) == LocalNetworkPairingState.Missing &&
               !deviceStore.Delete("ipad-http"),
            "Das gelöschte Gerät ist sofort nicht mehr freigegeben und ein erneutes Löschen bleibt folgenlos.");
        using var deletedDeviceRequest = new HttpRequestMessage(HttpMethod.Get, "/local-sync/pairing/status");
        deletedDeviceRequest.Headers.Add("X-BueroCockpit-Device-Id", "ipad-http");
        deletedDeviceRequest.Headers.Add("X-BueroCockpit-Trust-Key", trustKey);
        var deletedDeviceResponse = await http.SendAsync(deletedDeviceRequest);
        Assert(deletedDeviceResponse.StatusCode == HttpStatusCode.Forbidden,
            "Ein gelöschtes Gerät verliert auch bei laufendem Sync-Dienst sofort den Zugriff.");
        Assert(Directory.Exists(Path.Combine(dataRoot, "Sync", "inbox")),
            "Das Löschen der Gerätekopplung entfernt keine bereits empfangenen mobilen Eingangsdaten.");
    }
    finally
    {
        await service.StopAsync();
    }
}

static MobileInboxUploadRequest CreateUploadRequest(string uploadId, string deviceId, string notes)
{
    var taskData = JsonSerializer.SerializeToUtf8Bytes(new
    {
        id = uploadId,
        schemaVersion = 1,
        createdAt = DateTimeOffset.UtcNow,
        source = "isolated-test",
        status = "new",
        customerName = "Testkunde",
        address = "",
        phone = "",
        email = "",
        title = "Mobiler Testeingang",
        category = "Test",
        notes,
        photos = new[]
        {
            new { id = "photo-1", originalPath = "originals/foto.jpg", previewPath = "previews/foto.jpg" }
        },
        sketches = Array.Empty<object>(),
        files = Array.Empty<object>()
    });
    var jpegOriginal = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02, 0x03, 0xFF, 0xD9 };
    var jpegPreview = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x04, 0x05, 0xFF, 0xD9 };
    var files = new List<MobileInboxUploadFile>
    {
        UploadFile("aufgabe.json", "application/json", "task", taskData),
        UploadFile("originals/foto.jpg", "image/jpeg", "original-photo", jpegOriginal),
        UploadFile("previews/foto.jpg", "image/jpeg", "preview-photo", jpegPreview)
    };
    return new MobileInboxUploadRequest(uploadId, deviceId, "local-sync-inbox-v1", DateTimeOffset.UtcNow, files);
}

static MobileInboxUploadRequest CreateVersionedUpdateRequest(
    string uploadId,
    string deviceId,
    string desktopTaskId,
    DateTimeOffset baseRevision)
{
    var taskData = JsonSerializer.SerializeToUtf8Bytes(new
    {
        id = uploadId,
        schemaVersion = 2,
        createdAt = DateTimeOffset.UtcNow,
        source = "isolated-test",
        status = "new",
        operation = "update",
        desktopTaskId,
        baseRevision = baseRevision.ToString("O"),
        confirmedRevision = baseRevision.ToString("O"),
        baseValues = new
        {
            notes = "Basisnotiz",
            categoryId = "category-1",
            workflowType = WorkflowCategoryService.DirectWorkflowType,
            workflowStep = "Auftrag",
            dueDate = (DateTime?)null,
            followUpDate = (DateTime?)null,
            followUpReason = "",
            technician = ""
        },
        customerName = "Testkunde",
        address = "",
        phone = "",
        email = "",
        title = "Versionierte Änderung",
        category = "Test",
        categoryId = "category-1",
        workflowType = WorkflowCategoryService.DirectWorkflowType,
        workflowStep = "Auftrag",
        dueDate = (DateTime?)null,
        followUpDate = (DateTime?)null,
        followUpReason = "",
        technician = "",
        notes = "iPad-Notiz",
        photos = Array.Empty<object>(),
        sketches = Array.Empty<object>(),
        files = Array.Empty<object>()
    });
    return new MobileInboxUploadRequest(
        uploadId,
        deviceId,
        "local-sync-inbox-v2",
        DateTimeOffset.UtcNow,
        [UploadFile("aufgabe.json", "application/json", "task", taskData)]);
}

static MobileInboxUploadFile UploadFile(string path, string contentType, string purpose, byte[] data) =>
    new(path, contentType, data.LongLength, Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant(), purpose, data);

static int ReserveFreePort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}
