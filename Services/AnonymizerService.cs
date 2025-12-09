using System.Text.RegularExpressions;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;

namespace GrokBlazorApp.Services
{
    /// <summary>
    /// Anonymizer service using Microsoft.Extensions.Compliance.Redaction engine
    /// </summary>
    public class AnonymizerService : IAnonymizerService
    {
        private readonly IRedactorProvider _redactorProvider;
        private readonly Dictionary<DataClassification, List<Regex>> _classificationPatterns;

        public AnonymizerService(IRedactorProvider redactorProvider)
        {
            _redactorProvider = redactorProvider;
            _classificationPatterns = BuildClassificationPatterns();
        }

        private static Dictionary<DataClassification, List<Regex>> BuildClassificationPatterns()
        {
            var options = RegexOptions.Compiled | RegexOptions.IgnoreCase;

            return new Dictionary<DataClassification, List<Regex>>
            {
                // PII Data patterns
                [DataTaxonomy.PiiData] = new List<Regex>
                {
                    // Email addresses
                    new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", options),
                    
                    // US Phone numbers (various formats including international)
                    new Regex(@"(?:\+?1[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b", options),
                    
                    // Social Security Numbers (XXX-XX-XXXX or XXXXXXXXX)
                    new Regex(@"\b\d{3}[-\s]?\d{2}[-\s]?\d{4}\b", options),
                    
                    // Dates (various formats - more precise patterns)
                    new Regex(@"\b(?:0?[1-9]|1[0-2])[-/](?:0?[1-9]|[12]\d|3[01])[-/](?:19|20)\d{2}\b", options), // MM/DD/YYYY
                    new Regex(@"\b(?:19|20)\d{2}[-/](?:0?[1-9]|1[0-2])[-/](?:0?[1-9]|[12]\d|3[01])\b", options), // YYYY-MM-DD
                    new Regex(@"\b(?:January|February|March|April|May|June|July|August|September|October|November|December)\s+\d{1,2},?\s+\d{4}\b", options), // Month DD, YYYY
                    
                    // Names patterns (common titles followed by names)
                    new Regex(@"\b(?:Mr\.?|Mrs\.?|Ms\.?|Dr\.?|Prof\.?)\s+[A-Z][a-z]+(?:\s+[A-Z][a-z]+)*\b", options),
                    
                    // Zip Codes (5 digits, optionally +4) - more precise
                    new Regex(@"\b\d{5}(?:-\d{4})?\b", options),
                    
                    // Street addresses
                    new Regex(@"\b\d+\s+[A-Za-z0-9\s]+(?:Street|St\.?|Avenue|Ave\.?|Road|Rd\.?|Boulevard|Blvd\.?|Drive|Dr\.?|Lane|Ln\.?|Way|Court|Ct\.?|Place|Pl\.?|Circle|Cir\.?)\b", options),
                    
                    // PO Box
                    new Regex(@"\bP\.?O\.?\s*Box\s+\d+\b", options),
                    
                    // IP Addresses
                    new Regex(@"\b(?:\d{1,3}\.){3}\d{1,3}\b", options),
                },

                // Financial Data patterns
                [DataTaxonomy.FinancialData] = new List<Regex>
                {
                    // Credit card numbers (various formats with Luhn-compatible patterns)
                    new Regex(@"\b(?:\d{4}[-\s]?){3}\d{4}\b", options),
                    
                    // Bank account numbers (generic pattern)
                    new Regex(@"\b(?:Account|Acct)\.?\s*#?\s*\d{8,17}\b", options),
                    
                    // Routing numbers
                    new Regex(@"\b(?:Routing|ABA)\.?\s*#?\s*\d{9}\b", options),
                    
                    // Currency amounts (large sums that might be sensitive)
                    new Regex(@"\$\s*\d{1,3}(?:,\d{3})+(?:\.\d{2})?\b", options),
                },

                // Medical Data patterns
                [DataTaxonomy.MedicalData] = new List<Regex>
                {
                    // Medical Record Numbers (MRN)
                    new Regex(@"\b(?:MRN|Medical Record|Patient ID|Chart)\.?\s*#?\s*:?\s*[A-Z0-9]{6,15}\b", options),
                    
                    // Health Insurance numbers
                    new Regex(@"\b(?:Policy|Insurance|Member)\s*(?:Number|No\.?|#)\s*:?\s*[A-Z0-9]{6,20}\b", options),
                    
                    // DEA numbers
                    new Regex(@"\b[A-Z]{2}\d{7}\b", options),
                    
                    // NPI (National Provider Identifier)
                    new Regex(@"\bNPI\s*:?\s*\d{10}\b", options),
                    
                    // Diagnosis codes (ICD-10)
                    new Regex(@"\b[A-Z]\d{2}(?:\.\d{1,4})?\b", options),
                },

                // General Sensitive Data
                [DataTaxonomy.SensitiveData] = new List<Regex>
                {
                    // Driver's License (generic pattern)
                    new Regex(@"\b(?:DL|Driver'?s?\s*License|License)\s*#?\s*:?\s*[A-Z0-9]{5,15}\b", options),
                    
                    // Passport numbers
                    new Regex(@"\b(?:Passport)\s*#?\s*:?\s*[A-Z0-9]{6,12}\b", options),
                    
                    // VIN numbers
                    new Regex(@"\b[A-HJ-NPR-Z0-9]{17}\b", options),
                }
            };
        }

        public string AnonymizeText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            // Process each classification in order of specificity
            var classificationsInOrder = new[]
            {
                DataTaxonomy.MedicalData,
                DataTaxonomy.FinancialData,
                DataTaxonomy.PiiData,
                DataTaxonomy.SensitiveData
            };

            foreach (var classification in classificationsInOrder)
            {
                if (_classificationPatterns.TryGetValue(classification, out var patterns))
                {
                    var redactor = _redactorProvider.GetRedactor(classification);
                    
                    foreach (var pattern in patterns)
                    {
                        text = pattern.Replace(text, match =>
                        {
                            return redactor.Redact(match.Value);
                        });
                    }
                }
            }

            return text;
        }
    }
}
