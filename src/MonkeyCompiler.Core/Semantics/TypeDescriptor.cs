namespace MonkeyCompiler.Core.Semantics;

public enum TypeKind
{
    Any,
    Int,
    String,
    Bool,
    Char,
    Void,
    Array,
    Hash,
    Function
}

public sealed record TypeDescriptor
{
    private TypeDescriptor(TypeKind kind)
    {
        Kind = kind;
    }

    private TypeDescriptor(TypeDescriptor elementType)
    {
        Kind = TypeKind.Array;
        ElementType = elementType;
    }

    private TypeDescriptor(TypeDescriptor keyType, TypeDescriptor valueType)
    {
        Kind = TypeKind.Hash;
        KeyType = keyType;
        ValueType = valueType;
    }

    private TypeDescriptor(IReadOnlyList<TypeDescriptor> parameters, TypeDescriptor returnType)
    {
        Kind = TypeKind.Function;
        ParameterTypes = parameters;
        ReturnType = returnType;
    }

    public TypeKind Kind { get; }
    public TypeDescriptor? ElementType { get; }
    public TypeDescriptor? KeyType { get; }
    public TypeDescriptor? ValueType { get; }
    public IReadOnlyList<TypeDescriptor> ParameterTypes { get; } = Array.Empty<TypeDescriptor>();
    public TypeDescriptor? ReturnType { get; }

    public static readonly TypeDescriptor Any = new(TypeKind.Any);
    public static readonly TypeDescriptor Int = new(TypeKind.Int);
    public static readonly TypeDescriptor String = new(TypeKind.String);
    public static readonly TypeDescriptor Bool = new(TypeKind.Bool);
    public static readonly TypeDescriptor Char = new(TypeKind.Char);
    public static readonly TypeDescriptor Void = new(TypeKind.Void);

    public static TypeDescriptor Array(TypeDescriptor elementType) => new(elementType);
    public static TypeDescriptor Hash(TypeDescriptor keyType, TypeDescriptor valueType) => new(keyType, valueType);
    public static TypeDescriptor Function(IReadOnlyList<TypeDescriptor> parameters, TypeDescriptor returnType) => new(parameters, returnType);

    public override string ToString()
    {
        return Kind switch
        {
            TypeKind.Array => $"array<{ElementType}>",
            TypeKind.Hash => $"hash<{KeyType}, {ValueType}>",
            TypeKind.Function => $"fn({string.Join(", ", ParameterTypes)}) : {ReturnType}",
            _ => Kind.ToString().ToLowerInvariant()
        };
    }

    public bool Equals(TypeDescriptor? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || Kind != other.Kind)
        {
            return false;
        }

        return Kind switch
        {
            TypeKind.Array => ElementType!.Equals(other.ElementType),
            TypeKind.Hash => KeyType!.Equals(other.KeyType) && ValueType!.Equals(other.ValueType),
            TypeKind.Function => ReturnType!.Equals(other.ReturnType) && ParameterTypes.SequenceEqual(other.ParameterTypes),
            _ => true
        };
    }

    public override int GetHashCode()
    {
        return Kind switch
        {
            TypeKind.Array => HashCode.Combine(Kind, ElementType),
            TypeKind.Hash => HashCode.Combine(Kind, KeyType, ValueType),
            TypeKind.Function => ParameterTypes.Aggregate(HashCode.Combine(Kind, ReturnType), HashCode.Combine),
            _ => Kind.GetHashCode()
        };
    }
}
