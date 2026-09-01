using System.Security.Claims;
using AgriAI.Domain.Entities;
using AgriAI.Infrastructure.Data;
using AgriAI.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriAI.Web.Controllers
{
    public class HelpdeskController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HelpdeskController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Helpdesk or /Home/Helpdesk
        [HttpGet]
        [Route("Helpdesk")]
        [Route("Home/Helpdesk")]
        public async Task<IActionResult> Index(string? filter)
        {
            await EnsureTicketTableExistsAsync();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUser = currentUserId != null ? await _userManager.FindByIdAsync(currentUserId) : null;
            var currentEmail = currentUser?.Email ?? User.Identity?.Name;

            bool isAdmin = User.IsInRole("Admin");
            bool isFarmer = User.IsInRole("Farmer");
            bool isCustomer = User.IsInRole("Customer");

            ViewBag.IsAdmin = isAdmin;
            ViewBag.IsFarmer = isFarmer;
            ViewBag.IsCustomer = isCustomer;
            ViewBag.CurrentUser = currentUser;
            ViewBag.ActiveFilter = filter ?? "all";

            if (isAdmin)
            {
                var query = _context.SupportTickets.AsQueryable();

                if (filter == "payouts")
                {
                    query = query.Where(t => t.Category == "Escrow & Payout");
                }
                else if (filter == "orders")
                {
                    query = query.Where(t => t.Category == "Quality Issue" || t.Category == "Delivery Delay");
                }
                else if (filter == "guest")
                {
                    query = query.Where(t => t.UserRole == "Guest");
                }
                else if (filter == "resolved")
                {
                    query = query.Where(t => t.Status == "Resolved");
                }

                var allTickets = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
                var allDbTickets = await _context.SupportTickets.ToListAsync();

                ViewBag.TotalCount = allDbTickets.Count;
                ViewBag.PayoutDisputesCount = allDbTickets.Count(t => t.Category == "Escrow & Payout" && t.Status != "Resolved");
                ViewBag.CustomerIssuesCount = allDbTickets.Count(t => (t.Category == "Quality Issue" || t.Category == "Delivery Delay") && t.Status != "Resolved");
                ViewBag.GuestQueriesCount = allDbTickets.Count(t => t.UserRole == "Guest" && t.Status != "Resolved");
                ViewBag.ResolvedCount = allDbTickets.Count(t => t.Status == "Resolved");

                // Load all related orders for instant inspection modal
                var orderIds = allDbTickets.Where(t => t.OrderId.HasValue).Select(t => t.OrderId!.Value).Distinct().ToList();
                var relatedOrders = await _context.Orders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                    .Where(o => orderIds.Contains(o.Id))
                    .ToDictionaryAsync(o => o.Id, o => o);

                ViewBag.RelatedOrders = relatedOrders;

                return View(allTickets);
            }
            else if (isFarmer)
            {
                var myTickets = await _context.SupportTickets
                    .Where(t => (currentUserId != null && t.UserId == currentUserId) || (currentEmail != null && t.SenderEmail == currentEmail))
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();

                // Farmer's listed produce orders for dispute dropdown
                var farmerOrders = await _context.Orders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                    .Where(o => o.OrderItems.Any(oi => oi.Product != null && (oi.Product.FarmerId == currentUserId || oi.Product.FarmerId == currentEmail)))
                    .OrderByDescending(o => o.OrderDate)
                    .Take(20)
                    .ToListAsync();

                ViewBag.FarmerOrders = farmerOrders;
                return View(myTickets);
            }
            else if (isCustomer)
            {
                var myTickets = await _context.SupportTickets
                    .Where(t => (currentUserId != null && t.UserId == currentUserId) || (currentEmail != null && t.SenderEmail == currentEmail))
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();

                // Customer's purchased orders for dispute dropdown
                var customerOrders = await _context.Orders
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                    .Where(o => o.CustomerId == currentUserId || o.CustomerId == currentEmail)
                    .OrderByDescending(o => o.OrderDate)
                    .Take(20)
                    .ToListAsync();

                ViewBag.CustomerOrders = customerOrders;
                return View(myTickets);
            }
            else
            {
                // Guest Public View
                return View(new List<SupportTicket>());
            }
        }

        // POST: /Helpdesk/CreateTicket
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTicket(
            string senderName,
            string senderEmail,
            string? senderPhone,
            string category,
            int? orderId,
            string subject,
            string message,
            string priority,
            IFormFile? attachmentFile)
        {
            await EnsureTicketTableExistsAsync();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = "Guest";
            if (User.IsInRole("Admin")) role = "Admin";
            else if (User.IsInRole("Farmer")) role = "Farmer";
            else if (User.IsInRole("Customer")) role = "Customer";

            string? attachmentPath = null;
            if (attachmentFile != null && attachmentFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "tickets");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(attachmentFile.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await attachmentFile.CopyToAsync(fileStream);
                }

                attachmentPath = "/uploads/tickets/" + uniqueFileName;
            }

            var randomCode = new Random().Next(1000, 9999);
            var ticketNumber = $"TKT-{DateTime.UtcNow:yyMMdd}-{randomCode}";

            var ticket = new SupportTicket
            {
                TicketNumber = ticketNumber,
                UserId = currentUserId,
                SenderName = senderName?.Trim() ?? (User.Identity?.Name ?? "AgriAI User"),
                SenderEmail = senderEmail?.Trim() ?? "user@agriai.com",
                SenderPhone = senderPhone?.Trim(),
                UserRole = role,
                Category = category ?? "General",
                OrderId = orderId,
                Subject = subject?.Trim() ?? "Support Request",
                Message = message?.Trim() ?? string.Empty,
                AttachmentPath = attachmentPath,
                Priority = string.IsNullOrEmpty(priority) ? "Medium" : priority,
                Status = "Open",
                CreatedAt = DateTime.UtcNow
            };

            _context.SupportTickets.Add(ticket);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Support Ticket #{ticket.TicketNumber} submitted successfully. Our operations team will review it shortly!";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Helpdesk/ResolveTicket (Admin Only)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResolveTicket(int id, string? adminResponse, string status, bool releasePayout)
        {
            var ticket = await _context.SupportTickets.FindAsync(id);
            if (ticket == null)
            {
                TempData["ErrorMessage"] = "Ticket not found.";
                return RedirectToAction(nameof(Index));
            }

            ticket.Status = string.IsNullOrEmpty(status) ? "Resolved" : status;
            ticket.AdminResponse = adminResponse?.Trim();
            ticket.ResolvedByAdminId = User.Identity?.Name ?? "Administrator";
            ticket.ResolvedAt = DateTime.UtcNow;

            // If release payout flag is checked and ticket has OrderId, disburse Escrow
            if (releasePayout && ticket.OrderId.HasValue)
            {
                var order = await _context.Orders.FindAsync(ticket.OrderId.Value);
                if (order != null)
                {
                    order.Status = "Delivered & Settled (Admin Approved)";
                    order.AdminApprovedPayout = true;
                    order.AdminPayoutReason = $"Escrow payout released by Admin via Helpdesk Ticket #{ticket.TicketNumber}. Grievance resolved.";
                    order.DeliveredAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Ticket #{ticket.TicketNumber} updated successfully to status '{ticket.Status}'!";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Helpdesk/DeleteTicket (Admin Only)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTicket(int id)
        {
            var ticket = await _context.SupportTickets.FindAsync(id);
            if (ticket != null)
            {
                _context.SupportTickets.Remove(ticket);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Ticket #{ticket.TicketNumber} deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task EnsureTicketTableExistsAsync()
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync(@"
                    CREATE TABLE IF NOT EXISTS SupportTickets (
                        Id INT AUTO_INCREMENT PRIMARY KEY,
                        TicketNumber VARCHAR(50) NOT NULL,
                        UserId VARCHAR(255) NULL,
                        SenderName VARCHAR(255) NOT NULL,
                        SenderEmail VARCHAR(255) NOT NULL,
                        SenderPhone VARCHAR(50) NULL,
                        UserRole VARCHAR(50) NOT NULL,
                        Category VARCHAR(100) NOT NULL,
                        OrderId INT NULL,
                        Subject VARCHAR(255) NOT NULL,
                        Message TEXT NOT NULL,
                        AttachmentPath VARCHAR(500) NULL,
                        Priority VARCHAR(50) NOT NULL,
                        Status VARCHAR(50) NOT NULL,
                        AdminResponse TEXT NULL,
                        ResolvedByAdminId VARCHAR(255) NULL,
                        ResolvedAt DATETIME NULL,
                        CreatedAt DATETIME NOT NULL
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                ");
            }
            catch
            {
                // Table already exists or handled by EF
            }
        }
    }
}
