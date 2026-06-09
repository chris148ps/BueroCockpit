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
                AssignedTo TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                CompletedAt TEXT NULL
            );
            """);

        ExecuteNonQuery(connection, """
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
                AddedAt TEXT NOT NULL
            );
            """);

        SeedDefaultCategories(connection);
    }

    public List<CategoryItem> GetCategories()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, SortOrder, Color, IsVisible
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
                Color = reader.GetString(3),
                IsVisible = reader.GetInt32(4) == 1
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
                   DueDate, FollowUpDate, AssignedTo, CreatedAt, UpdatedAt, CompletedAt
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
                AssignedTo = reader.GetString(9),
                CreatedAt = ReadDate(reader.GetString(10)),
                UpdatedAt = ReadDate(reader.GetString(11)),
                CompletedAt = ReadNullableDate(reader, 12)
            });
        }

        return items;
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
            SELECT Id, TaskId, FileName, StoredPath, ThumbnailPath, FileType, AddedAt
            FROM Attachments
            WHERE TaskId = $taskId
            ORDER BY AddedAt DESC;
            """;
        command.Parameters.AddWithValue("$taskId", taskId);

        using var reader = command.ExecuteReader();
        var items = new List<AttachmentItem>();
        while (reader.Read())
        {
            items.Add(new AttachmentItem
            {
                Id = reader.GetString(0),
                TaskId = reader.GetString(1),
                FileName = reader.GetString(2),
                StoredPath = reader.GetString(3),
                ThumbnailPath = reader.GetString(4),
                FileType = reader.GetString(5),
                AddedAt = ReadDate(reader.GetString(6))
            });
        }

        return items;
    }

    public void SaveTask(TaskItem task)
    {
        task.UpdatedAt = DateTime.Now;
        if (task.CreatedAt == default)
        {
            task.CreatedAt = task.UpdatedAt;
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Tasks (Id, Title, CustomerName, Description, CategoryId, Status, Priority,
                               DueDate, FollowUpDate, AssignedTo, CreatedAt, UpdatedAt, CompletedAt)
            VALUES ($id, $title, $customerName, $description, $categoryId, $status, $priority,
                    $dueDate, $followUpDate, $assignedTo, $createdAt, $updatedAt, $completedAt)
            ON CONFLICT(Id) DO UPDATE SET
                Title = excluded.Title,
                CustomerName = excluded.CustomerName,
                Description = excluded.Description,
                CategoryId = excluded.CategoryId,
                Status = excluded.Status,
                Priority = excluded.Priority,
                DueDate = excluded.DueDate,
                FollowUpDate = excluded.FollowUpDate,
                AssignedTo = excluded.AssignedTo,
                UpdatedAt = excluded.UpdatedAt,
                CompletedAt = excluded.CompletedAt;
            """;
        AddTaskParameters(command, task);
        command.ExecuteNonQuery();
    }

    public void DeleteTask(string taskId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var table in new[] { "Attachments", "Materials", "Tasks" })
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"DELETE FROM {table} WHERE {(table == "Tasks" ? "Id" : "TaskId")} = $taskId;";
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

    public void SaveAttachment(AttachmentItem item)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Attachments (Id, TaskId, FileName, StoredPath, ThumbnailPath, FileType, AddedAt)
            VALUES ($id, $taskId, $fileName, $storedPath, $thumbnailPath, $fileType, $addedAt);
            """;
        command.Parameters.AddWithValue("$id", item.Id);
        command.Parameters.AddWithValue("$taskId", item.TaskId);
        command.Parameters.AddWithValue("$fileName", item.FileName);
        command.Parameters.AddWithValue("$storedPath", item.StoredPath);
        command.Parameters.AddWithValue("$thumbnailPath", item.ThumbnailPath);
        command.Parameters.AddWithValue("$fileType", item.FileType);
        command.Parameters.AddWithValue("$addedAt", ToDb(item.AddedAt));
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
                INSERT INTO Categories (Id, Name, SortOrder, Color, IsVisible)
                VALUES ($id, $name, $sortOrder, $color, 1);
                """;
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
            command.Parameters.AddWithValue("$name", defaults[i]);
            command.Parameters.AddWithValue("$sortOrder", i);
            command.Parameters.AddWithValue("$color", i == 0 ? "#E9EEF7" : "#F2F3F5");
            command.ExecuteNonQuery();
        }
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
}
