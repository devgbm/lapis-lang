using System.Collections.Immutable;
using Lapis.Ast.Surface;
using Lapis.Diagnostics;

namespace Lapis.Ast.Core;

/// <summary>
/// Única fonte de <see cref="CoreExpr.NodeId"/>. Cada programa tem sua própria
/// instância, o que torna os ids determinísticos para o mesmo arquivo — snapshots
/// estáveis (plano 05 §"Decisões de design").
/// </summary>
public sealed class CoreFactory
{
    private int _nextNodeId;

    public int NodeCount => _nextNodeId;

    public CoreLiteral Literal(SourceSpan span, ConstantValue value) => new(Next(), span, value);

    public CoreLiteral Unit(SourceSpan span) => Literal(span, ConstUnit.Instance);

    public CoreVariable Variable(SourceSpan span, string name) => new(Next(), span, name);

    public CoreLet Let(
        SourceSpan span,
        string name,
        TypeSyntax? annotation,
        CoreExpr value,
        CoreExpr body,
        bool isSynthetic,
        SourceSpan? nameSpan = null) =>
        new(Next(), span, name, annotation, value, body, isSynthetic) { NameSpan = nameSpan ?? span };

    public CoreLambda Lambda(
        SourceSpan span,
        ImmutableArray<CoreTypeParameter> typeParameters,
        ImmutableArray<CoreParameter> parameters,
        TypeSyntax? returnType,
        CoreExpr body,
        SourceSpan bodyEndSpan) =>
        new(Next(), span, typeParameters, parameters, returnType, body) { BodyEndSpan = bodyEndSpan };

    public CoreInstantiate Instantiate(
        SourceSpan span,
        CoreExpr target,
        ImmutableArray<CoreGenericArgument> arguments) =>
        new(Next(), span, target, arguments);

    public CoreCall Call(SourceSpan span, CoreExpr callee, ImmutableArray<CoreExpr> arguments) =>
        new(Next(), span, callee, arguments);

    public CoreReturn Return(SourceSpan span, CoreExpr? value) => new(Next(), span, value);

    public CoreIf If(SourceSpan span, CoreExpr condition, CoreExpr then, CoreExpr @else) =>
        new(Next(), span, condition, then, @else);

    public CoreBinary Binary(SourceSpan span, BinaryOperator op, CoreExpr left, CoreExpr right, SourceSpan operatorSpan) =>
        new(Next(), span, op, left, right) { OperatorSpan = operatorSpan };

    public CoreUnary Unary(SourceSpan span, UnaryOperator op, CoreExpr operand) => new(Next(), span, op, operand);

    public CoreArray Array(SourceSpan span, ImmutableArray<CoreExpr> elements) => new(Next(), span, elements);

    public CoreIndex Index(SourceSpan span, CoreExpr target, CoreExpr index) => new(Next(), span, target, index);

    public CoreField Field(SourceSpan span, CoreExpr target, string name, SourceSpan nameSpan) =>
        new(Next(), span, target, name) { NameSpan = nameSpan };

    public CoreEnumDef EnumDef(
        SourceSpan span,
        ImmutableArray<CoreTypeParameter> typeParameters,
        ImmutableArray<CoreVariantDecl> variants) =>
        new(Next(), span, typeParameters, variants);

    public CoreMatch Match(SourceSpan span, CoreExpr scrutinee, ImmutableArray<CoreArm> arms) =>
        new(Next(), span, scrutinee, arms);

    public CoreTypeDef TypeDef(
        SourceSpan span,
        ImmutableArray<CoreTypeParameter> typeParameters,
        ImmutableArray<CoreFieldDecl> fields) =>
        new(Next(), span, typeParameters, fields);

    public CoreConstruct Construct(
        SourceSpan span,
        string typeName,
        ImmutableArray<CoreGenericArgument> typeArguments,
        ImmutableArray<CoreFieldInit> fields,
        SourceSpan typeNameSpan) =>
        new(Next(), span, typeName, typeArguments, fields) { TypeNameSpan = typeNameSpan };

    public CoreGoto Goto(SourceSpan span, string label, SourceSpan labelSpan, bool isImplicit = false) =>
        new(Next(), span, label) { LabelSpan = labelSpan, IsImplicit = isImplicit };

    public CoreGotoIf GotoIf(SourceSpan span, string label, CoreExpr condition, SourceSpan labelSpan) =>
        new(Next(), span, label, condition) { LabelSpan = labelSpan };

    public CoreLabeled Labeled(SourceSpan span, CoreExpr entry, ImmutableArray<CoreJoin> joins) =>
        new(Next(), span, entry, joins);

    public CoreProgram Program(CoreExpr body) => new(body, _nextNodeId);

    private int Next() => _nextNodeId++;
}
