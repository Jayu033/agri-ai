using System.Diagnostics;
using AgriAI.Infrastructure.Data;
using AgriAI.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriAI.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .ToListAsync();
            return View(products);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Helpdesk()
        {
            return View("About");
        }

        public async Task<IActionResult> LiveMandi([FromServices] AgriAI.Web.Services.ILiveMandiService mandiService)
        {
            var rates = await mandiService.GetLiveGujaratMandiRatesAsync(150);
            return View(rates);
        }

        [HttpGet]
        public async Task<IActionResult> GetLiveMandiRates([FromServices] AgriAI.Web.Services.ILiveMandiService mandiService)
        {
            var rates = await mandiService.GetLiveGujaratMandiRatesAsync(150);
            return Json(rates);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
