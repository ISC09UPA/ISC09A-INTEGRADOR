using AirportParking.Application.DTOs;
using AirportParking.Infrastructure.Cryptography;
using AirportParking.Infrastructure.Persistence;
using AirportParking.Infrastructure.Services;
using Xunit;

namespace AirportParking.UnitTests;

public class AccessDecisionTests
{
    private readonly InMemoryParkingStore _store;
    private readonly AccessDecisionService _decisionService;

    public AccessDecisionTests()
    {
        var hasher = new Argon2idPasswordHasher();
        var crypto = new Aes256GcmCryptographyService();
        _store = new InMemoryParkingStore(hasher, crypto);
        _decisionService = new AccessDecisionService(_store);
    }

    [Fact]
    public async Task EvaluateAccess_WhitelistVehicle_ShouldAllowAccessInstantly()
    {
        // Arrange
        var request = new AccessEvaluationRequest(
            LaneId: "entry-01",
            LaneType: "ENTRY",
            Placa: "SEG001MX",
            Confianza: 0.99
        );

        // Act
        var response = await _decisionService.EvaluateAccessAsync(request);

        // Assert
        Assert.Equal("ALLOW", response.Decision);
        Assert.True(response.OpenBarrier);
        Assert.Contains("Vehículo Oficial", response.Motivo);
    }

    [Fact]
    public async Task EvaluateAccess_EntryWithFreeBay_ShouldAllowAndAssignBay()
    {
        // Arrange
        var request = new AccessEvaluationRequest(
            LaneId: "entry-01",
            LaneType: "ENTRY",
            Placa: "NUE-555-X",
            Confianza: 0.96
        );

        // Act
        var response = await _decisionService.EvaluateAccessAsync(request);

        // Assert
        Assert.Equal("ALLOW", response.Decision);
        Assert.True(response.OpenBarrier);
        Assert.NotNull(response.StayId);
    }

    [Fact]
    public async Task EvaluateAccess_Exit_ShouldComputeCashlessTariffAndOpenBarrier()
    {
        // Arrange
        var request = new AccessEvaluationRequest(
            LaneId: "exit-01",
            LaneType: "EXIT",
            Placa: "ABC-123-D",
            Confianza: 0.98
        );

        // Act
        var response = await _decisionService.EvaluateAccessAsync(request);

        // Assert
        Assert.Equal("ALLOW", response.Decision);
        Assert.True(response.OpenBarrier);
        Assert.True(response.AmountDue > 0);
        Assert.Contains("Apple Pay", response.Motivo);
    }
}
