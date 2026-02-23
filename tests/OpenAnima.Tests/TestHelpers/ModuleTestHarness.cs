using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using OpenAnima.Contracts;

namespace OpenAnima.Tests.TestHelpers;

/// <summary>
/// Helper for creating test module assemblies at runtime for integration tests.
/// </summary>
public static class ModuleTestHarness
{
    /// <summary>
    /// Creates a test module directory with module.json and a minimal DLL implementing IModule.
    /// </summary>
    /// <param name="basePath">Base directory for test modules</param>
    /// <param name="moduleName">Name of the module to create</param>
    /// <returns>Path to the created module directory</returns>
    public static string CreateTestModuleDirectory(string basePath, string moduleName)
    {
        // Create module directory
        string moduleDir = Path.Combine(basePath, moduleName);
        Directory.CreateDirectory(moduleDir);

        // Create module.json manifest
        var manifest = new
        {
            name = moduleName,
            version = "1.0.0",
            description = $"Test module {moduleName}",
            entryAssembly = $"{moduleName}.dll"
        };

        string manifestPath = Path.Combine(moduleDir, "module.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true
        }));

        // Create minimal DLL using Reflection.Emit
        string dllPath = Path.Combine(moduleDir, $"{moduleName}.dll");
        CreateMinimalModuleDll(dllPath, moduleName);

        return moduleDir;
    }

    /// <summary>
    /// Creates a minimal .NET assembly DLL that implements IModule using Reflection.Emit.
    /// </summary>
    private static void CreateMinimalModuleDll(string dllPath, string moduleName)
    {
        var assemblyName = new AssemblyName(moduleName);
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            assemblyName,
            AssemblyBuilderAccess.Run);

        var moduleBuilder = assemblyBuilder.DefineDynamicModule(moduleName);

        // Define Metadata class implementing IModuleMetadata
        var metadataType = moduleBuilder.DefineType(
            $"{moduleName}.Metadata",
            TypeAttributes.Public | TypeAttributes.Class,
            typeof(object),
            new[] { typeof(IModuleMetadata) });

        // Implement Name property
        var nameProperty = metadataType.DefineProperty("Name", PropertyAttributes.None, typeof(string), null);
        var nameGetter = metadataType.DefineMethod("get_Name",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(string), Type.EmptyTypes);
        var nameGetterIL = nameGetter.GetILGenerator();
        nameGetterIL.Emit(OpCodes.Ldstr, moduleName);
        nameGetterIL.Emit(OpCodes.Ret);
        nameProperty.SetGetMethod(nameGetter);

        // Implement Version property
        var versionProperty = metadataType.DefineProperty("Version", PropertyAttributes.None, typeof(string), null);
        var versionGetter = metadataType.DefineMethod("get_Version",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(string), Type.EmptyTypes);
        var versionGetterIL = versionGetter.GetILGenerator();
        versionGetterIL.Emit(OpCodes.Ldstr, "1.0.0");
        versionGetterIL.Emit(OpCodes.Ret);
        versionProperty.SetGetMethod(versionGetter);

        // Implement Description property
        var descProperty = metadataType.DefineProperty("Description", PropertyAttributes.None, typeof(string), null);
        var descGetter = metadataType.DefineMethod("get_Description",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(string), Type.EmptyTypes);
        var descGetterIL = descGetter.GetILGenerator();
        descGetterIL.Emit(OpCodes.Ldstr, $"Test module {moduleName}");
        descGetterIL.Emit(OpCodes.Ret);
        descProperty.SetGetMethod(descGetter);

        var metadataTypeInfo = metadataType.CreateType();

        // Define Module class implementing IModule
        var moduleType = moduleBuilder.DefineType(
            $"{moduleName}.Module",
            TypeAttributes.Public | TypeAttributes.Class,
            typeof(object),
            new[] { typeof(IModule) });

        // Add metadata field
        var metadataField = moduleType.DefineField("_metadata", typeof(IModuleMetadata), FieldAttributes.Private);

        // Implement constructor
        var ctor = moduleType.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            Type.EmptyTypes);
        var ctorIL = ctor.GetILGenerator();
        ctorIL.Emit(OpCodes.Ldarg_0);
        ctorIL.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);
        ctorIL.Emit(OpCodes.Ldarg_0);
        ctorIL.Emit(OpCodes.Newobj, metadataTypeInfo!.GetConstructor(Type.EmptyTypes)!);
        ctorIL.Emit(OpCodes.Stfld, metadataField);
        ctorIL.Emit(OpCodes.Ret);

        // Implement Metadata property
        var metadataProp = moduleType.DefineProperty("Metadata", PropertyAttributes.None, typeof(IModuleMetadata), null);
        var metadataGetMethod = moduleType.DefineMethod("get_Metadata",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(IModuleMetadata), Type.EmptyTypes);
        var metadataGetIL = metadataGetMethod.GetILGenerator();
        metadataGetIL.Emit(OpCodes.Ldarg_0);
        metadataGetIL.Emit(OpCodes.Ldfld, metadataField);
        metadataGetIL.Emit(OpCodes.Ret);
        metadataProp.SetGetMethod(metadataGetMethod);

        // Implement InitializeAsync method
        var initMethod = moduleType.DefineMethod("InitializeAsync",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            typeof(Task),
            new[] { typeof(CancellationToken) });
        var initIL = initMethod.GetILGenerator();
        initIL.Emit(OpCodes.Call, typeof(Task).GetProperty("CompletedTask")!.GetGetMethod()!);
        initIL.Emit(OpCodes.Ret);

        // Implement ShutdownAsync method
        var shutdownMethod = moduleType.DefineMethod("ShutdownAsync",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            typeof(Task),
            new[] { typeof(CancellationToken) });
        var shutdownIL = shutdownMethod.GetILGenerator();
        shutdownIL.Emit(OpCodes.Call, typeof(Task).GetProperty("CompletedTask")!.GetGetMethod()!);
        shutdownIL.Emit(OpCodes.Ret);

        moduleType.CreateType();

        // Save assembly to disk using PersistedAssemblyBuilder (requires .NET 9+)
        // For .NET 8, we need a different approach - copy a pre-built template
        // Since we're using Reflection.Emit with Run access, we can't save to disk directly
        // We need to use a workaround: create a simple C# file and compile it

        CreateModuleDllViaCompilation(dllPath, moduleName);
    }

    /// <summary>
    /// Creates a module DLL by compiling C# source code at runtime.
    /// This is a workaround for .NET 8 where AssemblyBuilder.Save is not available.
    /// </summary>
    private static void CreateModuleDllViaCompilation(string dllPath, string moduleName)
    {
        // Get the directory containing OpenAnima.Contracts.dll
        var contractsAssembly = typeof(IModule).Assembly;
        var contractsPath = contractsAssembly.Location;

        // Create a simple C# source file
        string sourceCode = $@"
using System.Threading;
using System.Threading.Tasks;
using OpenAnima.Contracts;

namespace {moduleName}
{{
    public class Metadata : IModuleMetadata
    {{
        public string Name => ""{moduleName}"";
        public string Version => ""1.0.0"";
        public string Description => ""Test module {moduleName}"";
    }}

    public class Module : IModule
    {{
        private readonly IModuleMetadata _metadata = new Metadata();
        public IModuleMetadata Metadata => _metadata;

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {{
            return Task.CompletedTask;
        }}

        public Task ShutdownAsync(CancellationToken cancellationToken = default)
        {{
            return Task.CompletedTask;
        }}
    }}
}}
";

        // Write source to temp file
        string tempDir = Path.GetDirectoryName(dllPath)!;
        string sourceFile = Path.Combine(tempDir, $"{moduleName}.cs");
        File.WriteAllText(sourceFile, sourceCode);

        // Compile using dotnet CLI
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build -c Release -o \"{tempDir}\" /p:OutputType=Library /p:TargetFramework=net8.0",
            WorkingDirectory = tempDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Create a minimal csproj file
        string csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyName>{moduleName}</AssemblyName>
    <OutputType>Library</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""OpenAnima.Contracts"">
      <HintPath>{contractsPath}</HintPath>
    </Reference>
  </ItemGroup>
</Project>";

        string csprojPath = Path.Combine(tempDir, $"{moduleName}.csproj");
        File.WriteAllText(csprojPath, csprojContent);

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process != null)
        {
            process.WaitForExit();
            // Clean up temp files
            try
            {
                File.Delete(sourceFile);
                File.Delete(csprojPath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
