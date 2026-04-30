using System.Globalization;
using ClosedXML.Excel;

namespace ExcelDash.Services;

public sealed class ExcelImportService
{
    public ExcelImportResult Import(Stream excelStream)
    {
        using var workbook = new XLWorkbook(excelStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet is null)
        {
            return ExcelImportResult.Failure("No worksheet found in the Excel file.");
        }

        var used = worksheet.RangeUsed();
        if (used is null)
        {
            return ExcelImportResult.Failure("Worksheet is empty.");
        }

        var firstRow = used.FirstRowUsed();
        var lastRow = used.LastRowUsed();
        var firstCell = used.FirstCellUsed();
        var lastCell = used.LastCellUsed();

        if (firstRow is null || lastRow is null || firstCell is null || lastCell is null)
        {
            return ExcelImportResult.Failure("Worksheet is empty.");
        }

        var headerRowNumber = firstRow.RowNumber();
        var firstCol = firstCell.Address.ColumnNumber;
        var lastCol = lastCell.Address.ColumnNumber;

        var headers = new List<string>(capacity: lastCol - firstCol + 1);
        for (var col = firstCol; col <= lastCol; col++)
        {
            var raw = worksheet.Cell(headerRowNumber, col).GetString().Trim();
            headers.Add(string.IsNullOrWhiteSpace(raw) ? $"Column{col - firstCol + 1}" : raw);
        }

        var rows = new List<string[]>();
        for (var rowNumber = headerRowNumber + 1; rowNumber <= lastRow.RowNumber(); rowNumber++)
        {
            var row = new string[headers.Count];
            var hasAnyValue = false;

            for (var col = firstCol; col <= lastCol; col++)
            {
                var cell = worksheet.Cell(rowNumber, col);
                var value = cell.Value.ToString()?.Trim() ?? "";
                if (!string.IsNullOrWhiteSpace(value))
                {
                    hasAnyValue = true;
                }

                row[col - firstCol] = NormalizeNumber(value);
            }

            if (hasAnyValue)
            {
                rows.Add(row);
            }
        }

        if (rows.Count == 0)
        {
            return ExcelImportResult.Failure("No data rows found. Ensure the first row contains headers and rows below contain data.");
        }

        return ExcelImportResult.Success(
            fileName: "",
            sheetName: worksheet.Name,
            headers: headers,
            rows: rows
        );
    }

    private static string NormalizeNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _))
        {
            return value;
        }

        if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var currentCultureNumber))
        {
            return currentCultureNumber.ToString(CultureInfo.InvariantCulture);
        }

        return value;
    }
}

public sealed record ExcelImportResult(
    bool Ok,
    string Error,
    string FileName,
    string SheetName,
    IReadOnlyList<string> Headers,
    IReadOnlyList<string[]> Rows
)
{
    public static ExcelImportResult Failure(string error) =>
        new(false, error, "", "", Array.Empty<string>(), Array.Empty<string[]>());

    public static ExcelImportResult Success(string fileName, string sheetName, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows) =>
        new(true, "", fileName, sheetName, headers, rows);
}

