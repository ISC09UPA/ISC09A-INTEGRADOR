namespace AirportParking.Application.DTOs;

public record AccessEvaluationRequest(
    string LaneId,
    string LaneType, // ENTRY, EXIT
    string Placa,
    double Confianza = 0.95
);

public record AccessEvaluationResponse(
    string Decision, // ALLOW, DENIED, REVIEW_REQUIRED
    string Motivo,
    bool OpenBarrier,
    decimal AmountDue,
    string? StayId,
    string? CustomerName
);

public record BarrierCommandRequest(
    string LaneId,
    string Command, // OPEN, CLOSE
    string? Reason
);
