using InventoryControl.Infrastructure.Integrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryControl.Controllers;

[Authorize(Roles = "Admin")]
public class StoresController : Controller
{
    private readonly PlatformRegistry _registry;

    public StoresController(PlatformRegistry registry)
    {
        _registry = registry;
    }

    public IActionResult Index()
    {
        var stores = _registry.GetAllStores();
        ViewBag.RegisteredPlatforms = _registry.GetRegisteredPlatforms().ToList();
        return View(stores);
    }
}
