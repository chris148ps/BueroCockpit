using BueroCockpit.Models;
using Microsoft.Data.Sqlite;

namespace BueroCockpit.Data;

public sealed class BueroRepository
{
    private readonly string _connectionString;

    public BueroRepository()
    {
        AppPaths.EnsureBaseDirectories();
        _connectionString = new SqliteConnectionStringBuilder { DataSource = AppPaths.DatabasePath }.ToString();
    }

    public void Initialize()
    {
        using var connection = OpenConnection();

        ExecuteNonQuery(connection, """
            CREATE TABLE IF NOT EXISTS Categories (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                SortOrder INTEGER NOT NULL,
                SortMode TEXT NOT NULL DEFAULT 'Geändert am',
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
                Priority TEXT NOT NULL,
                DueDate TEXT NULL,
                FollowUpDate TEXT NULL,
                SentAt TEXT NULL,
                CustomerAddress TEXT NOT NULL DEFAULT '',
                Technician TEXT NOT NULL DEFAULT '',
                SortPosition REAL NOT NULL DEFAULT 0,
                AssignedTo TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                CompletedAt TEXT NULL
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

    public List<CategoryItem> GetCategories()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, SortOrder, SortMode, Color, IsVisible
            FROM Categories
            WHERE IsVisible = 1
            ORDER BY SortOrder, Name;
            """;

        using var reader = command.ExecuteReader();
        var items = new List<CategoryItem>();
        while (reader.Read())
        {
            items.Add(new CategoryItem
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                SortOrder = reader.GetInt32(2),
                SortMode = reader.GetString(3),
                Color = reader.GetString(4),
                IsVisible = reader.GetInt32(5) == 1
            });
        }

        return items;
    }

    public List<TaskItem> GetTasks()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Title, CustomerName, Description, CategoryId, Status, Priority,
                   DueDate, FollowUpDate, SentAt, CustomerAddress, Technician, SortPosition,
                   AssignedTo, CreatedAt, UpdatedAt, CompletedAt
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
                Priority = reader.GetString(6),
                DueDate = ReadNullableDate(reader, 7),
                FollowUpDate = ReadNullableDate(reader, 8),
                SentAt = ReadNullableDate(reader, 9),
                CustomerAddress = reader.GetString(10),
                Technician = reader.GetString(11),
                SortPosition = reader.GetDouble(12),
                AssignedTo = reader.GetString(13),
                CreatedAt = ReadDate(reader.GetString(14)),
                UpdatedAt = ReadDate(reader.GetString(15)),
                CompletedAt = ReadNullableDate(reader, 16)
            });
        }

        LoadTaskCategories(items);
        return items;
    }

    public void SaveCategory(CategoryItem category)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Categories (Id, Name, SortOrder, SortMode, Color, IsVisible)
            VALUES ($id, $name, $sortOrder, $sortMode, $color, $isVisible)
            ON CONFLICT(Id) DO UPDATE SET
                Name = excluded.Name,
                SortOrder = excluded.SortOrder,
                SortMode = excluded.SortMode,
                Color = excluded.Color,
                IsVisible = excluded.IsVisible;
            """;
        command.Parameters.AddWithValue("$id", category.Id);
        command.Parameters.AddWithValue("$name", category.Name);
        command.Parameters.AddWithValue("$sortOrder", category.SortOrder);
        command.Parameters.AddWithValue("$sortMode", category.SortMode);
        command.Parameters.AddWithValue("$color", category.Color);
        command.Parameters.AddWithValue("$isVisible", category.IsVisible ? 1 : 0);
        command.ExecuteNonQuery();
    }

    public void HideCategory(string categoryId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Categories SET IsVisible = 0 WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", categoryId);
        command.ExecuteNonQuery();
    }

    public int GetTaskCountForCategory(string categoryId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Tasks WHERE CategoryId = $categoryId;";
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
            items.Add(new AttachmentItem
            {
                Id = reader.GetString(0),
                TaskId = attachmentTaskId,
                FileName = reader.GetString(2),
                StoredPath = AppPaths.ResolveTaskAttachmentPath(attachmentTaskId, reader.GetString(3)),
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

        if (!EnsureTaskCategoryState(task))
        {
            return;
        }

        task.UpdatedAt = DateTime.Now;
        if (task.CreatedAt == default)
        {
            task.CreatedAt = task.UpdatedAt;
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Tasks (Id, Title, CustomerName, Description, CategoryId, Status, Priority,
                               DueDate, FollowUpDate, SentAt, CustomerAddress, Technician, SortPosition,
                               AssignedTo, CreatedAt, UpdatedAt, CompletedAt)
            VALUES ($id, $title, $customerName, $description, $categoryId, $status, $priority,
                    $dueDate, $followUpDate, $sentAt, $customerAddress, $technician, $sortPosition,
                    $assignedTo, $createdAt, $updatedAt, $completedAt)
            ON CONFLICT(Id) DO UPDATE SET
                Title = excluded.Title,
                CustomerName = excluded.CustomerName,
                CustomerAddress = excluded.CustomerAddress,
                Description = excluded.Description,
                CategoryId = excluded.CategoryId,
                Status = excluded.Status,
                Priority = excluded.Priority,
                DueDate = excluded.DueDate,
                FollowUpDate = excluded.FollowUpDate,
                SentAt = excluded.SentAt,
                Technician = excluded.Technician,
                SortPosition = excluded.SortPosition,
                AssignedTo = excluded.AssignedTo,
                UpdatedAt = excluded.UpdatedAt,
                CompletedAt = excluded.CompletedAt;
            """;
        AddTaskParameters(command, task);
        command.ExecuteNonQuery();

        SaveTaskCategories(task);
    }

    public void DeleteTask(string taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var (table, column) in new[]
        {
            ("AttachmentEditSessions", "TaskId"),
            ("Attachments", "TaskId"),
            ("Materials", "TaskId"),
            ("TaskCategories", "TaskId"),
            ("Tasks", "Id")
        })
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"DELETE FROM {table} WHERE {column} = $taskId;";
            command.Parameters.AddWithValue("$taskId", taskId);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
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
        command.Parameters.AddWithValue("$id", item.Id);
        command.Parameters.AddWithValue("$taskId", item.TaskId);
        command.Parameters.AddWithValue("$quantity", item.Quantity);
        command.Parameters.AddWithValue("$unit", item.Unit);
        command.Parameters.AddWithValue("$name", item.Name);
        command.Parameters.AddWithValue("$status", item.Status);
        command.Parameters.AddWithValue("$supplier", item.Supplier);
        command.Parameters.AddWithValue("$orderedAt", ToDb(item.OrderedAt));
        command.Parameters.AddWithValue("$note", item.Note);
        command.ExecuteNonQuery();
    }

    public void DeleteMaterial(string materialId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Materials WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", materialId);
        command.ExecuteNonQuery();
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
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void ExecuteNonQuery(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void MigrateSchema(SqliteConnection connection)
    {
        AddColumnIfMissing(connection, "Categories", "SortMode", "TEXT NOT NULL DEFAULT 'Geändert am'");
        AddColumnIfMissing(connection, "Tasks", "SentAt", "TEXT NULL");
        AddColumnIfMissing(connection, "Tasks", "CustomerAddress", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "Tasks", "Technician", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "Tasks", "SortPosition", "REAL NOT NULL DEFAULT 0");
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

        var defaults = new[]
        {
            "Übersicht",
            "Offene Aufgaben",
            "Angebote erstellen",
            "Angebote gesendet",
            "Material bestellen",
            "Terminieren",
            "Terminiert",
            "Wartet auf Kunde",
            "Protokolle",
            "Retoure / Rückgabe",
            "Archiv"
        };

        for (var i = 0; i < defaults.Length; i++)
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Categories (Id, Name, SortOrder, SortMode, Color, IsVisible)
                VALUES ($id, $name, $sortOrder, $sortMode, $color, 1);
                """;
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
            command.Parameters.AddWithValue("$name", defaults[i]);
            command.Parameters.AddWithValue("$sortOrder", i);
            command.Parameters.AddWithValue("$sortMode", "Geändert am");
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
                !task.CategoryIds.Contains(categoryId, StringComparer.OrdinalIgnoreCase))
            {
                task.CategoryIds.Add(categoryId);
            }
        }

        foreach (var task in tasks)
        {
            if (task.CategoryIds.Count == 0 && !string.IsNullOrWhiteSpace(task.CategoryId))
            {
                task.CategoryIds.Add(task.CategoryId);
            }

            EnsureTaskCategoryState(task);
        }
    }

    private void SaveTaskCategories(TaskItem task)
    {
        if (task is null)
        {
            return;
        }

        if (!EnsureTaskCategoryState(task))
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

    private static bool EnsureTaskCategoryState(TaskItem task)
    {
        task.CategoryIds ??= new List<string>();
        task.CategoryIds = task.CategoryIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(task.CategoryId) &&
            !task.CategoryIds.Contains(task.CategoryId, StringComparer.OrdinalIgnoreCase))
        {
            task.CategoryIds.Insert(0, task.CategoryId);
        }

        if (task.CategoryIds.Count == 0)
        {
            task.CategoryId = string.Empty;
            return false;
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
        command.Parameters.AddWithValue("$id", task.Id);
        command.Parameters.AddWithValue("$title", task.Title);
        command.Parameters.AddWithValue("$customerName", task.CustomerName);
        command.Parameters.AddWithValue("$description", task.Description);
        command.Parameters.AddWithValue("$categoryId", task.CategoryId);
        command.Parameters.AddWithValue("$status", task.Status);
        command.Parameters.AddWithValue("$priority", task.Priority);
        command.Parameters.AddWithValue("$dueDate", ToDb(task.DueDate));
        command.Parameters.AddWithValue("$followUpDate", ToDb(task.FollowUpDate));
        command.Parameters.AddWithValue("$sentAt", ToDb(task.SentAt));
        command.Parameters.AddWithValue("$customerAddress", task.CustomerAddress);
        command.Parameters.AddWithValue("$technician", task.Technician);
        command.Parameters.AddWithValue("$sortPosition", task.SortPosition);
        command.Parameters.AddWithValue("$assignedTo", task.AssignedTo);
        command.Parameters.AddWithValue("$createdAt", ToDb(task.CreatedAt));
        command.Parameters.AddWithValue("$updatedAt", ToDb(task.UpdatedAt));
        command.Parameters.AddWithValue("$completedAt", ToDb(task.CompletedAt));
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
