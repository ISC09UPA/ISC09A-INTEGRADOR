using AirportParking.Application.DTOs;
using AirportParking.Domain.Entities;
using AirportParking.Infrastructure.Cryptography;
using AirportParking.Infrastructure.Persistence;
using AirportParking.Infrastructure.Services;
using Xunit;

namespace AirportParking.UnitTests;

public class TariffAndCashlessBillingTests
{
    private readonly InMemoryParkingStore _store;
    private readonly AccessDecisionService _decisionService;

    public TariffAndCashlessBillingTests()
    {
        var hasher = new Argon2idPasswordHasher();
        var crypto = new Aes256GcmCryptographyService();
        _store = new InMemoryParkingStore(hasher, crypto);
        _decisionService = new AccessDecisionService(_store);
    }

    [Fact]
    public async Task Tariff_DefaultTariff_ShouldHaveCorrectAirportValues()
    {
        // Act
        var tariff = await _store.GetCurrentTariffAsync();

        // Assert
        Assert.NotNull(tariff);
        Assert.Equal(15, tariff.FraccionMinutos);
        Assert.Equal(11.25m, tariff.CostoFraccion); // $45.00 / hora
        Assert.Equal(350.00m, tariff.TopeDiario);
        Assert.Equal(15, tariff.ToleranciaSalidaMinutos);
        Assert.True(tariff.IsActive);
    }

    [Fact]
    public async Task EvaluateExit_WhenProlongedStay_ShouldCapAtDailyMaximum()
    {
        // Arrange: Crear una estancia muy prolongada (e.g. 15 horas = $675 sin tope)
        var plate = "LONG-999-STAY";
        var cleanPlate = "LONG999STAY";
        var ancientStay = new Stay
        {
            Id = "stay-ancient-01",
            PlacaNormalizada = cleanPlate,
            CarrilEntrada = "entry-01",
            FechaIngreso = DateTime.UtcNow.AddHours(-15),
            EstadoCobro = "PENDING"
        };
        await _store.AddStayAsync(ancientStay);

        var request = new AccessEvaluationRequest(
            LaneId: "exit-01",
            LaneType: "EXIT",
            Placa: plate,
            Confianza: 0.99
        );

        // Act
        var response = await _decisionService.EvaluateAccessAsync(request);

        // Assert
        Assert.Equal("ALLOW", response.Decision);
        Assert.True(response.OpenBarrier);
        Assert.Equal(350.00m, response.AmountDue); // Tope diario respetado
    }

    [Fact]
    public async Task EvaluateExit_ShouldAccumulateCashlessRevenue()
    {
        // Arrange
        var initialRevenue = await _store.GetTodayCashlessRevenueAsync();
        var request = new AccessEvaluationRequest(
            LaneId: "exit-01",
            LaneType: "EXIT",
            Placa: "ABC-123-D",
            Confianza: 0.98
        );

        // Act
        var response = await _decisionService.EvaluateAccessAsync(request);
        var updatedRevenue = await _store.GetTodayCashlessRevenueAsync();

        // Assert
        Assert.True(response.AmountDue > 0);
        Assert.Equal(initialRevenue + response.AmountDue, updatedRevenue);
    }

    [Fact]
    public async Task EvaluateEntry_WhenLotIsCompletelyFull_ShouldDenyAccess()
    {
        // Arrange: Ocupar todos los cajones existentes
        var bays = await _store.GetAllBaysAsync();
        foreach (var bay in bays)
        {
            bay.Status = "occupied";
            await _store.UpdateBayAsync(bay);
        }

        var unreservedRequest = new AccessEvaluationRequest(
            LaneId: "entry-01",
            LaneType: "ENTRY",
            Placa: "FULL-999-NO",
            Confianza: 0.97
        );

        // Act
        var response = await _decisionService.EvaluateAccessAsync(unreservedRequest);

        // Assert
        Assert.Equal("DENIED", response.Decision);
        Assert.False(response.OpenBarrier);
        Assert.Contains("Sin cajones disponibles", response.Motivo);
    }

    [Fact]
    public async Task EvaluateEntry_WhenLotIsFull_ButVehicleHasBooking_ShouldAllowAccess()
    {
        // Arrange: Llenar el estacionamiento pero registrar reserva previa para el auto
        var bays = await _store.GetAllBaysAsync();
        foreach (var bay in bays)
        {
            bay.Status = "occupied";
            await _store.UpdateBayAsync(bay);
        }

        var vipPlate = "VIP-777-RES";
        var cleanVipPlate = "VIP777RES";
        var booking = new Booking
        {
            Id = "book-vip-01",
            CustomerId = "cust-01",
            VehicleId = "veh-01",
            ZoneId = "z1-covered-lvl1",
            FechaInicio = DateTime.UtcNow.AddMinutes(-30),
            FechaFin = DateTime.UtcNow.AddHours(4),
            MontoTotal = 180.00m,
            Estado = "CONFIRMED",
            CodigoQr = "QR-VIP-777"
        };
        await _store.AddBookingAsync(booking);

        // Simular que el vehículo de la reserva coincide
        var veh = new Vehicle
        {
            Id = "veh-vip-01",
            CustomerId = "cust-01",
            PlacaNormalizada = cleanVipPlate,
            Alias = "Auto con Reserva",
            IsActive = true
        };
        await _store.AddVehicleAsync(veh);

        var request = new AccessEvaluationRequest(
            LaneId: "entry-01",
            LaneType: "ENTRY",
            Placa: vipPlate,
            Confianza: 0.99
        );

        // Act
        var response = await _decisionService.EvaluateAccessAsync(request);

        // Assert
        Assert.Equal("ALLOW", response.Decision);
        Assert.True(response.OpenBarrier);
        Assert.Contains("Reserva Confirmada", response.Motivo);
    }
}
