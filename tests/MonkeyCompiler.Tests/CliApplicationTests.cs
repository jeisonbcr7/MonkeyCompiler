using MonkeyCompiler.Cli;
using Xunit;

namespace MonkeyCompiler.Tests;

public class CliApplicationTests
{
    private readonly MonkeycApplication _app = new();

    [Fact]
    public void Run_WithValidProgram_ExecutesPipeline()
    {
        var filePath = CreateTempFile(
            """
            fn main() : void {
              let x: int = 10
              let y: int = 5
              print(x + y)
            }
            """
        );

        var writer = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(writer);
            var exitCode = _app.Run(new[] { "run", filePath }, writer);

            Assert.Equal(0, exitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            File.Delete(filePath);
        }

        var output = writer.ToString();
        Assert.Contains("Escaneando", output);
        Assert.Contains("Parseando AST", output);
        Assert.Contains("Verificando tipos", output);
        Assert.Contains("Generando IL", output);
        Assert.Contains("Ejecutando programa", output);
        Assert.Contains("15", output);
    }

    [Fact]
    public void Run_WithLexicalError_StopsPipeline()
    {
        var filePath = CreateTempFile(
            """
            fn main() : void {
              let x: int = 1 $
            }
            """
        );

        var writer = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(writer);
            var exitCode = _app.Run(new[] { "run", filePath }, writer);

            Assert.Equal(1, exitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            File.Delete(filePath);
        }

        var output = writer.ToString();
        Assert.Contains("Errores léxicos", output);
        Assert.Contains(filePath, output);
        Assert.DoesNotContain("Generando IL", output);
    }

    [Fact]
    public void Run_WithSemanticError_ReportsTypedDiagnostics()
    {
        var filePath = CreateTempFile(
            """
            fn bad(a: int) : int {
              return true
            }

            fn main() : void {
              let z: int = bad(1)
            }
            """
        );

        var writer = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(writer);
            var exitCode = _app.Run(new[] { "run", filePath }, writer);

            Assert.Equal(1, exitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            File.Delete(filePath);
        }

        var output = writer.ToString();
        Assert.Contains("Errores semánticos", output);
        Assert.Contains("return", output);
        Assert.Contains(filePath, output);
    }

    [Fact]
    public void Run_WithAstAndTokensOptions_PrintsDebugSections()
    {
        var filePath = CreateTempFile(
            """
            fn main() : void {
              print(42)
            }
            """
        );

        var writer = new StringWriter();
        var originalOut = Console.Out;
        try
        {
            Console.SetOut(writer);
            var exitCode = _app.Run(new[] { "run", filePath, "--tokens", "--ast" }, writer);

            Assert.Equal(0, exitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            File.Delete(filePath);
        }

        var output = writer.ToString();
        Assert.Contains("🧪 Tokens:", output);
        Assert.Contains("📐 AST generado:", output);
        Assert.Contains("MAIN", output);
        Assert.Contains("Func main", output);
    }

    private static string CreateTempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"monkey-{Guid.NewGuid():N}.monkey");
        File.WriteAllText(path, content.Trim());
        return path;
    }
}
