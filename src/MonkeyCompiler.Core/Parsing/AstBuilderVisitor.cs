using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using MonkeyCompiler.Core.Ast;

namespace MonkeyCompiler.Core.Parsing;

internal sealed class AstBuilderVisitor : MonkeyBaseVisitor<AstNode>
{
    public ProgramNode Build(MonkeyParser.ProgramContext context) => Visit(context) as ProgramNode ?? throw new InvalidOperationException("No se pudo construir el AST del programa.");

    public override AstNode VisitProgram(MonkeyParser.ProgramContext context)
    {
        var functions = new List<FunctionDeclarationNode>();
        var statements = new List<StatementNode>();

        foreach (var child in context.children)
        {
            switch (child)
            {
                case MonkeyParser.FunctionDeclarationContext functionContext:
                    functions.Add(Visit(functionContext) as FunctionDeclarationNode ?? throw new InvalidOperationException());
                    break;
                case MonkeyParser.StatementContext statementContext:
                    statements.Add((StatementNode)Visit(statementContext));
                    break;
            }
        }

        var mainFunction = Visit(context.mainFunction()) as FunctionDeclarationNode ?? throw new InvalidOperationException();

        return new ProgramNode(functions, statements, mainFunction);
    }

    public override AstNode VisitMainFunction(MonkeyParser.MainFunctionContext context)
    {
        var body = (BlockStatementNode)Visit(context.blockStatement());
        return new FunctionDeclarationNode("main", Array.Empty<ParameterNode>(), new VoidTypeNode(GetLocation(context.VOID().Symbol)), body, GetLocation(context.FN().Symbol));
    }

    public override AstNode VisitFunctionDeclaration(MonkeyParser.FunctionDeclarationContext context)
    {
        var name = context.identifier().IDENTIFIER().GetText();
        var parameters = VisitParameters(context.functionParameters());
        var returnType = (TypeNode)Visit(context.type());
        var body = (BlockStatementNode)Visit(context.blockStatement());

        return new FunctionDeclarationNode(name, parameters, returnType, body, GetLocation(context.FN().Symbol));
    }

    public override AstNode VisitStatement(MonkeyParser.StatementContext context)
    {
        return Visit(context.GetChild(0)) as StatementNode ?? throw new InvalidOperationException();
    }

    public override AstNode VisitLetStatement(MonkeyParser.LetStatementContext context)
    {
        var name = context.identifier().IDENTIFIER().GetText();
        var isConst = context.CONST() is not null;
        var type = (TypeNode)Visit(context.type());
        var value = (ExpressionNode)Visit(context.expression());

        return new LetStatementNode(name, isConst, type, value, GetLocation(context.LET().Symbol));
    }

    public override AstNode VisitReturnStatement(MonkeyParser.ReturnStatementContext context)
    {
        var expression = context.expression() is null ? null : (ExpressionNode)Visit(context.expression());
        return new ReturnStatementNode(expression, GetLocation(context.RETURN().Symbol));
    }

    public override AstNode VisitExpressionStatement(MonkeyParser.ExpressionStatementContext context)
    {
        var expression = (ExpressionNode)Visit(context.expression());
        return new ExpressionStatementNode(expression, expression.Location);
    }

    public override AstNode VisitIfStatement(MonkeyParser.IfStatementContext context)
    {
        var condition = (ExpressionNode)Visit(context.expression());
        var consequence = (BlockStatementNode)Visit(context.blockStatement(0));
        var alternative = context.blockStatement().Length > 1 ? (BlockStatementNode)Visit(context.blockStatement(1)) : null;

        return new IfStatementNode(condition, consequence, alternative, GetLocation(context.IF().Symbol));
    }

    public override AstNode VisitBlockStatement(MonkeyParser.BlockStatementContext context)
    {
        var statements = new List<StatementNode>();
        foreach (var statementContext in context.statement())
        {
            statements.Add((StatementNode)Visit(statementContext));
        }

        return new BlockStatementNode(statements, GetLocation(context.LBRACE().Symbol));
    }

    public override AstNode VisitPrintStatement(MonkeyParser.PrintStatementContext context)
    {
        var expression = (ExpressionNode)Visit(context.expression());
        return new PrintStatementNode(expression, GetLocation(context.PRINT().Symbol));
    }

    public override AstNode VisitExpression(MonkeyParser.ExpressionContext context)
    {
        var left = (ExpressionNode)Visit(context.additionExpression(0));

        for (var i = 1; i < context.additionExpression().Length; i++)
        {
            var operatorNode = context.children[2 * i - 1] as ITerminalNode ?? throw new InvalidOperationException();
            var right = (ExpressionNode)Visit(context.additionExpression(i));
            left = new InfixExpressionNode(left, operatorNode.Symbol.Text, right, GetLocation(operatorNode.Symbol));
        }

        return left;
    }

    public override AstNode VisitAdditionExpression(MonkeyParser.AdditionExpressionContext context)
    {
        var left = (ExpressionNode)Visit(context.multiplicationExpression(0));

        for (var i = 1; i < context.multiplicationExpression().Length; i++)
        {
            var operatorNode = context.children[2 * i - 1] as ITerminalNode ?? throw new InvalidOperationException();
            var right = (ExpressionNode)Visit(context.multiplicationExpression(i));
            left = new InfixExpressionNode(left, operatorNode.Symbol.Text, right, GetLocation(operatorNode.Symbol));
        }

        return left;
    }

    public override AstNode VisitMultiplicationExpression(MonkeyParser.MultiplicationExpressionContext context)
    {
        var left = (ExpressionNode)Visit(context.elementExpression(0));

        for (var i = 1; i < context.elementExpression().Length; i++)
        {
            var operatorNode = context.children[2 * i - 1] as ITerminalNode ?? throw new InvalidOperationException();
            var right = (ExpressionNode)Visit(context.elementExpression(i));
            left = new InfixExpressionNode(left, operatorNode.Symbol.Text, right, GetLocation(operatorNode.Symbol));
        }

        return left;
    }

    public override AstNode VisitElementExpression(MonkeyParser.ElementExpressionContext context)
    {
        var baseExpression = (ExpressionNode)Visit(context.primitiveExpression());

        if (context.elementAccess() is not null)
        {
            var index = (ExpressionNode)Visit(context.elementAccess().expression());
            return new IndexExpressionNode(baseExpression, index, GetLocation(context.elementAccess().LBRACK().Symbol));
        }

        if (context.callExpression() is not null)
        {
            var callContext = context.callExpression();
            var arguments = callContext.expressionList() is null
                ? Array.Empty<ExpressionNode>()
                : callContext.expressionList().expression().Select(e => (ExpressionNode)Visit(e)).ToArray();

            return new CallExpressionNode(baseExpression, arguments, GetLocation(callContext.LPAREN().Symbol));
        }

        return baseExpression;
    }

    public override AstNode VisitPrimitiveExpression(MonkeyParser.PrimitiveExpressionContext context)
    {
        if (context.numericLiteral() is not null)
        {
            var literal = int.Parse(context.numericLiteral().INTEGER_LITERAL().GetText());
            return new IntegerLiteralNode(literal, GetLocation(context.Start));
        }

        if (context.stringLiteral() is not null)
        {
            var rawText = context.stringLiteral().STRING_LITERAL().GetText();
            var value = rawText[1..^1];
            return new StringLiteralNode(value, GetLocation(context.Start));
        }

        if (context.charLiteral() is not null)
        {
            var rawText = context.charLiteral().CHAR_LITERAL().GetText();
            var value = rawText[1..^1];
            var unescaped = value switch
            {
                "\\n" => '\\n',
                "\\t" => '\\t',
                "\\r" => '\\r',
                "\\b" => '\\b',
                "\\"" => '\\"',
                "\\'" => '\'',
                _ when value.StartsWith("\\") => value[^1],
                _ => value[0]
            };

            return new CharLiteralNode(unescaped, GetLocation(context.Start));
        }

        if (context.booleanLiteral() is not null)
        {
            var value = context.booleanLiteral().TRUE() is not null;
            return new BooleanLiteralNode(value, GetLocation(context.Start));
        }

        if (context.identifier() is not null)
        {
            var name = context.identifier().IDENTIFIER().GetText();
            return new IdentifierExpressionNode(name, GetLocation(context.Start));
        }

        if (context.expression() is not null)
        {
            return Visit(context.expression()) as ExpressionNode ?? throw new InvalidOperationException();
        }

        if (context.arrayLiteral() is not null)
        {
            return Visit(context.arrayLiteral()) as ExpressionNode ?? throw new InvalidOperationException();
        }

        if (context.functionLiteral() is not null)
        {
            return Visit(context.functionLiteral()) as ExpressionNode ?? throw new InvalidOperationException();
        }

        if (context.hashLiteral() is not null)
        {
            return Visit(context.hashLiteral()) as ExpressionNode ?? throw new InvalidOperationException();
        }

        throw new InvalidOperationException("Expresión primitiva no soportada.");
    }

    public override AstNode VisitArrayLiteral(MonkeyParser.ArrayLiteralContext context)
    {
        var elements = context.expressionList()?.expression().Select(e => (ExpressionNode)Visit(e)).ToArray() ?? Array.Empty<ExpressionNode>();
        return new ArrayLiteralNode(elements, GetLocation(context.LBRACK().Symbol));
    }

    public override AstNode VisitFunctionLiteral(MonkeyParser.FunctionLiteralContext context)
    {
        var parameters = VisitParameters(context.functionParameters());
        var returnType = (TypeNode)Visit(context.type());
        var body = (BlockStatementNode)Visit(context.blockStatement());

        return new FunctionLiteralNode(parameters, returnType, body, GetLocation(context.FN().Symbol));
    }

    public override AstNode VisitHashLiteral(MonkeyParser.HashLiteralContext context)
    {
        var pairs = new List<HashItemNode>();
        foreach (var content in context.hashContent())
        {
            pairs.Add((HashItemNode)Visit(content));
        }

        return new HashLiteralNode(pairs, GetLocation(context.LBRACE().Symbol));
    }

    public override AstNode VisitHashContent(MonkeyParser.HashContentContext context)
    {
        var key = (ExpressionNode)Visit(context.expression(0));
        var value = (ExpressionNode)Visit(context.expression(1));
        return new HashItemNode(key, value, key.Location);
    }

    public override AstNode VisitType(MonkeyParser.TypeContext context)
    {
        if (context.INT() is not null)
        {
            return new IntTypeNode(GetLocation(context.INT().Symbol));
        }

        if (context.STRING() is not null)
        {
            return new StringTypeNode(GetLocation(context.STRING().Symbol));
        }

        if (context.BOOL() is not null)
        {
            return new BoolTypeNode(GetLocation(context.BOOL().Symbol));
        }

        if (context.CHAR() is not null)
        {
            return new CharTypeNode(GetLocation(context.CHAR().Symbol));
        }

        if (context.VOID() is not null)
        {
            return new VoidTypeNode(GetLocation(context.VOID().Symbol));
        }

        if (context.arrayType() is not null)
        {
            return Visit(context.arrayType()) as TypeNode ?? throw new InvalidOperationException();
        }

        if (context.hashType() is not null)
        {
            return Visit(context.hashType()) as TypeNode ?? throw new InvalidOperationException();
        }

        if (context.functionType() is not null)
        {
            return Visit(context.functionType()) as TypeNode ?? throw new InvalidOperationException();
        }

        throw new InvalidOperationException("Tipo no soportado.");
    }

    public override AstNode VisitArrayType(MonkeyParser.ArrayTypeContext context)
    {
        var elementType = (TypeNode)Visit(context.type());
        return new ArrayTypeNode(elementType, GetLocation(context.ARRAY().Symbol));
    }

    public override AstNode VisitHashType(MonkeyParser.HashTypeContext context)
    {
        var keyType = (TypeNode)Visit(context.type(0));
        var valueType = (TypeNode)Visit(context.type(1));
        return new HashTypeNode(keyType, valueType, GetLocation(context.HASH().Symbol));
    }

    public override AstNode VisitFunctionType(MonkeyParser.FunctionTypeContext context)
    {
        var parameterTypes = context.functionParameterTypes()?.type().Select(t => (TypeNode)Visit(t)).ToArray() ?? Array.Empty<TypeNode>();
        var returnType = (TypeNode)Visit(context.type());
        return new FunctionTypeNode(parameterTypes, returnType, GetLocation(context.FN().Symbol));
    }

    private IReadOnlyList<ParameterNode> VisitParameters(MonkeyParser.FunctionParametersContext? context)
    {
        if (context is null)
        {
            return Array.Empty<ParameterNode>();
        }

        return context.parameter().Select(p =>
        {
            var name = p.identifier().IDENTIFIER().GetText();
            var type = (TypeNode)Visit(p.type());
            return new ParameterNode(name, type, GetLocation(p.identifier().Start));
        }).ToArray();
    }

    private static SourceLocation GetLocation(IToken token)
    {
        return new SourceLocation(token.Line, token.Column + 1);
    }
}
