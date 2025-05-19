
namespace LapisLang.Core;

public static class LangDefaults
{
    public static class Types
    {
        public static TypeRune Integer = new("integer");
        public static TypeRune String = new("string");
        public static TypeRune Boolean = new("boolean");
        public static TypeRune Decimal = new("decimal");
        public static TypeRune Type = new("type");
        public static TypeRune Namespace = new("namespace");
        public static TypeRune Unkown = new("unkown");
        public static TypeRune Function = new("function");
    }

    public static NamespaceRune RootNamespace()
    {
        var root = new NamespaceRune("@");

        root.Add(Types.Integer);
        root.Add(Types.String);
        root.Add(Types.Boolean);
        root.Add(Types.Decimal);
        root.Add(Types.Type);
        root.Add(Types.Unkown);

        return root;
    }
}