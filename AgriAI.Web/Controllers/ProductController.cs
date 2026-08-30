using System.Security.Claims;
using AgriAI.Application.Interfaces;
using AgriAI.Domain.Entities;
using AgriAI.Infrastructure.Data;
using AgriAI.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AgriAI.Web.Controllers
{
    public class ProductController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProductController(IUnitOfWork unitOfWork, ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _context = context;
            _userManager = userManager;
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

            // Fetch Farmer details & inventory statistics
            ApplicationUser? farmerUser = null;
            if (!string.IsNullOrEmpty(product.FarmerId))
            {
                farmerUser = await _userManager.FindByIdAsync(product.FarmerId)
                          ?? await _userManager.FindByEmailAsync(product.FarmerId)
                          ?? await _userManager.FindByNameAsync(product.FarmerId);
            }

            if (farmerUser == null)
            {
                var farmersInRole = await _userManager.GetUsersInRoleAsync("Farmer");
                farmerUser = farmersInRole.FirstOrDefault();
            }

            // Get total farmer harvest counts & total inventory stock
            int farmerTotalProducts = 0;
            int farmerTotalStock = 0;
            if (!string.IsNullOrEmpty(product.FarmerId))
            {
                var farmerProducts = await _context.Products.Where(p => p.FarmerId == product.FarmerId).ToListAsync();
                farmerTotalProducts = farmerProducts.Count;
                farmerTotalStock = farmerProducts.Sum(p => p.StockQuantity);
            }

            if (farmerTotalProducts == 0)
            {
                farmerTotalProducts = 1;
                farmerTotalStock = product.StockQuantity;
            }

            ViewBag.FarmerUser = farmerUser;
            ViewBag.FarmerTotalProducts = farmerTotalProducts;
            ViewBag.FarmerTotalStock = farmerTotalStock;

            return View(product);
        }

        // GET: Product/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (User.IsInRole("Farmer"))
            {
                return RedirectToAction("EditProduct", "Farmer", new { id });
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                TempData["ErrorMessage"] = "Product not found.";
                return RedirectToAction(nameof(Index));
            }

            var categories = await _context.Categories.ToListAsync();
            ViewData["CategoryId"] = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(categories, "Id", "Name", product.CategoryId);
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

        // POST: Product/UpdateStock
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStock(int id, int stockQuantity, decimal? price, string? returnUrl)
        {
            if (User.IsInRole("Admin"))
            {
                TempData["ErrorMessage"] = "Administrators cannot update produce stock. Stock is managed by the farmer.";
                return Redirect("/Admin/Dashboard?tab=adminProductsTab");
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                TempData["ErrorMessage"] = "Product not found.";
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction(nameof(Index));
            }

            if (stockQuantity < 0)
            {
                TempData["ErrorMessage"] = "Stock quantity cannot be negative.";
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                return RedirectToAction(nameof(Details), new { id });
            }

            product.StockQuantity = stockQuantity;
            if (price.HasValue && price.Value >= 0)
            {
                product.Price = price.Value;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Stock for '{product.Name}' updated to {stockQuantity} units successfully!";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            if (User.IsInRole("Farmer"))
            {
                return Redirect("/Farmer/Dashboard?tab=farmerProductsTab");
            }

            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
