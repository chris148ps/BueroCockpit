using System.Text.Json;
using BueroCockpit.Data;
using BueroCockpit.Models;
using BueroCockpit.Services;

var tempRoot = Path.Combine(Path.GetTempPath(), $"BueroCockpit-WorkflowTests-{Guid.NewGuid():N}");
Directory.CreateDirectory(tempRoot);

try
{
    var repository = new BueroRepository(Path.Combine(tempRoot, "data", "workflow-tests.db"));
    repository.Initialize();

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

    third.IsVisible = true;
    repository.SaveCategory(third);
    var exportRoot = Path.Combine(tempRoot, "export");
    var exportResult = await new IpadSnapshotExportService().ExportLiveNowAsync(
        repository,
        exportRoot,
        "test",
        "isolated-test");
    Assert(exportResult.Success, "Der isolierte Snapshot-Export war erfolgreich.");
    var tasksJsonPath = Path.Combine(exportRoot, "Sync", "live", "tasks.json");
    using var tasksDocument = JsonDocument.Parse(await File.ReadAllTextAsync(tasksJsonPath));
    var exportedTask = tasksDocument.RootElement.EnumerateArray().Single(item =>
        item.GetProperty("id").GetString() == legacyTask.Id);
    Assert(exportedTask.GetProperty("currentCategoryId").GetString() == third.Id, "Der Export enthält currentCategoryId.");
    Assert(exportedTask.GetProperty("workflowType").GetString() == WorkflowCategoryService.DirectWorkflowType, "Der Export enthält workflowType.");
    Assert(exportedTask.GetProperty("workflowStep").GetString() == "Auftrag", "Der Export enthält workflowStep.");
    Assert(exportedTask.GetProperty("status").GetString() == "Auftrag", "Der Export enthält status.");

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
