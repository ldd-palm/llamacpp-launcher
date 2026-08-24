using System.Globalization;

namespace LlamaCppLauncher.Server;

public static class ParamFormatter
{
    public static string FormatTotalParams(long paramCount)
    {
        double billions = paramCount / 1_000_000_000.0;
        return billions.ToString("0.00", CultureInfo.InvariantCulture) + " B";
    }
}
