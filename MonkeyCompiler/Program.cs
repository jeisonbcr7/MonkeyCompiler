using System;
using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;

namespace MonkeyCompiler
{
    internal class Program
    {
        static int Main(string[] args)
        {
            // 1. Elegir archivo de entrada
            var inputFile = args.Length > 0 ? args[0] : "test.monkey";

            if (!File.Exists(inputFile))
            {
                Console.WriteLine($"No se encontró el archivo '{inputFile}'.");
                Console.WriteLine("Crea un archivo test.monkey en la raíz del proyecto o pasa la ruta por parámetro.");
                return 1;
            }

            var code = File.ReadAllText(inputFile);

            // 2. Crear lexer y parser de ANTLR
            var inputStream = new AntlrInputStream(code);
            var lexer = new MonkeyLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new MonkeyParser(tokenStream);

            // 3. Configurar listeners de error (para errores léxicos y sintácticos)
            var errorListener = new MonkeyErrorListener();
            lexer.RemoveErrorListeners();
            parser.RemoveErrorListeners();
            lexer.AddErrorListener(errorListener);
            parser.AddErrorListener(errorListener);

            // 4. Invocar la regla inicial: program
            var tree = parser.program();

            // 5. Revisar si hubo errores
            if (errorListener.HasErrors)
            {
                Console.WriteLine("❌ Se encontraron errores:");
                foreach (var err in errorListener.Errors)
                {
                    Console.WriteLine(err);
                }
                return 1;
            }

            Console.WriteLine("✅ Parseo exitoso.");
            Console.WriteLine("Árbol (parse tree) en formato plano:");
            Console.WriteLine(tree.ToStringTree(parser));

            return 0;
        }
    }

    /// <summary>
    /// Listener simple para capturar y formatear errores de ANTLR.
    /// Lo usamos tanto para el lexer como para el parser.
    /// </summary>
    public class MonkeyErrorListener : BaseErrorListener
    {
        public List<string> Errors { get; } = new();
        public bool HasErrors => Errors.Count > 0;

        public void SyntaxError(
            IRecognizer recognizer,
            IToken offendingSymbol,
            int line,
            int charPositionInLine,
            string msg,
            RecognitionException e)
        {
            var tokenText = offendingSymbol?.Text ?? "<no-token>";
            Errors.Add($"[L{line}, C{charPositionInLine}] Token '{tokenText}': {msg}");
        }
    }
}
