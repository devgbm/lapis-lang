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
        CoreInstantiate n => VisitInstantiate(n),
        CoreReturn n => VisitReturn(n),
        CoreIf n => VisitIf(n),
        CoreBinary n => VisitBinary(n),
        CoreUnary n => VisitUnary(n),
        CoreArray n => VisitArray(n),
        CoreIndex n => VisitIndex(n),
        CoreField n => VisitField(n),
        CoreEnumDef n => VisitEnumDef(n),
        CoreMatch n => VisitMatch(n),
        CoreTypeDef n => VisitTypeDef(n),
        CoreConstruct n => VisitConstruct(n),
        CoreGoto n => VisitGoto(n),
        CoreGotoIf n => VisitGotoIf(n),
        CoreLabeled n => VisitLabeled(n),
        CoreAssign n => VisitAssign(n),
        _ => throw InternalCompilerException.Unreachable(node, node.Span),
    };

    protected abstract TResult VisitLiteral(CoreLiteral node);

    protected abstract TResult VisitVariable(CoreVariable node);

    protected abstract TResult VisitLet(CoreLet node);

    protected abstract TResult VisitLambda(CoreLambda node);

    protected abstract TResult VisitCall(CoreCall node);

    protected abstract TResult VisitInstantiate(CoreInstantiate node);

    protected abstract TResult VisitReturn(CoreReturn node);

    protected abstract TResult VisitIf(CoreIf node);

    protected abstract TResult VisitBinary(CoreBinary node);

    protected abstract TResult VisitUnary(CoreUnary node);

    protected abstract TResult VisitArray(CoreArray node);

    protected abstract TResult VisitIndex(CoreIndex node);

    protected abstract TResult VisitField(CoreField node);

    protected abstract TResult VisitEnumDef(CoreEnumDef node);

    protected abstract TResult VisitMatch(CoreMatch node);

    protected abstract TResult VisitTypeDef(CoreTypeDef node);

    protected abstract TResult VisitConstruct(CoreConstruct node);

    protected abstract TResult VisitGoto(CoreGoto node);

    protected abstract TResult VisitGotoIf(CoreGotoIf node);

    protected abstract TResult VisitLabeled(CoreLabeled node);

    protected abstract TResult VisitAssign(CoreAssign node);
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

            case CoreInstantiate n:
                Visit(n.Target);

                foreach (var argument in n.Arguments)
                {
                    if (argument is CoreValueArgument value)
                    {
                        Visit(value.Value);
                    }
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

            case CoreMatch n:
                Visit(n.Scrutinee);

                foreach (var arm in n.Arms)
                {
                    Visit(arm.Body);
                }

                break;

            case CoreTypeDef:
                break;

            case CoreConstruct n:
                foreach (var argument in n.TypeArguments)
                {
                    if (argument is CoreValueArgument value)
                    {
                        Visit(value.Value);
                    }
                }

                foreach (var field in n.Fields)
                {
                    Visit(field.Value);
                }

                break;

            case CoreGoto:
                break;

            case CoreAssign n:
                Visit(n.Value);
                break;

            case CoreGotoIf n:
                Visit(n.Condition);
                break;

            case CoreLabeled n:
                Visit(n.Entry);

                foreach (var join in n.Joins)
                {
                    Visit(join.Body);
                }

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
