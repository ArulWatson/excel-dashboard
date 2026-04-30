using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ExcelDash.Data;
using ExcelDash.Models;
using ExcelDash.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ExcelDash.Pages;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ExcelImportService _excel;

    public IndexModel(AppDbContext db, ExcelImportService excel)
    {
        _db = db;
        _excel = excel;
    }

    [BindProperty]
    [Required(ErrorMessage = "Please choose an .xlsx file.")]
    public IFormFile? ExcelFile { get; set; }

    public string ErrorMessage { get; private set; } = "";

    public List<DatasetListItem> RecentDatasets { get; private set; } = [];

    public async Task OnGetAsync()
    {
        RecentDatasets = await _db.Datasets
            .OrderByDescending(x => x.Id)
            .Select(x => new DatasetListItem(x.Id, x.FileName, x.SheetName, x.UploadedAt))
            .Take(10)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostUploadAsync()
    {
        await OnGetAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (ExcelFile is null || ExcelFile.Length == 0)
        {
            ErrorMessage = "Empty upload.";
            return Page();
        }

        if (!Path.GetExtension(ExcelFile.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Only .xlsx files are supported.";
            return Page();
        }

        ExcelImportResult imported;
        try
        {
            await using var stream = ExcelFile.OpenReadStream();
            imported = _excel.Import(stream);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to read Excel file: {ex.Message}";
            return Page();
        }

        if (!imported.Ok)
        {
            ErrorMessage = imported.Error;
            return Page();
        }

        var dataset = new Dataset
        {
            FileName = ExcelFile.FileName,
            SheetName = imported.SheetName,
            UploadedAt = DateTimeOffset.UtcNow,
            HeadersJson = JsonSerializer.Serialize(imported.Headers),
            RowsJson = JsonSerializer.Serialize(imported.Rows),
        };

        _db.Datasets.Add(dataset);
        await _db.SaveChangesAsync();

        return RedirectToPage("/Datasets/View", new { id = dataset.Id });

    }
}

public sealed record DatasetListItem(int Id, string FileName, string SheetName, DateTimeOffset UploadedAt);
