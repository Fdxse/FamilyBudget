// Small wrapper around Chart.js used from Blazor (Components/Shared/BudgetChart.razor).
// Chart.js itself is loaded as a plain script (lib/chartjs/chart.umd.min.js) and exposes window.Chart.

// One Chart.js instance per canvas, so a chart can be redrawn or removed later.
const chartsByCanvas = new WeakMap();

// All amounts in the app are Swedish kronor.
const moneyFormatter = new Intl.NumberFormat("sv-SE", { style: "currency", currency: "SEK", maximumFractionDigits: 0 });

// Short axis labels: 12 500 -> "13 tkr", 1 250 000 -> "1,3 mkr".
function shortMoney(value) {
    const absoluteValue = Math.abs(value);
    if (absoluteValue >= 1000000) {
        return (value / 1000000).toLocaleString("sv-SE", { maximumFractionDigits: 1 }) + " mkr";
    }
    if (absoluteValue >= 1000) {
        return (value / 1000).toLocaleString("sv-SE", { maximumFractionDigits: 0 }) + " tkr";
    }
    return value.toLocaleString("sv-SE") + " kr";
}

// Adds the parts of the configuration that cannot come from C# (functions) and shared styling.
function buildConfiguration(chartDefinition) {
    const isHorizontal = chartDefinition.horizontal === true;
    const valueAxis = isHorizontal ? "x" : "y";
    const categoryAxis = isHorizontal ? "y" : "x";

    const datasets = chartDefinition.datasets.map(dataset => ({
        ...dataset,
        // Thin rounded bars and 2px lines with visible markers.
        borderRadius: dataset.type === "line" ? undefined : 4,
        borderWidth: dataset.type === "line" ? 2 : 0,
        pointRadius: dataset.type === "line" ? 3 : undefined,
        pointHoverRadius: dataset.type === "line" ? 5 : undefined,
        maxBarThickness: 28,
        // Lower order is drawn on top, so lines are never hidden behind bars.
        order: dataset.type === "line" ? 0 : 1,
        backgroundColor: dataset.color,
        borderColor: dataset.color,
    }));

    return {
        type: chartDefinition.type ?? "bar",
        data: { labels: chartDefinition.labels, datasets },
        options: {
            indexAxis: categoryAxis,
            responsive: true,
            maintainAspectRatio: false,
            animation: { duration: 250 },
            // Hovering anywhere over a month/year shows all series for it.
            interaction: { mode: "index", intersect: false, axis: categoryAxis },
            plugins: {
                legend: { display: datasets.length > 1, position: "top", labels: { boxWidth: 12, boxHeight: 12 } },
                tooltip: {
                    callbacks: {
                        label: context => `${context.dataset.label}: ${moneyFormatter.format(context.parsed[valueAxis])}`,
                    },
                },
            },
            scales: {
                [valueAxis]: {
                    beginAtZero: true,
                    grid: { color: "rgba(0, 0, 0, 0.06)" },
                    border: { display: false },
                    ticks: { callback: value => shortMoney(value), color: "#52514e" },
                },
                [categoryAxis]: {
                    grid: { display: false },
                    ticks: { color: "#52514e", autoSkip: !isHorizontal },
                },
            },
        },
    };
}

// Draws (or redraws) a chart on the canvas.
export function renderChart(canvasElement, chartDefinition) {
    destroyChart(canvasElement);
    const chartInstance = new window.Chart(canvasElement, buildConfiguration(chartDefinition));
    chartsByCanvas.set(canvasElement, chartInstance);
}

// Removes the chart from the canvas, e.g. when the Blazor component goes away.
export function destroyChart(canvasElement) {
    const existingChart = canvasElement ? chartsByCanvas.get(canvasElement) : undefined;
    if (existingChart) {
        existingChart.destroy();
        chartsByCanvas.delete(canvasElement);
    }
}
