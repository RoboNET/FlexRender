using FlexRender;
using Xunit;

namespace FlexRender.Tests.Snapshots;

/// <summary>
/// Golden-image snapshot tests for charts (types × themes).
/// </summary>
public sealed class ChartSnapshotTests : SnapshotTestBase
{
    [Fact]
    public async Task BarChart_Light()
    {
        var template = Parser.Parse("""
            canvas:
              width: 600
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: bar
                width: 600
                height: 320
                categories: [Q1, Q2, Q3, Q4]
                series:
                  - label: "2024"
                    data: [12, 30, 22, 48]
                title: Revenue
                legend: bottom
                palette: ocean
            """);
        await AssertSnapshot("chart_bar_light", template, new ObjectValue());
    }

    [Fact]
    public async Task BarChart_HorizontalDark()
    {
        var template = Parser.Parse("""
            canvas:
              width: 600
              background: "#1e1e1e"
            layout:
              - type: chart
                chart-type: bar
                width: 600
                height: 320
                horizontal: true
                categories: [A, B, C, D]
                series:
                  - data: [5, 40, 25, 60]
                theme: dark
                legend: none
                palette: vivid
            """);
        await AssertSnapshot("chart_bar_horizontal_dark", template, new ObjectValue());
    }

    [Fact]
    public async Task LineChart_Minimal()
    {
        var template = Parser.Parse("""
            canvas:
              width: 600
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: line
                width: 600
                height: 300
                categories: [Mon, Tue, Wed, Thu, Fri]
                series:
                  - label: Visitors
                    data: [120, 200, 150, 280, 240]
                  - label: Signups
                    data: [20, 45, 30, 60, 50]
                theme: minimal
                points: true
                legend: bottom
            """);
        await AssertSnapshot("chart_line_minimal", template, new ObjectValue());
    }

    [Fact]
    public async Task AreaChart_Light()
    {
        var template = Parser.Parse("""
            canvas:
              width: 600
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: area
                width: 600
                height: 300
                categories: [Jan, Feb, Mar, Apr]
                series:
                  - data: [30, 60, 45, 80]
                legend: none
                palette: forest
            """);
        await AssertSnapshot("chart_area_light", template, new ObjectValue());
    }

    [Fact]
    public async Task PieChart_Light()
    {
        var template = Parser.Parse("""
            canvas:
              width: 400
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: pie
                width: 400
                height: 360
                categories: [Direct, Social, Search]
                series:
                  - data: [30, 50, 20]
                legend: bottom
                palette: sunset
            """);
        await AssertSnapshot("chart_pie_light", template, new ObjectValue());
    }

    [Fact]
    public async Task DonutChart_Dark()
    {
        var template = Parser.Parse("""
            canvas:
              width: 400
              background: "#1e1e1e"
            layout:
              - type: chart
                chart-type: donut
                width: 400
                height: 360
                categories: [A, B, C, D]
                series:
                  - data: [10, 20, 30, 40]
                theme: dark
                legend: bottom
                palette: ocean
            """);
        await AssertSnapshot("chart_donut_dark", template, new ObjectValue());
    }

    [Fact]
    public async Task ScatterChart_Light()
    {
        var template = Parser.Parse("""
            canvas:
              width: 480
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: scatter
                width: 480
                height: 320
                series:
                  - label: cloud
                    data: [[1, 12], [3, 30], [5, 22], [7, 48], [9, 35]]
                legend: none
                palette: ocean
            """);
        await AssertSnapshot("chart_scatter_light", template, new ObjectValue());
    }

    [Fact]
    public async Task BubbleChart_Light()
    {
        var template = Parser.Parse("""
            canvas:
              width: 480
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: bubble
                width: 480
                height: 320
                series:
                  - data: [[1, 12, 6], [3, 30, 18], [5, 22, 10], [7, 48, 24]]
                legend: none
                palette: sunset
            """);
        await AssertSnapshot("chart_bubble_light", template, new ObjectValue());
    }

    [Fact]
    public async Task GaugeChart_Dark()
    {
        var template = Parser.Parse("""
            canvas:
              width: 280
              background: "#1e1e1e"
            layout:
              - type: chart
                chart-type: gauge
                width: 280
                height: 220
                value: 72
                max: 100
                label: CPU
                theme: dark
                palette: vivid
            """);
        await AssertSnapshot("chart_gauge_dark", template, new ObjectValue());
    }

    [Fact]
    public async Task ProgressChart_Light()
    {
        var template = Parser.Parse("""
            canvas:
              width: 240
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: progress
                width: 240
                height: 240
                value: 64
                max: 100
                label: Disk
                palette: forest
            """);
        await AssertSnapshot("chart_progress_light", template, new ObjectValue());
    }

    [Fact]
    public async Task SparklineChart_Light()
    {
        var template = Parser.Parse("""
            canvas:
              width: 200
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: sparkline
                width: 200
                height: 50
                smooth: true
                series:
                  - data: [3, 8, 4, 10, 6, 9, 5, 11]
            """);
        await AssertSnapshot("chart_sparkline_light", template, new ObjectValue());
    }

    [Fact]
    public async Task EmptyChart_NoDataPlaceholder()
    {
        var template = Parser.Parse("""
            canvas:
              width: 300
              background: "#ffffff"
            layout:
              - type: chart
                chart-type: bar
                width: 300
                height: 180
                series: []
            """);
        await AssertSnapshot("chart_no_data", template, new ObjectValue());
    }
}
