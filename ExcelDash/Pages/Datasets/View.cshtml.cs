using System.Globalization;
using System.Text.Json;
using ExcelDash.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ExcelDash.Pages.Datasets;

public sealed class ViewModel(AppDbContext db) : PageModel
{
    private readonly AppDbContext _db = db;

    public int DatasetId { get; private set; }
    public string FileName { get; private set; } = "";
    public string SheetName { get; private set; } = "";
    public DateTimeOffset UploadedAt { get; private set; }

    public List<string> Headers { get; private set; } = [];
    public List<string[]> Rows { get; private set; } = [];

    public int RowCount => Rows.Count;
    public int ColumnCount => Headers.Count;

    public HashSet<string> NumericColumns { get; private set; } = new(StringComparer.Ordinal);
    public string CategoryColumn { get; private set; } = "";

    public bool HasChart { get; private set; }
    public string ChartSubtitle { get; private set; } = "";
    public string ChartConfigJson { get; private set; } = "{}";

    public List<string[]> PreviewRows { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var dataset = await _db.Datasets.FirstOrDefaultAsync(x => x.Id == id);
        if (dataset is null)
        {
            return NotFound();
        }

        DatasetId = dataset.Id;
        FileName = dataset.FileName;
        SheetName = dataset.SheetName;
        UploadedAt = dataset.UploadedAt;

        Headers = JsonSerializer.Deserialize<List<string>>(dataset.HeadersJson) ?? [];
        Rows = JsonSerializer.Deserialize<List<string[]>>(dataset.RowsJson) ?? [];

        PreviewRows = Rows.Take(20).ToList();

        InferColumns();
        BuildChart();

        return Page();
    }

    private void InferColumns()
    {
        if (Headers.Count == 0 || Rows.Count == 0)
        {
            return;
        }

        var numeric = new HashSet<int>();
        for (var colIndex = 0; colIndex < Headers.Count; colIndex++)
        {
            var samples = 0;
            var numericSamples = 0;

            foreach (var row in Rows)
            {
                if (colIndex >= row.Length)
                {
                    continue;
                }

                var v = row[colIndex];
                if (string.IsNullOrWhiteSpace(v))
                {
                    continue;
                }

                samples++;
                if (double.TryParse(v, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _))
                {
                    numericSamples++;
                }
            }

            if (samples > 0 && (double)numericSamples / samples >= 0.8)
            {
                numeric.Add(colIndex);
                NumericColumns.Add(Headers[colIndex]);
            }
        }

        // Choose a category column: first non-numeric column with a reasonable number of distinct values.
        for (var colIndex = 0; colIndex < Headers.Count; colIndex++)
        {
            if (numeric.Contains(colIndex))
            {
                continue;
            }

            var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in Rows.Take(500))
            {
                if (colIndex >= row.Length)
                {
                    continue;
                }

                var v = row[colIndex]?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(v))
                {
                    continue;
                }

                distinct.Add(v);
                if (distinct.Count > 25)
                {
                    break;
                }
            }

            if (distinct.Count is >= 2 and <= 25)
            {
                CategoryColumn = Headers[colIndex];
                return;
            }
        }
    }

    private void BuildChart()
    {
        if (Headers.Count == 0 || Rows.Count == 0 || NumericColumns.Count == 0)
        {
            HasChart = false;
            return;
        }

        var numericIndex = Headers.FindIndex(h => NumericColumns.Contains(h));
        if (numericIndex < 0)
        {
            HasChart = false;
            return;
        }

        var categoryIndex = !string.IsNullOrWhiteSpace(CategoryColumn) ? Headers.IndexOf(CategoryColumn) : -1;

        List<string> labels;
        List<double> values;

        if (categoryIndex >= 0)
        {
            var sums = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in Rows)
            {
                if (numericIndex >= row.Length)
                {
                    continue;
                }

                if (!double.TryParse(row[numericIndex], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var n))
                {
                    continue;
                }

                var cat = categoryIndex < row.Length ? (row[categoryIndex]?.Trim() ?? "") : "";
                if (string.IsNullOrWhiteSpace(cat))
                {
                    cat = "(blank)";
                }

                sums[cat] = sums.TryGetValue(cat, out var existing) ? existing + n : n;
            }

            labels = sums.Keys.OrderBy(k => k).Take(25).ToList();
            values = labels.Select(l => sums[l]).ToList();
            ChartSubtitle = $"Sum of {Headers[numericIndex]} by {Headers[categoryIndex]} (top {labels.Count}).";
        }
        else
        {
            // If no category column, chart the first 25 rows as a quick “series”.
            labels = new List<string>();
            values = new List<double>();

            for (var i = 0; i < Rows.Count && labels.Count < 25; i++)
            {
                var row = Rows[i];
                if (numericIndex >= row.Length)
                {
                    continue;
                }

                if (!double.TryParse(row[numericIndex], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var n))
                {
                    continue;
                }

                labels.Add($"Row {i + 1}");
                values.Add(n);
            }

            ChartSubtitle = $"First {labels.Count} values of {Headers[numericIndex]}.";
        }

        if (labels.Count < 2)
        {
            HasChart = false;
            return;
        }

        HasChart = true;

        var config = new
        {
            type = "bar",
            data = new
            {
                labels,
                datasets = new[]
                {
                    new
                    {
                        label = Headers[numericIndex],
                        data = values,
                        backgroundColor = "rgba(13,110,253,0.35)",
                        borderColor = "rgba(13,110,253,1.0)",
                        borderWidth = 1,
                    }
                }
            },
            options = new
            {
                responsive = true,
                plugins = new
                {
                    legend = new { display = true },
                },
                scales = new
                {
                    y = new { beginAtZero = true }
                }
            }
        };

        ChartConfigJson = JsonSerializer.Serialize(config);
    }
}

