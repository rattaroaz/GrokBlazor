using Microsoft.Extensions.Compliance.Classification;

namespace GrokBlazorApp.Services;

/// <summary>
/// Data classification taxonomy for PII redaction using Microsoft.Extensions.Compliance.Redaction
/// </summary>
public static class DataTaxonomy
{
    public static string TaxonomyName => "PiiTaxonomy";

    public static DataClassification SensitiveData => new(TaxonomyName, nameof(SensitiveData));
    public static DataClassification PiiData => new(TaxonomyName, nameof(PiiData));
    public static DataClassification FinancialData => new(TaxonomyName, nameof(FinancialData));
    public static DataClassification MedicalData => new(TaxonomyName, nameof(MedicalData));
}

/// <summary>
/// Attribute for marking sensitive data that should be redacted
/// </summary>
public class SensitiveDataAttribute : DataClassificationAttribute
{
    public SensitiveDataAttribute() : base(DataTaxonomy.SensitiveData) { }
}

/// <summary>
/// Attribute for marking PII data (emails, phones, SSN, etc.)
/// </summary>
public class PiiDataAttribute : DataClassificationAttribute
{
    public PiiDataAttribute() : base(DataTaxonomy.PiiData) { }
}

/// <summary>
/// Attribute for marking financial data (credit cards, bank accounts)
/// </summary>
public class FinancialDataAttribute : DataClassificationAttribute
{
    public FinancialDataAttribute() : base(DataTaxonomy.FinancialData) { }
}

/// <summary>
/// Attribute for marking medical/health data
/// </summary>
public class MedicalDataAttribute : DataClassificationAttribute
{
    public MedicalDataAttribute() : base(DataTaxonomy.MedicalData) { }
}
