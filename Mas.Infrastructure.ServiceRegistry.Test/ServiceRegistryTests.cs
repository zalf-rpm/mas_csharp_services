using Mas.Infrastructure.Common;
using Mas.Schema.Common;
using Mas.Schema.Registry;

namespace Mas.Infrastructure.ServiceRegistry;

[TestClass]
public class ServiceRegistryTests
{
    [TestMethod]
    public void RegisterService()
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

        var admin = connectionManager.Connect<IAdmin>(
                "capnp://i1g7XsHI1TjS_t6Sxm6s5JKcXIcvO0oVjnLyWFm3eco@192.168.109.176:42000/281d9a2a-e9e0-4dc4-aab6-b4343c924ec5")
            .Result;

        var created = admin.AddCategory(testCategory, true).Result;

        Console.WriteLine(created);

        var registrar = connectionManager.Connect<IRegistrar>(
                "capnp://i1g7XsHI1TjS_t6Sxm6s5JKcXIcvO0oVjnLyWFm3eco@192.168.109.176:42000/d9e808b5-c5f7-4a52-8b7d-f62c6a75c37a")
            .Result;

        var regParams = new Schema.Registry.Registrar.RegParams
        {
            CategoryId = "Test",
            Cap = testService,
            RegName = "Hello Service"
        };


        var (unreg, sturdyref) = registrar.Register(regParams).Result;

        var registry = connectionManager.Connect<IRegistry>(
                "capnp://i1g7XsHI1TjS_t6Sxm6s5JKcXIcvO0oVjnLyWFm3eco@192.168.109.176:42000/695d27a7-5c5d-451e-aa77-2078dc1b5c54")
            .Result;
        var entries = registry.Entries("Test").Result;

        Console.WriteLine("Before Unregister");
        foreach (var entry in entries)
        {
            Console.WriteLine(entry.Name);
            var serviceFromReg = entry;
        }

        var unregisterResult = unreg.Unregister().Result;

        Console.WriteLine("After Unregister");
        Console.WriteLine(unregisterResult);

        entries = registry.Entries("Test").Result;
        foreach (var entry in entries)
        {
            Console.WriteLine(entry.Name);
            var serviceFromReg = entry;
        }
    }
}