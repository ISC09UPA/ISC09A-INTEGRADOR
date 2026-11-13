using AirportParking.Domain.Entities;
using AirportParking.Infrastructure.Cryptography;
using AirportParking.Infrastructure.Persistence;
using Xunit;

namespace AirportParking.UnitTests;

public class ParkingBayIoTTests
{
    private readonly InMemoryParkingStore _store;

    public ParkingBayIoTTests()
    {
        var hasher = new Argon2idPasswordHasher();
        var crypto = new Aes256GcmCryptographyService();
        _store = new InMemoryParkingStore(hasher, crypto);
    }

    [Fact]
    public async Task GetAllBays_ShouldReturnInitialFourDigitalTwinBays()
    {
        // Act
        var bays = (await _store.GetAllBaysAsync()).ToList();

        // Assert
        Assert.Equal(4, bays.Count);
        Assert.Contains(bays, b => b.BayNumber == 1 && b.Status == "occupied");
        Assert.Contains(bays, b => b.BayNumber == 2 && b.Status == "free");
        Assert.Contains(bays, b => b.BayNumber == 3 && b.Status == "ev");
        Assert.Contains(bays, b => b.BayNumber == 4 && b.Status == "occupied");
    }

    [Fact]
    public async Task EvBay_ShouldHaveDifferentiatedChargingRate()
    {
        // Act
        var evBay = await _store.GetBayByIdAsync("bay-03");

        // Assert
        Assert.NotNull(evBay);
        Assert.Equal("ev", evBay.Status);
        Assert.Equal(65.00m, evBay.RatePerHour); // Tarifa preferencial para bahía EV
        Assert.Contains("EV", evBay.CurrentVehicle);
    }

    [Fact]
    public async Task UpdateBay_WhenVehicleParks_ShouldUpdateStatusAndTelemetry()
    {
        // Arrange
        var bay = await _store.GetBayByIdAsync("bay-02");
        Assert.NotNull(bay);
        Assert.Equal("free", bay.Status);

        // Act
        bay.Status = "occupied";
        bay.CurrentPlate = "TEST-404-X";
        bay.CurrentVehicle = "Mazda CX-5";
        bay.SensorDistance = "19.5 cm";
        bay.OccupiedSince = DateTime.UtcNow;
        await _store.UpdateBayAsync(bay);

        var updatedBay = await _store.GetBayByIdAsync("bay-02");

        // Assert
        Assert.NotNull(updatedBay);
        Assert.Equal("occupied", updatedBay.Status);
        Assert.Equal("TEST-404-X", updatedBay.CurrentPlate);
        Assert.Equal("19.5 cm", updatedBay.SensorDistance);
        Assert.NotNull(updatedBay.OccupiedSince);
    }

    [Fact]
    public async Task UpdateBay_WhenVehicleDeparts_ShouldResetToFreeState()
    {
        // Arrange
        var bay = await _store.GetBayByIdAsync("bay-01");
        Assert.NotNull(bay);
        Assert.Equal("occupied", bay.Status);

        // Act: Salida de vehículo
        bay.Status = "free";
        bay.CurrentPlate = "—";
        bay.CurrentVehicle = "Despejado";
        bay.SensorDistance = "> 150 cm";
        bay.OccupiedSince = null;
        await _store.UpdateBayAsync(bay);

        var clearedBay = await _store.GetBayByIdAsync("bay-01");

        // Assert
        Assert.NotNull(clearedBay);
        Assert.Equal("free", clearedBay.Status);
        Assert.Equal("—", clearedBay.CurrentPlate);
        Assert.Null(clearedBay.OccupiedSince);
    }
}
