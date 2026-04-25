namespace LapisLang.Core;

public class NativeFuncSymbol : ExprSymbol
{
    public NativeFuncSymbol(
        FuncTypeSymbol type,
        Delegate @delegate,
        bool isInstanceMethod = false,
        BindFlag flags = BindFlag.None
    )
    {
        FuncType = type;
        Delegate = @delegate;
        IsInstanceMethod = isInstanceMethod;
        Flags = flags;
    }

    public override TypeSymbol Type { get => FuncType; }
    public FuncTypeSymbol FuncType { get; }
    public Delegate Delegate { get; }
    public bool IsInstanceMethod { get; }
}
