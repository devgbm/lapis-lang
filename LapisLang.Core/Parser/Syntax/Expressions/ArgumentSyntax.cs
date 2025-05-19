using System.Reflection.Metadata;

namespace LapisLang.Core;

public class ArgumentSyntax : Syntax
{
    public ArgumentSyntax(SourceSpan sourceSpan, TypeNameSyntax typeName, Token name)
    {
        SourceSpan = sourceSpan;
        TypeName = typeName;
        Name = name;
    }

    public override SourceSpan SourceSpan { get;  }
    public TypeNameSyntax TypeName { get; }
    public Token Name { get; }
} 