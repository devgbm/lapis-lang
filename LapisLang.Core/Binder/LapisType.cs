
namespace LapisLang.Core;

public class LapisType
{
    public LapisType(string name)
    {
        Name = name;
    }
    public string Name { get; set; }
}



public static class LangDefaults
{
    public static class Types
    {
        public static LapisType Integer = new("integer");
        public static LapisType String = new("string");
        public static LapisType Boolean = new("boolean");
        public static LapisType Decimal = new("decimal");
        public static LapisType Unkown = new("unkown");
    }
}