namespace LapisLang.Core;


public enum RuneKind
{
    Namespace,   // Namespace
    Type,        // Tipo (classe, struct, enum, etc.)
    Method,      // Método
    Field,       // Campo
    Property,    // Propriedade
    Parameter,   // Parâmetro
    Variable,    // Variável local
    Constant,    // Constante
}
public abstract class Rune
{
    protected HashSet<Rune> _children = new();
    protected Rune? _parent;

    public string Name { get; }
    public RuneKind Kind { get; }

    public string FullName
    {
        get
        {
            if (_parent is null) return Name;
            else if (_parent.IsRoot) return $"{_parent.Name}{Name}";
            else return $"{_parent.Name}.{Name}";
        }
    }
    public bool IsRoot => _parent is null;
    public Rune Root => _parent?.Root ?? this;

    protected Rune(string name, RuneKind kind)
    {
        Name = name;
        Kind = kind;
    }

    public virtual bool Add(Rune rune)
    {
        if (_children.Contains(rune)) return false;
        _children.Add(rune);
        if (rune._parent is null) rune._parent = this;
        return true;
    }

    public Rune? Query(string name)
    {
        if (name == "@")
        {
            if (Root.Name == "@") return Root;
            else return null;
        }
        if (name.StartsWith("@"))
        {
            return Root.Query(name[1..]);
        }
        else if (name.Contains('.'))
        {
            var fragments = name.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
            var foundChild = Query(fragments[0]);
            return foundChild?.Query(fragments[1]);
        }
        else
        {
            return _children.FirstOrDefault(e => e.Name == name);
        }
    }

    public IEnumerable<Rune> GetChildren() => _children;

    public IEnumerable<T> GetChildrenOfType<T>() where T : Rune
    {
        return _children.OfType<T>();
    }

    public override bool Equals(object? obj)
    {
        if (obj is Rune other)
        {
            return Name == other.Name && Kind == other.Kind;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Kind);
    }
}

public class TypeRune : Rune
{
    public TypeRune(string name) : base(name, RuneKind.Type)
    {
    }
}

public class NamespaceRune : Rune
{
    public NamespaceRune(string name) : base(name, RuneKind.Namespace)
    {
    }
}

public class VariableRune : Rune
{
    public VariableRune(string name, TypeRune type, BoundExpression expression) : base(name, RuneKind.Variable)
    {
        Type = type;
        Expression = expression;
    }

    public TypeRune Type { get; }
    public BoundExpression Expression { get; }
}