using MonkeyCompiler.Core.CodeGeneration;
using MonkeyCompiler.Core.Parsing;
using MonkeyCompiler.Core.Semantics;

var inputFile = args.Length > 0 ? args[0] : "test.monkey";
var parser = new ParserService();
var parseResult = parser.ParseFile(inputFile);

if (!parseResult.Success || parseResult.Program is null)
{
    Console.WriteLine("❌ Se encontraron errores de parseo:");
    foreach (var error in parseResult.Errors)
    {
        Console.WriteLine(error);
    }

    return 1;
}

var typeChecker = new TypeCheckingVisitor();
var typeResult = typeChecker.Check(parseResult.Program);
if (!typeResult.Success)
{
    Console.WriteLine("❌ Se encontraron errores de tipos:");
    foreach (var error in typeResult.Errors)
    {
        Console.WriteLine(error);
    }

    return 1;
}

Console.WriteLine("✅ Analizador sintáctico y semántico ejecutados correctamente. Generando IL...");
var generator = new CodeGenerator();
var generated = generator.Generate(parseResult.Program);

Console.WriteLine("▶ Ejecutando programa Monkey...");
var exitCode = (int)generated.EntryPoint.Invoke(null, new object[] { Array.Empty<string>() })!;
return exitCode;
