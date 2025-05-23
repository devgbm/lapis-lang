namespace LapisLang.Core;


public abstract class RuneNew;


public class ScopeRuneNew : RuneNew
{
    private Dictionary<string, RuneNew> _runes = new();
}

