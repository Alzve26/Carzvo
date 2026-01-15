using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Carzvo.Data;
using Carzvo.Models;

var builder = WebApplication.CreateBuilder(args);

// Получаем строку подключения
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Настройка контекста базы данных с MySQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString,
        ServerVersion.AutoDetect(connectionString),
        options => options.EnableRetryOnFailure()));

// Настройка Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Настройки пароля
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    // Настройки пользователя
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;

    // Настройки блокировки
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

// НАСТРОЙКА COOKIE С ОБРАБОТЧИКОМ ДЛЯ НАЗНАЧЕНИЯ РОЛИ
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;

    // ★ ВАЖНО: Обработчик события при входе в систему ★
    options.Events.OnSignedIn = async context =>
    {
        try
        {
            // Получаем сервисы через DI
            var serviceProvider = context.HttpContext.RequestServices;
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            // Получаем текущего пользователя
            var user = await userManager.GetUserAsync(context.Principal);

            if (user != null)
            {
                // 1. Проверяем и создаем роль "User" если её нет
                if (!await roleManager.RoleExistsAsync("User"))
                {
                    await roleManager.CreateAsync(new IdentityRole("User"));
                    logger.LogInformation("Создана роль 'User'");
                }

                // 2. Проверяем, есть ли у пользователя роль "User"
                if (!await userManager.IsInRoleAsync(user, "User"))
                {
                    // 3. Назначаем роль "User" всем новым пользователям
                    await userManager.AddToRoleAsync(user, "User");
                    logger.LogInformation($"Роль 'User' назначена пользователю {user.Email}");

                    // 4. Обновляем Claim (опционально, для мгновенного отражения в интерфейсе)
                    await userManager.RemoveClaimsAsync(user, await userManager.GetClaimsAsync(user));
                    await userManager.AddClaimAsync(user, new System.Security.Claims.Claim(
                        System.Security.Claims.ClaimTypes.Role, "User"));
                }
            }
        }
        catch (Exception ex)
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Ошибка при назначении роли 'User'");
        }
    };
});

// Добавление контроллеров с представлениями
builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation();

// В Program.cs, в секцию конфигурации сервисов (builder.Services...)
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { new CultureInfo("ru-RU") };
    options.DefaultRequestCulture = new RequestCulture("ru-RU");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

// Добавление Razor Pages
builder.Services.AddRazorPages();

// Настройка сессии
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ★ ВАЖНО: Создание приложения должно быть ВЫШЕ, чем использование app ★
var app = builder.Build();

// ★ ПЕРЕНЕСЕНО СЮДА: Настройка маршрута для Account ★
app.MapControllerRoute(
    name: "account",
    pattern: "account/{action=Login}/{id?}",
    defaults: new { controller = "Account" });

// Конфигурация pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// ★ ДОПОЛНИТЕЛЬНЫЙ MIDDLEWARE ДЛЯ ГАРАНТИИ НАЗНАЧЕНИЯ РОЛИ ★
app.Use(async (context, next) =>
{
    // Проверяем только аутентифицированных пользователей
    if (context.User.Identity?.IsAuthenticated == true)
    {
        try
        {
            var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = context.RequestServices.GetRequiredService<RoleManager<IdentityRole>>();
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

            var user = await userManager.GetUserAsync(context.User);

            if (user != null)
            {
                // Гарантируем, что у пользователя есть роль "User"
                if (!await roleManager.RoleExistsAsync("User"))
                {
                    await roleManager.CreateAsync(new IdentityRole("User"));
                    logger.LogInformation("Создана роль 'User' (в middleware)");
                }

                if (!await userManager.IsInRoleAsync(user, "User"))
                {
                    await userManager.AddToRoleAsync(user, "User");
                    logger.LogInformation($"Роль 'User' назначена {user.Email} (в middleware)");
                }
            }
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Ошибка в middleware назначения роли");
        }
    }

    await next();
});

// Создание ролей и администратора при запуске
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await InitializeRoles(services);
}

app.Run();

// Функция инициализации ролей
async Task InitializeRoles(IServiceProvider serviceProvider)
{
    var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var config = serviceProvider.GetRequiredService<IConfiguration>();
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

    // Список ролей
    string[] roleNames = { "Admin", "Manager", "Driver", "User" };

    foreach (var roleName in roleNames)
    {
        var roleExist = await roleManager.RoleExistsAsync(roleName);
        if (!roleExist)
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
            logger.LogInformation($"Создана роль: {roleName}");
        }
    }

    // Создание администратора
    var adminEmail = config["ApplicationSettings:AdminEmail"] ?? "admin@carzvo.com";
    var adminPassword = config["ApplicationSettings:AdminPassword"] ?? "Admin@123";

    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = "Администратор Системы",
            EmailConfirmed = true,
            PhoneNumberConfirmed = true,
            CompanyName = "Carzvo",
            Status = "Активен"
        };

        var createAdmin = await userManager.CreateAsync(adminUser, adminPassword);
        if (createAdmin.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
            await userManager.AddToRoleAsync(adminUser, "Manager");
            await userManager.AddToRoleAsync(adminUser, "User"); // ★ Админ тоже получает роль User
            logger.LogInformation($"Создан администратор: {adminEmail}");
        }
        else
        {
            logger.LogError($"Ошибка создания администратора: {string.Join(", ", createAdmin.Errors.Select(e => e.Description))}");
        }
    }
    else
    {
        // ★ Гарантируем, что у администратора есть все роли
        foreach (var roleName in new[] { "Admin", "Manager", "User" })
        {
            if (!await userManager.IsInRoleAsync(adminUser, roleName))
            {
                await userManager.AddToRoleAsync(adminUser, roleName);
                logger.LogInformation($"Роль {roleName} назначена администратору {adminEmail}");
            }
        }
    }

    logger.LogInformation("Инициализация ролей завершена");
}