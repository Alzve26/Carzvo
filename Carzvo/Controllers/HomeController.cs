using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Carzvo.Data;
using Carzvo.Models;
using System.Security.Claims;

namespace Carzvo.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Carzvo - Главная";

            try
            {

                ViewData["TotalShipments"] = await _context.Shipments.CountAsync();
                ViewData["ActiveShipments"] = await _context.Shipments
                    .Where(s => s.Status == ShipmentStatus.InTransit)
                    .CountAsync();
                ViewData["DeliveredShipments"] = await _context.Shipments
                    .Where(s => s.Status == ShipmentStatus.Delivered)
                    .CountAsync();
                ViewData["PendingShipments"] = await _context.Shipments
                    .Where(s => s.Status == ShipmentStatus.Pending)
                    .CountAsync();
                ViewData["TotalDrivers"] = await _context.Users
                    .Where(u => u.IsDriver)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении статистики для главной страницы");
                // Значения по умолчанию
                ViewData["TotalShipments"] = 0;
                ViewData["ActiveShipments"] = 0;
                ViewData["DeliveredShipments"] = 0;
                ViewData["PendingShipments"] = 0;
                ViewData["TotalDrivers"] = 0;
            }

            return View();
        }

        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToAction("Login", "Account");
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound();
                }

                var model = new DashboardViewModel();

                if (User.IsInRole("Admin")  || User.IsInRole("Manager"))
                {
                    model.IsAdminOrManager = true;
                    model.TotalShipments = await _context.Shipments.CountAsync();
                    model.PendingShipments = await _context.Shipments
                        .Where(s => s.Status == ShipmentStatus.Pending)
                        .CountAsync();
                    model.InTransitShipments = await _context.Shipments
                        .Where(s => s.Status == ShipmentStatus.InTransit)
                        .CountAsync();
                    model.TotalUsers = await _context.Users.CountAsync();
                    model.TotalDrivers = await _context.Users
                        .Where(u => u.IsDriver)
                        .CountAsync();
                    model.RecentShipments = await _context.Shipments
                        .Include(s => s.User)
                        .OrderByDescending(s => s.CreatedAt)
                        .Take(5)
                        .ToListAsync();
                }
                else if (User.IsInRole("Driver"))
                {
                    model.IsDriver = true;
                    model.MyShipments = await _context.Shipments
                        .Where(s => s.DriverId == userId)
                        .OrderByDescending(s => s.CreatedAt)
                        .ToListAsync();
                }
                else // Regular user
                {
                    model.MyShipments = await _context.Shipments
                        .Where(s => s.UserId == userId)
                        .OrderByDescending(s => s.CreatedAt)
                        .ToListAsync();
                }

                ViewData["UserName"] = user.FullName ?? user.UserName;
                ViewData["UserRole"] = User.IsInRole("Admin") ? "Администратор" :
                                     User.IsInRole("Manager") ? "Менеджер" :
                                     User.IsInRole("Driver") ? "Водитель" : "Пользователь";

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке Dashboard");
                TempData["ErrorMessage"] = "Произошла ошибка при загрузке данных. Пожалуйста, попробуйте позже.";
                return RedirectToAction("Index");
            }
        }

        public IActionResult About()
        {
            ViewData["Title"] = "О нас";
            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Title"] = "Контакты";
            return View();
        }

        public IActionResult Services()
        {
            ViewData["Title"] = "Услуги";
            return View();
        }

        public IActionResult Privacy()
        {
            ViewData["Title"] = "Политика конфиденциальности";
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    // Простая модель для страницы ошибки
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}