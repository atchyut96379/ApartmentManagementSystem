using ApartmentManagementSystem.Models;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ApartmentManagementSystem.Services
{
    public class PaymentReceiptPdfService
    {
        private readonly SocietySettings _society;

        public PaymentReceiptPdfService(IOptions<SocietySettings> society)
        {
            _society = society.Value;
        }

        public byte[] GeneratePdf(PaymentReceiptViewModel receipt)
        {
            var apartmentName = string.IsNullOrWhiteSpace(_society.ApartmentName)
                ? "Apartment Management"
                : _society.ApartmentName;

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Column(col =>
                    {
                        col.Item().Text(apartmentName).Bold().FontSize(18);
                        col.Item().Text("Maintenance Payment Receipt").FontSize(14);
                        col.Item().PaddingTop(4).Text($"Receipt #: {receipt.ReceiptNumber}")
                            .FontColor(Colors.Grey.Darken2);
                    });

                    page.Content().PaddingVertical(20).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(140);
                            columns.RelativeColumn();
                        });

                        AddRow(table, "Flat number", receipt.FlatNumber);
                        AddRow(table, "Name", receipt.Name);
                        AddRow(table, "Month", $"{receipt.Month} {receipt.Year}");
                        AddRow(table, "Amount", $"₹{receipt.Amount:N2}");
                        AddRow(table, "Fine", $"₹{receipt.Fine:N2}");
                        AddRow(table, "Total paid", $"₹{receipt.TotalPaid:N2}");
                        AddRow(table, "Transaction ID", receipt.TransactionId);
                        AddRow(table, "Paid on", receipt.PaidAt.ToString("dd-MM-yyyy HH:mm"));
                        AddRow(table, "Gateway", receipt.PaymentGateway);
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Generated on ");
                        text.Span(DateTime.Now.ToString("dd-MM-yyyy HH:mm"));
                    });
                });
            }).GeneratePdf();
        }

        private static void AddRow(TableDescriptor table, string label, string value)
        {
            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                .PaddingVertical(6).Text(label).SemiBold();
            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                .PaddingVertical(6).Text(value);
        }
    }
}
