using MonkeyCompiler.Core.Parsing;
using MonkeyCompiler.Core.Semantics;
using Xunit;

namespace MonkeyCompiler.Tests;

public class TypeCheckingVisitorTests
{
    private readonly ParserService _parser = new();
    private readonly TypeCheckingVisitor _typeChecker = new();

    [Fact]
    public void Check_RedeclarationInSameScope_Fails()
    {
        const string code = """
let global: int = 1

fn main() : void {
  let value: int = 2
  let value: int = 3
}
""";

        var program = _parser.Parse(code).Program!;
        var result = _typeChecker.Check(program);

        Assert.False(result.Success);
        var error = Assert.Single(result.Errors);
        Assert.Contains("ya está declarado", error);
    }

    [Fact]
    public void Check_UseBeforeDeclaration_Fails()
    {
        const string code = """
fn main() : void {
  let y: int = x
  let x: int = 5
}
""";

        var program = _parser.Parse(code).Program!;
        var result = _typeChecker.Check(program);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("antes de ser declarado"));
    }

    [Fact]
    public void Check_NestedScopesAndFunctionsInsideArrays_Pass()
    {
        const string code = """
fn increment(x: int) : int { return x + 1 }

fn main() : void {
  let funcs: array<fn(int) : int> = [increment, fn(y: int) : int { return y * 2 }]
  let first: int = funcs[0](10)
  {
    let hidden: int = 5
    let doubled: int = funcs[1](hidden)
    print(doubled)
  }
  let lenValue: int = len(funcs)
}
""";

        var program = _parser.Parse(code).Program!;
        var result = _typeChecker.Check(program);

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Check_HashAccessAndTypeMismatch_Fails()
    {
        const string code = """
let users: hash<string, int> = { "ana": 1, "bob": 2 }

fn main() : void {
  let first: int = users["ana"]
  let wrong: int = users[0]
}
""";

        var program = _parser.Parse(code).Program!;
        var result = _typeChecker.Check(program);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("hash") && e.Contains("string"));
    }
}
