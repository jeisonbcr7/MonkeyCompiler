using MonkeyCompiler.Core.Parsing;
using Xunit;

namespace MonkeyCompiler.Tests;

public class ParserServiceTests
{
    private readonly ParserService _parser = new();

    [Fact]
    public void Parse_ValidProgram_ReturnsTree()
    {
        const string code = """
fn add(a: int) : int {
  return a
}

fn main() : void {
  let result: int = add(2)
  print(result)
}
""";

        var result = _parser.Parse(code);

        Assert.True(result.Success);
        Assert.Empty(result.Errors);
        Assert.False(string.IsNullOrWhiteSpace(result.TreeText));
    }

    [Fact]
    public void Parse_InvalidProgram_ReportsErrors()
    {
        const string code = "let x: int = 1";

        var result = _parser.Parse(code);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }
}
