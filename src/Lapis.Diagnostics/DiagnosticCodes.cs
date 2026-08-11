namespace Lapis.Diagnostics;

/// <summary>
/// Catálogo de códigos de diagnóstico — plans/appendix-b-diagnostics.md.
///
/// Um código publicado nunca é reciclado com outro significado. Os testes
/// asseveram código + span, nunca a mensagem.
/// </summary>
public static class DiagnosticCodes
{
    // LAP00xx — Lexer
    public const string UnexpectedCharacter = "LAP0001";
    public const string IntegerLiteralOutOfRange = "LAP0002";
    public const string UnknownEscapeSequence = "LAP0003";
    public const string UnterminatedString = "LAP0004";
    public const string UnterminatedBlockComment = "LAP0005";
    public const string MalformedFloatLiteral = "LAP0006";

    // LAP01xx — Parser
    public const string UnexpectedToken = "LAP0101";
    public const string ExpectedSemicolon = "LAP0102";
    public const string ExpectedEquals = "LAP0103";
    public const string ParameterRequiresType = "LAP0104";
    public const string ExpectedCloseBracket = "LAP0105";
    public const string ExpectedFieldSemicolon = "LAP0106";
    public const string MatchRequiresArm = "LAP0107";
    public const string UnclosedBrace = "LAP0108";
    public const string UnexpectedEndOfFile = "LAP0109";
    public const string ExpectedExpression = "LAP0110";
    public const string ExpectedType = "LAP0111";
    public const string ExpectedIdentifier = "LAP0112";
    public const string ExpectedPattern = "LAP0113";
    public const string ChainedComparison = "LAP0114";
    public const string ExpressionTooDeep = "LAP0115";
    public const string EmptyGenericArgumentList = "LAP0116";

    // LAP02xx — Type checker
    public const string UnknownVariable = "LAP0201";
    public const string DuplicateDefinition = "LAP0202";

    // LAP0203 (colisão de nome de variante) foi aposentado: com variantes sempre
    // qualificadas (Q3) não há injeção no escopo, logo não há colisão possível.
    public const string UnknownType = "LAP0204";

    // LAP0205 (binding não usado) está reservado, não emitido: é um warning que
    // ninguém produz ainda. Fica no catálogo para que o número não seja reusado.
    public const string UnusedBinding = "LAP0205";

    // Mutação (Q25)
    public const string NotAssignable = "LAP0206";
    public const string MutableCapturedByFunction = "LAP0207";

    public const string TypeMismatch = "LAP0210";

    public const string NotCallable = "LAP0220";
    public const string ArgumentCountMismatch = "LAP0221";
    public const string ArgumentTypeMismatch = "LAP0222";

    public const string ConditionMustBeBool = "LAP0230";
    public const string IncompatibleBranches = "LAP0231";

    public const string HeterogeneousArray = "LAP0240";
    public const string EmptyArrayNeedsAnnotation = "LAP0241";
    public const string NotIndexable = "LAP0242";
    public const string IndexMustBeInt = "LAP0243";

    public const string UnknownField = "LAP0250";
    public const string UnknownVariant = "LAP0251";
    public const string NotConstructible = "LAP0252";
    public const string MissingField = "LAP0253";
    public const string ExtraField = "LAP0254";
    public const string DuplicateFieldInitializer = "LAP0255";

    public const string PatternTypeMismatch = "LAP0260";
    public const string IncompatibleMatchArms = "LAP0261";
    public const string NonExhaustiveMatch = "LAP0262";
    public const string UnreachableArm = "LAP0263";
    public const string VariantArityMismatch = "LAP0264";

    public const string ReturnTypeMismatch = "LAP0270";
    public const string EmptyReturnInNonVoid = "LAP0271";
    public const string MissingReturn = "LAP0272";
    public const string UnreachableAfterReturn = "LAP0273";
    public const string ReturnOutsideFunction = "LAP0274";

    public const string OperatorNotApplicable = "LAP0280";
    public const string FunctionsNotComparable = "LAP0281";

    public const string GenericArityMismatch = "LAP0290";
    public const string ExpectedTypeArgument = "LAP0291";
    public const string ExpectedConstArgument = "LAP0292";
    public const string ConstArgumentTypeMismatch = "LAP0293";
    public const string GenericArgumentNotConstant = "LAP0294";
    public const string GenericTypeNeedsArguments = "LAP0295";

    // LAP0296 e LAP0297 (inferência de argumento genérico) foram aposentados:
    // nenhum argumento genérico é inferido (Q7), todos são escritos.
    public const string CannotDetermineGenericArguments = "LAP0298";

    // LAP050x — macros (plano 17)
    public const string NoMacroRuleMatches = "LAP0501";
    public const string AmbiguousMacroRules = "LAP0502";
    public const string UnknownSyntaxCategory = "LAP0504";
    public const string MacroExpansionTooDeep = "LAP0505";
    public const string MacroExpansionWrongContext = "LAP0506";
    public const string UnknownMacro = "LAP0509";
    public const string MacroIsNotAValue = "LAP0510";
    public const string DuplicateMacro = "LAP0511";

    // LAP052x — goto e label (plano 16)
    public const string UnknownLabel = "LAP0520";
    public const string LabelOutOfScope = "LAP0521";
    public const string DuplicateLabel = "LAP0522";

    // LAP03xx — Execução
    //
    // LAP0301 (divisão por zero) foi aposentado: a divisão inteira por zero passou
    // a produzir o maior Int (Q9), então a operação é total e não há o que relatar.
    // O código não é reciclado.
    public const string CallDepthExceeded = "LAP0302";
    public const string JumpLimitExceeded = "LAP0303";
}
