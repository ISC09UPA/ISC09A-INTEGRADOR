using AirportParking.Application.Common.Interfaces;
using AirportParking.Application.DTOs;
using AirportParking.Domain.Entities;

namespace AirportParking.Infrastructure.Services;

public class AccessDecisionService : IAccessDecisionService
{
    private readonly IParkingStore _store;

    public AccessDecisionService(IParkingStore store)
    {
        _store = store;
    }

    public async Task<AccessEvaluationResponse> EvaluateAccessAsync(AccessEvaluationRequest request)
    {
        var cleanPlate = request.Placa.Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();

        // 1. Verificar Lista Blanca (Ambulancias, Patrullas)
        var listEntry = await _store.GetListEntryAsync(cleanPlate);
        if (listEntry != null && listEntry.TipoLista == "WHITE_LIST")
        {
            await _store.LogAccessEventAsync(new AccessEvent
            {
                LaneId = request.LaneId,
                LaneType = request.LaneType,
                PlacaNormalizada = cleanPlate,
                ConfianzaLpr = request.Confianza,
                Decision = "ALLOW",
                Motivo = $"Acceso Prioritario: {listEntry.Justificacion}",
                Timestamp = DateTime.UtcNow
            });
            await _store.SetBarrierStatusAsync("OPEN");

            return new AccessEvaluationResponse(
                Decision: "ALLOW",
                Motivo: $"Vehículo Oficial: {listEntry.Justificacion}",
                OpenBarrier: true,
                AmountDue: 0m,
                StayId: null,
                CustomerName: "Servicio Oficial"
            );
        }

        // 2. Evaluar Ingreso (ENTRY)
        if (request.LaneType.Equals("ENTRY", StringComparison.OrdinalIgnoreCase))
        {
            // Validar si tiene reserva previa
            var booking = await _store.GetActiveBookingByPlateAsync(cleanPlate);
            var isReserved = booking != null;

            // Revisar aforo disponible
            var bays = (await _store.GetAllBaysAsync()).ToList();
            var freeBays = bays.Count(b => b.Status == "free");

            if (freeBays == 0 && !isReserved)
            {
                await _store.LogAccessEventAsync(new AccessEvent
                {
                    LaneId = request.LaneId,
                    LaneType = "ENTRY",
                    PlacaNormalizada = cleanPlate,
                    ConfianzaLpr = request.Confianza,
                    Decision = "DENIED",
                    Motivo = "Capacidad Total Agotada (Sin cupo)",
                    Timestamp = DateTime.UtcNow
                });

                return new AccessEvaluationResponse(
                    Decision: "DENIED",
                    Motivo: "Estacionamiento Completo. Sin cajones disponibles.",
                    OpenBarrier: false,
                    AmountDue: 0m,
                    StayId: null,
                    CustomerName: null
                );
            }

            // Registrar Estancia Activa
            var stay = new Stay
            {
                Id = $"stay-{Guid.NewGuid():N}"[..12],
                BookingId = booking?.Id,
                PlacaNormalizada = cleanPlate,
                CarrilEntrada = request.LaneId,
                FechaIngreso = DateTime.UtcNow,
                EstadoCobro = "PENDING"
            };
            await _store.AddStayAsync(stay);

            // Asignar primer cajón libre
            var targetBay = bays.FirstOrDefault(b => b.Status == "free");
            if (targetBay != null)
            {
                targetBay.Status = "occupied";
                targetBay.CurrentPlate = request.Placa.ToUpperInvariant();
                targetBay.CurrentVehicle = "Vehículo en Carril";
                targetBay.SensorDistance = "19 cm";
                targetBay.OccupiedSince = DateTime.UtcNow;
                await _store.UpdateBayAsync(targetBay);
            }

            await _store.SetBarrierStatusAsync("OPEN");
            await _store.LogAccessEventAsync(new AccessEvent
            {
                LaneId = request.LaneId,
                LaneType = "ENTRY",
                PlacaNormalizada = cleanPlate,
                ConfianzaLpr = request.Confianza,
                Decision = "ALLOW",
                Motivo = isReserved ? "Reserva Web Activa" : "Acceso Cashless Rotativo",
                Timestamp = DateTime.UtcNow
            });

            return new AccessEvaluationResponse(
                Decision: "ALLOW",
                Motivo: isReserved ? "Bienvenido · Reserva Confirmada" : "Bienvenido · Registro Digital LPR",
                OpenBarrier: true,
                AmountDue: 0m,
                StayId: stay.Id,
                CustomerName: isReserved ? "Cliente Reservado" : "Usuario Registrado"
            );
        }

        // 3. Evaluar Salida (EXIT)
        var activeStay = await _store.GetActiveStayByPlateAsync(cleanPlate);
        var tariff = await _store.GetCurrentTariffAsync();

        var elapsedHours = activeStay != null
            ? Math.Max(1.0, (DateTime.UtcNow - activeStay.FechaIngreso).TotalHours)
            : 2.5;

        var amountDue = Math.Min(tariff.TopeDiario, (decimal)Math.Ceiling(elapsedHours) * 45.00m);

        // Cobro automático simulado (100% Cashless)
        await _store.AddCashlessRevenueAsync(amountDue);
        if (activeStay != null)
        {
            activeStay.FechaSalida = DateTime.UtcNow;
            activeStay.CarrilSalida = request.LaneId;
            activeStay.MontoCalculado = amountDue;
            activeStay.EstadoCobro = "PAID";
            await _store.UpdateStayAsync(activeStay);
        }

        // Liberar cajón asociado si existe
        var occupiedBay = (await _store.GetAllBaysAsync()).FirstOrDefault(b =>
            b.CurrentPlate.Replace("-", "").Equals(cleanPlate, StringComparison.OrdinalIgnoreCase));

        if (occupiedBay != null)
        {
            occupiedBay.Status = "free";
            occupiedBay.CurrentPlate = "—";
            occupiedBay.CurrentVehicle = "Despejado";
            occupiedBay.SensorDistance = "> 150 cm";
            occupiedBay.OccupiedSince = null;
            await _store.UpdateBayAsync(occupiedBay);
        }

        await _store.SetBarrierStatusAsync("OPEN");
        await _store.LogAccessEventAsync(new AccessEvent
        {
            LaneId = request.LaneId,
            LaneType = "EXIT",
            PlacaNormalizada = cleanPlate,
            ConfianzaLpr = request.Confianza,
            Decision = "ALLOW",
            Motivo = $"Cobro Cashless Exitoso (${amountDue:F2} MXN)",
            Timestamp = DateTime.UtcNow
        });

        return new AccessEvaluationResponse(
            Decision: "ALLOW",
            Motivo: $"Pago Procesado (${amountDue:F2} MXN vía Apple Pay). Buen viaje.",
            OpenBarrier: true,
            AmountDue: amountDue,
            StayId: activeStay?.Id,
            CustomerName: "Laura Sánchez"
        );
    }

    public async Task<bool> SetBarrierStateAsync(string laneId, string command, string? reason)
    {
        var isEmergency = command.Equals("EMERGENCY_LOCK", StringComparison.OrdinalIgnoreCase);
        var status = isEmergency ? "EMERGENCY_LOCK" : (command.Equals("OPEN", StringComparison.OrdinalIgnoreCase) ? "OPEN" : "CLOSED");
        await _store.SetBarrierStatusAsync(status);

        await _store.LogAccessEventAsync(new AccessEvent
        {
            LaneId = laneId,
            LaneType = isEmergency ? "EMERGENCY_SYSTEM" : "MANUAL_CONTROL",
            PlacaNormalizada = isEmergency ? "ALERTA_C5" : "MANUAL",
            ConfianzaLpr = 1.0,
            Decision = isEmergency ? "EMERGENCY_LOCK" : (status == "OPEN" ? "ALLOW" : "DENIED"),
            Motivo = isEmergency
                ? $"🚨 CÓDIGO ROJO ACTIVADO: Enclavamiento de subida/cerrado. Alerta a autoridades y LEDs rojos encendidos. ({reason ?? "Sin justificación"})"
                : $"Acción de Supervisor: {command} ({reason ?? "Sin justificación"})",
            Timestamp = DateTime.UtcNow
        });

        return true;
    }
}
