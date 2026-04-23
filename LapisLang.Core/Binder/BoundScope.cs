

namespace LapisLang.Core;

public class BoundScope : ExprSymbol
{
    private Dictionary<string, Symbol> _scope = new();
    private BoundScope? _parent;
    public TypeSymbol? _expectedReturn;
    public BoundScope? Parent { get => _parent; }


    public override TypeSymbol Type { get => LangDefaults.Types.Namespace;}

    public string? _selfName;
    public TypeSymbol? _selfType;
    public string Name { get; set; }

    public BoundScope(string name, BoundScope? parent = null)
    {
        this._parent = parent;
        Name = name;
    }

    public (UnaryOperator, TypeSymbol) ResolveUnaryExpression(Token operatorToken, ExprSymbol operand)
    {
        if (operatorToken.Kind == TokenKind.TypeofKeyword) return (UnaryOperator.TypeOf, LangDefaults.Types.Type);
        if (operand.Type == LangDefaults.Types.Integer)
        {
            return operatorToken.Kind switch
            {
                TokenKind.Minus => (UnaryOperator.Inverse, LangDefaults.Types.Integer),
                TokenKind.Plus => (UnaryOperator.Identity, LangDefaults.Types.Integer),
                TokenKind.TypeofKeyword => (UnaryOperator.TypeOf, LangDefaults.Types.Type),
                _ => (UnaryOperator.Unkown, LangDefaults.Types.Unkown)
            };
        }
        if (operand.Type == LangDefaults.Types.Boolean)
        {
            return operatorToken.Kind switch
            {
                TokenKind.NotKeyword => (UnaryOperator.LogicalNegation, LangDefaults.Types.Boolean),
                TokenKind.TypeofKeyword => (UnaryOperator.TypeOf, LangDefaults.Types.Type),
                _ => (UnaryOperator.Unkown, LangDefaults.Types.Unkown)
            };
        }
        if (operand.Type == LangDefaults.Types.Decimal)
        {
            return operatorToken.Kind switch
            {
                TokenKind.Minus => (UnaryOperator.Inverse, LangDefaults.Types.Decimal),
                TokenKind.Plus => (UnaryOperator.Identity, LangDefaults.Types.Decimal),
                TokenKind.TypeofKeyword => (UnaryOperator.TypeOf, LangDefaults.Types.Type),
                _ => (UnaryOperator.Unkown, LangDefaults.Types.Unkown)
            };
        }        

        if (operand.Type == LangDefaults.Types.Type)
        {
            return operatorToken.Kind switch
            {
                TokenKind.TypeofKeyword => (UnaryOperator.TypeOf, LangDefaults.Types.Type),
                _ => (UnaryOperator.Unkown, LangDefaults.Types.Unkown)
            };
        }

        return (UnaryOperator.Unkown, LangDefaults.Types.Unkown);
    }
    public (BinaryOperator, TypeSymbol) ResolveBinaryExpression(Token operatorToken, ExprSymbol left, ExprSymbol right)
    {
        if (left.Type == LangDefaults.Types.Integer && right.Type == LangDefaults.Types.Integer)
        {
            return operatorToken.Kind switch
            {
                TokenKind.Plus => (BinaryOperator.Add, LangDefaults.Types.Integer),
                TokenKind.Minus => (BinaryOperator.Sub, LangDefaults.Types.Integer),
                TokenKind.Star => (BinaryOperator.Mul, LangDefaults.Types.Integer),
                TokenKind.Slash => (BinaryOperator.Div, LangDefaults.Types.Integer),
                TokenKind.Percent => (BinaryOperator.Mod, LangDefaults.Types.Integer),
                TokenKind.DoubleEquals => (BinaryOperator.Equality, LangDefaults.Types.Boolean),
                TokenKind.BangEquals => (BinaryOperator.Inequality, LangDefaults.Types.Boolean),
                TokenKind.LeftArrow => (BinaryOperator.LessThan, LangDefaults.Types.Boolean),
                TokenKind.RightArrow => (BinaryOperator.GreatherThan, LangDefaults.Types.Boolean),
                TokenKind.LeftArrowEquals => (BinaryOperator.LessEqualThan, LangDefaults.Types.Boolean),
                TokenKind.RightArrowEquals => (BinaryOperator.GreatherEqualThan, LangDefaults.Types.Boolean),

                _ => (BinaryOperator.Unkown, LangDefaults.Types.Unkown)
            };
        }
        if (left.Type == LangDefaults.Types.Decimal && right.Type == LangDefaults.Types.Decimal)
        {
            return operatorToken.Kind switch
            {
                TokenKind.Plus => (BinaryOperator.Add, LangDefaults.Types.Decimal),
                TokenKind.Minus => (BinaryOperator.Sub, LangDefaults.Types.Decimal),
                TokenKind.Star => (BinaryOperator.Mul, LangDefaults.Types.Decimal),
                TokenKind.Slash => (BinaryOperator.Div, LangDefaults.Types.Decimal),
                TokenKind.Percent => (BinaryOperator.Mod, LangDefaults.Types.Decimal),
                TokenKind.DoubleEquals => (BinaryOperator.Equality, LangDefaults.Types.Boolean),
                TokenKind.BangEquals => (BinaryOperator.Inequality, LangDefaults.Types.Boolean),
                TokenKind.LeftArrow => (BinaryOperator.LessThan, LangDefaults.Types.Boolean),
                TokenKind.RightArrow => (BinaryOperator.GreatherThan, LangDefaults.Types.Boolean),
                TokenKind.LeftArrowEquals => (BinaryOperator.LessEqualThan, LangDefaults.Types.Boolean),
                TokenKind.RightArrowEquals => (BinaryOperator.GreatherEqualThan, LangDefaults.Types.Boolean),

                _ => (BinaryOperator.Unkown, LangDefaults.Types.Unkown)
            };
        }
        if (left.Type == LangDefaults.Types.Boolean && right.Type == LangDefaults.Types.Boolean)
        {
            return operatorToken.Kind switch
            {
                TokenKind.DoubleEquals => (BinaryOperator.Equality, LangDefaults.Types.Integer),
                TokenKind.BangEquals => (BinaryOperator.Inequality, LangDefaults.Types.Integer),
                TokenKind.AndKeyword => (BinaryOperator.LogicalAnd, LangDefaults.Types.Integer),
                TokenKind.OrKeyword => (BinaryOperator.LogicalOr, LangDefaults.Types.Integer),

                _ => (BinaryOperator.Unkown, LangDefaults.Types.Unkown)
            };
        }
        if (left.Type == LangDefaults.Types.Type && right.Type == LangDefaults.Types.Type)
        {
            return operatorToken.Kind switch
            {
                TokenKind.And => (BinaryOperator.TypeWith, LangDefaults.Types.Type),
                TokenKind.BangAnd => (BinaryOperator.TypeWithout, LangDefaults.Types.Type),
                TokenKind.MatchKeyword => (BinaryOperator.TypeContains, LangDefaults.Types.Boolean),
                _ => (BinaryOperator.Unkown, LangDefaults.Types.Unkown)
            };
        }
        return (BinaryOperator.Unkown, LangDefaults.Types.Unkown);
    }

    public bool TryGetSymbol(string name, out Symbol symbol)
    {
        if (_scope.TryGetValue(name, out symbol)) return true;
        if (_parent is not null && _parent.TryGetSymbol(name, out symbol)) return true;
        symbol = Symbol.Unkown;
        return false;
    }
    public bool TryGetExprSymbol(string name, out ExprSymbol exprSymbol)
    {
        var foundSymbol = TryGetSymbol(name, out var symbol);
        if (foundSymbol && symbol is ExprSymbol expr)
        {
            exprSymbol = expr;
            return true;
        }

        if (_parent is not null && _parent.TryGetExprSymbol(name, out exprSymbol)) return true;

        exprSymbol = ExprSymbol.Unkown;
        return false;
    }

    public bool TryGetTypeSymbol(string name, out TypeSymbol exprSymbol)
    {
        var foundSymbol = TryGetSymbol(name, out var symbol);
        if (foundSymbol && symbol is TypeSymbol expr)
        {
            exprSymbol = expr;
            return true;
        }

        exprSymbol = LangDefaults.Types.Unkown;
        return false;
    }

    internal bool Define(string name, Symbol symbol)
    {
        if (_scope.ContainsKey(name)) return false;
        _scope.Add(name, symbol);

        if(symbol is BoundScope bs) bs._parent = this;
        return true;
    }

    internal BoundScope Derive()
    {
        return new BoundScope(Name + "_local" ,this)
        {
            _expectedReturn = this._expectedReturn
        };
    }
}
