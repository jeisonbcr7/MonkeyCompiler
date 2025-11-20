namespace MonkeyCompiler.Core.Ast;

public readonly record struct SourceLocation(int Line, int Column)
{
    public static readonly SourceLocation None = new(0, 0);
}

public abstract record AstNode(SourceLocation Location);

public abstract record TypeNode(SourceLocation Location) : AstNode(Location);

public sealed record IntTypeNode(SourceLocation Location) : TypeNode(Location);
public sealed record StringTypeNode(SourceLocation Location) : TypeNode(Location);
public sealed record BoolTypeNode(SourceLocation Location) : TypeNode(Location);
public sealed record CharTypeNode(SourceLocation Location) : TypeNode(Location);
public sealed record VoidTypeNode(SourceLocation Location) : TypeNode(Location);
public sealed record ArrayTypeNode(TypeNode ElementType, SourceLocation Location) : TypeNode(Location);
public sealed record HashTypeNode(TypeNode KeyType, TypeNode ValueType, SourceLocation Location) : TypeNode(Location);
public sealed record FunctionTypeNode(IReadOnlyList<TypeNode> ParameterTypes, TypeNode ReturnType, SourceLocation Location) : TypeNode(Location);

public abstract record StatementNode(SourceLocation Location) : AstNode(Location);

public sealed record ProgramNode(
    IReadOnlyList<FunctionDeclarationNode> Functions,
    IReadOnlyList<StatementNode> Statements,
    FunctionDeclarationNode MainFunction) : AstNode(SourceLocation.None);

public sealed record ParameterNode(string Name, TypeNode Type, SourceLocation Location) : AstNode(Location);
public sealed record BlockStatementNode(IReadOnlyList<StatementNode> Statements, SourceLocation Location) : StatementNode(Location);
public sealed record LetStatementNode(string Name, bool IsConst, TypeNode Type, ExpressionNode Value, SourceLocation Location) : StatementNode(Location);
public sealed record ReturnStatementNode(ExpressionNode? Expression, SourceLocation Location) : StatementNode(Location);
public sealed record ExpressionStatementNode(ExpressionNode Expression, SourceLocation Location) : StatementNode(Location);
public sealed record IfStatementNode(ExpressionNode Condition, BlockStatementNode Consequence, BlockStatementNode? Alternative, SourceLocation Location) : StatementNode(Location);
public sealed record PrintStatementNode(ExpressionNode Expression, SourceLocation Location) : StatementNode(Location);

public sealed record FunctionDeclarationNode(
    string Name,
    IReadOnlyList<ParameterNode> Parameters,
    TypeNode ReturnType,
    BlockStatementNode Body,
    SourceLocation Location) : AstNode(Location);

public abstract record ExpressionNode(SourceLocation Location) : AstNode(Location);

public sealed record IdentifierExpressionNode(string Name, SourceLocation Location) : ExpressionNode(Location);
public sealed record IntegerLiteralNode(int Value, SourceLocation Location) : ExpressionNode(Location);
public sealed record StringLiteralNode(string Value, SourceLocation Location) : ExpressionNode(Location);
public sealed record BooleanLiteralNode(bool Value, SourceLocation Location) : ExpressionNode(Location);
public sealed record CharLiteralNode(char Value, SourceLocation Location) : ExpressionNode(Location);
public sealed record ArrayLiteralNode(IReadOnlyList<ExpressionNode> Elements, SourceLocation Location) : ExpressionNode(Location);
public sealed record HashLiteralNode(IReadOnlyList<HashItemNode> Pairs, SourceLocation Location) : ExpressionNode(Location);
public sealed record HashItemNode(ExpressionNode Key, ExpressionNode Value, SourceLocation Location) : AstNode(Location);
public sealed record FunctionLiteralNode(IReadOnlyList<ParameterNode> Parameters, TypeNode ReturnType, BlockStatementNode Body, SourceLocation Location) : ExpressionNode(Location);
public sealed record CallExpressionNode(ExpressionNode Function, IReadOnlyList<ExpressionNode> Arguments, SourceLocation Location) : ExpressionNode(Location);
public sealed record IndexExpressionNode(ExpressionNode Target, ExpressionNode Index, SourceLocation Location) : ExpressionNode(Location);
public sealed record InfixExpressionNode(ExpressionNode Left, string Operator, ExpressionNode Right, SourceLocation Location) : ExpressionNode(Location);
