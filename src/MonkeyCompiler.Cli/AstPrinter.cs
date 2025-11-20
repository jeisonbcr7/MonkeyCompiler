using System.Text;
using MonkeyCompiler.Core.Ast;

namespace MonkeyCompiler.Cli;

internal static class AstPrinter
{
    public static string Print(ProgramNode program)
    {
        var builder = new StringBuilder();
        PrintProgram(program, builder, 0);
        return builder.ToString();
    }

    private static void PrintProgram(ProgramNode program, StringBuilder builder, int indent)
    {
        AppendLine(builder, indent, "Program");

        if (program.Functions.Count > 0)
        {
            AppendLine(builder, indent + 1, "Funciones:");
            foreach (var function in program.Functions)
            {
                PrintFunction(function, builder, indent + 2);
            }
        }

        if (program.Statements.Count > 0)
        {
            AppendLine(builder, indent + 1, "Sentencias globales:");
            foreach (var statement in program.Statements)
            {
                PrintStatement(statement, builder, indent + 2);
            }
        }

        AppendLine(builder, indent + 1, "main:");
        PrintFunction(program.MainFunction, builder, indent + 2);
    }

    private static void PrintFunction(FunctionDeclarationNode function, StringBuilder builder, int indent)
    {
        var parameters = string.Join(", ", function.Parameters.Select(p => $"{p.Name}: {DescribeType(p.Type)}"));
        AppendLine(builder, indent, $"Func {function.Name}({parameters}) -> {DescribeType(function.ReturnType)}");
        PrintBlock(function.Body, builder, indent + 1);
    }

    private static void PrintBlock(BlockStatementNode block, StringBuilder builder, int indent)
    {
        AppendLine(builder, indent, "Bloque");
        foreach (var statement in block.Statements)
        {
            PrintStatement(statement, builder, indent + 1);
        }
    }

    private static void PrintStatement(StatementNode statement, StringBuilder builder, int indent)
    {
        switch (statement)
        {
            case BlockStatementNode block:
                PrintBlock(block, builder, indent);
                break;
            case LetStatementNode let:
                AppendLine(builder, indent, $"Let {(let.IsConst ? "const " : string.Empty)}{let.Name}: {DescribeType(let.Type)}");
                AppendLine(builder, indent + 1, "Valor:");
                PrintExpression(let.Value, builder, indent + 2);
                break;
            case ReturnStatementNode ret:
                AppendLine(builder, indent, "Return");
                if (ret.Expression is not null)
                {
                    PrintExpression(ret.Expression, builder, indent + 1);
                }
                break;
            case ExpressionStatementNode expressionStatement:
                AppendLine(builder, indent, "Expr");
                PrintExpression(expressionStatement.Expression, builder, indent + 1);
                break;
            case IfStatementNode ifStatement:
                AppendLine(builder, indent, "If");
                AppendLine(builder, indent + 1, "Condición:");
                PrintExpression(ifStatement.Condition, builder, indent + 2);
                AppendLine(builder, indent + 1, "Consequence:");
                PrintBlock(ifStatement.Consequence, builder, indent + 2);
                if (ifStatement.Alternative is not null)
                {
                    AppendLine(builder, indent + 1, "Else:");
                    PrintBlock(ifStatement.Alternative, builder, indent + 2);
                }
                break;
            case PrintStatementNode printStatement:
                AppendLine(builder, indent, "Print");
                PrintExpression(printStatement.Expression, builder, indent + 1);
                break;
            default:
                AppendLine(builder, indent, $"Sentencia no soportada: {statement.GetType().Name}");
                break;
        }
    }

    private static void PrintExpression(ExpressionNode expression, StringBuilder builder, int indent)
    {
        switch (expression)
        {
            case IdentifierExpressionNode identifier:
                AppendLine(builder, indent, $"Identificador {identifier.Name}");
                break;
            case IntegerLiteralNode integerLiteral:
                AppendLine(builder, indent, $"Entero {integerLiteral.Value}");
                break;
            case StringLiteralNode stringLiteral:
                AppendLine(builder, indent, $"String \"{stringLiteral.Value}\"");
                break;
            case BooleanLiteralNode booleanLiteral:
                AppendLine(builder, indent, $"Bool {booleanLiteral.Value.ToString().ToLowerInvariant()}");
                break;
            case CharLiteralNode charLiteral:
                AppendLine(builder, indent, $"Char '{charLiteral.Value}'");
                break;
            case ArrayLiteralNode arrayLiteral:
                AppendLine(builder, indent, "Array");
                foreach (var element in arrayLiteral.Elements)
                {
                    PrintExpression(element, builder, indent + 1);
                }
                break;
            case HashLiteralNode hashLiteral:
                AppendLine(builder, indent, "Hash");
                foreach (var pair in hashLiteral.Pairs)
                {
                    AppendLine(builder, indent + 1, "Entrada");
                    AppendLine(builder, indent + 2, "Llave:");
                    PrintExpression(pair.Key, builder, indent + 3);
                    AppendLine(builder, indent + 2, "Valor:");
                    PrintExpression(pair.Value, builder, indent + 3);
                }
                break;
            case FunctionLiteralNode functionLiteral:
                var parameters = string.Join(", ", functionLiteral.Parameters.Select(p => $"{p.Name}: {DescribeType(p.Type)}"));
                AppendLine(builder, indent, $"Func literal ({parameters}) -> {DescribeType(functionLiteral.ReturnType)}");
                PrintBlock(functionLiteral.Body, builder, indent + 1);
                break;
            case CallExpressionNode callExpression:
                AppendLine(builder, indent, "Llamada");
                AppendLine(builder, indent + 1, "Función:");
                PrintExpression(callExpression.Function, builder, indent + 2);
                if (callExpression.Arguments.Count > 0)
                {
                    AppendLine(builder, indent + 1, "Argumentos:");
                    foreach (var argument in callExpression.Arguments)
                    {
                        PrintExpression(argument, builder, indent + 2);
                    }
                }
                break;
            case IndexExpressionNode indexExpression:
                AppendLine(builder, indent, "Index");
                AppendLine(builder, indent + 1, "Objetivo:");
                PrintExpression(indexExpression.Target, builder, indent + 2);
                AppendLine(builder, indent + 1, "Índice:");
                PrintExpression(indexExpression.Index, builder, indent + 2);
                break;
            case InfixExpressionNode infixExpression:
                AppendLine(builder, indent, $"Infix '{infixExpression.Operator}'");
                PrintExpression(infixExpression.Left, builder, indent + 1);
                PrintExpression(infixExpression.Right, builder, indent + 1);
                break;
            default:
                AppendLine(builder, indent, $"Expresión no soportada: {expression.GetType().Name}");
                break;
        }
    }

    private static string DescribeType(TypeNode type) => type switch
    {
        IntTypeNode => "int",
        StringTypeNode => "string",
        BoolTypeNode => "bool",
        CharTypeNode => "char",
        VoidTypeNode => "void",
        ArrayTypeNode array => $"[{DescribeType(array.ElementType)}]",
        HashTypeNode hash => $"{{{DescribeType(hash.KeyType)}:{DescribeType(hash.ValueType)}}}",
        FunctionTypeNode function => $"func({string.Join(", ", function.ParameterTypes.Select(DescribeType))})->{DescribeType(function.ReturnType)}",
        _ => type.GetType().Name
    };

    private static void AppendLine(StringBuilder builder, int indent, string text)
    {
        builder.Append(' ', indent * 2);
        builder.AppendLine(text);
    }
}
