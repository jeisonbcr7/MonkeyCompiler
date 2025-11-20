using Antlr4.Runtime;
using MonkeyCompiler.Core.Ast;
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
        var parserErrorListener = new SyntaxErrorListener();

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
            return new ParserResult(false, program: null, treeText: null, errors);
        }

        var visitor = new AstBuilderVisitor();
        var program = visitor.Build(tree);

        return new ParserResult(true, program, tree.ToStringTree(parser), Array.Empty<string>());
    }

    public ParserResult ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            return new ParserResult(false, program: null, treeText: null, new[] { $"No se encontró el archivo '{path}'." });
        }

        var code = File.ReadAllText(path);
        return Parse(code);
    }
}

public sealed record ParserResult(bool Success, ProgramNode? Program, string? TreeText, IReadOnlyList<string> Errors);
