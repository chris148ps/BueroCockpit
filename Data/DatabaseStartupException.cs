namespace BueroCockpit.Data;

public sealed class DatabaseStartupException : InvalidOperationException
{
    public DatabaseStartupException(string databasePath, string diagnosticMessage, Exception innerException)
        : base(diagnosticMessage, innerException)
    {
        DatabasePath = databasePath;
        DiagnosticMessage = diagnosticMessage;
    }

    public string DatabasePath { get; }

    public string DiagnosticMessage { get; }
}
