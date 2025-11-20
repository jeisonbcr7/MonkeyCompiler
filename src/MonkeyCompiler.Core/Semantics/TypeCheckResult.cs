namespace MonkeyCompiler.Core.Semantics;

public sealed record TypeCheckResult(bool Success, IReadOnlyList<string> Errors)
{
    public static readonly TypeCheckResult Ok = new(true, Array.Empty<string>());
}
