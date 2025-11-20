using MonkeyCompiler.Core.Ast;
using MonkeyCompiler.Core.Parsing;
using Xunit;

namespace MonkeyCompiler.Tests;

public class ParserServiceTests
{
    private readonly ParserService _parser = new();

    [Fact]
    public void Parse_ValidProgram_BuildsAst()
    {
        const string code = """
fn add(a: int, b: int) : int {
  return a + b
}

fn main() : void {
  let result: int = add(2, 3)
  print(result)
}
""";

        var result = _parser.Parse(code);

        Assert.True(result.Success);
        Assert.NotNull(result.Program);
        Assert.False(string.IsNullOrWhiteSpace(result.TreeText));

        var program = result.Program!;
        var addFunction = Assert.Single(program.Functions);
        Assert.Equal("add", addFunction.Name);
        Assert.Equal(2, addFunction.Parameters.Count);
        Assert.IsType<IntTypeNode>(addFunction.ReturnType);

        var main = program.MainFunction;
        Assert.Equal("main", main.Name);
        Assert.Collection(
            main.Body.Statements,
            statement =>
            {
                var let = Assert.IsType<LetStatementNode>(statement);
                Assert.Equal("result", let.Name);
                var call = Assert.IsType<CallExpressionNode>(let.Value);
                var addIdentifier = Assert.IsType<IdentifierExpressionNode>(call.Function);
                Assert.Equal("add", addIdentifier.Name);
            },
            statement => Assert.IsType<PrintStatementNode>(statement));
    }

    [Fact]
    public void Parse_FunctionLiteralAndCalls_AreCaptured()
    {
        const string code = """
fn helper(cb: fn(int) : int) : int {
  return cb(5)
}

fn main() : void {
  let addOne: fn(int) : int = fn(x: int) : int { return x + 1 }
  let value: int = helper(addOne)
}
""";

        var result = _parser.Parse(code);

        Assert.True(result.Success);
        var program = Assert.NotNull(result.Program);

        var helper = Assert.Single(program.Functions);
        Assert.Equal("helper", helper.Name);
        var parameterType = Assert.IsType<FunctionTypeNode>(helper.Parameters[0].Type);
        Assert.Single(parameterType.ParameterTypes);

        var main = program.MainFunction;
        Assert.Collection(
            main.Body.Statements,
            first =>
            {
                var let = Assert.IsType<LetStatementNode>(first);
                var literal = Assert.IsType<FunctionLiteralNode>(let.Value);
                Assert.Single(literal.Parameters);
                var bodyReturn = Assert.IsType<ReturnStatementNode>(literal.Body.Statements.Single());
                var infix = Assert.IsType<InfixExpressionNode>(bodyReturn.Expression);
                Assert.Equal("+", infix.Operator);
            },
            second =>
            {
                var let = Assert.IsType<LetStatementNode>(second);
                var call = Assert.IsType<CallExpressionNode>(let.Value);
                Assert.IsType<IdentifierExpressionNode>(call.Function);
                Assert.Single(call.Arguments);
            });
    }

    [Fact]
    public void Parse_ArrayAndHashLiterals_Succeed()
    {
        const string code = """
let globals: array<int> = [1, 2, 3]

fn main() : void {
  let counts: hash<string, int> = { "one": 1, "two": 2 }
  let first: int = globals[0]
}
""";

        var result = _parser.Parse(code);

        Assert.True(result.Success);
        var program = Assert.NotNull(result.Program);

        var globalLet = Assert.IsType<LetStatementNode>(program.Statements.Single());
        var arrayLiteral = Assert.IsType<ArrayLiteralNode>(globalLet.Value);
        Assert.Equal(3, arrayLiteral.Elements.Count);

        var main = program.MainFunction;
        Assert.Collection(
            main.Body.Statements,
            first =>
            {
                var let = Assert.IsType<LetStatementNode>(first);
                var hashLiteral = Assert.IsType<HashLiteralNode>(let.Value);
                Assert.Equal(2, hashLiteral.Pairs.Count);
            },
            second =>
            {
                var let = Assert.IsType<LetStatementNode>(second);
                var index = Assert.IsType<IndexExpressionNode>(let.Value);
                Assert.IsType<IdentifierExpressionNode>(index.Target);
            });
    }

    [Fact]
    public void Parse_InvalidProgram_ReportsErrorsWithLocation()
    {
        const string code = "fn main() : void { let x: int = }";

        var result = _parser.Parse(code);

        Assert.False(result.Success);
        Assert.Null(result.Program);
        var error = Assert.Single(result.Errors);
        Assert.Contains("Token '}'", error);
        Assert.Contains("L1", error);
    }
}
