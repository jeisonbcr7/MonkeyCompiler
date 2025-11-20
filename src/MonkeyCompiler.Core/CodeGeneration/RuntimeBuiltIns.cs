using System.Collections;
using System.Linq;

namespace MonkeyCompiler.Core.CodeGeneration;

internal static class RuntimeBuiltIns
{
    public static int Len(object? value)
    {
        return value switch
        {
            null => 0,
            string s => s.Length,
            ICollection collection => collection.Count,
            Array array => array.Length,
            _ => 0
        };
    }

    public static object? First(object? value)
    {
        return value switch
        {
            null => null,
            string s when s.Length > 0 => s[0],
            IList list when list.Count > 0 => list[0],
            Array array when array.Length > 0 => array.GetValue(0),
            _ => null
        };
    }

    public static object? Last(object? value)
    {
        return value switch
        {
            null => null,
            string s when s.Length > 0 => s[^1],
            IList list when list.Count > 0 => list[^1],
            Array array when array.Length > 0 => array.GetValue(array.Length - 1),
            _ => null
        };
    }

    public static object? Rest(object? value)
    {
        return value switch
        {
            null => null,
            string s when s.Length > 1 => s[1..],
            IList list when list.Count > 1 => list.Cast<object?>().Skip(1).ToList(),
            Array array when array.Length > 1 => array.Cast<object?>().Skip(1).ToArray(),
            _ => value
        };
    }

    public static object? Push(object? collection, object? value)
    {
        switch (collection)
        {
            case IList list:
                list.Add(value);
                return list;
            case Array array:
                var elements = array.Cast<object?>().Concat(new[] { value }).ToArray();
                return elements;
            default:
                return collection;
        }
    }
}
