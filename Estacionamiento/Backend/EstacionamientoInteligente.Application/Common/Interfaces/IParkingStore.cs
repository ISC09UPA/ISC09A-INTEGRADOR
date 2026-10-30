using AirportParking.Domain.Entities;

namespace AirportParking.Application.Common.Interfaces;

public interface IParkingStore
{
    // Customers
    Task<Customer?> GetCustomerByEmailHashAsync(string emailHash);
    Task<Customer?> GetCustomerByIdAsync(string customerId);
    Task AddCustomerAsync(Customer customer);

    // Vehicles
    Task<IEnumerable<Vehicle>> GetVehiclesByCustomerAsync(string customerId);
    Task<Vehicle?> GetVehicleByPlateAsync(string plate);
    Task AddVehicleAsync(Vehicle vehicle);

    // Parking Bays
    Task<IEnumerable<ParkingBay>> GetAllBaysAsync();
    Task<ParkingBay?> GetBayByIdAsync(string id);
    Task UpdateBayAsync(ParkingBay bay);

    // Bookings
    Task<IEnumerable<Booking>> GetBookingsByCustomerAsync(string customerId);
    Task<Booking?> GetActiveBookingByPlateAsync(string plate);
    Task AddBookingAsync(Booking booking);

    // Stays
    Task<Stay?> GetActiveStayByPlateAsync(string plate);
    Task AddStayAsync(Stay stay);
    Task UpdateStayAsync(Stay stay);

    // Tariffs
    Task<Tariff> GetCurrentTariffAsync();

    // Whitelist / Blacklist
    Task<ListEntry?> GetListEntryAsync(string plate);

    // Security Alerts (Police Gateway Zero Trust)
    Task<IEnumerable<SecurityAlert>> GetSecurityAlertsAsync();
    Task AddSecurityAlertAsync(SecurityAlert alert);
    Task UpdateSecurityAlertAsync(SecurityAlert alert);

    // Access Events & Metrics
    Task LogAccessEventAsync(AccessEvent accessEvent);
    Task<IEnumerable<AccessEvent>> GetRecentEventsAsync(int limit = 10);
    Task<decimal> GetTodayCashlessRevenueAsync();
    Task AddCashlessRevenueAsync(decimal amount);

    // Barrier State
    Task<string> GetBarrierStatusAsync();
    Task SetBarrierStatusAsync(string status);
}
