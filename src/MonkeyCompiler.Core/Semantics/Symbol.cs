namespace MonkeyCompiler.Core.Semantics;

public enum SymbolCategory
{
    Variable,
    Function,
    BuiltIn
}

public sealed record Symbol(
    string Name,
    TypeDescriptor Type,
    SymbolCategory Category,
    bool IsConst = false,
    bool IsParameter = false);
