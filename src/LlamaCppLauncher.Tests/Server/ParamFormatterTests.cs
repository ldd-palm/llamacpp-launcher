using LlamaCppLauncher.Server;

namespace LlamaCppLauncher.Tests.Server;

public class ParamFormatterTests
{
    [Theory]
    [InlineData(7_620_000_000, "7.62 B")]
    [InlineData(1_000_000_000, "1.00 B")]
    [InlineData(0, "0.00 B")]
    [InlineData(700_000_000, "0.70 B")]
    public void FormatTotalParams_FormatsAsBillionsWithTwoDecimals(long paramCount, string expected)
    {
        Assert.Equal(expected, ParamFormatter.FormatTotalParams(paramCount));
    }
}
