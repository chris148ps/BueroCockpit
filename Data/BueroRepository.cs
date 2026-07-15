using System.Text;
using BueroCockpit.Models;
using Microsoft.Data.Sqlite;

namespace BueroCockpit.Data;

public sealed class BueroRepository
{
    private readonly string _databasePath;
    private readonly string _connectionString;
    public event Action<string>? DataWritten;

    public BueroRepository()
        : this(AppPaths.DatabasePath)
    {
    }

    public BueroRepository(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Der Datenbankpfad darf nicht leer sein.", nameof(databasePath));
        }

        _databasePath = Path.GetFullPath(databasePath);
        try
        {
            if (string.Equals(_databasePath, AppPaths.DatabasePath, StringComparison.Ordinal))
            {
                AppPaths.EnsureBaseDirectories();
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
            }
        }
        catch (Exception ex)
        {
            throw CreateStartupException("Der Datenordner konnte nicht vorbereitet werden.", ex);
        }

        _connectionString = new SqliteConnectionStringBuilder { DataSource = _databasePath }.ToString();
    }

    public void Initialize()
    {
        using var connection = OpenConnection();

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS Categories (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                ParentId TEXT NULL,
                SortOrder INTEGER NOT NULL,
                SortMode TEXT NOT NULL DEFAULT 'Erstellt am',
                Color TEXT NOT NULL,
                IsVisible INTEGER NOT NULL
            );
            """);

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS Tasks (
                Id TEXT PRIMARY KEY,
                Title TEXT NOT NULL,
                CustomerName TEXT NOT NULL,
                Description TEXT NOT NULL,
                CategoryId TEXT NOT NULL,
                Status TEXT NOT NULL,
                WorkflowType TEXT NOT NULL DEFAULT '',
                WorkflowStep TEXT NOT NULL DEFAULT '',
                Priority TEXT NOT NULL,
                DueDate TEXT NULL,
                FollowUpDate TEXT NULL,
                SentAt TEXT NULL,
                MaterialOrderedAt TEXT NULL,
                CustomerAddress TEXT NOT NULL DEFAULT '',
                CustomerEmail TEXT NOT NULL DEFAULT '',
                CustomerPhone TEXT NOT NULL DEFAULT '',
                Technician TEXT NOT NULL DEFAULT '',
                SortPosition REAL NOT NULL DEFAULT 0,
                AssignedTo TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                CompletedAt TEXT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0,
                DeletedAt TEXT NULL
            );
            """);

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS TaskCategories (
                TaskId TEXT NOT NULL,
                CategoryId TEXT NOT NULL,
                PRIMARY KEY (TaskId, CategoryId),
                FOREIGN KEY (TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE,
                FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Materials (
                Id TEXT PRIMARY KEY,
                TaskId TEXT NOT NULL,
                Quantity REAL NOT NULL,
                Unit TEXT NOT NULL,
                Name TEXT NOT NULL,
                Status TEXT NOT NULL,
                Supplier TEXT NOT NULL,
                OrderedAt TEXT NULL,
                Note TEXT NOT NULL
            );
            """);

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS WorkflowCategoryMappings (
                WorkflowType TEXT NOT NULL,
                WorkflowStep TEXT NOT NULL,
                CategoryId TEXT NOT NULL,
                PRIMARY KEY (WorkflowType, WorkflowStep)
            );
            """);

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS Attachments (
                Id TEXT PRIMARY KEY,
                TaskId TEXT NOT NULL,
                FileName TEXT NOT NULL,
                StoredPath TEXT NOT NULL,
                ThumbnailPath TEXT NOT NULL,
                FileType TEXT NOT NULL,
                ContentHash TEXT NOT NULL DEFAULT '',
                AddedAt TEXT NOT NULL
            );
            """);

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS AttachmentEditSessions (
                Id TEXT PRIMARY KEY,
                AttachmentId TEXT NOT NULL,
                TaskId TEXT NOT NULL,
                ExportPath TEXT NOT NULL,
                BackupPath TEXT NULL,
                ExportedAt TEXT NOT NULL,
                OriginalHashAtExport TEXT NOT NULL,
                ExportedFileHashAtExport TEXT NOT NULL,
                Status TEXT NOT NULL,
                ImportedAt TEXT NULL
            );
            """);

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS DeskItems (
                Id TEXT PRIMARY KEY,
                Type TEXT NOT NULL,
                Text TEXT NOT NULL,
                PdfPath TEXT NOT NULL DEFAULT '',
                FileName TEXT NOT NULL DEFAULT '',
                DisplayName TEXT NOT NULL DEFAULT '',
                ReferencePath TEXT NOT NULL DEFAULT '',
                PdfThumbnailPath TEXT NOT NULL DEFAULT '',
                LinkedTaskId TEXT NOT NULL DEFAULT '',
                ContentHash TEXT NOT NULL DEFAULT '',
                X REAL NOT NULL,
                Y REAL NOT NULL,
                Width REAL NOT NULL,
                Height REAL NOT NULL,
                IsImportant INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            """);

        MigrateSchema(connection);

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS TaskCategories (
                TaskId TEXT NOT NULL,
                CategoryId TEXT NOT NULL,
                PRIMARY KEY (TaskId, CategoryId),
                FOREIGN KEY (TaskId) REFERENCES Tasks(Id) ON DELETE CASCADE,
                FOREIGN KEY (CategoryId) REFERENCES Categories(Id) ON DELETE CASCADE
            );
            """);

        ExecuteNonQuery(connection, """
            INSERT OR IGNORE INTO TaskCategories (TaskId, CategoryId)
            SELECT Id, CategoryId
            FROM Tasks
            WHERE IFNULL(CategoryId, '') <> '';
            """);

        SeedDefaultCategories(connection);
    }

    public List<WorkflowCategoryMapping> GetWorkflowCategoryMappings()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT WorkflowType, WorkflowStep, CategoryId
            FROM WorkflowCategoryMappings
            ORDER BY WorkflowType, WorkflowStep;
            """;

        using var reader = command.ExecuteReader();
        var mappings = new List<WorkflowCategoryMapping>();
        while (reader.Read())
        {
            mappings.Add(new WorkflowCategoryMapping(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2)));
        }

        return mappings;
    }

    public void SaveWorkflowCategoryMapping(string workflowType, string workflowStep, string categoryId)
    {
        if (string.IsNullOrWhiteSpace(workflowType) ||
            string.IsNullOrWhiteSpace(workflowStep) ||
            string.IsNullOrWhiteSpace(categoryId))
        {
            throw new ArgumentException("Vorgangstyp, Workflowstatus und Kategorie-ID müssen gesetzt sein.");
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO WorkflowCategoryMappings (WorkflowType, WorkflowStep, CategoryId)
            VALUES ($workflowType, $workflowStep, $categoryId)
            ON CONFLICT(WorkflowType, WorkflowStep) DO UPDATE SET
                CategoryId = excluded.CategoryId;
            """;
        command.Parameters.AddWithValue("$workflowType", workflowType.Trim());
        command.Parameters.AddWithValue("$workflowStep", workflowStep.Trim());
        command.Parameters.AddWithValue("$categoryId", categoryId.Trim());
        command.ExecuteNonQuery();
        NotifyDataWritten("Workflow-Kategoriezuordnung gespeichert");
    }

    public void DeleteWorkflowCategoryMapping(string workflowType, string workflowStep)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM WorkflowCategoryMappings
            WHERE WorkflowType = $workflowType AND WorkflowStep = $workflowStep;
            """;
        command.Parameters.AddWithValue("$workflowType", workflowType.Trim());
        command.Parameters.AddWithValue("$workflowStep", workflowStep.Trim());
        command.ExecuteNonQuery();
        NotifyDataWritten("Workflow-Kategoriezuordnung entfernt");
    }

    public int ReplaceWorkflowCategoryMappings(string categoryId, string replacementCategoryId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE WorkflowCategoryMappings
            SET CategoryId = $replacementCategoryId
            WHERE CategoryId = $categoryId;
            """;
        command.Parameters.AddWithValue("$categoryId", categoryId.Trim());
        command.Parameters.AddWithValue("$replacementCategoryId", replacementCategoryId.Trim());
        var changed = command.ExecuteNonQuery();
        if (changed > 0)
        {
            NotifyDataWritten("Workflow-Kategoriezuordnungen ersetzt");
        }

        return changed;
    }

    public int DeleteWorkflowCategoryMappingsForCategory(string categoryId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM WorkflowCategoryMappings WHERE CategoryId = $categoryId;";
        command.Parameters.AddWithValue("$categoryId", categoryId.Trim());
        var changed = command.ExecuteNonQuery();
        if (changed > 0)
        {
            NotifyDataWritten("Workflow-Kategoriezuordnungen entfernt");
        }

        return changed;
    }

    public List<CategoryItem> GetCategories()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, ParentId, SortOrder, SortMode, Color, IsVisible
            FROM Categories
            WHERE IsVisible = 1
            ORDER BY COALESCE(ParentId, ''), SortOrder, Name;
            """;

        using var reader = command.ExecuteReader();
        var items = new List<CategoryItem>();
        while (reader.Read())
        {
            items.Add(new CategoryItem
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                ParentId = reader.IsDBNull(2) ? null : reader.GetString(2),
                SortOrder = reader.GetInt32(3),
                SortMode = reader.GetString(4),
                Color = reader.GetString(5),
                IsVisible = reader.GetInt32(6) == 1
            });
        }

        return items;
    }

    public List<CategoryItem> GetAllCategories()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, ParentId, SortOrder, SortMode, Color, IsVisible
            FROM Categories
            ORDER BY COALESCE(ParentId, ''), SortOrder, Name;
            """;

        using var reader = command.ExecuteReader();
        var items = new List<CategoryItem>();
        while (reader.Read())
        {
            items.Add(new CategoryItem
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                ParentId = reader.IsDBNull(2) ? null : reader.GetString(2),
                SortOrder = reader.GetInt32(3),
                SortMode = reader.GetString(4),
                Color = reader.GetString(5),
                IsVisible = reader.GetInt32(6) == 1
            });
        }

        return items;
    }

    public List<TaskItem> GetTasks()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Title, CustomerName, Description, CategoryId, Status, WorkflowType, WorkflowStep, Priority,
                   DueDate, FollowUpDate, SentAt, MaterialOrderedAt, CustomerAddress, CustomerEmail, CustomerPhone, Technician, SortPosition,
                   AssignedTo, CreatedAt, UpdatedAt, CompletedAt, IsDeleted, DeletedAt
            FROM Tasks
            ORDER BY UpdatedAt DESC;
            """;

        using var reader = command.ExecuteReader();
        var items = new List<TaskItem>();
        while (reader.Read())
        {
            items.Add(new TaskItem
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                CustomerName = reader.GetString(2),
                Description = reader.GetString(3),
                CategoryId = reader.GetString(4),
                Status = reader.GetString(5),
                WorkflowType = reader.GetString(6),
                WorkflowStep = reader.GetString(7),
                Priority = reader.GetString(8),
                DueDate = ReadNullableDate(reader, 9),
                FollowUpDate = ReadNullableDate(reader, 10),
                SentAt = ReadNullableDate(reader, 11),
                MaterialOrderedAt = ReadNullableDate(reader, 12),
                CustomerAddress = reader.GetString(13),
                CustomerEmail = reader.GetString(14),
                CustomerPhone = reader.GetString(15),
                Technician = reader.GetString(16),
                SortPosition = reader.GetDouble(17),
                AssignedTo = reader.GetString(18),
                CreatedAt = ReadDateOrFallback(reader.GetString(19), ReadDate(reader.GetString(20))),
                UpdatedAt = ReadDate(reader.GetString(20)),
                CompletedAt = ReadNullableDate(reader, 21),
                IsDeleted = reader.GetInt32(22) == 1,
                DeletedAt = ReadNullableDate(reader, 23)
            });
        }

        LoadTaskCategories(items);
        return items;
    }

    public void SaveCategory(CategoryItem category)
    {
        ArgumentNullException.ThrowIfNull(category);
        if (string.IsNullOrWhiteSpace(category.Name))
        {
            throw new ArgumentException("Eine Kategorie benötigt einen Namen.", nameof(category));
        }

        category.Name = category.Name.Trim();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Categories (Id, Name, ParentId, SortOrder, SortMode, Color, IsVisible)
            VALUES ($id, $name, $parentId, $sortOrder, $sortMode, $color, $isVisible)
            ON CONFLICT(Id) DO UPDATE SET
                Name = excluded.Name,
                ParentId = excluded.ParentId,
                SortOrder = excluded.SortOrder,
                SortMode = excluded.SortMode,
                Color = excluded.Color,
                IsVisible = excluded.IsVisible;
            """;
        command.Parameters.AddWithValue("$id", category.Id);
        command.Parameters.AddWithValue("$name", category.Name);
        command.Parameters.AddWithValue("$parentId", string.IsNullOrWhiteSpace(category.ParentId) ? DBNull.Value : category.ParentId);
        command.Parameters.AddWithValue("$sortOrder", category.SortOrder);
        command.Parameters.AddWithValue("$sortMode", category.SortMode);
        command.Parameters.AddWithValue("$color", category.Color);
        command.Parameters.AddWithValue("$isVisible", category.IsVisible ? 1 : 0);
        command.ExecuteNonQuery();
        NotifyDataWritten("Kategorie gespeichert");
    }

    public void HideCategory(string categoryId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Categories SET IsVisible = 0 WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", categoryId);
        command.ExecuteNonQuery();
        NotifyDataWritten("Kategorie ausgeblendet");
    }

    public int GetTaskCountForCategory(string categoryId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Tasks WHERE CategoryId = $categoryId AND IFNULL(IsDeleted, 0) = 0;";
        command.Parameters.AddWithValue("$categoryId", categoryId);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public int GetNextCategorySortOrder()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Categories;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public double GetNextTaskSortPosition(string categoryId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(SortPosition), -1) + 1 FROM Tasks WHERE CategoryId = $categoryId;";
        command.Parameters.AddWithValue("$categoryId", categoryId);
        return Convert.ToDouble(command.ExecuteScalar());
    }

    public double GetTopTaskSortPosition(string categoryId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MIN(SortPosition), 0) - 1 FROM Tasks WHERE CategoryId = $categoryId;";
        command.Parameters.AddWithValue("$categoryId", categoryId);
        return Convert.ToDouble(command.ExecuteScalar());
    }

    public List<MaterialItem> GetMaterials(string taskId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, TaskId, Quantity, Unit, Name, Status, Supplier, OrderedAt, Note
            FROM Materials
            WHERE TaskId = $taskId
            ORDER BY Name;
            """;
        command.Parameters.AddWithValue("$taskId", taskId);

        using var reader = command.ExecuteReader();
        var items = new List<MaterialItem>();
        while (reader.Read())
        {
            items.Add(new MaterialItem
            {
                Id = reader.GetString(0),
                TaskId = reader.GetString(1),
                Quantity = reader.GetDecimal(2),
                Unit = reader.GetString(3),
                Name = reader.GetString(4),
                Status = reader.GetString(5),
                Supplier = reader.GetString(6),
                OrderedAt = ReadNullableDate(reader, 7),
                Note = reader.GetString(8)
            });
        }

        return items;
    }

    public List<AttachmentItem> GetAttachments(string taskId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, TaskId, FileName, StoredPath, ThumbnailPath, FileType, ContentHash, AddedAt
            FROM Attachments
            WHERE TaskId = $taskId
            ORDER BY AddedAt DESC;
            """;
        command.Parameters.AddWithValue("$taskId", taskId);

        using var reader = command.ExecuteReader();
        var items = new List<AttachmentItem>();
        while (reader.Read())
        {
            var attachmentTaskId = reader.GetString(1);
            var fileName = reader.GetString(2);
            items.Add(new AttachmentItem
            {
                Id = reader.GetString(0),
                TaskId = attachmentTaskId,
                FileName = fileName,
                StoredPath = AppPaths.ResolveTaskAttachmentPath(attachmentTaskId, reader.GetString(3), fileName),
                ThumbnailPath = AppPaths.ResolveTaskAttachmentPath(attachmentTaskId, reader.GetString(4)),
                FileType = reader.GetString(5),
                ContentHash = reader.GetString(6),
                AddedAt = ReadDate(reader.GetString(7))
            });
        }

        return items;
    }

    public List<DeskItem> GetDeskItems()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Type, Text, PdfPath, FileName, DisplayName, ReferencePath, PdfThumbnailPath,
                   LinkedTaskId, ContentHash, X, Y, Width, Height, IsImportant, CreatedAt, UpdatedAt
            FROM DeskItems
            ORDER BY CreatedAt, Id;
            """;

        using var reader = command.ExecuteReader();
        var items = new List<DeskItem>();
        while (reader.Read())
        {
            items.Add(new DeskItem
            {
                Id = reader.GetString(0),
                Type = reader.GetString(1),
                Text = reader.GetString(2),
                FilePath = AppPaths.ResolveStoredPath(reader.GetString(3)),
                FileName = reader.GetString(4),
                DisplayName = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                ReferencePath = AppPaths.ResolveStoredPath(reader.GetString(6)),
                ThumbnailPath = AppPaths.ResolveStoredPath(reader.GetString(7)),
                LinkedTaskId = reader.GetString(8),
                ContentHash = reader.GetString(9),
                X = reader.GetDouble(10),
                Y = reader.GetDouble(11),
                Width = reader.GetDouble(12),
                Height = reader.GetDouble(13),
                IsImportant = reader.GetInt32(14) == 1,
                CreatedAt = ReadDate(reader.GetString(15)),
                UpdatedAt = ReadDate(reader.GetString(16))
            });
        }

        return items;
    }

    public void SaveTask(TaskItem task)
    {
        if (task is null)
        {
            return;
        }

        using var connection = OpenConnection();
        var validCategoryIds = LoadCategoryIds(connection);

        if (!EnsureTaskCategoryState(task, validCategoryIds))
        {
            return;
        }

        task.UpdatedAt = DateTime.Now;
        if (task.CreatedAt == default)
        {
            task.CreatedAt = task.UpdatedAt;
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Tasks (Id, Title, CustomerName, Description, CategoryId, Status, WorkflowType, WorkflowStep, Priority,
                               DueDate, FollowUpDate, SentAt, MaterialOrderedAt, CustomerAddress, CustomerEmail, CustomerPhone, Technician, SortPosition,
                               AssignedTo, CreatedAt, UpdatedAt, CompletedAt, IsDeleted, DeletedAt)
            VALUES ($id, $title, $customerName, $description, $categoryId, $status, $workflowType, $workflowStep, $priority,
                    $dueDate, $followUpDate, $sentAt, $materialOrderedAt, $customerAddress, $customerEmail, $customerPhone, $technician, $sortPosition,
                    $assignedTo, $createdAt, $updatedAt, $completedAt, $isDeleted, $deletedAt)
            ON CONFLICT(Id) DO UPDATE SET
                Title = excluded.Title,
                CustomerName = excluded.CustomerName,
                CustomerAddress = excluded.CustomerAddress,
                CustomerEmail = excluded.CustomerEmail,
                CustomerPhone = excluded.CustomerPhone,
                Description = excluded.Description,
                CategoryId = excluded.CategoryId,
                Status = excluded.Status,
                WorkflowType = excluded.WorkflowType,
                WorkflowStep = excluded.WorkflowStep,
                Priority = excluded.Priority,
                DueDate = excluded.DueDate,
                FollowUpDate = excluded.FollowUpDate,
                SentAt = excluded.SentAt,
                MaterialOrderedAt = excluded.MaterialOrderedAt,
                Technician = excluded.Technician,
                SortPosition = excluded.SortPosition,
                AssignedTo = excluded.AssignedTo,
                UpdatedAt = excluded.UpdatedAt,
                CompletedAt = excluded.CompletedAt,
                IsDeleted = excluded.IsDeleted,
                DeletedAt = excluded.DeletedAt;
            """;
        AddTaskParameters(command, task);
        command.ExecuteNonQuery();

        SaveTaskCategories(task, validCategoryIds);
        NotifyDataWritten("Aufgabe gespeichert");
    }

    public void DeleteTask(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return;
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Tasks
            SET IsDeleted = 1,
                DeletedAt = COALESCE(DeletedAt, $deletedAt),
                UpdatedAt = $updatedAt
            WHERE Id = $taskId;
            """;
        command.Parameters.AddWithValue("$taskId", taskId);
        command.Parameters.AddWithValue("$deletedAt", DateTime.Now.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", DateTime.Now.ToString("O"));
        command.ExecuteNonQuery();
        NotifyDataWritten("Aufgabe geloescht");
    }

    public void EmptyTrash()
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        ExecuteNonQuery(connection, transaction, """
            DELETE FROM AttachmentEditSessions
            WHERE TaskId IN (
                SELECT Id
                FROM Tasks
                WHERE IsDeleted = 1
            );
            """);

        ExecuteNonQuery(connection, transaction, """
            DELETE FROM Attachments
            WHERE TaskId IN (
                SELECT Id
                FROM Tasks
                WHERE IsDeleted = 1
            );
            """);

        ExecuteNonQuery(connection, transaction, """
            DELETE FROM Materials
            WHERE TaskId IN (
                SELECT Id
                FROM Tasks
                WHERE IsDeleted = 1
            );
            """);

        ExecuteNonQuery(connection, transaction, """
            DELETE FROM TaskCategories
            WHERE TaskId IN (
                SELECT Id
                FROM Tasks
                WHERE IsDeleted = 1
            );
            """);

        ExecuteNonQuery(connection, transaction, """
            DELETE FROM Tasks
            WHERE IsDeleted = 1;
            """);

        transaction.Commit();
        NotifyDataWritten("Papierkorb geleert");
    }

    public void SaveMaterial(MaterialItem item)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Materials (Id, TaskId, Quantity, Unit, Name, Status, Supplier, OrderedAt, Note)
            VALUES ($id, $taskId, $quantity, $unit, $name, $status, $supplier, $orderedAt, $note)
            ON CONFLICT(Id) DO UPDATE SET
                Quantity = excluded.Quantity,
                Unit = excluded.Unit,
                Name = excluded.Name,
                Status = excluded.Status,
                Supplier = excluded.Supplier,
                OrderedAt = excluded.OrderedAt,
                Note = excluded.Note;
            """;
        AddParameter(command, "$id", item.Id ?? string.Empty);
        AddParameter(command, "$taskId", item.TaskId ?? string.Empty);
        AddParameter(command, "$quantity", item.Quantity);
        AddParameter(command, "$unit", item.Unit ?? string.Empty);
        AddParameter(command, "$name", item.Name ?? string.Empty);
        AddParameter(command, "$status", item.Status ?? string.Empty);
        AddParameter(command, "$supplier", item.Supplier ?? string.Empty);
        AddParameter(command, "$orderedAt", ToDb(item.OrderedAt));
        AddParameter(command, "$note", item.Note ?? string.Empty);
        command.ExecuteNonQuery();
        NotifyDataWritten("Material gespeichert");
    }

    public void DeleteMaterial(string materialId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Materials WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", materialId);
        command.ExecuteNonQuery();
        NotifyDataWritten("Material geloescht");
    }

    public void DeleteAttachment(string attachmentId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var editSessionCommand = connection.CreateCommand())
        {
            editSessionCommand.Transaction = transaction;
            editSessionCommand.CommandText = "DELETE FROM AttachmentEditSessions WHERE AttachmentId = $id;";
            editSessionCommand.Parameters.AddWithValue("$id", attachmentId);
            editSessionCommand.ExecuteNonQuery();
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM Attachments WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", attachmentId);
        command.ExecuteNonQuery();

        transaction.Commit();
        NotifyDataWritten("Anhang geloescht");
    }

    public bool HasAttachmentPathReference(string path)
    {
        return HasDataPathReference(path);
    }

    public bool HasDataPathReference(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT StoredPath AS Path FROM Attachments
            UNION ALL
            SELECT ThumbnailPath FROM Attachments
            UNION ALL
            SELECT PdfPath FROM DeskItems
            UNION ALL
            SELECT ReferencePath FROM DeskItems
            UNION ALL
            SELECT PdfThumbnailPath FROM DeskItems;
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (AppPaths.PathsEqual(path, reader.GetString(0)))
            {
                return true;
            }
        }

        return false;
    }

    public void SaveAttachment(AttachmentItem item)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Attachments (Id, TaskId, FileName, StoredPath, ThumbnailPath, FileType, ContentHash, AddedAt)
            VALUES ($id, $taskId, $fileName, $storedPath, $thumbnailPath, $fileType, $contentHash, $addedAt)
            ON CONFLICT(Id) DO UPDATE SET
                TaskId = excluded.TaskId,
                FileName = excluded.FileName,
                StoredPath = excluded.StoredPath,
                ThumbnailPath = excluded.ThumbnailPath,
                FileType = excluded.FileType,
                ContentHash = excluded.ContentHash,
                AddedAt = excluded.AddedAt;
            """;
        command.Parameters.AddWithValue("$id", item.Id);
        command.Parameters.AddWithValue("$taskId", item.TaskId);
        command.Parameters.AddWithValue("$fileName", item.FileName);
        command.Parameters.AddWithValue("$storedPath", AppPaths.ToStoredPath(item.StoredPath));
        command.Parameters.AddWithValue("$thumbnailPath", AppPaths.ToStoredPath(item.ThumbnailPath));
        command.Parameters.AddWithValue("$fileType", item.FileType);
        command.Parameters.AddWithValue("$contentHash", item.ContentHash);
        command.Parameters.AddWithValue("$addedAt", ToDb(item.AddedAt));
        command.ExecuteNonQuery();
        NotifyDataWritten("Anhang gespeichert");
    }

    public void SaveDeskItem(DeskItem item)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO DeskItems (
                Id, Type, Text, PdfPath, FileName, DisplayName, ReferencePath, PdfThumbnailPath,
                LinkedTaskId, ContentHash, X, Y, Width, Height, IsImportant, CreatedAt, UpdatedAt)
            VALUES (
                $id, $type, $text, $pdfPath, $fileName, $displayName, $referencePath, $pdfThumbnailPath,
                $linkedTaskId, $contentHash, $x, $y, $width, $height, $isImportant, $createdAt, $updatedAt)
            ON CONFLICT(Id) DO UPDATE SET
                Type = excluded.Type,
                Text = excluded.Text,
                PdfPath = excluded.PdfPath,
                FileName = excluded.FileName,
                DisplayName = excluded.DisplayName,
                ReferencePath = excluded.ReferencePath,
                PdfThumbnailPath = excluded.PdfThumbnailPath,
                LinkedTaskId = excluded.LinkedTaskId,
                ContentHash = excluded.ContentHash,
                X = excluded.X,
                Y = excluded.Y,
                Width = excluded.Width,
                Height = excluded.Height,
                IsImportant = excluded.IsImportant,
                UpdatedAt = excluded.UpdatedAt;
            """;
        command.Parameters.AddWithValue("$id", item.Id);
        command.Parameters.AddWithValue("$type", item.Type);
        command.Parameters.AddWithValue("$text", item.Text);
        command.Parameters.AddWithValue("$pdfPath", AppPaths.ToStoredPath(item.FilePath));
        command.Parameters.AddWithValue("$fileName", item.FileName);
        command.Parameters.AddWithValue("$displayName", item.DisplayName);
        command.Parameters.AddWithValue("$referencePath", AppPaths.ToStoredPath(item.ReferencePath));
        command.Parameters.AddWithValue("$pdfThumbnailPath", AppPaths.ToStoredPath(item.ThumbnailPath));
        command.Parameters.AddWithValue("$linkedTaskId", item.LinkedTaskId);
        command.Parameters.AddWithValue("$contentHash", item.ContentHash);
        command.Parameters.AddWithValue("$x", item.X);
        command.Parameters.AddWithValue("$y", item.Y);
        command.Parameters.AddWithValue("$width", item.Width);
        command.Parameters.AddWithValue("$height", item.Height);
        command.Parameters.AddWithValue("$isImportant", item.IsImportant ? 1 : 0);
        command.Parameters.AddWithValue("$createdAt", ToDb(item.CreatedAt));
        command.Parameters.AddWithValue("$updatedAt", ToDb(item.UpdatedAt));
        command.ExecuteNonQuery();
        NotifyDataWritten("Schreibtisch gespeichert");
    }

    public void DeleteDeskItem(string deskItemId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM DeskItems
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", deskItemId);
        command.ExecuteNonQuery();
        NotifyDataWritten("Schreibtisch geloescht");
    }

    public void UpdateAttachmentThumbnail(string attachmentId, string thumbnailPath)
    {
        using var connection = OpenConnection();

        var storedPath = string.Empty;
        using (var readCommand = connection.CreateCommand())
        {
            readCommand.CommandText = """
                SELECT StoredPath
                FROM Attachments
                WHERE Id = $id
                LIMIT 1;
                """;
            readCommand.Parameters.AddWithValue("$id", attachmentId);

            using var reader = readCommand.ExecuteReader();
            if (reader.Read())
            {
                storedPath = AppPaths.ToStoredPath(reader.GetString(0));
            }
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Attachments
            SET StoredPath = $storedPath,
                ThumbnailPath = $thumbnailPath
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", attachmentId);
        command.Parameters.AddWithValue("$storedPath", storedPath);
        command.Parameters.AddWithValue("$thumbnailPath", AppPaths.ToStoredPath(thumbnailPath));
        command.ExecuteNonQuery();
        NotifyDataWritten("Anhang-Thumbnail gespeichert");
    }

    public AttachmentEditSession? GetLatestEditSessionForAttachment(string attachmentId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, AttachmentId, TaskId, ExportPath, BackupPath, ExportedAt,
                   OriginalHashAtExport, ExportedFileHashAtExport, Status, ImportedAt
            FROM AttachmentEditSessions
            WHERE AttachmentId = $attachmentId
            ORDER BY ExportedAt DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$attachmentId", attachmentId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadAttachmentEditSession(reader) : null;
    }

    public void SaveAttachmentEditSession(AttachmentEditSession session)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AttachmentEditSessions (
                Id, AttachmentId, TaskId, ExportPath, BackupPath, ExportedAt,
                OriginalHashAtExport, ExportedFileHashAtExport, Status, ImportedAt)
            VALUES (
                $id, $attachmentId, $taskId, $exportPath, $backupPath, $exportedAt,
                $originalHashAtExport, $exportedFileHashAtExport, $status, $importedAt)
            ON CONFLICT(Id) DO UPDATE SET
                AttachmentId = excluded.AttachmentId,
                TaskId = excluded.TaskId,
                ExportPath = excluded.ExportPath,
                BackupPath = excluded.BackupPath,
                ExportedAt = excluded.ExportedAt,
                OriginalHashAtExport = excluded.OriginalHashAtExport,
                ExportedFileHashAtExport = excluded.ExportedFileHashAtExport,
                Status = excluded.Status,
                ImportedAt = excluded.ImportedAt;
            """;
        command.Parameters.AddWithValue("$id", session.Id);
        command.Parameters.AddWithValue("$attachmentId", session.AttachmentId);
        command.Parameters.AddWithValue("$taskId", session.TaskId);
        command.Parameters.AddWithValue("$exportPath", session.ExportPath);
        command.Parameters.AddWithValue("$backupPath", (object?)session.BackupPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$exportedAt", ToDb(session.ExportedAt));
        command.Parameters.AddWithValue("$originalHashAtExport", session.OriginalHashAtExport);
        command.Parameters.AddWithValue("$exportedFileHashAtExport", session.ExportedFileHashAtExport);
        command.Parameters.AddWithValue("$status", session.Status);
        command.Parameters.AddWithValue("$importedAt", ToDb(session.ImportedAt));
        command.ExecuteNonQuery();
        NotifyDataWritten("Anhang-Bearbeitungssitzung gespeichert");
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            connection.Open();
            return connection;
        }
        catch (SqliteException ex)
        {
            connection.Dispose();
            throw CreateStartupException("SQLite konnte die Datenbank nicht öffnen.", ex);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private DatabaseStartupException CreateStartupException(string summary, Exception exception)
    {
        var diagnosticMessage = BuildStartupDiagnosticMessage(_databasePath, summary, exception);
        Console.Error.WriteLine(diagnosticMessage);
        return new DatabaseStartupException(_databasePath, diagnosticMessage, exception);
    }

    public static string BuildStartupDiagnosticMessage(string databasePath, string summary, Exception? exception)
    {
        var directoryPath = Path.GetDirectoryName(databasePath) ?? string.Empty;
        var resolvedDirectoryPath = ResolveLinkTargetPath(directoryPath);
        var resolvedDatabasePath = !string.IsNullOrWhiteSpace(resolvedDirectoryPath)
            ? Path.Combine(resolvedDirectoryPath, Path.GetFileName(databasePath))
            : string.Empty;
        var hasResolvedDirectoryPath = !string.IsNullOrWhiteSpace(resolvedDirectoryPath) &&
                                       !string.Equals(resolvedDirectoryPath, directoryPath, StringComparison.Ordinal);
        var builder = new StringBuilder();
        builder.AppendLine("BüroCockpit konnte die SQLite-Datenbank nicht öffnen.");
        builder.AppendLine(summary);
        builder.AppendLine();
        builder.AppendLine($"Datenbankpfad: {databasePath}");
        builder.AppendLine($"Datenordner: {directoryPath}");
        if (hasResolvedDirectoryPath)
        {
            builder.AppendLine($"Datenordner-Zielpfad: {resolvedDirectoryPath}");
            builder.AppendLine($"Effektiver Datenbankpfad: {resolvedDatabasePath}");
        }

        builder.AppendLine($"Datenordner existiert: {FormatBool(Directory.Exists(directoryPath))}");
        builder.AppendLine($"Datenbankdatei existiert: {FormatBool(File.Exists(databasePath))}");
        builder.AppendLine($"WAL-Datei existiert: {FormatBool(File.Exists(databasePath + "-wal"))}");
        builder.AppendLine($"SHM-Datei existiert: {FormatBool(File.Exists(databasePath + "-shm"))}");
        builder.AppendLine($"Datenordner-Schreibrecht laut Dateiattributen: {DescribeWritableMode(directoryPath)}");
        builder.AppendLine($"Datenbank-Schreibrecht laut Dateiattributen: {DescribeWritableMode(databasePath)}");
        builder.AppendLine("Echter Schreibtest im Ordner: nicht ausgeführt, damit keine Produktiv- oder Cloud-Dateien verändert werden.");
        builder.AppendLine($"CloudStorage/OneDrive/iCloud-Pfad: {FormatBool(IsCloudStoragePath(databasePath) || IsCloudStoragePath(resolvedDirectoryPath) || IsCloudStoragePath(resolvedDatabasePath))}");
        builder.AppendLine();
        builder.AppendLine("Mögliche Ursachen:");
        builder.AppendLine("- Datenbank oder Datenordner ist nicht lokal verfügbar.");
        builder.AppendLine("- OneDrive/iCloud hat die Datei nur als Platzhalter und noch nicht vollständig geladen.");
        builder.AppendLine("- Es fehlen Schreibrechte im Datenordner oder an buerocockpit.db, buerocockpit.db-wal oder buerocockpit.db-shm.");
        builder.AppendLine("- Die Datenbank ist durch einen anderen Prozess gesperrt.");
        builder.AppendLine("- Die Datenbankdatei ist beschädigt.");
        builder.AppendLine();
        builder.AppendLine("Hinweis: Bei CloudStorage/OneDrive muss der BüroCockpit-Datenordner lokal verfügbar sein. Es wurde keine Reparatur, Kopie oder Migration ausgeführt.");

        if (exception is not null)
        {
            builder.AppendLine();
            builder.AppendLine($"Technischer Fehler: {exception.GetType().Name}: {exception.Message}");
            if (exception is SqliteException sqliteException)
            {
                builder.AppendLine($"SQLite ErrorCode: {sqliteException.SqliteErrorCode}");
                builder.AppendLine($"SQLite ExtendedErrorCode: {sqliteException.SqliteExtendedErrorCode}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatBool(bool value)
    {
        return value ? "ja" : "nein";
    }

    private static string ResolveLinkTargetPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            var directoryInfo = new DirectoryInfo(path);
            return directoryInfo.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsCloudStoragePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path.Contains($"{Path.DirectorySeparatorChar}CloudStorage{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("OneDrive", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("iCloud", StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeWritableMode(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "nicht prüfbar";
        }

        if (!Directory.Exists(path) && !File.Exists(path))
        {
            return "nicht prüfbar, Pfad existiert nicht";
        }

        if (OperatingSystem.IsWindows())
        {
            return "nicht prüfbar über Unix-Dateiattribute";
        }

        try
        {
            var mode = File.GetUnixFileMode(path);
            var writable = (mode & (UnixFileMode.UserWrite | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0;
            return $"{(writable ? "ja" : "nein")} ({mode})";
        }
        catch (Exception ex)
        {
            return $"nicht prüfbar ({ex.Message})";
        }
    }

    private void NotifyDataWritten(string reason)
    {
        DataWritten?.Invoke(reason);
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void ExecuteNonQuery(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void MigrateSchema(SqliteConnection connection)
    {
        AddColumnIfMissing(connection, "Categories", "SortMode", "TEXT NOT NULL DEFAULT 'Erstellt am'");
        AddColumnIfMissing(connection, "Categories", "ParentId", "TEXT NULL");
        AddColumnIfMissing(connection, "Tasks", "SentAt", "TEXT NULL");
        AddColumnIfMissing(connection, "Tasks", "WorkflowType", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "Tasks", "WorkflowStep", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "Tasks", "MaterialOrderedAt", "TEXT NULL");
        AddColumnIfMissing(connection, "Tasks", "CustomerAddress", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "Tasks", "CustomerEmail", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "Tasks", "CustomerPhone", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "Tasks", "Technician", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "Tasks", "SortPosition", "REAL NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "Tasks", "CompletedAt", "TEXT NULL");
        AddColumnIfMissing(connection, "Tasks", "IsDeleted", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "Tasks", "DeletedAt", "TEXT NULL");
        AddColumnIfMissing(connection, "Attachments", "ContentHash", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "DeskItems", "Type", "TEXT NOT NULL DEFAULT 'Note'");
        AddColumnIfMissing(connection, "DeskItems", "Text", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "DeskItems", "PdfPath", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "DeskItems", "FileName", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "DeskItems", "DisplayName", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "DeskItems", "ReferencePath", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "DeskItems", "PdfThumbnailPath", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "DeskItems", "LinkedTaskId", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "DeskItems", "ContentHash", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "DeskItems", "X", "REAL NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "DeskItems", "Y", "REAL NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "DeskItems", "Width", "REAL NOT NULL DEFAULT 260");
        AddColumnIfMissing(connection, "DeskItems", "Height", "REAL NOT NULL DEFAULT 190");
        AddColumnIfMissing(connection, "DeskItems", "IsImportant", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "DeskItems", "CreatedAt", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "DeskItems", "UpdatedAt", "TEXT NOT NULL DEFAULT ''");
    }

    private static void AddColumnIfMissing(SqliteConnection connection, string tableName, string columnName, string columnDefinition)
    {
        if (ColumnExists(connection, tableName, columnName))
        {
            return;
        }

        ExecuteNonQuery(connection, $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};");
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void SeedDefaultCategories(SqliteConnection connection)
    {
        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM Categories;";
        var count = Convert.ToInt32(countCommand.ExecuteScalar());
        if (count > 0)
        {
            return;
        }

        var defaults = new (string Name, string? ParentName)[]
        {
            ("Schreibtisch", null),
            ("Offene Aufgaben", null),
            ("Angebot", null),
            ("erstellen", "Angebot"),
            ("gesendet", "Angebot"),
            ("Material", null),
            ("bestellen", "Material"),
            ("bestellt", "Material"),
            ("Termin", null),
            ("terminieren", "Termin"),
            ("terminiert", "Termin"),
            ("zum terminieren gegeben", "Termin"),
            ("Firma", null),
            ("Retouren", "Firma"),
            ("Lager", "Firma"),
            ("Netzbetreiber", null),
            ("SH-Netz", "Netzbetreiber"),
            ("Marktstammdatenregister", "Netzbetreiber"),
            ("Wartet auf Kunde", null)
        };

        var categoryIdsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < defaults.Length; i++)
        {
            var id = Guid.NewGuid().ToString("N");
            categoryIdsByName[defaults[i].Name] = id;
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Categories (Id, Name, ParentId, SortOrder, SortMode, Color, IsVisible)
                VALUES ($id, $name, $parentId, $sortOrder, $sortMode, $color, 1);
                """;
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$name", defaults[i].Name);
            object parentValue = defaults[i].ParentName is { } parentName &&
                                 categoryIdsByName.TryGetValue(parentName, out var parentId)
                ? parentId
                : DBNull.Value;
            command.Parameters.AddWithValue("$parentId", parentValue);
            command.Parameters.AddWithValue("$sortOrder", i);
            command.Parameters.AddWithValue("$sortMode", "Erstellt am");
            command.Parameters.AddWithValue("$color", i == 0 ? "#E9EEF7" : "#F2F3F5");
            command.ExecuteNonQuery();
        }
    }

    private void LoadTaskCategories(List<TaskItem> tasks)
    {
        if (tasks.Count == 0)
        {
            return;
        }

        using var connection = OpenConnection();
        connection.Open();
        var validCategoryIds = LoadCategoryIds(connection);

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TaskId, CategoryId
            FROM TaskCategories
            ORDER BY CategoryId
        """;

        using var reader = command.ExecuteReader();
        var byTaskId = tasks.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);

        while (reader.Read())
        {
            var taskId = reader.GetString(0);
            var categoryId = reader.GetString(1);

            if (byTaskId.TryGetValue(taskId, out var task) &&
                !task.CategoryIds.Contains(categoryId, StringComparer.OrdinalIgnoreCase) &&
                validCategoryIds.Contains(categoryId, StringComparer.OrdinalIgnoreCase))
            {
                task.CategoryIds.Add(categoryId);
            }
        }

        foreach (var task in tasks)
        {
            if (task.CategoryIds.Count == 0 &&
                !string.IsNullOrWhiteSpace(task.CategoryId) &&
                validCategoryIds.Contains(task.CategoryId, StringComparer.OrdinalIgnoreCase))
            {
                task.CategoryIds.Add(task.CategoryId);
            }

            EnsureTaskCategoryState(task, validCategoryIds);
        }
    }

    private void SaveTaskCategories(TaskItem task, IReadOnlyCollection<string>? validCategoryIds = null)
    {
        if (task is null)
        {
            return;
        }

        if (!EnsureTaskCategoryState(task, validCategoryIds))
        {
            return;
        }

        using var connection = OpenConnection();
        connection.Open();

        using var deleteCommand = connection.CreateCommand();
        deleteCommand.CommandText = "DELETE FROM TaskCategories WHERE TaskId = $taskId";
        deleteCommand.Parameters.AddWithValue("$taskId", task.Id);
        deleteCommand.ExecuteNonQuery();

        foreach (var categoryId in task.CategoryIds
                     .Where(id => !string.IsNullOrWhiteSpace(id))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = """
                INSERT OR IGNORE INTO TaskCategories (TaskId, CategoryId)
                VALUES ($taskId, $categoryId)
            """;
            insertCommand.Parameters.AddWithValue("$taskId", task.Id);
            insertCommand.Parameters.AddWithValue("$categoryId", categoryId);
            insertCommand.ExecuteNonQuery();
        }
    }

    private static List<string> LoadCategoryIds(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id
            FROM Categories
            ORDER BY SortOrder, Name;
            """;

        using var reader = command.ExecuteReader();
        var categoryIds = new List<string>();
        while (reader.Read())
        {
            var categoryId = reader.GetString(0);
            if (!string.IsNullOrWhiteSpace(categoryId))
            {
                categoryIds.Add(categoryId);
            }
        }

        return categoryIds;
    }

    private static bool EnsureTaskCategoryState(TaskItem task, IReadOnlyCollection<string>? validCategoryIds = null)
    {
        task.CategoryIds ??= new List<string>();
        task.CategoryIds = task.CategoryIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (validCategoryIds is not null)
        {
            task.CategoryIds = task.CategoryIds
                .Where(id => validCategoryIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(task.CategoryId) &&
            (validCategoryIds is null || validCategoryIds.Contains(task.CategoryId, StringComparer.OrdinalIgnoreCase)) &&
            !task.CategoryIds.Contains(task.CategoryId, StringComparer.OrdinalIgnoreCase))
        {
            task.CategoryIds.Insert(0, task.CategoryId);
        }

        if (task.CategoryIds.Count == 0)
        {
            task.CategoryId = string.Empty;
            return true;
        }

        if (string.IsNullOrWhiteSpace(task.CategoryId) ||
            !task.CategoryIds.Contains(task.CategoryId, StringComparer.OrdinalIgnoreCase))
        {
            task.CategoryId = task.CategoryIds[0];
        }

        return !string.IsNullOrWhiteSpace(task.CategoryId);
    }

    private static void AddTaskParameters(SqliteCommand command, TaskItem task)
    {
        AddParameter(command, "$id", task.Id ?? string.Empty);
        AddParameter(command, "$title", task.Title ?? string.Empty);
        AddParameter(command, "$customerName", task.CustomerName ?? string.Empty);
        AddParameter(command, "$description", task.Description ?? string.Empty);
        AddParameter(command, "$categoryId", task.CategoryId ?? string.Empty);
        AddParameter(command, "$status", task.Status ?? string.Empty);
        AddParameter(command, "$workflowType", task.WorkflowType ?? string.Empty);
        AddParameter(command, "$workflowStep", task.WorkflowStep ?? string.Empty);
        AddParameter(command, "$priority", task.Priority ?? string.Empty);
        AddParameter(command, "$dueDate", ToDb(task.DueDate));
        AddParameter(command, "$followUpDate", ToDb(task.FollowUpDate));
        AddParameter(command, "$sentAt", ToDb(task.SentAt));
        AddParameter(command, "$materialOrderedAt", ToDb(task.MaterialOrderedAt));
        AddParameter(command, "$customerAddress", task.CustomerAddress ?? string.Empty);
        AddParameter(command, "$customerEmail", task.CustomerEmail ?? string.Empty);
        AddParameter(command, "$customerPhone", task.CustomerPhone ?? string.Empty);
        AddParameter(command, "$technician", task.Technician ?? string.Empty);
        AddParameter(command, "$sortPosition", task.SortPosition);
        AddParameter(command, "$assignedTo", task.AssignedTo ?? string.Empty);
        AddParameter(command, "$createdAt", ToDb(task.CreatedAt));
        AddParameter(command, "$updatedAt", ToDb(task.UpdatedAt));
        AddParameter(command, "$completedAt", ToDb(task.CompletedAt));
        AddParameter(command, "$isDeleted", task.IsDeleted ? 1 : 0);
        AddParameter(command, "$deletedAt", ToDb(task.DeletedAt));
    }

    private static void AddParameter(SqliteCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    private static object ToDb(DateTime? value)
    {
        return value.HasValue ? value.Value.ToString("O") : DBNull.Value;
    }

    private static DateTime? ReadNullableDate(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : ReadDate(reader.GetString(ordinal));
    }

    private static DateTime ReadDate(string value)
    {
        return DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    private static DateTime ReadDateOrFallback(string? value, DateTime fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : ReadDate(value);
    }

    private static AttachmentEditSession ReadAttachmentEditSession(SqliteDataReader reader)
    {
        return new AttachmentEditSession
        {
            Id = reader.GetString(0),
            AttachmentId = reader.GetString(1),
            TaskId = reader.GetString(2),
            ExportPath = reader.GetString(3),
            BackupPath = reader.IsDBNull(4) ? null : reader.GetString(4),
            ExportedAt = ReadDate(reader.GetString(5)),
            OriginalHashAtExport = reader.GetString(6),
            ExportedFileHashAtExport = reader.GetString(7),
            Status = reader.GetString(8),
            ImportedAt = ReadNullableDate(reader, 9)
        };
    }
}
