


namespace LapisLang.Core;


public class EvaluationScope
{
    public BoundScope BoundScope { get; }
    private Dictionary<string, ExprSymbol> _variables = new();
    public EvaluationScope? _parent = null;
    public ExprSymbol? ReturnRequest { get; set; }
    public bool ExpectReturn { get; set; } = false;
    public EvaluationScope(BoundScope? scope = null)
    {
        BoundScope = scope ?? LangDefaults.CreateDefaultScope();
    }
    public static EvaluationScope CreateScope(BoundScope? scope = null)
    {
        return new EvaluationScope(scope);
    }
    internal Func<ExprSymbol, ExprSymbol> GetOperatorFn(TypeSymbol operand, UnaryOperator unaryOperator)
    {
        if (unaryOperator == UnaryOperator.TypeOf) return (expr) => expr.Type;
        if (operand == LangDefaults.Types.Integer)
        {
            return unaryOperator switch
            {
                UnaryOperator.Inverse => (operand) => new IntegerSymbol(-((IntegerSymbol)operand).Value),
                UnaryOperator.Identity => (operand) => new IntegerSymbol(((IntegerSymbol)operand).Value),
                _ => throw new Exception()
            };
        }
        if (operand == LangDefaults.Types.Decimal)
        {
            return unaryOperator switch
            {
                UnaryOperator.Inverse => (operand) => new DecimalSymbol(-((DecimalSymbol)operand).Value),
                UnaryOperator.Identity => (operand) => new DecimalSymbol(((DecimalSymbol)operand).Value),
                _ => throw new Exception()
            };
        }

        if (operand == LangDefaults.Types.Boolean)
        {
            return unaryOperator switch
            {
                UnaryOperator.LogicalNegation => (ExprSymbol) => new BooleanSymbol(!((BooleanSymbol)ExprSymbol).Value),
                _ => throw new Exception()
            };
        }
        throw new Exception();
    }

    internal Func<ExprSymbol, ExprSymbol, ExprSymbol> GetOperatorFn(TypeSymbol type1, TypeSymbol type2, BinaryOperator binaryOperator)
    {
        if (type1 == LangDefaults.Types.Integer && type2 == LangDefaults.Types.Integer)
        {
            return binaryOperator switch
            {
                BinaryOperator.Add => (ExprSymbol left, ExprSymbol right) => new IntegerSymbol(((IntegerSymbol)left).Value + ((IntegerSymbol)right).Value),
                BinaryOperator.Sub => (ExprSymbol left, ExprSymbol right) => new IntegerSymbol(((IntegerSymbol)left).Value - ((IntegerSymbol)right).Value),
                BinaryOperator.Mul => (ExprSymbol left, ExprSymbol right) => new IntegerSymbol(((IntegerSymbol)left).Value * ((IntegerSymbol)right).Value),
                BinaryOperator.Div => (ExprSymbol left, ExprSymbol right) => new IntegerSymbol(((IntegerSymbol)right).Value == 0 ?
                ((IntegerSymbol)left).Value > 0 ? long.MaxValue: - long.MaxValue : ((IntegerSymbol)left).Value / ((IntegerSymbol)right).Value),
                BinaryOperator.Mod => (ExprSymbol left, ExprSymbol right) => new IntegerSymbol(((IntegerSymbol)left).Value % ((IntegerSymbol)right).Value),

                BinaryOperator.Equality => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((IntegerSymbol)left).Value == ((IntegerSymbol)right).Value),
                BinaryOperator.Inequality => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((IntegerSymbol)left).Value != ((IntegerSymbol)right).Value),
                BinaryOperator.GreatherThan => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((IntegerSymbol)left).Value > ((IntegerSymbol)right).Value),
                BinaryOperator.LessThan => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((IntegerSymbol)left).Value < ((IntegerSymbol)right).Value),
                BinaryOperator.LessEqualThan => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((IntegerSymbol)left).Value <= ((IntegerSymbol)right).Value),
                BinaryOperator.GreatherEqualThan => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((IntegerSymbol)left).Value >= ((IntegerSymbol)right).Value),
                _ => throw new Exception()
            };
        }

        if (type1 == LangDefaults.Types.Decimal && type2 == LangDefaults.Types.Decimal)
        {
            return binaryOperator switch
            {
                BinaryOperator.Add => (ExprSymbol left, ExprSymbol right) => new DecimalSymbol(((DecimalSymbol)left).Value + ((DecimalSymbol)right).Value),
                BinaryOperator.Sub => (ExprSymbol left, ExprSymbol right) => new DecimalSymbol(((DecimalSymbol)left).Value - ((DecimalSymbol)right).Value),
                BinaryOperator.Mul => (ExprSymbol left, ExprSymbol right) => new DecimalSymbol(((DecimalSymbol)left).Value * ((DecimalSymbol)right).Value),
                BinaryOperator.Div => (ExprSymbol left, ExprSymbol right) => new DecimalSymbol(((DecimalSymbol)left).Value / ((DecimalSymbol)right).Value),
                BinaryOperator.Mod => (ExprSymbol left, ExprSymbol right) => new DecimalSymbol(((DecimalSymbol)left).Value % ((DecimalSymbol)right).Value),

                BinaryOperator.Equality => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((DecimalSymbol)left).Value == ((DecimalSymbol)right).Value),
                BinaryOperator.Inequality => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((DecimalSymbol)left).Value != ((DecimalSymbol)right).Value),
                BinaryOperator.GreatherThan => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((DecimalSymbol)left).Value > ((DecimalSymbol)right).Value),
                BinaryOperator.LessThan => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((DecimalSymbol)left).Value < ((DecimalSymbol)right).Value),
                BinaryOperator.LessEqualThan => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((DecimalSymbol)left).Value >= ((DecimalSymbol)right).Value),
                BinaryOperator.GreatherEqualThan => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((DecimalSymbol)left).Value <= ((DecimalSymbol)right).Value),
                _ => throw new Exception()
            };
        }

        if (type1 == LangDefaults.Types.Boolean && type2 == LangDefaults.Types.Boolean)
        {
            return binaryOperator switch
            {
                BinaryOperator.LogicalAnd => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((BooleanSymbol)left).Value && ((BooleanSymbol)right).Value),
                BinaryOperator.LogicalOr => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((BooleanSymbol)left).Value || ((BooleanSymbol)right).Value),
                BinaryOperator.Equality => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((BooleanSymbol)left).Value == ((BooleanSymbol)right).Value),
                BinaryOperator.Inequality => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((BooleanSymbol)left).Value != ((BooleanSymbol)right).Value),
                _ => throw new Exception()
            };
        }

        if (type1 == LangDefaults.Types.Type && type2 == LangDefaults.Types.Type)
        {
            return binaryOperator switch
            {
                BinaryOperator.TypeWith => (ExprSymbol left, ExprSymbol right) => ((StructTypeSymbol)left).With((StructTypeSymbol)right),
                BinaryOperator.TypeWithout => (ExprSymbol left, ExprSymbol right) => ((StructTypeSymbol)left).Without((StructTypeSymbol)right),
                BinaryOperator.TypeContains => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((StructTypeSymbol)left).Contains((StructTypeSymbol)right)),
                _ => throw new Exception()
            };
        }

        // x == OptInt.Some  — variant tag check
        if (type1 is EnumTypeSymbol enumTagType && type2 is FuncTypeSymbol ftsTag && ftsTag.ReturnType == enumTagType)
        {
            return binaryOperator switch
            {
                BinaryOperator.Equality => (l, r) => new BooleanSymbol(((EnumInstanceSymbol)l).VariantName == ((EnumVariantConstructorSymbol)r).Variant.Name),
                BinaryOperator.Inequality => (l, r) => new BooleanSymbol(((EnumInstanceSymbol)l).VariantName != ((EnumVariantConstructorSymbol)r).Variant.Name),
                _ => throw new Exception()
            };
        }
        if (type1 is EnumTypeSymbol && type1 == type2)
        {
            return binaryOperator switch
            {
                BinaryOperator.Equality => (l, r) => new BooleanSymbol(EnumInstanceEqual((EnumInstanceSymbol)l, (EnumInstanceSymbol)r)),
                BinaryOperator.Inequality => (l, r) => new BooleanSymbol(!EnumInstanceEqual((EnumInstanceSymbol)l, (EnumInstanceSymbol)r)),
                _ => throw new Exception()
            };
        }

        throw new Exception();
    }

    private static bool EnumInstanceEqual(EnumInstanceSymbol a, EnumInstanceSymbol b)
    {
        if (a.VariantName != b.VariantName) return false;
        foreach (var (key, val) in a.Fields)
        {
            if (!b.Fields.TryGetValue(key, out var bVal)) return false;
            if (!ExprValueEqual(val, bVal)) return false;
        }
        return true;
    }

    private static bool ExprValueEqual(ExprSymbol a, ExprSymbol b) => (a, b) switch
    {
        (IntegerSymbol ia, IntegerSymbol ib) => ia.Value == ib.Value,
        (BooleanSymbol ba, BooleanSymbol bb) => ba.Value == bb.Value,
        (StringSymbol sa, StringSymbol sb) => sa.Value == sb.Value,
        (DecimalSymbol da, DecimalSymbol db) => da.Value == db.Value,
        (EnumInstanceSymbol ea, EnumInstanceSymbol eb) => EnumInstanceEqual(ea, eb),
        _ => ReferenceEquals(a, b)
    };

    internal bool Define(string name, ExprSymbol symbol)
    {
        return BoundScope.Define(name, symbol);
    }

    public void SetVariable(string name, ExprSymbol value)
    {
        Define(name, value);
        _variables[name] = value;
    }

    public ExprSymbol GetVariable(string name)
    {
        if (_variables.ContainsKey(name)) return _variables[name];
        if (_parent is not null) return _parent.GetVariable(name);
        if (BoundScope.TryGetExprSymbol(name, out var symbol)) return symbol;
        return ExprSymbol.Unkown;        
    }


    public EvaluationScope Derive()
    {
        var evaluationScope = new EvaluationScope(BoundScope.Derive());
        evaluationScope._parent = this;
        return evaluationScope;
    }
}
