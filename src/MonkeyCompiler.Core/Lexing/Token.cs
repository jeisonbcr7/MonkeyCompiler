using Antlr4.Runtime;

namespace MonkeyCompiler.Core.Lexing;

public sealed record Token(string Type, string Lexeme, int Line, int Column)
{
    public Token(IToken antlrToken, IVocabulary vocabulary)
        : this(
            vocabulary.GetSymbolicName(antlrToken.Type) ?? vocabulary.GetDisplayName(antlrToken.Type),
            antlrToken.Text,
            antlrToken.Line,
            antlrToken.Column + 1)
    {
    }
}
