using System.Security.Cryptography;
using System.Text;
using AirportParking.Application.Common.Interfaces;
using AirportParking.Application.DTOs;
using AirportParking.Domain.Entities;
using AirportParking.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Configurar Servicios de Arquitectura Limpia
builder.Services.AddOpenApi();
builder.Services.AddInfrastructureServices();

// Habilitar CORS para permitir llamadas desde el Panel Admin Web y la App Móvil Expo
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// ==============================================================================
// 1. Endpoints de Autenticación (/api/v1/auth)
// ==============================================================================
var authGroup = app.MapGroup("/api/v1/auth").WithTags("Authentication");

authGroup.MapPost("/login", async (LoginRequest request, IParkingStore store, IPasswordHasher hasher) =>
{
    var emailHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Email.ToLowerInvariant())));
    var customer = await store.GetCustomerByEmailHashAsync(emailHash);

    if (customer == null || !hasher.VerifyPassword(request.Password, customer.PasswordHash, customer.PasswordSalt))
    {
        return Results.Json(new { error = "Credenciales incorrectas o usuario no registrado." }, statusCode: 401);
    }

    var token = $"ey-aeropark-{Guid.NewGuid():N}";
    return Results.Ok(new AuthResponse(token, customer.Id, customer.Nombre, request.Email, customer.Role));
});

authGroup.MapPost("/register", async (RegisterRequest request, IParkingStore store, IPasswordHasher hasher, ICryptographyService crypto) =>
{
    var emailHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Email.ToLowerInvariant())));
    var existing = await store.GetCustomerByEmailHashAsync(emailHash);
    if (existing != null)
    {
        return Results.Conflict(new { error = "Ya existe una cuenta registrada con este correo electrónico." });
    }

    var (hash, salt) = hasher.HashPassword(request.Password);
    var newCustomer = new Customer
    {
        Id = $"cust-{Guid.NewGuid():N}"[..12],
        Nombre = request.Nombre,
        CorreoCifrado = crypto.Encrypt(request.Email),
        CorreoHash = emailHash,
        TelefonoCifrado = crypto.Encrypt(request.Telefono),
        PasswordHash = hash,
        PasswordSalt = salt,
        Estado = "ACTIVE"
    };

    await store.AddCustomerAsync(newCustomer);

    if (!string.IsNullOrWhiteSpace(request.Placa))
    {
        var normalizedPlate = request.Placa.ToUpperInvariant().Trim();
        var vehicle = new Vehicle
        {
            Id = $"veh-{Guid.NewGuid():N}"[..12],
            CustomerId = newCustomer.Id,
            PlacaNormalizada = normalizedPlate,
            Alias = !string.IsNullOrWhiteSpace(request.MarcaModelo) ? request.MarcaModelo : "Auto Principal",
            Marca = !string.IsNullOrWhiteSpace(request.MarcaModelo) ? request.MarcaModelo.Split(' ').FirstOrDefault() ?? "Auto" : "Auto",
            Modelo = request.MarcaModelo ?? "Sedan",
            Color = "Blanco",
            EsPredeterminado = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        await store.AddVehicleAsync(vehicle);
    }

    var token = $"ey-aeropark-{Guid.NewGuid():N}";
    return Results.Created($"/api/v1/auth/me", new AuthResponse(token, newCustomer.Id, newCustomer.Nombre, request.Email, "CUSTOMER"));
});

// ==============================================================================
// 2. Endpoints de Vehículos (/api/v1/vehicles)
// ==============================================================================
var vehicleGroup = app.MapGroup("/api/v1/vehicles").WithTags("Vehicles");

vehicleGroup.MapGet("/", async (IParkingStore store, string? customerId) =>
{
    var cid = customerId ?? "cust-01";
    var vehicles = await store.GetVehiclesByCustomerAsync(cid);
    var dtos = vehicles.Select(v => new VehicleDto(
        v.Id,
        v.CustomerId,
        v.PlacaNormalizada,
        v.Alias,
        v.Marca,
        v.Modelo,
        v.Color,
        v.EsPredeterminado
    ));
    return Results.Ok(dtos);
});

vehicleGroup.MapPost("/", async (RegisterVehicleRequest req, IParkingStore store, string? customerId) =>
{
    var cid = customerId ?? "cust-01";
    var cleanPlate = req.Placa.Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();

    var vehicle = new Vehicle
    {
        Id = $"veh-{Guid.NewGuid():N}"[..10],
        CustomerId = cid,
        PlacaNormalizada = cleanPlate,
        Alias = req.Alias ?? req.Placa,
        Marca = req.Marca ?? "AeroPark Car",
        Modelo = req.Modelo ?? "Modelo Standard",
        Color = req.Color ?? "Blanco",
        EsPredeterminado = req.EsPredeterminado,
        IsActive = true
    };

    await store.AddVehicleAsync(vehicle);
    return Results.Created($"/api/v1/vehicles/{vehicle.Id}", vehicle);
});

// ==============================================================================
// 3. Endpoints de Reservas (/api/v1/bookings)
// ==============================================================================
var bookingGroup = app.MapGroup("/api/v1/bookings").WithTags("Bookings");

bookingGroup.MapGet("/availability", async (IParkingStore store) =>
{
    var bays = (await store.GetAllBaysAsync()).ToList();
    var freeCount = bays.Count(b => b.Status == "free");
    var occupancyPercent = Math.Round(((4 - freeCount) / 4.0) * 100);

    var availability = new List<TerminalAvailabilityDto>
    {
        new("T2", "Terminal 2 (Banco de Pruebas 4 Cajones)", 4, freeCount, $"{occupancyPercent}%", 45.00m),
        new("T1", "Terminal 1 (Larga Estancia Techada)", 250, 120, "52%", 35.00m)
    };

    return Results.Ok(availability);
});

bookingGroup.MapPost("/", async (CreateBookingRequest req, IParkingStore store) =>
{
    var booking = new Booking
    {
        Id = $"book-{Guid.NewGuid():N}"[..10],
        CustomerId = req.CustomerId,
        VehicleId = req.VehicleId,
        ZoneId = req.Zone,
        FechaInicio = req.FechaInicio,
        FechaFin = req.FechaFin,
        MontoTotal = req.MontoTotal,
        Estado = "CONFIRMED",
        CodigoQr = $"AEROPARK-{Guid.NewGuid():N}"[..18].ToUpperInvariant()
    };

    await store.AddBookingAsync(booking);
    return Results.Created($"/api/v1/bookings/{booking.Id}", booking);
});

bookingGroup.MapGet("/my", async (IParkingStore store, string? customerId) =>
{
    var cid = customerId ?? "cust-01";
    var bookings = await store.GetBookingsByCustomerAsync(cid);
    return Results.Ok(bookings);
});

// ==============================================================================
// 4. Endpoints de Acceso LPR y Barrera (/api/v1/access)
// ==============================================================================
var accessGroup = app.MapGroup("/api/v1/access").WithTags("Access & Lanes");

accessGroup.MapPost("/evaluate", async (AccessEvaluationRequest request, IAccessDecisionService decisionService) =>
{
    var result = await decisionService.EvaluateAccessAsync(request);
    return Results.Ok(result);
});

accessGroup.MapPost("/barrier", async (BarrierCommandRequest request, IAccessDecisionService decisionService) =>
{
    var ok = await decisionService.SetBarrierStateAsync(request.LaneId, request.Command, request.Reason);
    return Results.Ok(new { success = ok, command = request.Command });
});

// ==============================================================================
// 5. Endpoints de Simulación de 4 Cajones IoT (/api/v1/bays)
// ==============================================================================
var bayGroup = app.MapGroup("/api/v1/bays").WithTags("Parking Bays IoT");

bayGroup.MapGet("/", async (IParkingStore store) =>
{
    var bays = await store.GetAllBaysAsync();
    var dtos = bays.Select(b =>
    {
        var elapsed = b.OccupiedSince.HasValue
            ? (DateTime.UtcNow - b.OccupiedSince.Value).ToString(@"hh\:mm\:ss")
            : "00:00:00";

        return new ParkingBayDto(
            b.Id,
            b.BayNumber,
            b.Name,
            b.Status,
            b.CurrentPlate,
            b.CurrentVehicle,
            b.SensorDistance,
            elapsed,
            b.RatePerHour
        );
    });
    return Results.Ok(dtos);
});

bayGroup.MapPost("/{id}/toggle", async (string id, HttpContext context, IParkingStore store) =>
{
    ToggleBayRequest? req = null;
    if (context.Request.ContentLength > 0 && context.Request.HasJsonContentType())
    {
        try { req = await context.Request.ReadFromJsonAsync<ToggleBayRequest>(); } catch { }
    }
    var bay = await store.GetBayByIdAsync(id);
    if (bay == null)
        return Results.NotFound(new { error = $"Cajón {id} no encontrado" });

    if (bay.Status == "free")
    {
        bay.Status = req?.NewStatus ?? "occupied";
        bay.CurrentPlate = req?.Plate ?? "NUE-901-B";
        bay.CurrentVehicle = req?.Vehicle ?? "BMW i4 M50";
        bay.SensorDistance = "19 cm";
        bay.OccupiedSince = DateTime.UtcNow;

        await store.LogAccessEventAsync(new AccessEvent
        {
            LaneId = "sim-bay-lane",
            LaneType = "OCCUPANCY",
            PlacaNormalizada = bay.CurrentPlate.Replace("-", ""),
            ConfianzaLpr = 1.0,
            Decision = "ALLOW",
            Motivo = $"Ocupación Sensor HC-SR04 en {bay.Name}",
            Timestamp = DateTime.UtcNow
        });
    }
    else
    {
        var fee = 45.00m;
        await store.AddCashlessRevenueAsync(fee);
        await store.LogAccessEventAsync(new AccessEvent
        {
            LaneId = "sim-bay-lane",
            LaneType = "OCCUPANCY",
            PlacaNormalizada = bay.CurrentPlate.Replace("-", ""),
            ConfianzaLpr = 1.0,
            Decision = "ALLOW",
            Motivo = $"Salida de {bay.Name} · Cobro Apple Pay (${fee:F2} MXN)",
            Timestamp = DateTime.UtcNow
        });

        bay.Status = "free";
        bay.CurrentPlate = "—";
        bay.CurrentVehicle = "Despejado";
        bay.SensorDistance = "> 150 cm";
        bay.OccupiedSince = null;
    }

    await store.UpdateBayAsync(bay);

    var elapsed = bay.OccupiedSince.HasValue
        ? (DateTime.UtcNow - bay.OccupiedSince.Value).ToString(@"hh\:mm\:ss")
        : "00:00:00";

    return Results.Ok(new ParkingBayDto(
        bay.Id,
        bay.BayNumber,
        bay.Name,
        bay.Status,
        bay.CurrentPlate,
        bay.CurrentVehicle,
        bay.SensorDistance,
        elapsed,
        bay.RatePerHour
    ));
});

// ==============================================================================
// 6. Dashboard Administrativo y Telemetría (/api/v1/admin)
// ==============================================================================
var adminGroup = app.MapGroup("/api/v1/admin").WithTags("Admin & Operations");

adminGroup.MapGet("/dashboard", async (IParkingStore store) =>
{
    var bays = (await store.GetAllBaysAsync()).ToList();
    var freeCount = bays.Count(b => b.Status == "free");
    var occupiedCount = bays.Count(b => b.Status == "occupied");
    var evCount = bays.Count(b => b.Status == "ev");
    var revenue = await store.GetTodayCashlessRevenueAsync();
    var barrier = await store.GetBarrierStatusAsync();

    var bayDtos = bays.Select(b =>
    {
        var elapsed = b.OccupiedSince.HasValue
            ? (DateTime.UtcNow - b.OccupiedSince.Value).ToString(@"hh\:mm\:ss")
            : "00:00:00";

        return new ParkingBayDto(
            b.Id,
            b.BayNumber,
            b.Name,
            b.Status,
            b.CurrentPlate,
            b.CurrentVehicle,
            b.SensorDistance,
            elapsed,
            b.RatePerHour
        );
    }).ToList();

    var occupancyPercent = $"{Math.Round(((4 - freeCount) / 4.0) * 100)}%";

    return Results.Ok(new DashboardSummaryDto(
        TotalBays: 4,
        FreeBays: freeCount,
        OccupiedBays: occupiedCount,
        EvBays: evCount,
        OccupancyPercentage: occupancyPercent,
        TodayCashlessRevenue: revenue,
        BarrierStatus: barrier,
        Bays: bayDtos
    ));
});

adminGroup.MapGet("/events", async (IParkingStore store) =>
{
    var events = await store.GetRecentEventsAsync(20);
    return Results.Ok(events);
});

adminGroup.MapGet("/alerts", async (IParkingStore store) =>
{
    var alerts = await store.GetSecurityAlertsAsync();
    return Results.Ok(alerts);
});

app.Run();
