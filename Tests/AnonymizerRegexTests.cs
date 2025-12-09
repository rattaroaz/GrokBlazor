using Xunit;
using System.Text.RegularExpressions;

namespace GrokBlazorApp.Tests
{
    public class AnonymizerRegexTests
    {
        [Fact]
        public void TestNewPhoneNumberRegex()
        {
            // Improved regex
            var phonePattern = @"\(?\d{3}\)?[-. ]?\d{3}[-. ]?\d{4}";
            var text = "Call me at (123) 456-7890 or 123 456 7890 or 123-456-7890 or 123.456.7890.";
            
            var result = Regex.Replace(text, phonePattern, "[REDACTED]");
            
            Assert.DoesNotContain("(123) 456-7890", result);
            Assert.DoesNotContain("123 456 7890", result);
            Assert.DoesNotContain("123-456-7890", result);
            Assert.DoesNotContain("123.456.7890", result);
            Assert.Contains("Call me at [REDACTED] or [REDACTED] or [REDACTED] or [REDACTED].", result);
        }
    }
}
