using Mas.Infrastructure.Common;
using Mas.Schema.Common;
using Mas.Schema.Registry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
                "capnp://hufg0o685tG3EBdwD5XcfJsBwMx09sJmNzjvaIUEvdU@192.168.109.176:32789/529f04b2-d165-4d14-ab8a-5184b024cca7")
            .Result;

        var created = admin.AddCategory(testCategory, true).Result;

        Console.WriteLine(created);

        var registrar = connectionManager.Connect<IRegistrar>(
                "capnp://hufg0o685tG3EBdwD5XcfJsBwMx09sJmNzjvaIUEvdU@192.168.109.176:32789/4a6a56b6-8eb7-426d-968c-b9c4c187bdd5")
            .Result;

        var regParams = new Schema.Registry.Registrar.RegParams
        {
            CategoryId = "Test",
            Cap = testService,
            RegName = "Hello Service"
        };


        var (unreg, sturdyref) = registrar.Register(regParams).Result;

        var registry = connectionManager.Connect<IRegistry>(
                "capnp://hufg0o685tG3EBdwD5XcfJsBwMx09sJmNzjvaIUEvdU@192.168.109.176:32789/ac13efca-bb2f-4e78-82d5-c58bba0526ba")
            .Result;
        var entries = registry.Entries("Test").Result;

        foreach (var entry in entries)
        {
            Console.WriteLine(entry.Name);
            var serviceFromReg = entry;
        }
    }
}