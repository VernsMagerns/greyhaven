namespace GreyHaven.Compat.Kernel;

/// <summary>
/// fnmatch-style glob matching as used by Linux module aliases: <c>*</c>, <c>?</c> and
/// <c>[...]</c> character classes (with ranges and <c>!</c>/<c>^</c> negation). Case-sensitive,
/// like the kernel. Allocation-free so thousands of patterns can be checked quickly.
/// </summary>
public static class Glob
{
    public static bool IsMatch(ReadOnlySpan<char> pattern, ReadOnlySpan<char> text)
    {
        int p = 0, t = 0, starP = -1, starT = -1;
        while (t < text.Length)
        {
            if (p < pattern.Length)
            {
                var c = pattern[p];
                if (c == '*')
                {
                    starP = p++;
                    starT = t;
                    continue;
                }
                if (c == '?')
                {
                    p++; t++;
                    continue;
                }
                if (c == '[')
                {
                    if (MatchClass(pattern, ref p, text[t]))
                    {
                        t++;
                        continue;
                    }
                }
                else if (c == text[t])
                {
                    p++; t++;
                    continue;
                }
            }
            if (starP < 0) return false;
            p = starP + 1;
            t = ++starT;
        }
        while (p < pattern.Length && pattern[p] == '*') p++;
        return p == pattern.Length;
    }

    /// <summary>Matches a <c>[...]</c> class at <paramref name="p"/>; on success advances past the closing bracket.</summary>
    private static bool MatchClass(ReadOnlySpan<char> pattern, ref int p, char ch)
    {
        var i = p + 1;
        var negate = i < pattern.Length && (pattern[i] == '!' || pattern[i] == '^');
        if (negate) i++;
        var matched = false;
        var first = true;
        while (i < pattern.Length && (pattern[i] != ']' || first))
        {
            first = false;
            var lo = pattern[i];
            if (i + 2 < pattern.Length && pattern[i + 1] == '-' && pattern[i + 2] != ']')
            {
                if (ch >= lo && ch <= pattern[i + 2]) matched = true;
                i += 3;
            }
            else
            {
                if (ch == lo) matched = true;
                i++;
            }
        }
        if (i >= pattern.Length) return false; // unterminated class: treat as no match
        if (matched == negate) return false;
        p = i + 1;
        return true;
    }
}
