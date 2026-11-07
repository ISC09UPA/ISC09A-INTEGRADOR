using System.Net;
using System.Net.Http.Json;
using InvernaderoInteligente.Application.DTOs;
using InvernaderoInteligente.Functions.Models;

namespace InvernaderoInteligente.Functions.Services;

/// <summary>Resultado de un POST a la API.</summary>
public enum ResultadoRegistro
{
    /// <summary>201 Created: se guardó.</summary>
    Creado,
    /// <summary>409 Conflict: ya existía (reintento de un mensaje). Se toma como éxito.</summary>
    Duplicado
}

public interface ITelemetriaApiClient
{
    Task<ResultadoRegistro> RegistrarLecturaAsync(CrearLecturaDto dto, CancellationToken ct = default);
    Task<ResultadoRegistro> RegistrarRiegoAsync(CrearRiegoDto dto, CancellationToken ct = default);
}

/// <summary>Llama a los endpoints POST de la API de Telemetría (contrato en CONTRATOS.md).</summary>
public class TelemetriaApiClient(HttpClient http) : ITelemetriaApiClient
{
    public const string HeaderApiKey = "X-Api-Key";

    public const string RutaLecturas = "api/Lecturas";
    public const string RutaRiegos = "api/Riegos";

    public Task<ResultadoRegistro> RegistrarLecturaAsync(CrearLecturaDto dto, CancellationToken ct = default)
        => PostAsync(RutaLecturas, dto, ct);

    public Task<ResultadoRegistro> RegistrarRiegoAsync(CrearRiegoDto dto, CancellationToken ct = default)
        => PostAsync(RutaRiegos, dto, ct);

    private async Task<ResultadoRegistro> PostAsync<T>(string ruta, T dto, CancellationToken ct)
    {
        using var respuesta = await http.PostAsJsonAsync(ruta, dto, ct);
        if (respuesta.IsSuccessStatusCode)
            return ResultadoRegistro.Creado;

        var codigo = respuesta.StatusCode;

        // 409 = la API ya tenía ese registro (idempotencia). No es error.
        if (codigo == HttpStatusCode.Conflict)
            return ResultadoRegistro.Duplicado;

        var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);

        // 401/403 = configuración (ApiKey) mal puesta: reintentar sirve cuando se corrija,
        // así que NO se manda a dead-letter de inmediato.
        if (codigo is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new HttpRequestException(
                $"La API rechazó la autenticación en POST {ruta} ({(int)codigo}). Revisa TelemetriaApi__ApiKey.",
                null, codigo);

        // Otros 4xx (salvo 404, 408 y 429) = la API rechazó los datos; reintentar no sirve.
        if ((int)codigo is >= 400 and < 500
            && codigo is not HttpStatusCode.NotFound
            && codigo is not HttpStatusCode.RequestTimeout
            && codigo is not HttpStatusCode.TooManyRequests)
        {
            throw new MensajeInvalidoException($"La API rechazó POST {ruta} ({(int)codigo}): {cuerpo}");
        }

        // 5xx, 404 (API a medio desplegar / URL mal), 408 o 429 = temporal; Service Bus reintentará.
        throw new HttpRequestException($"Error temporal en POST {ruta} ({(int)codigo}): {cuerpo}", null, codigo);
    }
}
