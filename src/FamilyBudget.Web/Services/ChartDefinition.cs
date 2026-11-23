using System.Text.Json.Serialization;

namespace FamilyBudget.Web.Services;

/// <summary>
/// Describes a chart for wwwroot/js/charts.js. Only data and colors live here;
/// formatting of kronor and shared styling are added on the JavaScript side.
/// </summary>
public sealed class ChartDefinition
{
    /// <summary>Chart.js chart type, normally "bar".</summary>
    public string Type { get; init; } = "bar";

    /// <summary>True draws the bars sideways, which suits long category names.</summary>
    public bool Horizontal { get; init; }

    /// <summary>Labels along the category axis, e.g. month names or years.</summary>
    public IReadOnlyList<string> Labels { get; init; } = [];

    public IReadOnlyList<ChartDataset> Datasets { get; init; } = [];
}

/// <summary>One series in a chart.</summary>
/// <param name="Label">Name shown in legend and tooltip.</param>
/// <param name="Data">One value per label.</param>
/// <param name="Color">Fill/line color.</param>
/// <param name="Type">Set to "line" to draw this series as a line on top of bars.</param>
public sealed record ChartDataset(
    string Label,
    IReadOnlyList<decimal> Data,
    string Color,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Type = null);

/// <summary>Chart colors, in a fixed order so each meaning always gets the same color.</summary>
public static class ChartColors
{
    public const string Income = "#2a78d6";   // blue
    public const string Expense = "#eb6834";  // orange
    public const string Net = "#1baf7a";      // aqua/green
    public const string Budget = "#2a78d6";   // blue, the plan
    public const string Outcome = "#eb6834";  // orange, what actually happened
}
