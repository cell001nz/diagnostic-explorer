using System.Diagnostics;
using DiagnosticExplorer;
using DiagnosticExplorer.Common;
using Diagnostics.Service.Common.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Diagnostics.Service.Common.Controllers;

[ApiController]
[Route("[controller]")]
[Route("[controller]/action/{action}")]
public class RegistrationController : ControllerBase
{
    private readonly RealtimeManager _manager;
    private RealtimeOptions _config;

    public RegistrationController(RealtimeManager manager, IOptions<RealtimeOptions> config)
    {
        _manager = manager;
        _config = config.Value;
    }

    [HttpPost]
    public IActionResult Register([FromBody] Registration registration)
    {
        // Trace.WriteLine($"Register redirection = {_config.RegistrationRedirect}");

        // if (!string.IsNullOrWhiteSpace(_config.RegistrationRedirect))
            // return Redirect($"{_config.RegistrationRedirect}/action/Register");

        return Ok(_manager.RegisterOld(registration));
    }


    [HttpPost]
    public IActionResult Deregister(Registration registration)
    {
        _manager.DeregisterOld(registration);
        return Ok();
    }
}