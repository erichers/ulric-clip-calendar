using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Ulric.ClipCalendar.Api.Domain;

namespace Ulric.ClipCalendar.Api.Pdf;

public static class SchedulePdf
{
    static SchedulePdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Build(IReadOnlyList<ClipExportRow> rows, string title)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(text => text.FontSize(9).FontColor("#1f1e1d"));

                page.Header().Column(column =>
                {
                    column.Item().Text("Ulric studio").FontSize(11).FontColor("#8f4630");
                    column.Item().PaddingTop(2).Text(title).FontSize(18).SemiBold();
                    column.Item().PaddingTop(2).Text("Planned clips. This file does not publish anything.").FontSize(9).FontColor("#5f5953");
                });

                page.Content().PaddingTop(14).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(72);
                        columns.ConstantColumn(78);
                        columns.RelativeColumn(1.3f);
                        columns.RelativeColumn(1.2f);
                        columns.ConstantColumn(36);
                        columns.RelativeColumn(1.4f);
                        columns.RelativeColumn(2.4f);
                        columns.ConstantColumn(72);
                    });

                    table.Header(header =>
                    {
                        foreach (var label in new[] { "Date", "Weekday", "Brand", "Series", "Part", "Platform", "Caption", "Status" })
                        {
                            header.Cell().Background("#faf9f5").BorderBottom(1).BorderColor("#e4ddd2").Padding(4).Text(label).SemiBold();
                        }
                    });

                    foreach (var row in rows)
                    {
                        Cell(table, row.PostDate.ToString("yyyy-MM-dd"));
                        Cell(table, Weekdays.English(row.PostDate));
                        Cell(table, row.Brand);
                        Cell(table, row.Series);
                        Cell(table, row.SeriesPart?.ToString() ?? "");
                        Cell(table, CsvExporter.PlatformLabel(row.Platforms));
                        Cell(table, row.Caption);
                        Cell(table, StatusLabels.For(row.Status));
                    }
                });

                page.Footer().AlignRight().Text(text =>
                {
                    text.Span("Ulric studio  ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void Cell(TableDescriptor table, string value)
    {
        table.Cell().BorderBottom(0.5f).BorderColor("#e4ddd2").Padding(4).Text(value);
    }
}
