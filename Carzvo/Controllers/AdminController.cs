using Carzvo.Data;
using Carzvo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Carzvo.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<AdminController> logger)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        // GET: /Admin - панель администратора
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Панель администратора";

            var model = new AdminDashboardViewModel
            {
                TotalUsers = await _context.Users.CountAsync(),
                TotalDrivers = await _context.Users
                    .Where(u => u.IsDriver)
                    .CountAsync(),
                TotalShipments = await _context.Shipments.CountAsync(),
                PendingShipments = await _context.Shipments
                    .Where(s => s.Status == ShipmentStatus.Pending)
                    .CountAsync(),
                PendingDriverApplications = await _context.DriverApplications
                    .Where(d => d.Status == ApplicationStatus.Pending)
                    .CountAsync(),
                RecentUsers = await _context.Users
                    .OrderByDescending(u => u.RegistrationDate)
                    .Take(10)
                    .ToListAsync()
            };

            return View(model);
        }

        // GET: /Admin/Users - список всех пользователей
        public async Task<IActionResult> Users()
        {
            var users = await _context.Users
                .OrderBy(u => u.UserName)
                .ToListAsync();

            var userRoles = new Dictionary<string, List<string>>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userRoles[user.Id] = roles.ToList();
            }

            ViewBag.UserRoles = userRoles;
            ViewData["Title"] = "Управление пользователями";

            return View(users);
        }

        // GET: /Admin/EditUserRoles/{id} - редактирование ролей пользователя
        public async Task<IActionResult> EditUserRoles(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            var allRoles = await _roleManager.Roles
                .Select(r => r.Name)
                .ToListAsync();

            var model = new EditUserRolesViewModel
            {
                UserId = user.Id,
                UserName = user.UserName ?? user.Email,
                UserFullName = user.FullName,
                CurrentRoles = userRoles.ToList(),
                AllRoles = allRoles ?? new List<string>()
            };

            ViewData["Title"] = "Редактирование ролей пользователя";

            return View(model);
        }

        // POST: /Admin/EditUserRoles - сохранение ролей пользователя
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUserRoles(EditUserRolesViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByIdAsync(model.UserId);
                if (user == null)
                {
                    return NotFound();
                }

                // Получаем текущие роли пользователя
                var userRoles = await _userManager.GetRolesAsync(user);

                // Удаляем все текущие роли
                var removeResult = await _userManager.RemoveFromRolesAsync(user, userRoles);
                if (!removeResult.Succeeded)
                {
                    ModelState.AddModelError("", "Ошибка при удалении ролей");
                    return View(model);
                }

                // Добавляем выбранные роли
                if (model.SelectedRoles != null && model.SelectedRoles.Any())
                {
                    var addResult = await _userManager.AddToRolesAsync(user, model.SelectedRoles);
                    if (!addResult.Succeeded)
                    {
                        ModelState.AddModelError("", "Ошибка при добавлении ролей");
                        return View(model);
                    }

                    // Обновляем статус водителя
                    user.IsDriver = model.SelectedRoles.Contains("Driver");
                    await _userManager.UpdateAsync(user);
                }

                _logger.LogInformation($"Роли пользователя {user.UserName} обновлены администратором {User.Identity?.Name}");
                TempData["SuccessMessage"] = "Роли пользователя успешно обновлены!";

                return RedirectToAction(nameof(Users));
            }

            return View(model);
        }

        // GET: /Admin/DriverApplications - заявки на водителей
        public async Task<IActionResult> DriverApplications()
        {
            var applications = await _context.DriverApplications
                .Include(d => d.User)
                .OrderByDescending(d => d.ApplicationDate)
                .ToListAsync();

            ViewData["Title"] = "Заявки на водителей";

            return View(applications);
        }

        // GET: /Admin/DriverApplicationDetails/{id} - детали заявки
        public async Task<IActionResult> DriverApplicationDetails(int id)
        {
            var application = await _context.DriverApplications
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (application == null)
            {
                return NotFound();
            }

            ViewData["Title"] = "Детали заявки на водителя";

            return View(application);
        }

        // POST: /Admin/ApproveDriverApplication/{id} - одобрить заявку
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveDriverApplication(int id, string managerComment)
        {
            var application = await _context.DriverApplications
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (application == null)
            {
                return NotFound();
            }

            application.Status = ApplicationStatus.Approved;
            application.ReviewDate = DateTime.Now;
            application.ManagerComment = managerComment;
            // Убрали: application.ReviewedBy = User.Identity?.Name;

            // Получаем пользователя из заявки
            var user = application.User;
            if (user != null)
            {
                // Добавляем пользователю роль Driver и обновляем статус
                user.IsDriver = true;
                await _userManager.AddToRoleAsync(user, "Driver");
                await _userManager.UpdateAsync(user);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Заявка на водителя {application.Id} одобрена администратором {User.Identity?.Name}");

            TempData["SuccessMessage"] = "Заявка успешно одобрена!";
            return RedirectToAction(nameof(DriverApplications));
        }

        // POST: /Admin/RejectDriverApplication/{id} - отклонить заявку
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectDriverApplication(int id, string managerComment)
        {
            var application = await _context.DriverApplications
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (application == null)
            {
                return NotFound();
            }

            application.Status = ApplicationStatus.Rejected;
            application.ReviewDate = DateTime.Now;
            application.ManagerComment = managerComment;
            // Убрали: application.ReviewedBy = User.Identity?.Name;

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Заявка на водителя {application.Id} отклонена администратором {User.Identity?.Name}");

            TempData["SuccessMessage"] = "Заявка отклонена!";
            return RedirectToAction(nameof(DriverApplications));
        }

        // GET: /Admin/Statistics - статистика
        public async Task<IActionResult> Statistics()
        {
            ViewData["Title"] = "Статистика системы";

            // Получаем статистику по доставкам
            var shipmentsByStatus = await _context.Shipments
                .GroupBy(s => s.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var shipmentsByStatusDict = shipmentsByStatus
                .ToDictionary(x => x.Status.ToString(), x => x.Count);

            // Получаем статистику по месяцам
            var sixMonthsAgo = DateTime.Now.AddMonths(-6);
            var shipmentsByMonth = await _context.Shipments
                .Where(s => s.CreatedAt >= sixMonthsAgo)
                .GroupBy(s => new { Year = s.CreatedAt.Year, Month = s.CreatedAt.Month })
                .Select(g => new {
                    Month = new DateTime(g.Key.Year, g.Key.Month, 1),
                    Count = g.Count()
                })
                .OrderBy(x => x.Month)
                .ToListAsync();

            var shipmentsByMonthDict = shipmentsByMonth
                .ToDictionary(x => x.Month.ToString("MMM yyyy"), x => x.Count);

            // Получаем статистику по типам транспорта
            var shipmentsByVehicleType = await _context.Shipments
                .GroupBy(s => s.VehicleType)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();

            var shipmentsByVehicleTypeDict = shipmentsByVehicleType
                .ToDictionary(x => x.Type.ToString(), x => x.Count);

            // Получаем пользовательскую статистику
            var totalUsers = await _context.Users.CountAsync();
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var managers = await _userManager.GetUsersInRoleAsync("Manager");
            var drivers = await _userManager.GetUsersInRoleAsync("Driver");
            var regularUsers = await _userManager.GetUsersInRoleAsync("User");

            var usersByRoleDict = new Dictionary<string, int>
            {
                ["Всего пользователей"] = totalUsers,
                ["Администраторы"] = admins?.Count ?? 0,
                ["Менеджеры"] = managers?.Count ?? 0,
                ["Водители"] = drivers?.Count ?? 0,
                ["Пользователи"] = regularUsers?.Count ?? 0
            };

            var model = new StatisticsViewModel
            {
                ShipmentsByStatus = shipmentsByStatusDict,
                ShipmentsByMonth = shipmentsByMonthDict,
                ShipmentsByVehicleType = shipmentsByVehicleTypeDict,
                UsersByRole = usersByRoleDict
            };

            return View(model);
        }

        // GET: /Admin/SystemSettings - настройки системы
        public IActionResult SystemSettings()
        {
            ViewData["Title"] = "Настройки системы";

            var settings = new SystemSettingsViewModel
            {
                CompanyName = "Carzvo Грузоперевозки",
                SupportEmail = "support@carzvo.com",
                SupportPhone = "+7 (999) 123-45-67",
                DefaultPricePerKg = 10,
                UrgentDeliveryMultiplier = 1.5m,
                MaxWeightPerShipment = 10000
            };

            return View(settings);
        }

        // GET: /Admin/CreateManager - создание менеджера
        [HttpGet]
        public IActionResult CreateManager()
        {
            ViewData["Title"] = "Создание менеджера";
            return View();
        }

        // POST: /Admin/CreateManager - создание менеджера
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateManager(CreateManagerViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Проверяем, существует ли пользователь с таким email
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("Email", "Пользователь с таким email уже существует");
                    return View(model);
                }

                // Создаем нового пользователя
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    EmailConfirmed = true,
                    PhoneNumber = model.PhoneNumber,
                    CompanyName = "Carzvo",
                    Status = "Активен",
                    RegistrationDate = DateTime.Now
                };

                // Создаем пользователя с паролем
                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    // Добавляем роль менеджера
                    await _userManager.AddToRoleAsync(user, "Manager");

                    _logger.LogInformation($"Создан новый менеджер: {model.Email}");
                    TempData["SuccessMessage"] = $"Менеджер {model.FullName} успешно создан!";

                    return RedirectToAction(nameof(Users));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }
    }
}