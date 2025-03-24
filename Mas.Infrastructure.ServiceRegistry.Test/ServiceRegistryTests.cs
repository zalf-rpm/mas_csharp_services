using Mas.Infrastructure.Common;
using Mas.Schema.Common;
using Mas.Schema.Registry;

namespace Mas.Infrastructure.ServiceRegistry.Test;

[TestClass]
public class ServiceRegistryTests

{
    [TestInitialize]
    public void AdminTest()
    {
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
            "capnp://ucIK3RykCwpfEDL9OFS2FZlRk7UCG-2G8KfBy-HR1jA@192.168.109.176:42000/30ec188b-53f9-492b-9a00-ef0fbf1b5165");

        var created = await admin.AddCategory(testCategory, true);

        Console.WriteLine("Created Test Category: " + created);

        var registry = await admin.Registry();

        var registrar = await connectionManager.Connect<IRegistrar>(
            "capnp://ucIK3RykCwpfEDL9OFS2FZlRk7UCG-2G8KfBy-HR1jA@192.168.109.176:42000/55fcc390-219c-4bbe-9e33-8f4ca1097868");

        var regParams = new Schema.Registry.Registrar.RegParams
        {
            CategoryId = "Test",
            Cap = testService,
            RegName = "Hello Service"
        };


        var (unreg, sturdyref) = await registrar.Register(regParams);


        var entries = await registry.Entries("Test");

        Console.WriteLine("Before Unregister");
        foreach (var entry in entries)
        {
            Console.WriteLine(entry.Name);
            var info = await entry.Ref.Info();
            Console.WriteLine(info.Description);
        }

        var unregisterResult = await unreg.Unregister();

        Console.WriteLine("After Unregister");
        Console.WriteLine(unregisterResult);

        entries = await registry.Entries("Test");
        foreach (var entry in entries) Console.WriteLine(entry.Name);
    }
}