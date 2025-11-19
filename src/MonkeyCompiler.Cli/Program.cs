using MonkeyCompiler.Core.Parsing;

var inputFile = args.Length > 0 ? args[0] : "test.monkey";
var parser = new ParserService();
var result = parser.ParseFile(inputFile);

if (!result.Success)
{
    Console.WriteLine("❌ Se encontraron errores:");
    foreach (var error in result.Errors)
    {
        Console.WriteLine(error);
    }

    return 1;
}

Console.WriteLine("✅ Parseo exitoso.");
Console.WriteLine("Árbol (parse tree) en formato plano:");
Console.WriteLine(result.TreeText);

return 0;
