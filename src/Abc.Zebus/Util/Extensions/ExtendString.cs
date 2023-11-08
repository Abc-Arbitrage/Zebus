using System.Diagnostics.CodeAnalysis;

namespace Abc.Zebus.Util.Extensions;

internal static class ExtendString
{
    [return: NotNullIfNotNull("input")]
    public static string? Qualifier(this string? input)
    {
        if (input == null)
            return null;

        var lastDotIndex = input.LastIndexOf('.');
        if (lastDotIndex == -1)
            return input;

        return input.Substring(0, lastDotIndex);
    }
}
