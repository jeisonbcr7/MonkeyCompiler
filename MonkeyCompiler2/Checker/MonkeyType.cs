using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Generated; // MonkeyParser, MonkeyLexer, etc.

namespace MonkeyCompiler.Semantics
{
    public enum BaseTypeKind
    {
        Int,
        String,
        Bool,
        Char,
        Void,
        Any,   // para built-ins genéricos si hace falta
        Error  // para seguir chequeando después de un error
    }

    public class MonkeyType
    {
        public BaseTypeKind Kind { get; set; }

        // Para array<T>
        public MonkeyType? ElementType { get; set; }

        // Para hash<K,V>
        public MonkeyType? KeyType { get; set; }
        public MonkeyType? ValueType { get; set; }

        // Para fn(T1,...,Tn) : Tr
        public List<MonkeyType>? ParameterTypes { get; set; }
        public MonkeyType? ReturnType { get; set; }

        // Para la regla 9: claves conocidas de un hash literal (solo si son constantes)
        public HashSet<string>? KnownKeys { get; set; }

        public MonkeyType(BaseTypeKind kind)
        {
            Kind = kind;
        }

        public static MonkeyType Int()    => new MonkeyType(BaseTypeKind.Int);
        public static MonkeyType String() => new MonkeyType(BaseTypeKind.String);
        public static MonkeyType Bool()   => new MonkeyType(BaseTypeKind.Bool);
        public static MonkeyType Char()   => new MonkeyType(BaseTypeKind.Char);
        public static MonkeyType Void()   => new MonkeyType(BaseTypeKind.Void);
        public static MonkeyType Any()    => new MonkeyType(BaseTypeKind.Any);
        public static MonkeyType Error()  => new MonkeyType(BaseTypeKind.Error);

        public static MonkeyType ArrayOf(MonkeyType element)
        {
            return new MonkeyType(BaseTypeKind.Int) // Kind no importa tanto, usamos ElementType para distinguir
            {
                Kind = BaseTypeKind.Any, // usamos Any + ElementType != null para marcar que es array
                ElementType = element
            };
        }

        public static MonkeyType HashOf(MonkeyType key, MonkeyType value)
        {
            return new MonkeyType(BaseTypeKind.Any)
            {
                KeyType = key,
                ValueType = value,
                KnownKeys = new HashSet<string>()
            };
        }

        public static MonkeyType FunctionOf(List<MonkeyType> @params, MonkeyType returnType)
        {
            return new MonkeyType(BaseTypeKind.Any)
            {
                ParameterTypes = @params,
                ReturnType = returnType
            };
        }

        public MonkeyType Clone()
        {
            return new MonkeyType(this.Kind)
            {
                ElementType = this.ElementType,
                KeyType = this.KeyType,
                ValueType = this.ValueType,
                ReturnType = this.ReturnType,
                ParameterTypes = this.ParameterTypes != null ? new List<MonkeyType>(this.ParameterTypes) : null,
                KnownKeys = this.KnownKeys != null ? new HashSet<string>(this.KnownKeys) : null
            };
        }

        public override string ToString()
        {
            if (ParameterTypes != null && ReturnType != null)
            {
                return $"fn({string.Join(", ", ParameterTypes)}) : {ReturnType}";
            }
            if (ElementType != null)
            {
                return $"array<{ElementType}>";
            }
            if (KeyType != null && ValueType != null)
            {
                return $"hash<{KeyType},{ValueType}>";
            }

            return Kind switch
            {
                BaseTypeKind.Int => "int",
                BaseTypeKind.String => "string",
                BaseTypeKind.Bool => "bool",
                BaseTypeKind.Char => "char",
                BaseTypeKind.Void => "void",
                BaseTypeKind.Any => "any",
                BaseTypeKind.Error => "error",
                _ => "unknown"
            };
        }
    }
}