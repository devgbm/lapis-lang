using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace LapisLang.Core;

public class FuncTypenameSyntax : TypeNameSyntax
{
    public FuncTypenameSyntax(
        SourceSpan sourceSpan,
        ImmutableArray<TypeNameSyntax> parameterTypes,
        TypeNameSyntax returnTypeName
    ): base(sourceSpan, null!)
    {
        SourceSpan = sourceSpan;
        ParameterTypes = parameterTypes;
        ReturnTypeName = returnTypeName;
    }

    public  override SourceSpan SourceSpan { get; }
    public ImmutableArray<TypeNameSyntax> ParameterTypes { get; }
    public TypeNameSyntax ReturnTypeName { get; }
}