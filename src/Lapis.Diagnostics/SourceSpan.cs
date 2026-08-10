namespace Lapis.Diagnostics;

/// <summary>
/// Um intervalo de caracteres dentro de um <see cref="SourceText"/>.
///
/// Guarda apenas offsets: linha e coluna são calculadas sob demanda pelo
/// <see cref="SourceText"/>, o que mantém os nós de AST leves (plano 00 §6).
/// </summary>
public readonly record struct SourceSpan(int Start, int Length)
{
    public int End => Start + Length;

    public bool IsEmpty => Length == 0;

    /// <summary>Span de nós sintetizados que não têm origem no texto.</summary>
    public static SourceSpan Synthetic => new(0, 0);

    public static SourceSpan FromBounds(int start, int end)
    {
        if (end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "o fim do span precede o início");
        }

        return new SourceSpan(start, end - start);
    }

    /// <summary>Menor span que contém <paramref name="first"/> e <paramref name="second"/>.</summary>
    public static SourceSpan Union(SourceSpan first, SourceSpan second) =>
        FromBounds(Math.Min(first.Start, second.Start), Math.Max(first.End, second.End));

    public bool Contains(int offset) => offset >= Start && offset < End;

    public override string ToString() => $"[{Start}..{End})";
}
