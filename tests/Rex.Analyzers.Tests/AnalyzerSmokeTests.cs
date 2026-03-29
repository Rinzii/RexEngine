using Rex.Analyzers;

namespace Rex.Analyzers.Tests;

public sealed class AnalyzerSmokeTests
{
    [Fact]
    public void ValidateMemberAnalyzer_reports_supported_diagnostics()
    {
        var analyzer = new ValidateMemberAnalyzer();

        Assert.NotEmpty(analyzer.SupportedDiagnostics);
    }
}
