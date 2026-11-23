using ClosedXML.Excel;
using FamilyBudget.Web.Data;
using FamilyBudget.Web.Data.Budget;
using Microsoft.EntityFrameworkCore;

namespace FamilyBudget.Web.Services;

/// <summary>
/// Builds an Excel workbook (.xlsx) with a budget group's data, for "Ladda ner till Excel".
/// Sheets: every occurrence, totals per month, expenses per category and year, and the entries themselves.
/// </summary>
public class ExcelExporter(
    IDbContextFactory<FamilyBudgetDbContext> contextFactory,
    BudgetOutcomeCalculator outcomeCalculator)
{
    // Swedish number formats: amounts with two decimals and thin-space thousands, dates as 2026-11-25.
    private const string MoneyFormat = "#,##0.00 \"kr\"";
    private const string WholeMoneyFormat = "#,##0 \"kr\"";
    private const string DateFormat = "yyyy-mm-dd";

    /// <summary>
    /// Creates the workbook for one group. With <paramref name="onlyYear"/> set, the occurrence and month
    /// sheets only cover that year; otherwise they cover the whole period.
    /// </summary>
    public async Task<byte[]> CreateWorkbookAsync(BudgetGroup budgetGroup, int? onlyYear)
    {
        List<OutcomeItem> items;
        if (onlyYear is { } year)
        {
            items = await outcomeCalculator.GetItemsAsync(budgetGroup.Id, new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
        }
        else
        {
            (items, _, _) = await outcomeCalculator.GetAllItemsAsync(budgetGroup.Id);
        }
        items = items.OrderBy(item => item.DueDate).ThenBy(item => item.Kind).ThenBy(item => item.EntryName).ToList();

        await using var databaseContext = await contextFactory.CreateDbContextAsync();
        var entries = await databaseContext.BudgetEntries
            .Where(entry => entry.BudgetGroupId == budgetGroup.Id)
            .Include(entry => entry.Category)
            .OrderBy(entry => entry.Kind).ThenBy(entry => entry.Name)
            .AsNoTracking()
            .ToListAsync();

        using var workbook = new XLWorkbook();
        AddOccurrenceSheet(workbook, items);
        AddMonthSheet(workbook, items);
        AddCategoryYearSheet(workbook, items);
        AddEntrySheet(workbook, entries);

        using var outputStream = new MemoryStream();
        workbook.SaveAs(outputStream);
        return outputStream.ToArray();
    }

    /// <summary>One row per due date of every entry: the raw data, ready for filtering and pivot tables.</summary>
    private static void AddOccurrenceSheet(XLWorkbook workbook, List<OutcomeItem> items)
    {
        var worksheet = workbook.Worksheets.Add("Poster");
        string[] headers = ["Datum", "År", "Månad", "Typ", "Namn", "Kategori", "Förväntat", "Betalt", "Belopp", "Status"];
        WriteHeaders(worksheet, headers);

        var rowNumber = 2;
        foreach (var item in items)
        {
            worksheet.Cell(rowNumber, 1).Value = item.DueDate.ToDateTime(TimeOnly.MinValue);
            worksheet.Cell(rowNumber, 2).Value = item.DueDate.Year;
            worksheet.Cell(rowNumber, 3).Value = item.DueDate.Month;
            worksheet.Cell(rowNumber, 4).Value = BudgetTexts.Kind(item.Kind);
            worksheet.Cell(rowNumber, 5).Value = item.EntryName;
            worksheet.Cell(rowNumber, 6).Value = item.CategoryName ?? "Utan kategori";
            worksheet.Cell(rowNumber, 7).Value = item.ExpectedAmount;
            if (item.PaidAmount is { } paidAmount)
            {
                worksheet.Cell(rowNumber, 8).Value = paidAmount;
            }
            worksheet.Cell(rowNumber, 9).Value = item.EffectiveAmount;
            worksheet.Cell(rowNumber, 10).Value = item.IsPaid ? (item.Kind == EntryKind.Income ? "Mottagen" : "Betald") : "Ej betald";
            rowNumber++;
        }

        worksheet.Column(1).Style.NumberFormat.Format = DateFormat;
        worksheet.Columns(7, 9).Style.NumberFormat.Format = MoneyFormat;
        FinishSheet(worksheet, headers.Length, rowNumber - 1, [12, 7, 7, 9, 32, 24, 14, 14, 14, 11]);
    }

    /// <summary>Income, expenses and net per month.</summary>
    private static void AddMonthSheet(XLWorkbook workbook, List<OutcomeItem> items)
    {
        var worksheet = workbook.Worksheets.Add("Per månad");
        string[] headers = ["År", "Månad", "Inkomster", "Utgifter", "Netto"];
        WriteHeaders(worksheet, headers);

        var monthGroups = items
            .GroupBy(item => (item.DueDate.Year, item.DueDate.Month))
            .OrderBy(monthGroup => monthGroup.Key);

        var rowNumber = 2;
        foreach (var monthGroup in monthGroups)
        {
            var income = monthGroup.Where(item => item.Kind == EntryKind.Income).Sum(item => item.EffectiveAmount);
            var expenses = monthGroup.Where(item => item.Kind == EntryKind.Expense).Sum(item => item.EffectiveAmount);
            worksheet.Cell(rowNumber, 1).Value = monthGroup.Key.Year;
            worksheet.Cell(rowNumber, 2).Value = BudgetTexts.MonthTitle(monthGroup.Key.Year, monthGroup.Key.Month).Split(' ')[0];
            worksheet.Cell(rowNumber, 3).Value = income;
            worksheet.Cell(rowNumber, 4).Value = expenses;
            // A formula, so the sheet still adds up if someone edits a value in Excel.
            worksheet.Cell(rowNumber, 5).FormulaA1 = $"C{rowNumber}-D{rowNumber}";
            rowNumber++;
        }

        worksheet.Columns(3, 5).Style.NumberFormat.Format = MoneyFormat;
        FinishSheet(worksheet, headers.Length, rowNumber - 1, [8, 12, 16, 16, 16]);
    }

    /// <summary>Expenses per category (rows) and year (columns), like the year-over-year statistics.</summary>
    private static void AddCategoryYearSheet(XLWorkbook workbook, List<OutcomeItem> items)
    {
        var worksheet = workbook.Worksheets.Add("Utgifter per kategori");
        var expenseItems = items.Where(item => item.Kind == EntryKind.Expense).ToList();
        var years = expenseItems.Select(item => item.DueDate.Year).Distinct().Order().ToList();
        var categoryNames = expenseItems
            .GroupBy(item => item.CategoryName ?? "Utan kategori")
            .OrderByDescending(categoryGroup => categoryGroup.Sum(item => item.EffectiveAmount))
            .Select(categoryGroup => categoryGroup.Key)
            .ToList();

        string[] headers = ["Kategori", .. years.Select(year => year.ToString()), "Totalt"];
        WriteHeaders(worksheet, headers);

        // Sum once per (category, year) instead of scanning the list for every cell.
        var amountByCategoryAndYear = expenseItems
            .GroupBy(item => (CategoryName: item.CategoryName ?? "Utan kategori", item.DueDate.Year))
            .ToDictionary(cellGroup => cellGroup.Key, cellGroup => cellGroup.Sum(item => item.EffectiveAmount));

        var rowNumber = 2;
        var totalColumnNumber = years.Count + 2;
        foreach (var categoryName in categoryNames)
        {
            worksheet.Cell(rowNumber, 1).Value = categoryName;
            for (var yearIndex = 0; yearIndex < years.Count; yearIndex++)
            {
                if (amountByCategoryAndYear.TryGetValue((categoryName, years[yearIndex]), out var cellAmount))
                {
                    worksheet.Cell(rowNumber, yearIndex + 2).Value = cellAmount;
                }
            }
            worksheet.Cell(rowNumber, totalColumnNumber).FormulaR1C1 = $"SUM(RC2:RC{totalColumnNumber - 1})";
            rowNumber++;
        }

        // Totals row at the bottom.
        if (categoryNames.Count > 0)
        {
            worksheet.Cell(rowNumber, 1).Value = "Totalt";
            for (var columnNumber = 2; columnNumber <= totalColumnNumber; columnNumber++)
            {
                worksheet.Cell(rowNumber, columnNumber).FormulaR1C1 = $"SUM(R2C:R{rowNumber - 1}C)";
            }
            worksheet.Row(rowNumber).Style.Font.Bold = true;
        }

        worksheet.Columns(2, totalColumnNumber).Style.NumberFormat.Format = WholeMoneyFormat;
        worksheet.Column(1).Width = 26;
        worksheet.Columns(2, totalColumnNumber).Width = 13;
        worksheet.SheetView.FreezeRows(1);
        worksheet.SheetView.FreezeColumns(1);
    }

    /// <summary>The bills and incomes as they are registered: amount, how often, from and to.</summary>
    private static void AddEntrySheet(XLWorkbook workbook, List<BudgetEntry> entries)
    {
        var worksheet = workbook.Worksheets.Add("Fakturor och inkomster");
        string[] headers = ["Typ", "Namn", "Kategori", "Belopp", "Hur ofta", "Första förfallodag", "Slutdatum", "Anteckning"];
        WriteHeaders(worksheet, headers);

        var rowNumber = 2;
        foreach (var entry in entries)
        {
            worksheet.Cell(rowNumber, 1).Value = BudgetTexts.Kind(entry.Kind);
            worksheet.Cell(rowNumber, 2).Value = entry.Name;
            worksheet.Cell(rowNumber, 3).Value = entry.Category?.Name ?? "Utan kategori";
            worksheet.Cell(rowNumber, 4).Value = entry.Amount;
            worksheet.Cell(rowNumber, 5).Value = BudgetTexts.Periodicity(entry.Periodicity);
            worksheet.Cell(rowNumber, 6).Value = entry.FirstDueDate.ToDateTime(TimeOnly.MinValue);
            if (entry.EndDate is { } endDate)
            {
                worksheet.Cell(rowNumber, 7).Value = endDate.ToDateTime(TimeOnly.MinValue);
            }
            worksheet.Cell(rowNumber, 8).Value = entry.Note ?? string.Empty;
            rowNumber++;
        }

        worksheet.Column(4).Style.NumberFormat.Format = MoneyFormat;
        worksheet.Columns(6, 7).Style.NumberFormat.Format = DateFormat;
        FinishSheet(worksheet, headers.Length, rowNumber - 1, [9, 32, 24, 14, 14, 16, 12, 40]);
    }

    private static void WriteHeaders(IXLWorksheet worksheet, string[] headers)
    {
        for (var columnIndex = 0; columnIndex < headers.Length; columnIndex++)
        {
            worksheet.Cell(1, columnIndex + 1).Value = headers[columnIndex];
        }
        worksheet.Row(1).Style.Font.Bold = true;
    }

    /// <summary>
    /// Bold header row that stays visible when scrolling, a filter on every column and fixed column widths.
    /// (Fixed widths instead of auto-fit, which needs fonts the container does not have.)
    /// </summary>
    private static void FinishSheet(IXLWorksheet worksheet, int columnCount, int lastRowNumber, int[] columnWidths)
    {
        worksheet.SheetView.FreezeRows(1);
        if (lastRowNumber >= 2)
        {
            worksheet.Range(1, 1, lastRowNumber, columnCount).SetAutoFilter();
        }
        for (var columnIndex = 0; columnIndex < columnWidths.Length; columnIndex++)
        {
            worksheet.Column(columnIndex + 1).Width = columnWidths[columnIndex];
        }
    }
}
