using System;
using Licenta.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Licenta.Services
{
    public interface IPdfService
    {
        byte[] GeneratePrescription(MedicalRecord record);
    }

    public class PdfService : IPdfService
    {
        public byte[] GeneratePrescription(MedicalRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            var patientName = record.Patient?.User?.FullName
                              ?? record.Patient?.User?.Email
                              ?? "Nespecificat";

            var doctorName = record.Doctor?.User?.FullName
                              ?? record.Doctor?.User?.Email
                              ?? "Nespecificat";

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Content().Column(col =>
                    {
                        col.Item().Text("REȚETĂ ȘI RECOMANDĂRI MEDICALE")
                            .Bold()
                            .FontSize(18)
                            .Underline()
                            .AlignCenter();

                        col.Item().Text($"Pacient: {patientName}");
                        col.Item().Text($"Doctor: {doctorName}");
                        col.Item().Text($"Data vizitei: {record.VisitDateUtc.ToLocalTime():f}");
                        col.Item().LineHorizontal(1);

                        col.Item().PaddingTop(10).Text("Diagnostic").Bold();
                        col.Item().Text(string.IsNullOrWhiteSpace(record.Diagnosis)
                            ? "-"
                            : record.Diagnosis);

                        col.Item().PaddingTop(10).Text("Simptome și Note").Bold();
                        col.Item().Text(string.IsNullOrWhiteSpace(record.Notes)
                            ? "-"
                            : record.Notes);

                        col.Item().PaddingTop(10).Text("Tratament / Recomandări").Bold();
                        col.Item().Text(string.IsNullOrWhiteSpace(record.Treatment)
                            ? "-"
                            : record.Treatment);
                    });

                    page.Footer()
                        .AlignRight()
                        .Text($"Generat automat de sistem - {DateTime.Now:f}")
                        .FontSize(9)
                        .Italic();
                });
            });

            return document.GeneratePdf();
        }
    }
}
