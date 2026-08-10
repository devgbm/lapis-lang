using Lapis.Diagnostics;

namespace Lapis.Ast.Core;

/// <summary>Travessia com resultado sobre a Core AST.</summary>
public abstract class CoreVisitor<TResult>
{
    public virtual TResult Visit(CoreExpr node) => node switch
    {
        CoreLiteral n => VisitLiteral(n),
        CoreVariable n => VisitVariable(n),
        CoreLet n => VisitLet(n),
        CoreLambda n => VisitLambda(n),
        CoreCall n => VisitCall(n),
        CoreReturn n => VisitReturn(n),
        CoreIf n => VisitIf(n),
        CoreBinary n => VisitBinary(n),
        CoreUnary n => VisitUnary(n),
        CoreArray n => VisitArray(n),
        CoreIndex n => VisitIndex(n),
        CoreField n => VisitField(n),
        CoreEnumDef n => VisitEnumDef(n),
        _ => throw InternalCompilerException.Unreachable(node, node.Span),
    };

    protected abstract TResult VisitLiteral(CoreLiteral node);

    protected abstract TResult VisitVariable(CoreVariable node);

    protected abstract TResult VisitLet(CoreLet node);

    protected abstract TResult VisitLambda(CoreLambda node);

    protected abstract TResult VisitCall(CoreCall node);

    protected abstract TResult VisitReturn(CoreReturn node);

    protected abstract TResult VisitIf(CoreIf node);

    protected abstract TResult VisitBinary(CoreBinary node);

    protected abstract TResult VisitUnary(CoreUnary node);

    protected abstract TResult VisitArray(CoreArray node);

    protected abstract TResult VisitIndex(CoreIndex node);

    protected abstract TResult VisitField(CoreField node);

    protected abstract TResult VisitEnumDef(CoreEnumDef node);
}

/// <summary>
/// Travessia sem resultado, com percurso padrão dos filhos. Usada para análises
/// simples (contagem de nós, variáveis livres).
/// </summary>
public abstract class CoreWalker
{
    public virtual void Visit(CoreExpr node)
    {
        OnNode(node);

        switch (node)
        {
            case CoreLiteral:
            case CoreVariable:
                break;

            case CoreLet n:
                Visit(n.Value);
                Visit(n.Body);
                break;

            case CoreLambda n:
                Visit(n.Body);
                break;

            case CoreCall n:
                Visit(n.Callee);
                foreach (var argument in n.Arguments)
                {
                    Visit(argument);
                }

                break;

            case CoreReturn n:
                if (n.Value is not null)
                {
                    Visit(n.Value);
                }

                break;

            case CoreIf n:
                Visit(n.Condition);
                Visit(n.Then);
                Visit(n.Else);
                break;

            case CoreBinary n:
                Visit(n.Left);
                Visit(n.Right);
                break;

            case CoreUnary n:
                Visit(n.Operand);
                break;

            case CoreArray n:
                foreach (var element in n.Elements)
                {
                    Visit(element);
                }

                break;

            case CoreIndex n:
                Visit(n.Target);
                Visit(n.Index);
                break;

            case CoreField n:
                Visit(n.Target);
                break;

            case CoreEnumDef:
                break;

            default:
                throw InternalCompilerException.Unreachable(node, node.Span);
        }
    }

    /// <summary>Chamado uma vez para cada nó, antes dos filhos.</summary>
    protected virtual void OnNode(CoreExpr node)
    {
    }
}
