using AirportParking.Domain.Entities;
using AirportParking.Infrastructure.Cryptography;
using AirportParking.Infrastructure.Persistence;
using Xunit;

namespace AirportParking.UnitTests;

public class SecurityAlertAndEmergencyTests
{
    private readonly InMemoryParkingStore _store;

    public SecurityAlertAndEmergencyTests()
    {
        var hasher = new Argon2idPasswordHasher();
        var crypto = new Aes256GcmCryptographyService();
        _store = new InMemoryParkingStore(hasher, crypto);
    }

    [Fact]
    public async Task GetSecurityAlerts_ShouldReturnInitialWatchlistAlert()
    {
        // Act
        var alerts = (await _store.GetSecurityAlertsAsync()).ToList();

        // Assert
        Assert.NotEmpty(alerts);
        var initialAlert = alerts.FirstOrDefault(a => a.PlacaNormalizada == "ROB404MX");
        Assert.NotNull(initialAlert);
        Assert.Equal("PENDING_REVIEW", initialAlert.EstadoDictamen);
        Assert.Contains("Robo", initialAlert.DelitoReportado);
    }

    [Fact]
    public async Task AddSecurityAlert_ShouldPersistNewPoliceReport()
    {
        // Arrange
        var newAlert = new SecurityAlert
        {
            Id = "alert-new-02",
            PlacaNormalizada = "SOS911XX",
            DelitoReportado = "Vehículo Sospechoso Perimetral",
            FuenteBoletin = "Guardia Nacional Estación Aeropuerto",
            ConfianzaCoincidencia = 0.99,
            EstadoDictamen = "PENDING_REVIEW"
        };

        // Act
        await _store.AddSecurityAlertAsync(newAlert);
        var alerts = (await _store.GetSecurityAlertsAsync()).ToList();

        // Assert
        Assert.Contains(alerts, a => a.PlacaNormalizada == "SOS911XX");
    }

    [Fact]
    public async Task UpdateSecurityAlert_ShouldRecordOfficialDictamen()
    {
        // Arrange
        var alerts = (await _store.GetSecurityAlertsAsync()).ToList();
        var alertToReview = alerts.First();

        // Act: Supervisor evalúa y descarta o confirma la alerta
        alertToReview.EstadoDictamen = "CONFIRMED";
        alertToReview.DictaminadoPor = "Oficial de Guardia C5";
        alertToReview.JustificacionDictamen = "Detección positiva confirmada visualmente.";
        alertToReview.FechaDictamen = DateTime.UtcNow;

        await _store.UpdateSecurityAlertAsync(alertToReview);
        var updatedList = (await _store.GetSecurityAlertsAsync()).ToList();
        var reviewed = updatedList.First(a => a.Id == alertToReview.Id);

        // Assert
        Assert.Equal("CONFIRMED", reviewed.EstadoDictamen);
        Assert.Equal("Oficial de Guardia C5", reviewed.DictaminadoPor);
        Assert.NotNull(reviewed.FechaDictamen);
    }

    [Fact]
    public async Task BarrierState_ShouldTransitionToLockedEmergencyProperly()
    {
        // Arrange: Estado inicial
        var initial = await _store.GetBarrierStatusAsync();
        Assert.Equal("CLOSED", initial);

        // Act: Activar protocolo Código Rojo / Enclavamiento
        await _store.SetBarrierStatusAsync("LOCKED_EMERGENCY");
        var lockedState = await _store.GetBarrierStatusAsync();

        // Desactivación con PIN de supervisor
        await _store.SetBarrierStatusAsync("CLOSED");
        var restoredState = await _store.GetBarrierStatusAsync();

        // Assert
        Assert.Equal("LOCKED_EMERGENCY", lockedState);
        Assert.Equal("CLOSED", restoredState);
    }

    [Fact]
    public async Task LogAccessEvent_ShouldRecordRecentLaneTelemetry()
    {
        // Arrange
        var testEvent = new AccessEvent
        {
            Id = "event-test-01",
            LaneId = "lane-north-01",
            LaneType = "ENTRY",
            PlacaNormalizada = "PRU123MX",
            ConfianzaLpr = 0.98,
            Decision = "ALLOW",
            Motivo = "Prueba Unitaria de Acceso",
            Timestamp = DateTime.UtcNow
        };

        // Act
        await _store.LogAccessEventAsync(testEvent);
        var recentEvents = (await _store.GetRecentEventsAsync(10)).ToList();

        // Assert
        Assert.Contains(recentEvents, e => e.PlacaNormalizada == "PRU123MX" && e.LaneId == "lane-north-01");
    }
}
