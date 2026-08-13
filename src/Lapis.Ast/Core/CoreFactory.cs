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
        SourceSpan? nameSpan = null,
        bool isMutable = false,
        SourceSpan? ownerSpan = null,
        NamedTypeSyntax? owner = null) =>
        new(Next(), span, name, annotation, value, body, isSynthetic)
        {
            NameSpan = nameSpan ?? span,
            IsMutable = isMutable,
            OwnerSpan = ownerSpan,
            Owner = owner,
        };

    public CoreAssign Assign(
        SourceSpan span,
        string name,
        CoreExpr value,
        SourceSpan nameSpan,
        ImmutableArray<CoreAssignSegment> path = default) =>
        new(Next(), span, name, value)
        {
            NameSpan = nameSpan,
            Path = path.IsDefault ? [] : path,
        };

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

    public CoreThrow Throw(SourceSpan span, CoreExpr value) => new(Next(), span, value);

    public CoreIf If(SourceSpan span, CoreExpr condition, CoreExpr then, CoreExpr @else) =>
        new(Next(), span, condition, then, @else);

    public CoreBinary Binary(SourceSpan span, BinaryOperator op, CoreExpr left, CoreExpr right, SourceSpan operatorSpan) =>
        new(Next(), span, op, left, right) { OperatorSpan = operatorSpan };

    public CoreUnary Unary(SourceSpan span, UnaryOperator op, CoreExpr operand) => new(Next(), span, op, operand);

    public CoreSpan Array(SourceSpan span, ImmutableArray<CoreExpr> elements) => new(Next(), span, elements);

    public CoreSpanRepeat SpanRepeat(
        SourceSpan span,
        TypeSyntax element,
        CoreExpr initializer,
        CoreExpr size) => new(Next(), span, element, initializer, size);

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

    public CoreIs Is(
        SourceSpan span,
        CoreExpr scrutinee,
        string? ownerName,
        string variantName,
        string? bindingName,
        CoreExpr then,
        CoreExpr otherwise,
        SourceSpan? variantSpan = null,
        SourceSpan? bindingSpan = null) =>
        new(Next(), span, scrutinee, ownerName, variantName, bindingName, then, otherwise)
        {
            VariantSpan = variantSpan ?? span,
            BindingSpan = bindingSpan,
        };

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

    public CoreLoop Loop(SourceSpan span, string? label, CoreExpr body, SourceSpan? labelSpan = null) =>
        new(Next(), span, label, body) { LabelSpan = labelSpan };

    public CoreBreak Break(SourceSpan span, string? label, CoreExpr? value, SourceSpan? labelSpan = null) =>
        new(Next(), span, label, value) { LabelSpan = labelSpan };

    public CoreContinue Continue(SourceSpan span, string? label, SourceSpan? labelSpan = null) =>
        new(Next(), span, label) { LabelSpan = labelSpan };

    public CoreProgram Program(CoreExpr body) => new(body, _nextNodeId);

    private int Next() => _nextNodeId++;
}
