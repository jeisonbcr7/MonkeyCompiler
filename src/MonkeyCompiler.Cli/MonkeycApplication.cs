using MonkeyCompiler.Core.Ast;
using MonkeyCompiler.Core.CodeGeneration;
using MonkeyCompiler.Core.Lexing;
using MonkeyCompiler.Core.Parsing;
using MonkeyCompiler.Core.Semantics;

namespace MonkeyCompiler.Cli;

public sealed class MonkeycApplication
{
    private readonly MonkeyLexerAdapter _lexer;
    private readonly ParserService _parser;
    private readonly TypeCheckingVisitor _typeChecker;
    private readonly CodeGenerator _generator;

    public MonkeycApplication()
    {
        _lexer = new MonkeyLexerAdapter();
        _parser = new ParserService();
        _typeChecker = new TypeCheckingVisitor();
        _generator = new CodeGenerator();
    }

    public int Run(string[] args, TextWriter? output = null)
    {
        var writer = output ?? Console.Out;

        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintUsage(writer);
            return 1;
        }

        var command = args[0];
        if (!string.Equals(command, "run", StringComparison.OrdinalIgnoreCase))
        {
            writer.WriteLine($"❌ Comando desconocido '{command}'.");
            PrintUsage(writer);
            return 1;
        }

        var options = ParseOptions(args.Skip(1).ToArray(), writer);
        if (!options.Success)
        {
            return 1;
        }

        if (options.FilePath is null)
        {
            writer.WriteLine("❌ Debes especificar un archivo a ejecutar.");
            PrintUsage(writer);
            return 1;
        }

        return RunPipeline(options.FilePath, options.ShowTokens, options.ShowAst, writer);
    }

    private static void PrintUsage(TextWriter writer)
    {
        writer.WriteLine("Uso: monkeyc run <archivo> [--tokens] [--ast]");
        writer.WriteLine("Ejemplo: monkeyc run programa.monkey --tokens --ast");
    }

    private static (bool Success, string? FilePath, bool ShowTokens, bool ShowAst) ParseOptions(string[] args, TextWriter writer)
    {
        string? filePath = null;
        var showTokens = false;
        var showAst = false;

        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--tokens":
                    showTokens = true;
                    break;
                case "--ast":
                    showAst = true;
                    break;
                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        writer.WriteLine($"❌ Opción desconocida '{arg}'.");
                        return (false, null, false, false);
                    }

                    if (filePath is not null)
                    {
                        writer.WriteLine("❌ Solo se puede especificar un archivo a la vez.");
                        return (false, null, false, false);
                    }

                    filePath = arg;
                    break;
            }
        }

        return (true, filePath, showTokens, showAst);
    }

    private int RunPipeline(string filePath, bool showTokens, bool showAst, TextWriter writer)
    {
        if (!File.Exists(filePath))
        {
            writer.WriteLine($"❌ No se encontró el archivo '{filePath}'.");
            return 1;
        }

        var source = File.ReadAllText(filePath);

        writer.WriteLine($"🔎 Escaneando '{filePath}'...");
        var scanResult = _lexer.Tokenize(source);
        if (!scanResult.Success)
        {
            PrintErrors(filePath, "Errores léxicos", scanResult.Errors, writer);
            return 1;
        }

        if (showTokens)
        {
            writer.WriteLine("🧪 Tokens:");
            PrintTokens(filePath, scanResult.Tokens, writer);
        }

        writer.WriteLine("🧩 Parseando AST...");
        var parseResult = _parser.Parse(source);
        if (!parseResult.Success || parseResult.Program is null)
        {
            PrintErrors(filePath, "Errores sintácticos", parseResult.Errors, writer);
            return 1;
        }

        if (showAst)
        {
            writer.WriteLine("📐 AST generado:");
            writer.WriteLine(AstPrinter.Print(parseResult.Program));
        }

        writer.WriteLine("📏 Verificando tipos...");
        var typeResult = _typeChecker.Check(parseResult.Program);
        if (!typeResult.Success)
        {
            PrintErrors(filePath, "Errores semánticos", typeResult.Errors, writer);
            return 1;
        }

        writer.WriteLine("⚙️  Generando IL...");
        var generated = _generator.Generate(parseResult.Program);

        writer.WriteLine("▶ Ejecutando programa Monkey...");
        var exitCode = (int)generated.EntryPoint.Invoke(null, new object[] { Array.Empty<string>() })!;
        return exitCode;
    }

    private static void PrintErrors(string filePath, string header, IEnumerable<string> errors, TextWriter writer)
    {
        writer.WriteLine($"❌ {header}:");
        foreach (var error in errors)
        {
            writer.WriteLine($"{filePath}:{error}");
        }
    }

    private static void PrintTokens(string filePath, IEnumerable<Token> tokens, TextWriter writer)
    {
        foreach (var token in tokens)
        {
            writer.WriteLine($"{filePath}:{token.Line}:{token.Column} {token.Type} '{token.Lexeme}'");
        }
    }
}
