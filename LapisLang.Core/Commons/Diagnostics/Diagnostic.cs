
namespace LapisLang.Core;

public class Diagnostic
{
    public Diagnostic(string message) : this(message, null)
    {
    }

    public Diagnostic(string message, SourceSpan? span)
    {
        Message = message;
        Span = span;
    }
    public string Message { get; }
    public SourceSpan? Span { get; }

    public string GenerateMessage()
    {
        if(Span is not null)
        {
            var lineNumber = Span.Source.GetLineNumberFromOffset(Span.Start);
            var line = Span.Source.GetLine(lineNumber);
            var sourceIndex = Span.Start - Span.Source.Text.IndexOf(line);
            var padLenght = lineNumber.ToString().Length + sourceIndex + 5;
            var path = Span.Source.Name is not null ? $"{Span.Source.Name}\n" : string.Empty;
            return $"{path}{lineNumber} |  {line}\n{"ʌ".PadLeft(padLenght,' ')}\n\n{Message}";
        }
        else
        {
            return Message;
        }

    }
}
