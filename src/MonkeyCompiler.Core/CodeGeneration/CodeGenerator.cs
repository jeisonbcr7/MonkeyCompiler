using System.Reflection;
using System.Reflection.Emit;
using MonkeyCompiler.Core.Ast;

namespace MonkeyCompiler.Core.CodeGeneration;

public sealed class GeneratedProgram
{
    public GeneratedProgram(AssemblyBuilder assembly, MethodInfo entryPoint)
    {
        Assembly = assembly;
        EntryPoint = entryPoint;
    }

    public AssemblyBuilder Assembly { get; }
    public MethodInfo EntryPoint { get; }
}

public sealed class CodeGenerator
{
    public GeneratedProgram Generate(ProgramNode program, string assemblyName = "MonkeyProgram")
    {
        var name = new AssemblyName(assemblyName);
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule(name.Name!);
        var typeBuilder = moduleBuilder.DefineType(
            "MonkeyProgram",
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.Abstract | TypeAttributes.BeforeFieldInit);

        var visitor = new CodeGenVisitor(moduleBuilder, typeBuilder);
        var entryPoint = visitor.EmitProgram(program);

        typeBuilder.CreateType();
        assemblyBuilder.SetEntryPoint(entryPoint);

        return new GeneratedProgram(assemblyBuilder, entryPoint);
    }
}
