using System.Net;
using Capnp.Rpc;
using Mas.Infrastructure.Common;
using Mas.Infrastructure.ServiceRegistry;
using Mas.Schema.Storage;
using Admin = Mas.Infrastructure.ServiceRegistry.Admin;
using Exception = System.Exception;
using Registrar = Mas.Infrastructure.ServiceRegistry.Registrar;
using Restorer = Mas.Infrastructure.Common.Restorer;


var id = Guid.NewGuid().ToString();
var name = id;
var desc = "";
Reg regs = new();
var categoriesContainerSr = "";
var restorerContainerSr = "";
var tcpPort = 42000;
var readRegSRsFromStdIn = false;
string regFilePath = null; // "./regs.json"; 
var checkIp = "8.8.8.8";
var checkPort = 53;

// Reading in Input parameters
foreach (var arg in args)
    try
    {
        if (arg.StartsWith("--id")) id = arg.Split('=')[1];
        else if (arg.StartsWith("--name")) name = arg.Split('=')[1];
        else if (arg.StartsWith("--description")) desc = arg.Split('=')[1];
        else if (arg.StartsWith("--categories_container_sr")) restorerContainerSr = arg.Split('=')[1];
        else if (arg.StartsWith("--restorer_container_sr")) categoriesContainerSr = arg.Split('=')[1];
        else if (arg.StartsWith("--port")) tcpPort = int.Parse(arg.Split('=')[1]);
        // else if (arg.StartsWith("--registrar_sr")) regs.Registry.RegSr = arg.Split('=')[1];
        // else if (arg.StartsWith("--reg_name")) regs.Registry.RegName = arg.Split('=')[1];
        // else if (arg.StartsWith("--reg_category")) regs.Registry.CatId = arg.Split('=')[1];
        // else if (arg.StartsWith("--regstdin")) readRegSRsFromStdIn = true;
        // else if (arg.StartsWith("--regfile")) regFilePath = arg.Split('=')[1];
        else if (arg.StartsWith("--check_IP")) checkIp = arg.Split('=')[1];
        else if (arg.StartsWith("--check_port")) checkPort = int.Parse(arg.Split('=')[1]);
    }
    catch (Exception)
    {
    }

// if (readRegSRsFromStdIn && Console.IsInputRedirected)
// {
//     var regsJson = new StringBuilder();
//     while (true)
//     {
//         var s = Console.ReadLine();
//         if (s == null) break;
//         regsJson.Append(s);
//     }
//     regs = DeserializeRegs(regsJson.ToString());
// }
// else if (regFilePath != null)
// {
//     var regsJson = await File.ReadAllTextAsync(regFilePath);
//     regs = DeserializeRegs(regsJson);
// }

var restorer = new Restorer { TcpHost = ConnectionManager.GetLocalIPAddress(checkIp, checkPort) };
using var conMan = new ConnectionManager(restorer);

var registry = new ServiceRegistry
{
    Restorer = restorer,
    Id = id,
    Name = name,
    Description = desc
};

conMan.Bind(IPAddress.Any, tcpPort, restorer);
restorer.TcpPort = conMan.Port;

try
{
    if (categoriesContainerSr != "")
        registry.SetCategoriesStorage(
            await conMan.Connect<Store.IContainer>(categoriesContainerSr));
    if (restorerContainerSr != "")
        restorer.StorageContainer =
            await conMan.Connect<Store.IContainer>(restorerContainerSr);
}


catch (Exception e)
{
    Console.WriteLine($"Exception trying to register registry SR. Exception {e}");
}

Console.WriteLine("Started ServiceRegistry with these Categories:");
foreach (var cat in registry.Categories) Console.WriteLine(cat.Id);
var registrySturdyRef = restorer.SaveStr(BareProxy.FromImpl(registry)).Item1;
Console.WriteLine($"registry_sr: {registrySturdyRef}");
// if (regs.Registry != null && regs.Registry.RegSr != "")
//     await TryRegisterService(conMan, regs.Registry, registry);


var registrar = new Registrar(registry, restorer);
var regSturdyRef = restorer.SaveStr(BareProxy.FromImpl(registrar)).Item1;
Console.WriteLine($"registrar_sr: {regSturdyRef}");
//await TryRegisterService(conMan, regs.registrar, registrar);


var registryAdmin = new Admin(registry);
var registryAdminSturdyRef = restorer.SaveStr(BareProxy.FromImpl(registryAdmin)).Item1;
Console.WriteLine($"registry_admin_sr: {registryAdminSturdyRef}");
//await TryRegisterService(conMan, regs.registry_admin, registryAdmin);


var serviceAdmin = new Mas.Infrastructure.Common.Service.Admin(registry, info =>
{
    registry.Id = info.Id;
    registry.Name = info.Name;
    registry.Description = info.Description;
});


var serviceAdminSturdyRef = restorer.SaveStr(BareProxy.FromImpl(serviceAdmin)).Item1;
Console.WriteLine($"service_admin_sr: {serviceAdminSturdyRef}");
//await TryRegisterService(conMan, regs.service_admin, serviceAdmin);            

while (true) Thread.Sleep(1000);

// static Reg DeserializeRegs(string regsJsonStr)
// {
//     return JsonSerializer.Deserialize<Reg>(regsJsonStr,
//         new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
// }
//
// static async Task TryRegisterService(ConnectionManager conMan, RegData r,
//     IIdentifiable service)
// {
//     if (r != null)
//         try
//         {
//             var remReg = await conMan.Connect<IRegistrar>(r.RegSr);
//             var regParams = new Schema.Registry.Registrar.RegParams
//             {
//                 Cap = service, RegName = r.RegName, CategoryId = r.CatId
//             };
//             var res = await remReg.Register(regParams);
//             r.UnregisterCapability = res.Item1;
//             r.ReregSr = res.Item2;
//         }
//         catch (Exception e)
//         {
//             Console.WriteLine($"Exception trying to register registry SR. Exception {e}");
//         }
// }


internal struct Reg
{
    public RegData Registry { get; set; }
    //public RegData registrar { get; set; }
    //public RegData registry_admin { get; set; }
    //public RegData service_admin { get; set; }
}