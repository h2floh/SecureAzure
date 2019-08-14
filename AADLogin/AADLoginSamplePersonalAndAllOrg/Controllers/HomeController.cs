using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AADLoginSamplePersonalAndAllOrg.Models;
using Microsoft.Extensions.Configuration;

namespace AADLoginSamplePersonalAndAllOrg.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        readonly IConfiguration configuration;

        public HomeController(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        [AllowAnonymous]
        public IActionResult Index()
        {
            var claims = ((System.Security.Claims.ClaimsIdentity)User.Identity).Claims;

            // App Roles
            ViewBag.Message = "Your app roles.";
            
            try
            {
                var list = new List<string>();
                foreach(var claim in claims)
                {
                    list.Add(claim.Type + ":" + claim.Value);
                }

                ViewData["appRoles"] = list;

            }
            catch (Exception e)
            {
                var exceptions = new List<String>();
                exceptions.Add(e.Message);
                ViewData["appRoles"] = exceptions;
            }

            return View();
        }

        [Authorize(Roles = "read, write")]
        public IActionResult AdminArea()
        {
            if (User.IsInRole("write"))
            {
                // Config Values
                try
                {
                    var list = new List<string>();
                    foreach (var item in configuration.AsEnumerable())
                    {
                        list.Add(item.Key + ":" + item.Value);
                    }

                    ViewData["appConfig"] = list;
                }
                catch (Exception e)
                {
                    var exceptions = new List<String>();
                    exceptions.Add(e.Message);
                    ViewData["appRoles"] = exceptions;
                }
            }

            return View();
        }

        [Authorize(Roles = "read")]
        public IActionResult Privacy()
        {
            return View();
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
