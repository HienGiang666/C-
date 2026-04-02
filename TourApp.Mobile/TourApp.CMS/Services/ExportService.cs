using System.Text;

namespace TourApp.CMS.Services
{
    public interface IExportService
    {
        byte[] ExportToExcel<T>(List<T> data, string sheetName) where T : class;
    }

    public class ExportService : IExportService
    {
        private readonly ILogger<ExportService> _logger;

        public ExportService(ILogger<ExportService> logger)
        {
            _logger = logger;
        }

        public byte[] ExportToExcel<T>(List<T> data, string sheetName) where T : class
        {
            try
            {
                if (data == null || data.Count == 0)
                    throw new ArgumentException("Data is empty");

                // Create Excel file in memory
                var excel = new StringBuilder();
                excel.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                excel.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
                excel.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
                excel.AppendLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
                excel.AppendLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
                excel.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\"");
                excel.AppendLine(" xmlns:html=\"http://www.w3.org/TR/REC-html40\">");
                excel.AppendLine($"<Worksheet ss:Name=\"{sheetName}\">");
                excel.AppendLine("<Table>");

                // Header row
                var properties = typeof(T).GetProperties();
                excel.AppendLine("<Row ss:StyleID=\"s1\">");
                foreach (var prop in properties)
                {
                    excel.AppendLine($"<Cell><Data ss:Type=\"String\">{prop.Name}</Data></Cell>");
                }
                excel.AppendLine("</Row>");

                // Data rows
                foreach (var item in data)
                {
                    excel.AppendLine("<Row>");
                    foreach (var prop in properties)
                    {
                        var value = prop.GetValue(item)?.ToString() ?? "";
                        excel.AppendLine($"<Cell><Data ss:Type=\"String\">{EscapeXml(value)}</Data></Cell>");
                    }
                    excel.AppendLine("</Row>");
                }

                excel.AppendLine("</Table>");
                excel.AppendLine("</Worksheet>");
                excel.AppendLine("</Workbook>");

                _logger.LogInformation($"Excel export created with {data.Count} rows");
                return Encoding.UTF8.GetBytes(excel.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error exporting to Excel: {ex.Message}");
                throw;
            }
        }

        private string EscapeXml(string text)
        {
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}
