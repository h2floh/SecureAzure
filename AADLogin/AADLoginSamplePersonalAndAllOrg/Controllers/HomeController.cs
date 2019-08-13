using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AADLoginSamplePersonalAndAllOrg.Models;
using Microsoft.Identity.Web.Client;
using AADLoginSamplePersonalAndAllOrg.Services;
using Microsoft.Extensions.Options;
using AADLoginSamplePersonalAndAllOrg.Infrastructure;
using Microsoft.Graph;
using Microsoft.Extensions.Configuration;

namespace AADLoginSamplePersonalAndAllOrg.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        readonly ITokenAcquisition tokenAcquisition;
        readonly WebOptions webOptions;
        readonly IConfiguration configuration;

        public HomeController(ITokenAcquisition tokenAcquisition,
                              IOptions<WebOptions> webOptionValue, IConfiguration configuration)
        {
            this.tokenAcquisition = tokenAcquisition;
            this.webOptions = webOptionValue.Value;
            this.configuration = configuration;
        }

        [AllowAnonymous]
        [MsalUiRequiredExceptionFilter(Scopes = new[] { GraphScopes.DirectoryReadAll })]
        public IActionResult Index()
        {
            var claims = ((System.Security.Claims.ClaimsIdentity)User.Identity).Claims;

            ViewBag.Message = "Your app roles.";
            ViewData["myconfig"] = configuration["myconfig"];
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
