using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.IO;

namespace MonkeyCompiler.ErrorHandling
{
    // Esta clase captura los errores de ANTLR y los guarda en una lista propia
    public class MonkeyErrorListener : BaseErrorListener
    {
        // Instancia estática para usarla fácilmente (Singleton pattern simplificado)
        public static MonkeyErrorListener Instance { get; } = new MonkeyErrorListener();

        // Lista para guardar los errores encontrados
        public List<string> Errors { get; } = new List<string>();

        // Método que ANTLR llama automáticamente cuando encuentra un error
        public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            // Formato personalizado: "Fila X, Columna Y: Mensaje"
            string errorMsg = $"Error Sintáctico - Fila {line}, Columna {charPositionInLine}: {msg}";
            
            Errors.Add(errorMsg);
            
            // Opcional: Imprimirlo en consola inmediatamente también
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(errorMsg);
            Console.ResetColor();
        }

        // Método para verificar si hubo errores
        public bool HasErrors() => Errors.Count > 0;
        
        public void Reset() => Errors.Clear();
    }
}