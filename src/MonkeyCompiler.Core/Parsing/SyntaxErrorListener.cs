using Antlr4.Runtime;

namespace MonkeyCompiler.Core.Parsing;

internal sealed class SyntaxErrorListener : BaseErrorListener
{
    public List<string> Errors { get; } = new();
    public bool HasErrors => Errors.Count > 0;

    public override void SyntaxError(IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
    {
        var column = charPositionInLine + 1;
        var tokenText = offendingSymbol?.Text ?? "<no-token>";
        Errors.Add($"[L{line}, C{column}] Token '{tokenText}': {msg}");
    }
}
