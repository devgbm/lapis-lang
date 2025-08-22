

using System.Collections.Immutable;

namespace LapisLang.Core;

public class StructTypeSymbol : TypeSymbol
{
    public StructTypeSymbol(ImmutableArray<FieldSymbol> fields, string? debugName) : base(debugName)
    {
        Fields = fields;
        IsCompileTime = fields.All(e => e.IsConstant);
        foreach (var field in fields) DefineMember(field.Name, field.Type);
    }

    public ImmutableArray<FieldSymbol> Fields { get; }

    public override bool IsCompileTime { get; }

    public override bool IsEquivalent(ExprSymbol symbol)
    {
        return symbol is StructTypeSymbol sts && Fields.Length == sts.Fields.Length && Fields.Zip(sts.Fields).All(e => e.First.Name == e.Second.Name && e.First.Type.IsEquivalent(e.Second.Type));
    }

    internal StructTypeSymbol With(StructTypeSymbol right)
    {
        var fields = ImmutableArray.CreateBuilder<FieldSymbol>();
        fields.AddRange(Fields);
        var rightFields = right.Fields.Where(f => !Fields.Any(e => e.Name == f.Name && e.Type == f.Type));
        fields.AddRange(rightFields);
        return new StructTypeSymbol(fields.ToImmutableArray(), "anonimous-type");
    }

    internal ExprSymbol Without(StructTypeSymbol right)
    {
        var fields = ImmutableArray.CreateBuilder<FieldSymbol>();
        fields.AddRange(Fields.Where(f => !right.Fields.Any(e => e.Name == f.Name && e.Type == f.Type)));
        return new StructTypeSymbol(fields.ToImmutableArray(), "anonimous-type");
    }
    internal bool Contains(StructTypeSymbol right)
    {
        return right.Fields.All(e => Fields.Any(f => f.Name == e.Name && f.Type == e.Type));
    }
}