using System.Security.Claims;
using System.Threading.RateLimiting;
using EatWeb.Data;
using EatWeb.Models;
using EatWeb.Services;
using EatWeb.Services.Storage;
using EatWeb.Realtime;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Falta ConnectionStrings:DefaultConnection. Configúrala con user-secrets en desarrollo o una variable de entorno en despliegue.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDataProtection()
    .SetApplicationName("Comidita")
    .PersistKeysToDbContext<ApplicationDbContext>();

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;

    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})

.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>();
// Configure application cookie

builder.Services.Configure<SecurityStampValidatorOptions>(o =>
    o.ValidationInterval = TimeSpan.FromMinutes(1));


builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// Anti-forgery
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});


builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("cliente", httpContext =>
    {
        var tieneComensal = httpContext.Request.Cookies
            .TryGetValue("eatweb_comensal", out var token) &&
            Guid.TryParse(token, out _);

        var clave = tieneComensal
            ? "comensal:" + token
            : "ip:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida");

        return RateLimitPartition.GetFixedWindowLimiter(
            clave,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = tieneComensal ? 120 : 40,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.AddPolicy("realtime", httpContext =>
    {
        var usuarioId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var tieneComensal = httpContext.Request.Cookies
            .TryGetValue("eatweb_comensal", out var token) &&
            Guid.TryParse(token, out _);

        var clave = !string.IsNullOrWhiteSpace(usuarioId)
            ? "usuario:" + usuarioId
            : tieneComensal
                ? "comensal:" + token
                : "ip:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida");

        return RateLimitPartition.GetFixedWindowLimiter(
            clave,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

// Session
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 16 * 1024;
});

builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection(StorageOptions.SectionName));
builder.Services.AddSingleton<IStorageService, S3StorageService>();

// Toda acción insegura (POST/PUT/PATCH/DELETE) exige antiforgery por defecto.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Add services
builder.Services.AddScoped<SesionService>();
builder.Services.AddScoped<CarritoService>();
builder.Services.AddScoped<PedidoService>();
builder.Services.AddScoped<CuentaService>();
builder.Services.AddScoped<SolicitudMesaService>();
builder.Services.AddScoped<RestauranteDefaultsService>();
builder.Services.AddScoped<DbSeeder>();
builder.Services.AddScoped<AccesoRestauranteService>();
builder.Services.AddScoped<RestauranteContextService>();
builder.Services.AddScoped<AdminDashboardService>();
builder.Services.AddScoped<OperacionRealtimeService>();

var app = builder.Build();

// Aplicar migraciones y sembrar datos
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SembrarAsync(scope.ServiceProvider);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapStaticAssets();

app.MapHub<ComiditaHub>("/hubs/operacion")
    .RequireRateLimiting("realtime");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapGet("/health", async (ApplicationDbContext db) =>
{
    try
    {
        return await db.Database.CanConnectAsync()
            ? Results.Ok(new { status = "ok" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();

// Ruta para escaneo QR
app.MapControllerRoute(
    name: "qr",
    pattern: "m/{codigo}",
    defaults: new { controller = "Cliente", action = "Ingreso" });

app.Run();