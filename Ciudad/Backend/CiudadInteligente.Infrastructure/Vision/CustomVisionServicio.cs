using System.Net.Http.Headers;
using System.Text.Json;
using CiudadInteligente.Application.Exceptions;
using CiudadInteligente.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CiudadInteligente.Infrastructure.Vision;

/// <param name="Url">
/// URL de prediccion completa del proyecto de deteccion de objetos, p. ej.
/// https://&lt;recurso&gt;.cognitiveservices.azure.com/customvision/v3.0/Prediction/&lt;projectId&gt;/detect/iterations/carros-v1/image
/// </param>
public sealed record OpcionesVision(string Url, string Llave, double Umbral)
{
    public bool EstaListo => !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(Llave);
}

/// <summary>Llama a la prediccion REST de Custom Vision y cuenta las detecciones por franja.</summary>
public sealed class CustomVisionServicio(HttpClient http, OpcionesVision opciones, ILogger<CustomVisionServicio> logger)
    : IServicioVision
{
    private const int Accesos = 4;

    public bool EstaConfigurado => opciones.EstaListo;

    public async Task<IReadOnlyList<int>> ContarPorAccesoAsync(Stream imagen, CancellationToken ct)
    {
        if (!EstaConfigurado)
        {
            throw new InvalidOperationException("Vision no esta configurado.");
        }

        using var peticion = new HttpRequestMessage(HttpMethod.Post, opciones.Url)
        {
            Content = new StreamContent(imagen)
        };
        peticion.Headers.Add("Prediction-Key", opciones.Llave);
        peticion.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var respuesta = await http.SendAsync(peticion, ct);
        if (!respuesta.IsSuccessStatusCode)
        {
            logger.LogWarning("Custom Vision respondio {Codigo}", (int)respuesta.StatusCode);
            throw new ReglaNegocioException($"El servicio de vision rechazo la imagen ({(int)respuesta.StatusCode}).");
        }

        using var doc = await JsonDocument.ParseAsync(await respuesta.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var conteo = new int[Accesos];

        if (doc.RootElement.TryGetProperty("predictions", out var predicciones))
        {
            foreach (var p in predicciones.EnumerateArray())
            {
                if (p.GetProperty("probability").GetDouble() < opciones.Umbral) continue;

                var caja = p.GetProperty("boundingBox");
                var centroX = caja.GetProperty("left").GetDouble() + caja.GetProperty("width").GetDouble() / 2;
                var franja = Math.Clamp((int)(centroX * Accesos), 0, Accesos - 1);
                conteo[franja]++;
            }
        }

        return conteo;
    }
}
