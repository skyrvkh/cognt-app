using System.Security.Cryptography;
using System.Text;

namespace Cognt.Modules.SpacedRepetitionSystem.Application.CardContexts;

public static partial class CardContextAuthoring
{
    public static string ComputeSelectedTextHash(string selectedText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedText);

        byte[] bytes = Encoding.UTF8.GetBytes(NormalizeInlineText(selectedText));
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    internal static string NewStableId(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        return string.Create(
            prefix.Length + 1 + 32,
            (prefix, value: Guid.CreateVersion7().ToString("N")),
            static (span, state) =>
            {
                state.prefix.AsSpan().CopyTo(span);
                span[state.prefix.Length] = '_';
                state.value.AsSpan().CopyTo(span[(state.prefix.Length + 1)..]);
            });
    }

    internal static string NormalizeInlineText(string text)
    {
        return string.Join(' ', text
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
