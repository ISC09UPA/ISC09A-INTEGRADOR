using System.Security.Cryptography;
using System.Text;
using AirportParking.Domain.Entities;
using AirportParking.Infrastructure.Cryptography;
using AirportParking.Infrastructure.Persistence;
using Xunit;

namespace AirportParking.UnitTests;

public class CustomerAndVehicleTests
{
    private readonly InMemoryParkingStore _store;

    public CustomerAndVehicleTests()
    {
        var hasher = new Argon2idPasswordHasher();
        var crypto = new Aes256GcmCryptographyService();
        _store = new InMemoryParkingStore(hasher, crypto);
    }

    [Fact]
    public async Task GetCustomerById_ShouldReturnInitialDemoUsersWithCorrectRoles()
    {
        // Act
        var customer = await _store.GetCustomerByIdAsync("cust-01");
        var admin = await _store.GetCustomerByIdAsync("admin-01");

        // Assert
        Assert.NotNull(customer);
        Assert.Equal("CUSTOMER", customer.Role);
        Assert.Contains("Laura", customer.Nombre);

        Assert.NotNull(admin);
        Assert.Equal("ADMIN", admin.Role);
        Assert.Contains("Carlos Mendoza", admin.Nombre);
    }

    [Fact]
    public async Task GetCustomerByEmailHash_ShouldFindUserWithoutDecryptingEmail()
    {
        // Arrange: Calcular hash SHA-256 del correo
        var rawEmail = "laura@aeropark.mx";
        var emailHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawEmail.ToLowerInvariant())));

        // Act
        var customer = await _store.GetCustomerByEmailHashAsync(emailHash);

        // Assert
        Assert.NotNull(customer);
        Assert.Equal("cust-01", customer.Id);
    }

    [Fact]
    public async Task AddCustomer_ShouldPersistAndAllowRetrieval()
    {
        // Arrange
        var newEmail = "viajero.nuevo@aeropark.mx";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(newEmail.ToLowerInvariant())));

        var newCustomer = new Customer
        {
            Id = "cust-new-99",
            Nombre = "Roberto Gómez",
            CorreoCifrado = "ENCRYPTED_EMAIL",
            CorreoHash = hash,
            TelefonoCifrado = "ENCRYPTED_PHONE",
            PasswordHash = "argon2id_hash_placeholder",
            PasswordSalt = "salt_placeholder",
            Role = "CUSTOMER"
        };

        // Act
        await _store.AddCustomerAsync(newCustomer);
        var retrieved = await _store.GetCustomerByEmailHashAsync(hash);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("Roberto Gómez", retrieved.Nombre);
        Assert.Equal("cust-new-99", retrieved.Id);
    }

    [Fact]
    public async Task AddVehicle_ShouldAssociateWithCustomerAndBeSearchableByPlate()
    {
        // Arrange
        var plate = "JAL9876";
        var newVehicle = new Vehicle
        {
            Id = "veh-new-01",
            CustomerId = "cust-01",
            PlacaNormalizada = plate,
            Alias = "BMW Serie 3",
            Marca = "BMW",
            Modelo = "M340i",
            Color = "Azul Portimao",
            IsActive = true
        };

        // Act
        await _store.AddVehicleAsync(newVehicle);
        var foundVehicle = await _store.GetVehicleByPlateAsync(plate);
        var customerVehicles = (await _store.GetVehiclesByCustomerAsync("cust-01")).ToList();

        // Assert
        Assert.NotNull(foundVehicle);
        Assert.Equal("BMW", foundVehicle.Marca);
        Assert.Contains(customerVehicles, v => v.PlacaNormalizada == plate);
    }

    [Fact]
    public async Task Whitelist_OfficialSecurityVehicle_ShouldBeIdentified()
    {
        // Act
        var entry = await _store.GetListEntryAsync("SEG001MX");

        // Assert
        Assert.NotNull(entry);
        Assert.Equal("WHITE_LIST", entry.TipoLista);
        Assert.Contains("Seguridad", entry.Justificacion);
    }
}
