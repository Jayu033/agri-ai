using System.Security.Claims;
using AgriAI.Application.Interfaces;
using AgriAI.Domain.Entities;
using AgriAI.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AgriAI.Web.Controllers
{
    public class ProductController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ApplicationDbContext _context;

        public ProductController(IUnitOfWork unitOfWork, ApplicationDbContext context)
        {
            _unitOfWork = unitOfWork;
            _context = context;
        }

        // GET: Product
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .ToListAsync();
            return View(products);
        }

        // GET: Product/Create
        public async Task<IActionResult> Create()
        {
            // Only Farmers and testing users can add products; Admins CANNOT add products
            if (User.IsInRole("Admin"))
            {
                TempData["ErrorMessage"] = "Only Farmers can add products. Admins cannot add products.";
                return RedirectToAction(nameof(Index));
            }

            var categories = await _context.Categories.ToListAsync();
            if (!categories.Any())
            {
                categories = new List<Category>
                {
                    new Category { Name = "Vegetables", Description = "Fresh organic vegetables" },
                    new Category { Name = "Fruits", Description = "Fresh farm fruits" },
                    new Category { Name = "Grains", Description = "Quality grains and pulses" },
                    new Category { Name = "Dairy", Description = "Fresh milk and dairy products" },
                    new Category { Name = "Fertilizers", Description = "Organic fertilizers" },
                    new Category { Name = "Seeds", Description = "High-yield seeds" },
                    new Category { Name = "Equipment", Description = "Farming tools and machinery" }
                };
                await _context.Categories.AddRangeAsync(categories);
                await _context.SaveChangesAsync();
                categories = await _context.Categories.ToListAsync();
            }

            ViewData["CategoryId"] = new SelectList(categories, "Id", "Name");
            return View();
        }

        // POST: Product/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description,Price,StockQuantity,CategoryId,FarmerId")] Product product, IFormFile? imageFile)
        {
            if (User.IsInRole("Admin"))
            {
                TempData["ErrorMessage"] = "Only Farmers can add products. Admins cannot add products.";
                return RedirectToAction(nameof(Index));
            }

            // Remove navigation properties from ModelState validation
            ModelState.Remove("Category");
            ModelState.Remove("Images");
            ModelState.Remove("Reviews");

            // Set FarmerId from logged in user if available
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    product.FarmerId = userId;
                }
            }
            if (string.IsNullOrEmpty(product.FarmerId))
            {
                product.FarmerId = "farmer-123";
            }
            ModelState.Remove("FarmerId");

            if (ModelState.IsValid)
            {
                try
                {
                    // Handle Image File Upload
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "products");
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(imageFile.FileName);
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(fileStream);
                        }

                        product.Images.Add(new ProductImage
                        {
                            ImageUrl = "/images/products/" + uniqueFileName,
                            IsPrimary = true
                        });
                    }

                    await _context.Products.AddAsync(product);
                    await _context.SaveChangesAsync();
                    
                    TempData["SuccessMessage"] = $"Crop produce '{product.Name}' listed successfully!";
                    if (!User.IsInRole("Admin"))
                    {
                        return RedirectToAction("Dashboard", "Farmer");
                    }
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error saving product: " + ex.Message);
                }
            }

            var categoriesList = await _context.Categories.ToListAsync();
            ViewData["CategoryId"] = new SelectList(categoriesList, "Id", "Name", product.CategoryId);
            return View(product);
        }

        // GET: Product/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // POST: Product/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            
            if (product == null)
            {
                TempData["ErrorMessage"] = "Product not found.";
                return RedirectToAction(nameof(Index));
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Product '{product.Name}' deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
