namespace ExcelDash.Models;

public sealed class Dataset
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
    public string SheetName { get; set; } = "";
    public DateTimeOffset UploadedAt { get; set; }

    public string HeadersJson { get; set; } = "[]";
    public string RowsJson { get; set; } = "[]";
}

