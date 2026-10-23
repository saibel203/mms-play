using Microsoft.AspNetCore.Mvc;
using MMS.Play.Presentation.Controllers.Base;

namespace MMS.Play.Presentation.Controllers;

public class TestController : BaseController
{
    [HttpGet("get")]
    public IActionResult Get()
    {
        return Ok("Tesat");
    }
}
