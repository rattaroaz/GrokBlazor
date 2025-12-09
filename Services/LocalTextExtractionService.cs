using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using Tesseract;
using Microsoft.Extensions.Logging;

namespace GrokBlazorApp.Services;

public class LocalTextExtractionService : ILocalTextExtractionService
{
    private readonly ILogger<LocalTextExtractionService> _logger;

    public LocalTextExtractionService(ILogger<LocalTextExtractionService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExtractTextAsync(string fileName, byte[] fileContent)
    {
        if (string.IsNullOrEmpty(fileName) || fileContent == null || fileContent.Length == 0)
        {
            return "No content available.";
        }

        string extension = Path.GetExtension(fileName).ToLowerInvariant();

        try
        {
            return extension switch
            {
                ".txt" => Encoding.UTF8.GetString(fileContent),
                ".pdf" => ExtractTextFromPdf(fileContent),
                ".docx" => ExtractTextFromDocx(fileContent),
                ".png" or ".jpg" or ".jpeg" or ".bmp" => await ExtractTextFromImageAsync(fileContent),
                _ => "Unsupported file type for text extraction."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from {FileName}", fileName);
            return $"Error extracting text: {ex.Message}";
        }
    }

    private string ExtractTextFromPdf(byte[] fileContent)
    {
        try
        {
            using var stream = new MemoryStream(fileContent);
            using var document = PdfDocument.Open(stream);
            var text = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                var pageText = page.Text;
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    text.Append(pageText);
                    text.AppendLine();
                }
                else
                {
                    // Try OCR on images in the page
                    var ocrText = ExtractTextFromPdfImages(page);
                    if (!string.IsNullOrWhiteSpace(ocrText))
                    {
                        text.Append(ocrText);
                        text.AppendLine();
                    }
                }
            }

            var extracted = text.ToString().Trim();
            if (string.IsNullOrWhiteSpace(extracted))
            {
                _logger.LogWarning("No text extracted from PDF even after OCR attempt. PDF might be empty or corrupted.");
                return "Error extracting text: No readable text found in PDF after trying OCR. PDF may be empty or corrupted.";
            }

            return extracted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from PDF");
            return $"Error extracting text: {ex.Message}";
        }
    }

    private string ExtractTextFromPdfImages(UglyToad.PdfPig.Content.Page page)
    {
        try
        {
            var images = page.GetImages();
            var ocrText = new StringBuilder();

            using var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default);
            foreach (var image in images)
            {
                using var img = Pix.LoadFromMemory(image.RawBytes.ToArray());
                using var pageOcr = engine.Process(img);
                var text = pageOcr.GetText();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    ocrText.Append(text);
                    ocrText.AppendLine();
                }
            }

            return ocrText.ToString().Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to OCR images in PDF page");
            return string.Empty;
        }
    }

    private string ExtractTextFromDocx(byte[] fileContent)
    {
        using var stream = new MemoryStream(fileContent);
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart?.Document.Body;

        if (body == null)
        {
            return "No text found in document.";
        }

        var text = new StringBuilder();
        foreach (var para in body.Elements<Paragraph>())
        {
            text.AppendLine(para.InnerText);
        }

        return text.ToString();
    }

    private Task<string> ExtractTextFromImageAsync(byte[] fileContent)
    {
        using var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default);
        using var img = Pix.LoadFromMemory(fileContent);
        using var page = engine.Process(img);
        var text = page.GetText();
        return Task.FromResult(text);
    }
}
