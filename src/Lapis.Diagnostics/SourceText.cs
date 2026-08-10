using System.Collections.Immutable;

namespace Lapis.Diagnostics;

/// <summary>Posição legível por humanos. <see cref="Line"/> e <see cref="Column"/> são 1-based.</summary>
public readonly record struct SourcePosition(int Offset, int Line, int Column)
{
    public override string ToString() => $"{Line},{Column}";
}

/// <summary>
/// O texto de um arquivo <c>.ls</c> mais um índice de início de linhas, usado
/// para converter offsets em linha/coluna ao renderizar diagnósticos.
/// </summary>
public sealed class SourceText
{
    private readonly ImmutableArray<int> _lineStarts;

    private SourceText(string fileName, string text)
    {
        FileName = fileName;
        Text = text;
        _lineStarts = BuildLineStarts(text);
    }

    public string FileName { get; }

    public string Text { get; }

    public int Length => Text.Length;

    public int LineCount => _lineStarts.Length;

    public char this[int index] => Text[index];

    public static SourceText From(string text, string fileName = "<memória>") => new(fileName, text);

    public static SourceText FromFile(string path) => new(path, File.ReadAllText(path));

    public string GetText(SourceSpan span) => Text.Substring(span.Start, span.Length);

    /// <summary>Converte um offset em linha/coluna 1-based.</summary>
    public SourcePosition GetPosition(int offset)
    {
        if (offset < 0 || offset > Text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var line = FindLineIndex(offset);
        return new SourcePosition(offset, line + 1, offset - _lineStarts[line] + 1);
    }

    /// <summary>Texto de uma linha 1-based, sem o terminador.</summary>
    public string GetLineText(int line)
    {
        if (line < 1 || line > _lineStarts.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(line));
        }

        var start = _lineStarts[line - 1];
        var end = line < _lineStarts.Length ? _lineStarts[line] : Text.Length;

        while (end > start && (Text[end - 1] == '\n' || Text[end - 1] == '\r'))
        {
            end--;
        }

        return Text[start..end];
    }

    private int FindLineIndex(int offset)
    {
        var index = _lineStarts.BinarySearch(offset);
        return index >= 0 ? index : ~index - 1;
    }

    private static ImmutableArray<int> BuildLineStarts(string text)
    {
        var builder = ImmutableArray.CreateBuilder<int>();
        builder.Add(0);

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
            {
                i++;
            }
            else if (c != '\n' && c != '\r')
            {
                continue;
            }

            builder.Add(i + 1);
        }

        return builder.ToImmutable();
    }
}
