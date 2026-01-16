using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Carzvo.Data;
using Carzvo.Models;
using System.Security.Claims;


namespace Carzvo.Controllers
{
    [Authorize]
    public class ShipmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ShipmentsController> _logger;

        public ShipmentsController(ApplicationDbContext context, ILogger<ShipmentsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: /Shipments - для обычных пользователей
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var shipments = await _context.Shipments
                .Include(s => s.Driver)
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return View(shipments);
        }

        // GET: /Shipments/Create
        [Authorize]
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Shipments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create(Shipment shipment)
        {
            if (ModelState.IsValid)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                shipment.UserId = userId;
                shipment.CreatedAt = DateTime.Now;
                shipment.Price = CalculatePrice(shipment);

                _context.Add(shipment);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Новая доставка создана: {shipment.OrderNumber}");

                TempData["SuccessMessage"] = "Заказ успешно создан! Номер вашего заказа: " + shipment.OrderNumber;
                return RedirectToAction("Dashboard", "Home");
            }

            return View(shipment);
        }

        // GET: /Shipments/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shipment = await _context.Shipments
                .Include(s => s.User)
                .Include(s => s.Driver)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (shipment == null)
            {
                return NotFound();
            }

            // Проверка прав
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && !User.IsInRole("Manager") &&
                shipment.UserId != userId && shipment.DriverId != userId)
            {
                return Forbid();
            }

            return View(shipment);
        }

        // GET: /Shipments/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Загружаем с связанными данными
            var shipment = await _context.Shipments
                .Include(s => s.User)
                .Include(s => s.Driver)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (shipment == null)
            {
                return NotFound();
            }

            // Проверка прав
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && !User.IsInRole("Manager") && shipment.UserId != userId)
            {
                return Forbid();
            }

            // Получение списка водителей для менеджера/админа
            if (User.IsInRole("Manager") || User.IsInRole("Admin"))
            {
                ViewBag.Drivers = await _context.Users
                    .Where(u => u.IsDriver && u.Status == "Активен")
                    .ToListAsync();
            }

            return View(shipment);
        }

        // POST: /Shipments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, IFormCollection form)
        {
            try
            {
                _logger.LogInformation($"=== НАЧАЛО ОБНОВЛЕНИЯ ЗАКАЗА {id} ===");
                _logger.LogInformation($"Пользователь: {User.Identity?.Name}");
                _logger.LogInformation($"Роли: Manager={User.IsInRole("Manager")}, Admin={User.IsInRole("Admin")}, User={User.IsInRole("User")}");

                // Логируем все данные формы
                foreach (var key in form.Keys)
                {
                    _logger.LogInformation($"  {key}: {form[key]}");
                }

                // Находим заказ
                var shipment = await _context.Shipments.FindAsync(id);
                if (shipment == null)
                {
                    _logger.LogError($"Заказ {id} не найден");
                    return NotFound();
                }

                _logger.LogInformation($"Текущий статус: {shipment.Status}, цена: {shipment.Price}, водитель: {shipment.DriverId}");

                // Определяем роль
                var isManagerOrAdmin = User.IsInRole("Manager") || User.IsInRole("Admin");
                var isUser = User.IsInRole("User");

                if (!isManagerOrAdmin && shipment.UserId != User.FindFirstValue(ClaimTypes.NameIdentifier))
                {
                    _logger.LogWarning($"Доступ запрещен. UserId: {shipment.UserId}, текущий пользователь: {User.FindFirstValue(ClaimTypes.NameIdentifier)}");
                    return Forbid();
                }

                // ОБНОВЛЕНИЕ ДАННЫХ
                if (isManagerOrAdmin)
                {
                    _logger.LogInformation("Обновление как менеджер/админ");

                    // Статус
                    if (Enum.TryParse<ShipmentStatus>(form["Status"], out var newStatus))
                    {
                        _logger.LogInformation($"Новый статус: {newStatus}");
                        shipment.Status = newStatus;
                    }
                    else
                    {
                        _logger.LogWarning($"Не удалось распарсить статус: {form["Status"]}");
                    }

                    // Водитель
                    var driverId = form["DriverId"].ToString();
                    _logger.LogInformation($"Новый водитель ID: {driverId}");
                    shipment.DriverId = string.IsNullOrEmpty(driverId) ? null : driverId;

                    // Цена
                    if (decimal.TryParse(form["Price"].ToString().Replace(',', '.'),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out decimal price))
                    {
                        _logger.LogInformation($"Новая цена: {price}");
                        shipment.Price = Math.Max(0, Math.Min(price, 1000000));
                    }
                    else
                    {
                        _logger.LogWarning($"Не удалось распарсить цену: {form["Price"]}");
                    }

                    // Автоматическое изменение статуса при назначении водителя
                    if (!string.IsNullOrEmpty(shipment.DriverId) && shipment.Status == ShipmentStatus.Pending)
                    {
                        shipment.Status = ShipmentStatus.Assigned;
                        _logger.LogInformation($"Автоматически меняем статус на: {ShipmentStatus.Assigned}");
                    }
                }
                else if (isUser)
                {
                    _logger.LogInformation("Обновление как обычный пользователь");

                    // Описание
                    shipment.Description = form["Description"].ToString();

                    // Даты
                    if (DateTime.TryParse(form["PickupDate"], out DateTime pickupDate))
                        shipment.PickupDate = pickupDate;

                    if (DateTime.TryParse(form["DeliveryDate"], out DateTime deliveryDate))
                        shipment.DeliveryDate = deliveryDate;

                    // Примечания
                    shipment.Notes = form["Notes"].ToString();
                }

                shipment.UpdatedAt = DateTime.Now;

                _logger.LogInformation($"Перед сохранением: Status={shipment.Status}, Price={shipment.Price}, DriverId={shipment.DriverId}");

                // СОХРАНЕНИЕ В БАЗУ
                _context.Update(shipment);
                int changes = await _context.SaveChangesAsync();

                _logger.LogInformation($"Изменений в базе: {changes}");
                _logger.LogInformation($"=== КОНЕЦ ОБНОВЛЕНИЯ ЗАКАЗА {id} ===");

                if (changes > 0)
                {
                    TempData["SuccessMessage"] = $"Заказ успешно обновлен! Изменено записей: {changes}";
                }
                else
                {
                    TempData["ErrorMessage"] = "Не удалось сохранить изменения";
                }

                return RedirectToAction("Dashboard", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ОШИБКА при обновлении заказа {id}");
                TempData["ErrorMessage"] = $"Ошибка: {ex.Message}";
                return RedirectToAction("Edit", new { id });
            }
        }

        // GET: /Shipments/Delete/5
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var shipment = await _context.Shipments
                .Include(s => s.User)
                .Include(s => s.Driver)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (shipment == null)
            {
                return NotFound();
            }

            // Проверка прав: пользователь может удалять только свои заказы
            // Админы и менеджеры могут удалять любые заказы
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && !User.IsInRole("Manager") && shipment.UserId != userId)
            {
                return Forbid();
            }

            return View(shipment);
        }

        // POST: /Shipments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var shipment = await _context.Shipments.FindAsync(id);
            if (shipment == null)
            {
                return NotFound();
            }

            // Проверка прав: пользователь может удалять только свои заказы
            // Админы и менеджеры могут удалять любые заказы
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && !User.IsInRole("Manager") && shipment.UserId != userId)
            {
                return Forbid();
            }

            // Логирование информации об удалении
            var orderNumber = shipment.OrderNumber;

            _context.Shipments.Remove(shipment);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Доставка удалена: {orderNumber}");
            TempData["SuccessMessage"] = $"Заказ {orderNumber} успешно удален!";

            // Перенаправление в зависимости от роли
            if (User.IsInRole("Admin") || User.IsInRole("Manager"))
            {
                // Для менеджеров и админов - на Dashboard
                return RedirectToAction("Dashboard", "Home");
            }
            else
            {
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: /Shipments/DriverShipments - для водителей
        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> DriverShipments()
        {
            var driverId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var shipments = await _context.Shipments
                .Include(s => s.User)
                .Where(s => s.DriverId == driverId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return View(shipments);
        }

        // POST: /Shipments/UpdateStatus/5 - для водителей
        [HttpPost]
        [Authorize(Roles = "Driver")]
        public async Task<IActionResult> UpdateStatus(int id, ShipmentStatus status)
        {
            try
            {
                var shipment = await _context.Shipments.FindAsync(id);
                if (shipment == null)
                {
                    return Json(new { success = false, message = "Заказ не найден" });
                }

                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Проверяем, что водитель может менять статус только своих заказов
                if (shipment.DriverId != currentUserId)
                {
                    return Json(new { success = false, message = "У вас нет прав для изменения этого заказа" });
                }

                // Проверяем валидность перехода статуса
                if (!IsValidStatusTransition(shipment.Status, status))
                {
                    return Json(new { success = false, message = "Некорректный переход статуса" });
                }

                shipment.Status = status;
                shipment.UpdatedAt = DateTime.Now;

                // Если статус "Доставлен", устанавливаем дату завершения
                if (status == ShipmentStatus.Delivered)
                {
                    shipment.CompletedAt = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                // Корректный JSON ответ с правильной кодировкой
                return Json(new
                {
                    success = true,
                    message = "Статус успешно обновлен",
                    newStatus = status.ToString(),
                    newStatusDisplay = GetStatusDisplayName(status)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении статуса заказа {Id}", id);
                return Json(new { success = false, message = "Произошла ошибка при обновлении статуса" });
            }
        }

        // Вспомогательный метод для проверки валидности перехода статуса
        private bool IsValidStatusTransition(ShipmentStatus currentStatus, ShipmentStatus newStatus)
        {
            // Водитель может менять статус только с Assigned на InTransit и с InTransit на Delivered
            switch (currentStatus)
            {
                case ShipmentStatus.Assigned:
                return newStatus == ShipmentStatus.InTransit;
                case ShipmentStatus.InTransit:
                return newStatus == ShipmentStatus.Delivered;
                default:
                return false;
            }
        }

        // Метод для получения отображаемого имени статуса
        private string GetStatusDisplayName(ShipmentStatus status)
        {
            return status switch
            {
                ShipmentStatus.Pending => "Ожидает",
                ShipmentStatus.Processing => "Обрабатывается",
                ShipmentStatus.Assigned => "Назначен",
                ShipmentStatus.InTransit => "В пути",
                ShipmentStatus.Delivered => "Доставлен",
                ShipmentStatus.Cancelled => "Отменен",
                _ => status.ToString()
            };
        }

        private bool ShipmentExists(int id)
        {
            return _context.Shipments.Any(e => e.Id == id);
        }

        private decimal CalculatePrice(Shipment shipment)
        {
            decimal basePrice = 1000;

            // Наценка за вес
            if (shipment.Weight > 1000) basePrice += shipment.Weight * 10;
            else if (shipment.Weight > 100) basePrice += shipment.Weight * 5;
            else basePrice += shipment.Weight * 2;

            // Наценка за тип транспорта
            switch (shipment.VehicleType)
            {
                case VehicleType.LargeTruck:
                basePrice *= 3;
                break;
                case VehicleType.MediumTruck:
                basePrice *= 2;
                break;
                case VehicleType.Refrigerator:
                basePrice *= 2.5m;
                break;
                case VehicleType.Tanker:
                basePrice *= 4;
                break;
            }

            // Наценка за срочность
            if ((shipment.DeliveryDate - shipment.PickupDate).TotalHours <= 24)
            {
                basePrice *= 1.5m;
            }

            return Math.Round(basePrice, 2);
        }

        // GET: /Shipments/AllOrders - для менеджеров и админов
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> AllOrders(string search = "", string status = "",
            string sortBy = "CreatedAt", string sortOrder = "desc")
        {
            // Начинаем запрос
            var query = _context.Shipments
                .Include(s => s.User)
                .Include(s => s.Driver)
                .AsQueryable();

            // Поиск
            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(s =>
                    s.OrderNumber.ToLower().Contains(search) ||
                    s.Description.ToLower().Contains(search) ||
                    s.PickupAddress.ToLower().Contains(search) ||
                    s.DeliveryAddress.ToLower().Contains(search) ||
                    s.User.FullName.ToLower().Contains(search) ||
                    s.User.PhoneNumber.Contains(search) ||
                    s.User.Email.ToLower().Contains(search) ||
                    (s.Driver != null && s.Driver.FullName.ToLower().Contains(search))
                );
            }

            // Фильтр по статусу
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<ShipmentStatus>(status, out var statusEnum))
            {
                query = query.Where(s => s.Status == statusEnum);
            }

            // Сортировка
            switch (sortBy.ToLower())
            {
                case "pickupdate":
                query = sortOrder.ToLower() == "asc"
                    ? query.OrderBy(s => s.PickupDate)
                    : query.OrderByDescending(s => s.PickupDate);
                break;
                case "price":
                query = sortOrder.ToLower() == "asc"
                    ? query.OrderBy(s => s.Price)
                    : query.OrderByDescending(s => s.Price);
                break;
                case "ordernumber":
                query = sortOrder.ToLower() == "asc"
                    ? query.OrderBy(s => s.OrderNumber)
                    : query.OrderByDescending(s => s.OrderNumber);
                break;
                default: // CreatedAt
                query = sortOrder.ToLower() == "asc"
                    ? query.OrderBy(s => s.CreatedAt)
                    : query.OrderByDescending(s => s.CreatedAt);
                break;
            }

            var shipments = await query.ToListAsync();

            // Передаем параметры в ViewBag для сохранения в форме
            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.SortBy = sortBy;
            ViewBag.SortOrder = sortOrder;

            return View(shipments);
        }
    }
}