namespace Rex.Analyzers.Tests;

// Minimal checks that analyzers load.
public sealed class AnalyzerSmokeTests
{
    [Fact]
    // ValidateMemberAnalyzer exposes at least one diagnostic id.
    public void ValidateMemberAnalyzer_reports_supported_diagnostics()
    {
        var analyzer = new ValidateMemberAnalyzer();

        Assert.NotEmpty(analyzer.SupportedDiagnostics);
    }
}
