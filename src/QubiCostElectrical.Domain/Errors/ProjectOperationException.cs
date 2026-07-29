namespace QubiCostElectrical;

public enum ProjectOperationKind
{
    ExcelExport,
    ExcelImport,
    PdfExport,
    ApuSync
}

public sealed class ProjectOperationException : Exception
{
    public ProjectOperationException(
        ProjectOperationKind kind,
        string userMessage,
        Exception? innerException = null)
        : base(userMessage, innerException)
    {
        Kind = kind;
        UserMessage = userMessage;
    }

    public ProjectOperationKind Kind { get; }
    public string UserMessage { get; }
}
