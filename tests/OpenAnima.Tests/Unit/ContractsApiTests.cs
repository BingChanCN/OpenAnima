using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using OpenAnima.Contracts;
using OpenAnima.Contracts.Routing;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Services;

namespace OpenAnima.Tests.Unit;

/// <summary>
/// Verifies all new Contracts API types exist in the correct namespaces with the correct shapes.
/// Tests are reflection-based and fast — no subprocess builds, no file I/O.
/// </summary>
[Trait("Category", "ContractsApi")]
public class ContractsApiTests
{
    // -----------------------------------------------------------------------
    // 1. IModuleConfig surface
    // -----------------------------------------------------------------------

    [Fact]
    public void IModuleConfig_ExistsIn_OpenAnima_Contracts_Namespace()
    {
        var type = typeof(IModuleConfig);
        Assert.Equal("OpenAnima.Contracts", type.Namespace);
    }

    [Fact]
    public void IModuleConfig_Has_GetConfig_Method_With_Two_String_Params()
    {
        var method = typeof(IModuleConfig).GetMethod("GetConfig",
            new[] { typeof(string), typeof(string) });

        Assert.NotNull(method);
        Assert.Equal(typeof(Dictionary<string, string>), method!.ReturnType);
    }

    [Fact]
    public void IModuleConfig_Has_SetConfigAsync_Method_With_Four_Params()
    {
        var method = typeof(IModuleConfig).GetMethod("SetConfigAsync",
            new[] { typeof(string), typeof(string), typeof(string), typeof(string) });

        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method!.ReturnType);

        var parameters = method.GetParameters();
        Assert.Equal(4, parameters.Length);
        Assert.All(parameters, p => Assert.Equal(typeof(string), p.ParameterType));
    }

    // -----------------------------------------------------------------------
    // 2. IModuleContext surface
    // -----------------------------------------------------------------------

    [Fact]
    public void IModuleContext_ExistsIn_OpenAnima_Contracts_Namespace()
    {
        var type = typeof(IModuleContext);
        Assert.Equal("OpenAnima.Contracts", type.Namespace);
    }

    [Fact]
    public void IModuleContext_Has_ActiveAnimaId_Property_NonNullable_String()
    {
        var property = typeof(IModuleContext).GetProperty("ActiveAnimaId");

        Assert.NotNull(property);
        Assert.Equal(typeof(string), property!.PropertyType);
        Assert.True(property.CanRead);
    }

    [Fact]
    public void IModuleContext_Has_ActiveAnimaChanged_Event_Of_Type_Action()
    {
        var eventInfo = typeof(IModuleContext).GetEvent("ActiveAnimaChanged");

        Assert.NotNull(eventInfo);
        // The event type is Action? (nullable Action delegate)
        Assert.Equal(typeof(Action), eventInfo!.EventHandlerType);
    }

    // -----------------------------------------------------------------------
    // 3. IModuleConfigSchema surface
    // -----------------------------------------------------------------------

    [Fact]
    public void IModuleConfigSchema_ExistsIn_OpenAnima_Contracts_Namespace()
    {
        var type = typeof(IModuleConfigSchema);
        Assert.Equal("OpenAnima.Contracts", type.Namespace);
    }

    [Fact]
    public void IModuleConfigSchema_Has_GetSchema_Method_ReturningReadOnlyListOfConfigFieldDescriptor()
    {
        var method = typeof(IModuleConfigSchema).GetMethod("GetSchema", Type.EmptyTypes);

        Assert.NotNull(method);
        Assert.Equal(typeof(IReadOnlyList<ConfigFieldDescriptor>), method!.ReturnType);
    }

    // -----------------------------------------------------------------------
    // 4. ConfigFieldType enum
    // -----------------------------------------------------------------------

    [Fact]
    public void ConfigFieldType_ExistsIn_OpenAnima_Contracts_Namespace()
    {
        var type = typeof(ConfigFieldType);
        Assert.Equal("OpenAnima.Contracts", type.Namespace);
        Assert.True(type.IsEnum);
    }

    [Fact]
    public void ConfigFieldType_Has_Exactly_Eight_Values()
    {
        var values = Enum.GetValues<ConfigFieldType>();
        Assert.Equal(8, values.Length);
    }

    [Theory]
    [InlineData("String")]
    [InlineData("Int")]
    [InlineData("Bool")]
    [InlineData("Enum")]
    [InlineData("Secret")]
    [InlineData("MultilineText")]
    [InlineData("Dropdown")]
    [InlineData("Number")]
    public void ConfigFieldType_Has_Expected_Value(string valueName)
    {
        Assert.True(Enum.IsDefined(typeof(ConfigFieldType), valueName),
            $"ConfigFieldType.{valueName} should be defined");
    }

    // -----------------------------------------------------------------------
    // 5. ConfigFieldDescriptor record
    // -----------------------------------------------------------------------

    [Fact]
    public void ConfigFieldDescriptor_ExistsIn_OpenAnima_Contracts_Namespace()
    {
        var type = typeof(ConfigFieldDescriptor);
        Assert.Equal("OpenAnima.Contracts", type.Namespace);
        Assert.True(type.IsClass); // Records are classes in C#
    }

    [Fact]
    public void ConfigFieldDescriptor_Constructor_Has_Ten_Parameters()
    {
        // Primary constructor for records is accessible via the primary ctor
        var ctor = typeof(ConfigFieldDescriptor).GetConstructors().FirstOrDefault();

        Assert.NotNull(ctor);
        Assert.Equal(10, ctor!.GetParameters().Length);
    }

    [Fact]
    public void ConfigFieldDescriptor_Can_Be_Instantiated_With_All_Parameters()
    {
        var descriptor = new ConfigFieldDescriptor(
            Key: "api-key",
            Type: ConfigFieldType.Secret,
            DisplayName: "API Key",
            DefaultValue: null,
            Description: "Your secret API key",
            EnumValues: null,
            Group: "Authentication",
            Order: 1,
            Required: true,
            ValidationPattern: null);

        Assert.Equal("api-key", descriptor.Key);
        Assert.Equal(ConfigFieldType.Secret, descriptor.Type);
        Assert.Equal("API Key", descriptor.DisplayName);
        Assert.True(descriptor.Required);
        Assert.Equal(1, descriptor.Order);
    }

    // -----------------------------------------------------------------------
    // 6. ICrossAnimaRouter surface
    // -----------------------------------------------------------------------

    [Fact]
    public void ICrossAnimaRouter_ExistsIn_OpenAnima_Contracts_Routing_Namespace()
    {
        var type = typeof(ICrossAnimaRouter);
        Assert.Equal("OpenAnima.Contracts.Routing", type.Namespace);
    }

    [Fact]
    public void ICrossAnimaRouter_Extends_IDisposable()
    {
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(ICrossAnimaRouter)));
    }

    [Theory]
    [InlineData("RegisterPort")]
    [InlineData("UnregisterPort")]
    [InlineData("GetPortsForAnima")]
    [InlineData("RouteRequestAsync")]
    [InlineData("CompleteRequest")]
    [InlineData("CancelPendingForAnima")]
    [InlineData("UnregisterAllForAnima")]
    public void ICrossAnimaRouter_Has_Required_Method(string methodName)
    {
        var method = typeof(ICrossAnimaRouter).GetMethod(methodName);
        Assert.NotNull(method);
    }

    [Fact]
    public void ICrossAnimaRouter_Has_Exactly_Seven_Methods()
    {
        // GetMethods on interface includes only the interface's own methods, not IDisposable.Dispose
        // We count methods declared directly on ICrossAnimaRouter (excluding Dispose from IDisposable)
        var methods = typeof(ICrossAnimaRouter)
            .GetMethods()
            .Where(m => m.DeclaringType == typeof(ICrossAnimaRouter))
            .ToList();

        Assert.Equal(7, methods.Count);
    }

    // -----------------------------------------------------------------------
    // 7. Routing companion types
    // -----------------------------------------------------------------------

    [Fact]
    public void PortRegistration_ExistsIn_OpenAnima_Contracts_Routing_Namespace()
    {
        var type = typeof(PortRegistration);
        Assert.Equal("OpenAnima.Contracts.Routing", type.Namespace);
        Assert.True(type.IsClass); // Records are classes
    }

    [Fact]
    public void PortRegistration_Has_AnimaId_PortName_Description_Properties()
    {
        Assert.NotNull(typeof(PortRegistration).GetProperty("AnimaId"));
        Assert.NotNull(typeof(PortRegistration).GetProperty("PortName"));
        Assert.NotNull(typeof(PortRegistration).GetProperty("Description"));
    }

    [Fact]
    public void RouteResult_ExistsIn_OpenAnima_Contracts_Routing_Namespace()
    {
        var type = typeof(RouteResult);
        Assert.Equal("OpenAnima.Contracts.Routing", type.Namespace);
    }

    [Fact]
    public void RouteResult_Has_Static_Factory_Methods()
    {
        var okMethod = typeof(RouteResult).GetMethod("Ok",
            BindingFlags.Static | BindingFlags.Public,
            new[] { typeof(string), typeof(string) });
        var failedMethod = typeof(RouteResult).GetMethod("Failed",
            BindingFlags.Static | BindingFlags.Public,
            new[] { typeof(RouteErrorKind), typeof(string) });
        var notFoundMethod = typeof(RouteResult).GetMethod("NotFound",
            BindingFlags.Static | BindingFlags.Public,
            new[] { typeof(string) });

        Assert.NotNull(okMethod);
        Assert.NotNull(failedMethod);
        Assert.NotNull(notFoundMethod);
    }

    [Fact]
    public void RouteRegistrationResult_ExistsIn_OpenAnima_Contracts_Routing_Namespace()
    {
        var type = typeof(RouteRegistrationResult);
        Assert.Equal("OpenAnima.Contracts.Routing", type.Namespace);
    }

    [Fact]
    public void RouteRegistrationResult_Has_Static_Factory_Methods()
    {
        var successMethod = typeof(RouteRegistrationResult).GetMethod("Success",
            BindingFlags.Static | BindingFlags.Public,
            Type.EmptyTypes);
        var errorMethod = typeof(RouteRegistrationResult).GetMethod("DuplicateError",
            BindingFlags.Static | BindingFlags.Public,
            new[] { typeof(string) });

        Assert.NotNull(successMethod);
        Assert.NotNull(errorMethod);
    }

    [Fact]
    public void RouteErrorKind_ExistsIn_OpenAnima_Contracts_Routing_Namespace()
    {
        var type = typeof(RouteErrorKind);
        Assert.Equal("OpenAnima.Contracts.Routing", type.Namespace);
        Assert.True(type.IsEnum);
    }

    [Fact]
    public void RouteErrorKind_Has_Exactly_Four_Values()
    {
        var values = Enum.GetValues<RouteErrorKind>();
        Assert.Equal(4, values.Length);
    }

    [Theory]
    [InlineData("Timeout")]
    [InlineData("NotFound")]
    [InlineData("Cancelled")]
    [InlineData("Failed")]
    public void RouteErrorKind_Has_Expected_Value(string valueName)
    {
        Assert.True(Enum.IsDefined(typeof(RouteErrorKind), valueName),
            $"RouteErrorKind.{valueName} should be defined");
    }

    // -----------------------------------------------------------------------
    // 8. DI resolution (integration-style)
    // -----------------------------------------------------------------------

    [Fact]
    public void DI_Resolves_IModuleContext_From_AnimaContext()
    {
        var services = new ServiceCollection();
        services.AddSingleton<AnimaContext>();
        services.AddSingleton<IModuleContext>(sp => sp.GetRequiredService<AnimaContext>());

        using var provider = services.BuildServiceProvider();

        var context = provider.GetRequiredService<IModuleContext>();
        Assert.NotNull(context);
    }

    [Fact]
    public async Task DI_Resolves_IModuleConfig_From_AnimaModuleConfigService()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"contracts-api-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<AnimaModuleConfigService>(sp => new AnimaModuleConfigService(tempDir));
            services.AddSingleton<IModuleConfig>(sp => sp.GetRequiredService<AnimaModuleConfigService>());

            await using var provider = services.BuildServiceProvider();

            var config = provider.GetRequiredService<IModuleConfig>();
            Assert.NotNull(config);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void DI_IModuleContext_And_IAnimaContext_Resolve_To_Same_Instance()
    {
        var services = new ServiceCollection();
        services.AddSingleton<AnimaContext>();
        services.AddSingleton<IModuleContext>(sp => sp.GetRequiredService<AnimaContext>());
        services.AddSingleton<IAnimaContext>(sp => sp.GetRequiredService<AnimaContext>());

        using var provider = services.BuildServiceProvider();

        var asModuleContext = provider.GetRequiredService<IModuleContext>();
        var asAnimaContext = provider.GetRequiredService<IAnimaContext>();

        // Same underlying singleton instance — same reference
        Assert.Same(asModuleContext, asAnimaContext);
    }

    [Fact]
    public async Task DI_IModuleConfig_And_IAnimaModuleConfigService_Resolve_To_Same_Instance()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"contracts-api-di-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<AnimaModuleConfigService>(sp => new AnimaModuleConfigService(tempDir));
            services.AddSingleton<IModuleConfig>(sp => sp.GetRequiredService<AnimaModuleConfigService>());
            services.AddSingleton<IAnimaModuleConfigService>(sp => sp.GetRequiredService<AnimaModuleConfigService>());

            await using var provider = services.BuildServiceProvider();

            var asModuleConfig = provider.GetRequiredService<IModuleConfig>();
            var asAnimaConfig = provider.GetRequiredService<IAnimaModuleConfigService>();

            // Same underlying singleton instance — same reference
            Assert.Same(asModuleConfig, asAnimaConfig);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    // -----------------------------------------------------------------------
    // 9. Contracts isolation
    // -----------------------------------------------------------------------

    [Fact]
    public void IModuleConfig_And_IModule_Are_In_Same_Assembly()
    {
        Assert.Equal(typeof(IModule).Assembly, typeof(IModuleConfig).Assembly);
    }

    [Fact]
    public void ICrossAnimaRouter_And_IModule_Are_In_Same_Assembly()
    {
        Assert.Equal(typeof(IModule).Assembly, typeof(ICrossAnimaRouter).Assembly);
    }

    [Fact]
    public void IModuleContext_And_IModule_Are_In_Same_Assembly()
    {
        Assert.Equal(typeof(IModule).Assembly, typeof(IModuleContext).Assembly);
    }

    [Fact]
    public void IModuleConfigSchema_And_IModule_Are_In_Same_Assembly()
    {
        Assert.Equal(typeof(IModule).Assembly, typeof(IModuleConfigSchema).Assembly);
    }

    [Fact]
    public void ConfigFieldDescriptor_And_IModule_Are_In_Same_Assembly()
    {
        Assert.Equal(typeof(IModule).Assembly, typeof(ConfigFieldDescriptor).Assembly);
    }
}
