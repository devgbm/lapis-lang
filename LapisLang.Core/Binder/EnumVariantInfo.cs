using System.Collections.Immutable;

namespace LapisLang.Core;

public class EnumVariantInfo
{
    public EnumVariantInfo(string name, ImmutableArray<FieldSymbol> fields)
    {
        Name = name;
        Fields = fields;
    }

    public string Name { get; }
    public ImmutableArray<FieldSymbol> Fields { get; }
}
