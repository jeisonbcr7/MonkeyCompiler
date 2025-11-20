using Antlr4.Runtime;
using MonkeyCompiler.Core.Lexing;

namespace MonkeyCompiler.Core.Parsing;

public class ParserService
{
    public ParserResult Parse(string sourceCode)
    {
        var inputStream = new AntlrInputStream(sourceCode);
        var lexer = new MonkeyLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new MonkeyParser(tokenStream);

        var lexerErrorListener = new MonkeyLexerErrorListener();
        var parserErrorListener = new MonkeyErrorListener();

        lexer.RemoveErrorListeners();
        parser.RemoveErrorListeners();
        lexer.AddErrorListener(lexerErrorListener);
        parser.AddErrorListener(parserErrorListener);

        var tree = parser.program();

        var errors = new List<string>();
        errors.AddRange(lexerErrorListener.Errors);
        errors.AddRange(parserErrorListener.Errors);

        if (errors.Count > 0)
        {
            return new ParserResult(false, treeText: null, errors);
        }

        return new ParserResult(true, tree.ToStringTree(parser), Array.Empty<string>());
    }

    public ParserResult ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            return new ParserResult(false, treeText: null, new[] { $"No se encontró el archivo '{path}'." });
        }

        var code = File.ReadAllText(path);
        return Parse(code);
    }
}

public sealed record ParserResult(bool Success, string? TreeText, IReadOnlyList<string> Errors);

internal sealed class MonkeyErrorListener : BaseErrorListener
{
    public List<string> Errors { get; } = new();
    public bool HasErrors => Errors.Count > 0;

    public override void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
    {
        var tokenText = offendingSymbol?.Text ?? "<no-token>";
        var column = charPositionInLine + 1;
        Errors.Add($"[L{line}, C{column}] Token '{tokenText}': {msg}");
    }
}
