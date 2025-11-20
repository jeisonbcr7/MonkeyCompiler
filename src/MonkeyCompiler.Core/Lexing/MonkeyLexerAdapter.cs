using Antlr4.Runtime;

namespace MonkeyCompiler.Core.Lexing;

public sealed class MonkeyLexerAdapter
{
    public TokenizationResult Tokenize(string source)
    {
        var input = new AntlrInputStream(source);
        var lexer = new MonkeyLexer(input);
        var errorListener = new MonkeyLexerErrorListener();

        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(errorListener);

        var tokens = new List<Token>();
        for (var antlrToken = lexer.NextToken(); antlrToken.Type != TokenConstants.EOF; antlrToken = lexer.NextToken())
        {
            tokens.Add(new Token(antlrToken, lexer.Vocabulary));
        }

        if (errorListener.HasErrors)
        {
            return new TokenizationResult(false, tokens, errorListener.Errors);
        }

        return new TokenizationResult(true, tokens, Array.Empty<string>());
    }
}

public sealed record TokenizationResult(bool Success, IReadOnlyList<Token> Tokens, IReadOnlyList<string> Errors);

internal sealed class MonkeyLexerErrorListener : IAntlrErrorListener<int>
{
    private readonly List<string> _errors = new();

    public IReadOnlyList<string> Errors => _errors;
    public bool HasErrors => _errors.Count > 0;

    public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
    {
        var column = charPositionInLine + 1;
        var message = BuildMessage(offendingSymbol, line, column, msg);
        _errors.Add(message);
    }

    private static string BuildMessage(int offendingSymbol, int line, int column, string message)
    {
        if (IsUnterminatedString(message, offendingSymbol))
        {
            return $"[L{line}, C{column}] Cadena sin cerrar.";
        }

        if (message.Contains("token recognition error", StringComparison.OrdinalIgnoreCase))
        {
            return $"[L{line}, C{column}] Carácter inválido '{FormatSymbol(offendingSymbol)}'.";
        }

        return $"[L{line}, C{column}] {message}";
    }

    private static bool IsUnterminatedString(string message, int offendingSymbol)
    {
        return message.Contains("unterminated string", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("missing '""'", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("EOF in string", StringComparison.OrdinalIgnoreCase) ||
               (message.Contains("token recognition error", StringComparison.OrdinalIgnoreCase) && offendingSymbol == '\"');
    }

    private static string FormatSymbol(int offendingSymbol)
    {
        if (offendingSymbol == TokenConstants.EOF)
        {
            return "<EOF>";
        }

        var character = char.ConvertFromUtf32(offendingSymbol);
        return character == "\n" ? "\\n" : character == "\t" ? "\\t" : character;
    }
}
