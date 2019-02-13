using System.IO;
using Microsoft.AspNetCore.Mvc;

namespace RZ.Server.Controllers
{
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public class DLController : Controller
    {
        //public IActionResult Index()
        //{
        //    return View();
        //}

        [HttpGet]
        [Route("DL/{filename}")]
        public IActionResult DL(string filename)
        {
            return PhysicalFile(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "DL", filename), "application/octet-stream");
        }
    }
}