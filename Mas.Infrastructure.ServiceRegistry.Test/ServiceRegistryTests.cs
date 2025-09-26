using System.Net;
using Capnp.Rpc;
using Mas.Infrastructure.ServiceRegistry; // for ServiceRegistry, Admin, Registrar
using Mas.Infrastructure.Common;
using Mas.Rpc.Test;
using Mas.Schema.Common;
using Mas.Schema.Registry;

namespace Mas.Infrastructure.ServiceRegistry.Test;

[TestClass]
public class ServiceRegistryTests
{
    private const int Port = 42000; // keep fixed to match existing test URIs

    // In-process host state
    private static ConnectionManager? _serverConMan;
    private static Restorer? _restorer;
    private static ServiceRegistry? _registry;
    private static Registrar? _registrar;
    private static Admin? _admin;
    private static bool _started;

    [ClassInitialize]
    public static void ClassInit(TestContext ctx)
    {
        if (_started) return;
        _restorer = new Restorer { TcpHost = ConnectionManager.GetLocalIPAddress() };
        _serverConMan = new ConnectionManager();
        _registry = new ServiceRegistry(new IdInformation
        {
            Id = "TestRegistryId",
            Name = "TestRegistry",
            Description = "Test Registry for automated tests"
        }, _restorer);

        // Bind server on requested port exposing restorer as bootstrap (mirrors Program.cs)
        _serverConMan.Bind(IPAddress.Any, Port, _restorer);
        _restorer.TcpPort = _serverConMan.Port;

        // Create and publish sturdy refs with fixed tokens so client side can connect identically to production startup
        var registrySr = _restorer.SaveStr(BareProxy.FromImpl(_registry), "registry").Item1;
        _registrar = new Registrar(_registry, _restorer);
        var registrarSr = _restorer.SaveStr(BareProxy.FromImpl(_registrar), "registrar").Item1;
        _admin = new Admin(_registry);
        var registryAdminSr = _restorer.SaveStr(BareProxy.FromImpl(_admin), "registry_admin").Item1;

        Console.WriteLine(
            $"[TestHost] ServiceRegistry started. SRs:\n registry={registrySr}\n registrar={registrarSr}\n registry_admin={registryAdminSr}");
        _started = true;
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void ClassCleanup()
    {
        try
        {
            _serverConMan?.Dispose();
        }
        catch
        {
            /* ignore */
        }

        _started = false;
    }

    [TestMethod]
    public async Task RegisterService()
    {
        var testServiceInformation = new IdInformation
        {
            Description = "I can say hello",
            Name = "Hello Service",
            Id = "HelloId"
        };

        var testCategory = new IdInformation
        {
            Description = "I am a Test Category",
            Name = "Test",
            Id = "Test"
        };


        var testService = new TestService(testServiceInformation);

        var connectionManager = new ConnectionManager();

        var admin = await connectionManager.Connect<IAdmin>(
            $"capnp://{ConnectionManager.GetLocalIPAddress()}:42000/registry_admin");

        var created = await admin.AddCategory(testCategory, true);

        Console.WriteLine("Created Test Category: " + created);

        var registry = await admin.Registry();

        var registrar =
            await connectionManager.Connect<IRegistrar>(
                $"capnp://{ConnectionManager.GetLocalIPAddress()}:42000/registrar");

        var regParams = new Schema.Registry.Registrar.RegParams
        {
            CategoryId = "Test",
            Cap = testService,
            RegName = "Hello Service"
        };

        var (unregisterCapability, _) = await registrar.Register(regParams);

        var entries = await registry.Entries("Test");

        Console.WriteLine("Before Unregister");
        foreach (var entry in entries)
        {
            Console.WriteLine(entry.Name);
            if (entry.Ref is not Proxy p) continue;
            Console.WriteLine("Is Proxy");
            var returnedTestService = p.Cast<IA>(false);
            // var info = await returnedTestService.Info();    
            // Console.WriteLine($"Returned service info: {info.Id} {info.Name} {info.Description}");
            var result = await returnedTestService.Method("Universe");
            Console.WriteLine("Returned service method returned: " + result);
        }

        var unregisterResult = await unregisterCapability.Unregister();


        Console.WriteLine("After Unregister");
        Console.WriteLine($"Unregister result: {unregisterResult}");

        entries = await registry.Entries("Test");
        foreach (var entry in entries) Console.WriteLine(entry.Name);
    }
}