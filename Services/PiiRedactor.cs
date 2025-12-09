using Microsoft.Extensions.Compliance.Redaction;

namespace GrokBlazorApp.Services;

/// <summary>
/// Custom redactor for PII data that replaces content with [REDACTED]
/// </summary>
public class PiiRedactor : Redactor
{
    private const string RedactedText = "[REDACTED]";

    public override int GetRedactedLength(ReadOnlySpan<char> input) => RedactedText.Length;

    public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
    {
        RedactedText.AsSpan().CopyTo(destination);
        return RedactedText.Length;
    }
}

/// <summary>
/// Custom redactor for financial data that replaces content with [FINANCIAL-REDACTED]
/// </summary>
public class FinancialRedactor : Redactor
{
    private const string RedactedText = "[FINANCIAL-REDACTED]";

    public override int GetRedactedLength(ReadOnlySpan<char> input) => RedactedText.Length;

    public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
    {
        RedactedText.AsSpan().CopyTo(destination);
        return RedactedText.Length;
    }
}

/// <summary>
/// Custom redactor for medical data that replaces content with [MEDICAL-REDACTED]
/// </summary>
public class MedicalRedactor : Redactor
{
    private const string RedactedText = "[MEDICAL-REDACTED]";

    public override int GetRedactedLength(ReadOnlySpan<char> input) => RedactedText.Length;

    public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
    {
        RedactedText.AsSpan().CopyTo(destination);
        return RedactedText.Length;
    }
}

/// <summary>
/// Custom redactor for general sensitive data
/// </summary>
public class SensitiveDataRedactor : Redactor
{
    private const string RedactedText = "[SENSITIVE-REDACTED]";

    public override int GetRedactedLength(ReadOnlySpan<char> input) => RedactedText.Length;

    public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
    {
        RedactedText.AsSpan().CopyTo(destination);
        return RedactedText.Length;
    }
}
