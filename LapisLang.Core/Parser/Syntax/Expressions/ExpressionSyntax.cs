using System.Diagnostics;

namespace LapisLang.Core;

[DebuggerDisplay("<{GetType().Name} {SourceSpan.AsText}>")]
public abstract class ExpressionSyntax : Syntax;
