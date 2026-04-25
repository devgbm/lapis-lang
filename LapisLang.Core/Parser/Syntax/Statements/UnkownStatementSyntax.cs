namespace LapisLang.Core;

public class UnkownStatementSyntax : StatementSyntax
{
    public UnkownStatementSyntax(SourceSpan sourceSpan)
    {
        SourceSpan = sourceSpan;
    }

    public override SourceSpan SourceSpan { get; }
}
