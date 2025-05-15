using System;
using System.Diagnostics;
using LapisLang.Core;

[DebuggerDisplay("<SourceSpan {AsText}>")]
public class SourceSpan
{
    public SourceSpan(SourceText source, int start, int length)
    {
        Source = source;
        Start = start;
        Length = length;
    }

    public ReadOnlySpan<char> AsText { get => Source.Text.AsSpan(Start, Length); }

    public SourceText Source { get; }
    public int Start { get; }
    public int Length { get; }

    public static SourceSpan Between(SourceSpan left, SourceSpan right)
    {
        if(left.Source != right.Source) throw new Exception("diferent sources!");

        var source = left.Source;
        var start = left.Start;
        var end = right.Start - left.Start + right.Length;
        return new SourceSpan(source, start, end);
    }
    public static implicit operator SourceSpan(Token t) => t.SourceSpan;
    public static implicit operator SourceSpan(Syntax t) => t.SourceSpan;

}
