using AirportParking.Application.DTOs;

namespace AirportParking.Application.Common.Interfaces;

public interface IAccessDecisionService
{
    Task<AccessEvaluationResponse> EvaluateAccessAsync(AccessEvaluationRequest request);
    Task<bool> SetBarrierStateAsync(string laneId, string command, string? reason);
}
