using System.Globalization;
using System.Text;

namespace Causalia.Internal;

internal static class CanonicalString
{
    public static string Create(IReadOnlyList<string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var builder = new StringBuilder();

        foreach (var value in values)
        {
            AppendTo(builder, value);
        }

        return builder.ToString();
    }

    public static void AppendTo(StringBuilder builder, string? value)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (value is null)
        {
            builder.Append("-1:");
            return;
        }

        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
    }
}
