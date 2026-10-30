using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using AirportParking.Application.Common.Interfaces;
using AirportParking.Domain.Entities;

namespace AirportParking.Infrastructure.Persistence;

public class InMemoryParkingStore : IParkingStore
{
    private readonly ConcurrentDictionary<string, Customer> _customers = new();
    private readonly ConcurrentDictionary<string, Vehicle> _vehicles = new();
    private readonly ConcurrentDictionary<string, ParkingBay> _bays = new();
    private readonly ConcurrentDictionary<string, Booking> _bookings = new();
    private readonly ConcurrentDictionary<string, Stay> _stays = new();
    private readonly ConcurrentDictionary<string, ListEntry> _listEntries = new();
    private readonly ConcurrentDictionary<string, SecurityAlert> _securityAlerts = new();
    private readonly ConcurrentQueue<AccessEvent> _recentAccessEvents = new();

    private Tariff _currentTariff;
    private decimal _todayCashlessRevenue = 180.00m;
    private string _barrierStatus = "CLOSED";

    public InMemoryParkingStore(IPasswordHasher passwordHasher, ICryptographyService cryptoService)
    {
        // 1. Inicializar Tarifa
        _currentTariff = new Tariff
        {
            Id = "tar-v1-2026",
            Version = 1,
            FraccionMinutos = 15,
            CostoFraccion = 11.25m, // $45.00 por hora
            TopeDiario = 350.00m,
            ToleranciaSalidaMinutos = 15,
            IsActive = true
        };

        // 2. Sembrar Cliente Demo (Laura Sánchez)
        var (hash, salt) = passwordHasher.HashPassword("AeroPark2026!");
        var email = "laura@aeropark.mx";
        var emailHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.ToLowerInvariant())));

        var customer = new Customer
        {
            Id = "cust-01",
            Nombre = "Laura Sánchez (Viajera Frecuente)",
            CorreoCifrado = cryptoService.Encrypt(email),
            CorreoHash = emailHash,
            TelefonoCifrado = cryptoService.Encrypt("+52 449 123 4567"),
            PasswordHash = hash,
            PasswordSalt = salt,
            Estado = "ACTIVE",
            Idioma = "es-MX",
            Role = "CUSTOMER"
        };
        _customers[customer.Id] = customer;

        // Sembrar Supervisor / Admin (Carlos Mendoza)
        var adminEmail = "admin@aeropark.mx";
        var adminEmailHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(adminEmail.ToLowerInvariant())));
        var (adminHash, adminSalt) = passwordHasher.HashPassword("AeroPark2026!");

        var adminUser = new Customer
        {
            Id = "admin-01",
            Nombre = "Carlos Mendoza (Supervisor de Pista)",
            CorreoCifrado = cryptoService.Encrypt(adminEmail),
            CorreoHash = adminEmailHash,
            TelefonoCifrado = cryptoService.Encrypt("+52 449 999 8888"),
            PasswordHash = adminHash,
            PasswordSalt = adminSalt,
            Estado = "ACTIVE",
            Idioma = "es-MX",
            Role = "ADMIN"
        };
        _customers[adminUser.Id] = adminUser;

        // 3. Sembrar Vehículos de Laura
        var veh1 = new Vehicle
        {
            Id = "veh-01",
            CustomerId = customer.Id,
            PlacaNormalizada = "ABC123D",
            Alias = "Tesla Blanco",
            Marca = "Tesla",
            Modelo = "Model 3 Performance",
            Color = "Blanco Perla",
            EsPredeterminado = true,
            IsActive = true
        };
        var veh2 = new Vehicle
        {
            Id = "veh-02",
            CustomerId = customer.Id,
            PlacaNormalizada = "XYZ789A",
            Alias = "Porsche Macan",
            Marca = "Porsche",
            Modelo = "Macan GTS",
            Color = "Gris Volcánico",
            EsPredeterminado = false,
            IsActive = true
        };
        _vehicles[veh1.Id] = veh1;
        _vehicles[veh2.Id] = veh2;

        // 4. Sembrar los 4 Cajones de Simulación IoT
        var bay1 = new ParkingBay
        {
            Id = "bay-01",
            BayNumber = 1,
            Name = "Cajón 01",
            Status = "occupied",
            CurrentPlate = "ABC-123-D",
            CurrentVehicle = "Tesla Model 3",
            SensorDistance = "18 cm",
            OccupiedSince = DateTime.UtcNow.AddHours(-2).AddMinutes(-45),
            RatePerHour = 45.00m
        };
        var bay2 = new ParkingBay
        {
            Id = "bay-02",
            BayNumber = 2,
            Name = "Cajón 02",
            Status = "free",
            CurrentPlate = "—",
            CurrentVehicle = "Despejado",
            SensorDistance = "> 150 cm",
            OccupiedSince = null,
            RatePerHour = 45.00m
        };
        var bay3 = new ParkingBay
        {
            Id = "bay-03",
            BayNumber = 3,
            Name = "Cajón 03",
            Status = "ev",
            CurrentPlate = "JLM-456-E",
            CurrentVehicle = "Audi e-tron GT (Carga EV)",
            SensorDistance = "22 cm",
            OccupiedSince = DateTime.UtcNow.AddHours(-1).AddMinutes(-12),
            RatePerHour = 65.00m
        };
        var bay4 = new ParkingBay
        {
            Id = "bay-04",
            BayNumber = 4,
            Name = "Cajón 04",
            Status = "occupied",
            CurrentPlate = "XYZ-789-A",
            CurrentVehicle = "Porsche Macan",
            SensorDistance = "16 cm",
            OccupiedSince = DateTime.UtcNow.AddHours(-4).AddMinutes(-10),
            RatePerHour = 45.00m
        };
        _bays[bay1.Id] = bay1;
        _bays[bay2.Id] = bay2;
        _bays[bay3.Id] = bay3;
        _bays[bay4.Id] = bay4;

        // 5. Sembrar Estancia Activa para ABC123D
        var stay1 = new Stay
        {
            Id = "stay-01",
            BookingId = "book-01",
            PlacaNormalizada = "ABC123D",
            CarrilEntrada = "carril-lpr-01",
            FechaIngreso = DateTime.UtcNow.AddHours(-2).AddMinutes(-45),
            EstadoCobro = "PENDING"
        };
        _stays[stay1.Id] = stay1;

        // 6. Sembrar Lista Blanca (Seguridad y Emergencias)
        _listEntries["SEG001MX"] = new ListEntry
        {
            Id = "le-01",
            PlacaNormalizada = "SEG001MX",
            TipoLista = "WHITE_LIST",
            Justificacion = "Patrulla de Seguridad Aeroportuaria #1"
        };
        _listEntries["AMB999MX"] = new ListEntry
        {
            Id = "le-02",
            PlacaNormalizada = "AMB999MX",
            TipoLista = "WHITE_LIST",
            Justificacion = "Ambulancia Médica Aeroportuaria"
        };

        // 7. Sembrar Alerta Policial (Zero Trust Human Review)
        var alert = new SecurityAlert
        {
            Id = "alert-01",
            PlacaNormalizada = "ROB404MX",
            DelitoReportado = "Reporte de Robo con Violencia (C5 / Fiscalía)",
            FuenteBoletin = "Plataforma Nacional de Alertas",
            ConfianzaCoincidencia = 0.94,
            EstadoDictamen = "PENDING_REVIEW"
        };
        _securityAlerts[alert.Id] = alert;
    }

    public Task<Customer?> GetCustomerByEmailHashAsync(string emailHash)
    {
        var customer = _customers.Values.FirstOrDefault(c => c.CorreoHash.Equals(emailHash, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(customer);
    }

    public Task<Customer?> GetCustomerByIdAsync(string customerId)
    {
        _customers.TryGetValue(customerId, out var customer);
        return Task.FromResult(customer);
    }

    public Task AddCustomerAsync(Customer customer)
    {
        _customers[customer.Id] = customer;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<Vehicle>> GetVehiclesByCustomerAsync(string customerId)
    {
        var vehicles = _vehicles.Values.Where(v => v.CustomerId == customerId && v.IsActive);
        return Task.FromResult<IEnumerable<Vehicle>>(vehicles);
    }

    public Task<Vehicle?> GetVehicleByPlateAsync(string plate)
    {
        var normalized = NormalizePlate(plate);
        var vehicle = _vehicles.Values.FirstOrDefault(v => v.PlacaNormalizada == normalized);
        return Task.FromResult(vehicle);
    }

    public Task AddVehicleAsync(Vehicle vehicle)
    {
        _vehicles[vehicle.Id] = vehicle;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<ParkingBay>> GetAllBaysAsync()
    {
        var sorted = _bays.Values.OrderBy(b => b.BayNumber);
        return Task.FromResult<IEnumerable<ParkingBay>>(sorted);
    }

    public Task<ParkingBay?> GetBayByIdAsync(string id)
    {
        _bays.TryGetValue(id, out var bay);
        return Task.FromResult(bay);
    }

    public Task UpdateBayAsync(ParkingBay bay)
    {
        _bays[bay.Id] = bay;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<Booking>> GetBookingsByCustomerAsync(string customerId)
    {
        var bookings = _bookings.Values.Where(b => b.CustomerId == customerId);
        return Task.FromResult<IEnumerable<Booking>>(bookings);
    }

    public Task<Booking?> GetActiveBookingByPlateAsync(string plate)
    {
        var normalized = NormalizePlate(plate);
        var now = DateTime.UtcNow;
        var booking = _bookings.Values.FirstOrDefault(b =>
            b.Estado == "CONFIRMED" &&
            b.FechaInicio <= now.AddHours(2) &&
            b.FechaFin >= now.AddHours(-1));
        return Task.FromResult(booking);
    }

    public Task AddBookingAsync(Booking booking)
    {
        _bookings[booking.Id] = booking;
        return Task.CompletedTask;
    }

    public Task<Stay?> GetActiveStayByPlateAsync(string plate)
    {
        var normalized = NormalizePlate(plate);
        var stay = _stays.Values.FirstOrDefault(s => s.PlacaNormalizada == normalized && s.FechaSalida == null);
        return Task.FromResult(stay);
    }

    public Task AddStayAsync(Stay stay)
    {
        _stays[stay.Id] = stay;
        return Task.CompletedTask;
    }

    public Task UpdateStayAsync(Stay stay)
    {
        _stays[stay.Id] = stay;
        return Task.CompletedTask;
    }

    public Task<Tariff> GetCurrentTariffAsync()
    {
        return Task.FromResult(_currentTariff);
    }

    public Task<ListEntry?> GetListEntryAsync(string plate)
    {
        var normalized = NormalizePlate(plate);
        _listEntries.TryGetValue(normalized, out var entry);
        return Task.FromResult(entry);
    }

    public Task<IEnumerable<SecurityAlert>> GetSecurityAlertsAsync()
    {
        return Task.FromResult<IEnumerable<SecurityAlert>>(_securityAlerts.Values);
    }

    public Task AddSecurityAlertAsync(SecurityAlert alert)
    {
        _securityAlerts[alert.Id] = alert;
        return Task.CompletedTask;
    }

    public Task UpdateSecurityAlertAsync(SecurityAlert alert)
    {
        _securityAlerts[alert.Id] = alert;
        return Task.CompletedTask;
    }

    public Task LogAccessEventAsync(AccessEvent accessEvent)
    {
        _recentAccessEvents.Enqueue(accessEvent);
        while (_recentAccessEvents.Count > 50)
            _recentAccessEvents.TryDequeue(out _);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<AccessEvent>> GetRecentEventsAsync(int limit = 10)
    {
        return Task.FromResult<IEnumerable<AccessEvent>>(_recentAccessEvents.Reverse().Take(limit));
    }

    public Task<decimal> GetTodayCashlessRevenueAsync()
    {
        return Task.FromResult(_todayCashlessRevenue);
    }

    public Task AddCashlessRevenueAsync(decimal amount)
    {
        _todayCashlessRevenue += amount;
        return Task.CompletedTask;
    }

    public Task<string> GetBarrierStatusAsync()
    {
        return Task.FromResult(_barrierStatus);
    }

    public Task SetBarrierStatusAsync(string status)
    {
        _barrierStatus = status;
        return Task.CompletedTask;
    }

    private static string NormalizePlate(string plate)
    {
        return plate.Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();
    }
}
