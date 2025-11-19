using Antlr4.Runtime;

namespace MonkeyCompiler.Core.Parsing;

public class ParserService
{
    public ParserResult Parse(string sourceCode)
    {
        var inputStream = new AntlrInputStream(sourceCode);
        var lexer = new MonkeyLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new MonkeyParser(tokenStream);

        var errorListener = new MonkeyErrorListener();
        lexer.RemoveErrorListeners();
        parser.RemoveErrorListeners();
        lexer.AddErrorListener(errorListener);
        parser.AddErrorListener(errorListener);

        var tree = parser.program();

        if (errorListener.HasErrors)
        {
            return new ParserResult(false, treeText: null, errorListener.Errors);
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
        Errors.Add($"[L{line}, C{charPositionInLine}] Token '{tokenText}': {msg}");
    }
}
