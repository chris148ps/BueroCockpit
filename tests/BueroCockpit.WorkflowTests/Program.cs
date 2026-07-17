using System.Net;
using System.Net.Sockets;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using BueroCockpit.Data;
using BueroCockpit.Models;
using BueroCockpit.Services;
using BueroCockpit.Services.LocalSync;

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

    var skipped = store.Accept(request);
    Assert(skipped.Status == "skipped" && skipped.SkippedObjects == 1,
        "Die identische stabile Objekt-ID mit identischem Inhalt wird idempotent übersprungen.");
    Assert(Directory.EnumerateDirectories(Path.Combine(dataRoot, "Sync", "inbox"), "mobile-*").Count() == 1,
        "Eine Wiederholung erzeugt weder einen zweiten Vorgang noch einen zweiten Eingangsordner.");

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
    var port = ReserveFreePort();
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
        deviceStore);

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
