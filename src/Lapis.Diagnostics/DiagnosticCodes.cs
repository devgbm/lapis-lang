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
    public const string IndexOutOfBounds = "LAP0244";
    public const string InvalidSpanSize = "LAP0245";
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

    /// <summary>
    /// O único código cuja mensagem vem do programa: é o texto que a
    /// <c>constraint</c> passou a <c>throw</c> (plano 18 §18.6).
    /// </summary>
    public const string ConstraintRejected = "LAP0503";

    public const string UnknownSyntaxCategory = "LAP0504";
    public const string MacroExpansionTooDeep = "LAP0505";
    public const string MacroExpansionWrongContext = "LAP0506";
    public const string ThrowOutsideConstraint = "LAP0507";
    public const string ThrowExpectsStr = "LAP0508";
    public const string UnknownMacro = "LAP0509";
    public const string MacroIsNotAValue = "LAP0510";
    public const string DuplicateMacro = "LAP0511";

    // LAP052x — goto e label (plano 16, retirado no plano 26 — Q32). LAP0520,
    // LAP0521 e LAP0522 ficam retirados e não reciclados: código publicado nunca
    // volta ao pool, mesma regra de LAP0301/LAP0706.

    // Controle de fluxo estruturado (plano 26, M16 — Q32): `loop`/`break`/
    // `continue` substituem `goto`/`label`.
    public const string BreakOrContinueOutsideLoop = "LAP0523";
    public const string UnknownLoopLabel = "LAP0524";
    public const string LoopLabelOutOfScope = "LAP0525";
    public const string IncompatibleBreakValues = "LAP0526";
    public const string BareIfCannotHaveBareIfBody = "LAP0527";

    // LAP06xx — reflection (plano 19)
    public const string ReflectExpectsType = "LAP0601";
    public const string TypeNotDeclaredYet = "LAP0602";

    // LAP070x — membros de tipo (plano 21)
    public const string UnknownMember = "LAP0701";
    public const string DuplicateMember = "LAP0702";
    public const string MemberShadowsVariant = "LAP0703";
    public const string MemberOwnerMustBeAType = "LAP0704";
    public const string MemberCannotBeVar = "LAP0705";

    // LAP0706 está reservado, não emitido. Ele cobriria "atribuição qualificada é
    // proibida" — regra que chegou a ser escrita e que o autor corrigiu:
    // `varInstance.name = "x"` **é** válido, porque a mutabilidade segue o binding
    // e não a forma do alvo. O número não é reciclado.
    public const string QualifiedAssignmentReserved = "LAP0706";

    /// <summary>
    /// O alvo de uma atribuição existe no tipo, mas é membro e não campo da
    /// instância. "Campo desconhecido" (<c>LAP0250</c>) seria mentira.
    /// </summary>
    public const string AssignToMember = "LAP0707";

    // LAP071x — membros de instância (plano 22)
    public const string MemberRequiresInstance = "LAP0710";
    public const string MemberIsStatic = "LAP0711";
    public const string SelfOutsideMember = "LAP0712";

    // Dois membros de mesmo nome cujos padrões de dono se cruzam: existe um tipo
    // que casa com os dois, e nada diz qual roda. A alternativa — escolher o mais
    // específico — é uma regra que o leitor teria de simular de cabeça (plano 23
    // §23.5).
    public const string OverlappingMember = "LAP0720";

    // `?` fora da posição de dono. Como tipo de valor ele seria um `Any`
    // estrutural pela porta dos fundos: `def x: Result<?, ?>` prometeria um
    // `Result` sobre o qual nada se pode fazer, e aceitaria qualquer um.
    public const string WildcardOutsideOwner = "LAP0721";

    // O padrão do dono tem aridade diferente da do tipo. Separado de LAP0291
    // porque a posição é outra: aqui não se está usando um genérico, está-se
    // declarando para quais instâncias dele o membro vale.
    public const string MemberOwnerArity = "LAP0722";

    // LAP073x — `is` (plano 25, M16, fecha Q23).
    //
    // A divisão entre eles segue quem sabe responder: LAP0730 é sobre a
    // **posição** da ligação, que é sintaxe, e sai do desugar; os outros três
    // dependem do **tipo** do escrutinado — qual enum declara a variante e
    // quantos valores ela carrega — e saem do checker.
    //
    // LAP0731 fica reservado, não emitido: cobriria "a ligação não atravessa um
    // `goto`" (plano 25 §25.4 original), situação que deixou de existir quando
    // a Q32 (plano 26) derrubou `goto`/`label`. Não reciclado.
    public const string IsBindingRequiresIfOrAnd = "LAP0730";
    public const string UnknownIsVariant = "LAP0732";
    public const string IsVariantHasNoPayload = "LAP0733";
    public const string IsVariantHasMultiplePayloads = "LAP0734";

    // LAP03xx — Execução
    //
    // LAP0301 (divisão por zero) foi aposentado: a divisão inteira por zero passou
    // a produzir o maior Int (Q9), então a operação é total e não há o que relatar.
    // O código não é reciclado.
    public const string CallDepthExceeded = "LAP0302";

    // Chegou com o `goto` para trás (M6) e continua com `loop` (plano 26, Q32) —
    // mesmo código, mesmo espírito, mecanismo mais simples por baixo. A mensagem
    // fala de "iterações", não mais de "saltos".
    public const string IterationLimitExceeded = "LAP0303";

    // Quantidade dinâmica de `.[T; inicial; n]` acima do orçamento. Mesmo espírito
    // do orçamento de saltos: um programa que pede um span grande demais termina
    // com diagnóstico em vez de travar a máquina.
    public const string SpanTooLarge = "LAP0304";
}
