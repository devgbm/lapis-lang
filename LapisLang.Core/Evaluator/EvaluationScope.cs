


namespace LapisLang.Core;


public class EvaluationScope
{
    public BoundScope BoundScope { get; }
    public EvaluationScope(BoundScope? scope = null)
    {
        BoundScope = scope ?? LangDefaults.CreateDefaultScope();
    }
    public static EvaluationScope CreateScope(BoundScope? scope = null)
    {
        return new EvaluationScope(scope);
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
                BinaryOperator.Div => (ExprSymbol left, ExprSymbol right) => new IntegerSymbol(((IntegerSymbol)left).Value / ((IntegerSymbol)right).Value),
                BinaryOperator.Mod => (ExprSymbol left, ExprSymbol right) => new IntegerSymbol(((IntegerSymbol)left).Value % ((IntegerSymbol)right).Value),

                BinaryOperator.Equality => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((IntegerSymbol)left).Value == ((IntegerSymbol)right).Value),
                BinaryOperator.Inequality => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((IntegerSymbol)left).Value != ((IntegerSymbol)right).Value),
                BinaryOperator.GreatherThan => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((IntegerSymbol)left).Value > ((IntegerSymbol)right).Value),
                BinaryOperator.LessThan => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((IntegerSymbol)left).Value < ((IntegerSymbol)right).Value),
                BinaryOperator.LessEqualThan => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((IntegerSymbol)left).Value >= ((IntegerSymbol)right).Value),
                BinaryOperator.GreatherEqualThan => (ExprSymbol left, ExprSymbol right) => new BooleanSymbol(((IntegerSymbol)left).Value <= ((IntegerSymbol)right).Value),
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

        throw new Exception();
    }

    internal bool Define(string name, Symbol symbol)
    {
        if (symbol is ExprSymbol es && es.IsCompileTime)
        {
            return BoundScope.Define(name, es);
        }
        throw new Exception("Runtime not suported");
    }


    public EvaluationScope Derive()
    {
        return new EvaluationScope(BoundScope.Derive());
    }
}
