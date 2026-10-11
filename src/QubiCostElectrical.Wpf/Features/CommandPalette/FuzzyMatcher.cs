using System;

namespace QubiCostElectrical.Wpf.Features.CommandPalette;

public static class FuzzyMatcher
{
    public static int Score(string? target, string? query, string? secondary = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return 100;

        if (string.IsNullOrWhiteSpace(target))
            return string.IsNullOrWhiteSpace(secondary) ? 0 : Score(secondary, query) / 2;

        var t = target.Trim();
        var q = query.Trim();

        // 1. Coincidencia exacta
        if (string.Equals(t, q, StringComparison.OrdinalIgnoreCase))
            return 1000;

        // 2. Prefijo exacto
        if (t.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            return 800 + Math.Min(100, (q.Length * 100) / Math.Max(1, t.Length));

        // 3. Subcadena contenida
        var subIndex = t.IndexOf(q, StringComparison.OrdinalIgnoreCase);
        if (subIndex >= 0)
        {
            // Bono si empieza al inicio de una palabra (después de espacio, guión o punto)
            var isWordBoundary = subIndex == 0 || char.IsWhiteSpace(t[subIndex - 1]) || t[subIndex - 1] == '-' || t[subIndex - 1] == '.';
            return (isWordBoundary ? 650 : 500) + Math.Min(100, (q.Length * 100) / Math.Max(1, t.Length));
        }

        // 4. Todas las palabras de la consulta están en el objetivo
        var queryWords = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (queryWords.Length > 1)
        {
            var allWordsMatch = true;
            foreach (var qw in queryWords)
            {
                if (!t.Contains(qw, StringComparison.OrdinalIgnoreCase) &&
                    (secondary == null || !secondary.Contains(qw, StringComparison.OrdinalIgnoreCase)))
                {
                    allWordsMatch = false;
                    break;
                }
            }

            if (allWordsMatch)
                return 450 + Math.Min(100, queryWords.Length * 30);
        }

        // 5. Subsecuencia fuzzy de caracteres
        var subseqScore = CalculateSubsequenceScore(t, q);
        if (subseqScore > 0)
            return subseqScore;

        // 6. Si no hubo match en el primario, evaluar el secundario
        if (!string.IsNullOrWhiteSpace(secondary))
        {
            var secScore = Score(secondary, query);
            if (secScore > 0)
                return secScore / 2;
        }

        return 0;
    }

    private static int CalculateSubsequenceScore(string target, string query)
    {
        var tUpper = target.ToUpperInvariant();
        var qUpper = query.ToUpperInvariant();

        int tIdx = 0;
        int qIdx = 0;
        int consecutive = 0;
        int score = 150;

        while (tIdx < tUpper.Length && qIdx < qUpper.Length)
        {
            if (tUpper[tIdx] == qUpper[qIdx])
            {
                qIdx++;
                consecutive++;
                score += 10 + (consecutive * 5);
            }
            else
            {
                consecutive = 0;
            }
            tIdx++;
        }

        return qIdx == qUpper.Length ? score : 0;
    }
}
