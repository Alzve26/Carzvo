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

        // GET: /Shipments/All - для менеджеров и админов
        [Authorize(Roles = "Manager,Admin")]
        public async Task<IActionResult> All()
        {
            var shipments = await _context.Shipments
                .Include(s => s.User)
                .Include(s => s.Driver)
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
                return RedirectToAction(nameof(Index));
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

            var shipment = await _context.Shipments.FindAsync(id);
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
        public async Task<IActionResult> Edit(int id, Shipment shipment)
        {
            if (id != shipment.Id)
            {
                return NotFound();
            }

            var existingShipment = await _context.Shipments.FindAsync(id);
            if (existingShipment == null)
            {
                return NotFound();
            }

            // Проверка прав
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!User.IsInRole("Admin") && !User.IsInRole("Manager") && existingShipment.UserId != userId)
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Обновляем только разрешенные поля в зависимости от роли
                    if (User.IsInRole("User"))
                    {
                        existingShipment.Description = shipment.Description;
                        existingShipment.PickupDate = shipment.PickupDate;
                        existingShipment.DeliveryDate = shipment.DeliveryDate;
                        existingShipment.Notes = shipment.Notes;
                    }
                    else if (User.IsInRole("Manager") || User.IsInRole("Admin"))
                    {
                        existingShipment.Status = shipment.Status;
                        existingShipment.DriverId = shipment.DriverId;
                        existingShipment.Price = shipment.Price;

                        // Если назначили водителя, меняем статус
                        if (!string.IsNullOrEmpty(shipment.DriverId) && existingShipment.Status == ShipmentStatus.Pending)
                        {
                            existingShipment.Status = ShipmentStatus.Assigned;
                        }
                    }

                    existingShipment.UpdatedAt = DateTime.Now;

                    _context.Update(existingShipment);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Доставка обновлена: {existingShipment.OrderNumber}");
                    TempData["SuccessMessage"] = "Заказ успешно обновлен!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ShipmentExists(shipment.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }

                return RedirectToAction(nameof(Index));
            }

            return View(shipment);
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
                return RedirectToAction(nameof(All));
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
            var shipment = await _context.Shipments.FindAsync(id);
            if (shipment == null)
            {
                return Json(new { success = false, message = "Заказ не найден" });
            }

            // Проверка, что водитель может обновлять только свои доставки
            var driverId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (shipment.DriverId != driverId)
            {
                return Json(new { success = false, message = "Нет прав для изменения этого заказа" });
            }

            shipment.Status = status;
            shipment.UpdatedAt = DateTime.Now;

            if (status == ShipmentStatus.Delivered)
            {
                shipment.CompletedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Статус доставки {shipment.OrderNumber} изменен на {status}");

            return Json(new { success = true, message = "Статус успешно обновлен" });
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
    }
}