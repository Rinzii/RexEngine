using Rex.Analyzers;

namespace Rex.Analyzers.Tests;

// Ensures analyzers remain loadable with stable diagnostic metadata.
public sealed class AnalyzerRegressionTests
{
    [Fact]
    public void Regression_validate_member_analyzer_exposes_diagnostics()
    {
        var analyzer = new ValidateMemberAnalyzer();

        Assert.NotEmpty(analyzer.SupportedDiagnostics);
        Assert.All(
            analyzer.SupportedDiagnostics,
            d => Assert.False(string.IsNullOrEmpty(d.Id)));
    }
}
