using Antlr4.Runtime;
using Generated; 

namespace MonkeyCompiler
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Nombre del archivo físico
            string fileName = "test.txt";

            // Verificamos si existe para evitar crasheos feos
            if (!File.Exists(fileName))
            {
                Console.WriteLine($"Error: No se encuentra el archivo '{fileName}'");
                return;
            }

            // Leemos tod el contenido del archivo
            string inputCode = File.ReadAllText(fileName);

            // --- Scanner y Parser ---
            var inputStream = new AntlrInputStream(inputCode);
            var lexer = new MonkeyLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new MonkeyParser(tokenStream);

            Console.WriteLine($"--- Compilando archivo: {fileName} ---");

            // Arrancamos el parser
            var tree = parser.program();

            // Verificación rápida
            if (parser.NumberOfSyntaxErrors == 0)
            {
                Console.WriteLine("✅ Sintaxis Correcta.");
                // Mostrar árbol (opcional)
                // Console.WriteLine(tree.ToStringTree(parser));
            }
            else
            {
                // ANTLR imprimirá los errores por defecto en la consola
                Console.WriteLine($"❌ Se encontraron {parser.NumberOfSyntaxErrors} errores de sintaxis.");
            }
        }
    }
}