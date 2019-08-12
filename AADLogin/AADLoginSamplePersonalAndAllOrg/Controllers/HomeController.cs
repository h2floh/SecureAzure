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

namespace AADLoginSamplePersonalAndAllOrg.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        readonly ITokenAcquisition tokenAcquisition;
        readonly WebOptions webOptions;

        public HomeController(ITokenAcquisition tokenAcquisition,
                              IOptions<WebOptions> webOptionValue)
        {
            this.tokenAcquisition = tokenAcquisition;
            this.webOptions = webOptionValue.Value;
        }

        [AllowAnonymous]
        [MsalUiRequiredExceptionFilter(Scopes = new[] { GraphScopes.DirectoryReadAll })]
        public async Task<IActionResult> Index()
        {
            var claims = ((System.Security.Claims.ClaimsIdentity)User.Identity).Claims;

            ViewBag.Message = "Your app roles.";
            try
            {
                string[] scopes = new[] { GraphScopes.DirectoryReadAll };

                GraphServiceClient graphServiceClient = GraphServiceClientFactory.GetAuthenticatedGraphClient(async () =>
                {
                    string result = await tokenAcquisition.GetAccessTokenOnBehalfOfUser(
                           HttpContext, scopes);
                    return result;
                }, webOptions.GraphApiUrl);

                var groups = await graphServiceClient.Me.MemberOf.Request().GetAsync();

                ViewData["appRoles"] = groups.CurrentPage;

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
