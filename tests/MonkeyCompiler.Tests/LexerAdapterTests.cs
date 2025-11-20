using MonkeyCompiler.Core.Lexing;
using Xunit;

namespace MonkeyCompiler.Tests;

public class LexerAdapterTests
{
    private readonly MonkeyLexerAdapter _adapter = new();

    [Fact]
    public void Tokenize_ValidInput_IgnoresCommentsAndWhitespace()
    {
        const string code = """
// comentario de línea
fn main() : void {
  // comentario interno
  let x: int = 10
  /* bloque
     anidado */
  let msg: string = "hola mundo"
}
""";

        var result = _adapter.Tokenize(code);

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.Collection(
            result.Tokens,
            t => AssertToken(t, "FN", "fn", 2, 1),
            t => AssertToken(t, "MAIN", "main", 2, 4),
            t => AssertToken(t, "LPAREN", "(", 2, 9),
            t => AssertToken(t, "RPAREN", ")", 2, 10),
            t => AssertToken(t, "COLON", ":", 2, 12),
            t => AssertToken(t, "VOID", "void", 2, 14),
            t => AssertToken(t, "LBRACE", "{", 2, 19),
            t => AssertToken(t, "LET", "let", 4, 3),
            t => AssertToken(t, "IDENTIFIER", "x", 4, 7),
            t => AssertToken(t, "COLON", ":", 4, 8),
            t => AssertToken(t, "INT", "int", 4, 10),
            t => AssertToken(t, "ASSIGN", "=", 4, 14),
            t => AssertToken(t, "INTEGER_LITERAL", "10", 4, 16),
            t => AssertToken(t, "LET", "let", 7, 3),
            t => AssertToken(t, "IDENTIFIER", "msg", 7, 7),
            t => AssertToken(t, "COLON", ":", 7, 10),
            t => AssertToken(t, "STRING", "string", 7, 12),
            t => AssertToken(t, "ASSIGN", "=", 7, 19),
            t => AssertToken(t, "STRING_LITERAL", "\"hola mundo\"", 7, 21),
            t => AssertToken(t, "RBRACE", "}", 8, 1));
    }

    [Fact]
    public void Tokenize_InvalidCharacter_ReportsClearError()
    {
        const string code = "let x: int = 10 $";

        var result = _adapter.Tokenize(code);

        Assert.False(result.Success);
        Assert.Contains("Carácter inválido '$'", result.Errors.Single());
        Assert.Contains("L1", result.Errors.Single());
    }

    [Fact]
    public void Tokenize_UnterminatedString_ReportsClearError()
    {
        const string code = "let msg: string = \"hola";

        var result = _adapter.Tokenize(code);

        Assert.False(result.Success);
        Assert.Contains("Cadena sin cerrar.", result.Errors.Single());
        Assert.Contains("L1", result.Errors.Single());
    }

    private static void AssertToken(Token token, string expectedType, string expectedLexeme, int line, int column)
    {
        Assert.Equal(expectedType, token.Type);
        Assert.Equal(expectedLexeme, token.Lexeme);
        Assert.Equal(line, token.Line);
        Assert.Equal(column, token.Column);
    }
}
