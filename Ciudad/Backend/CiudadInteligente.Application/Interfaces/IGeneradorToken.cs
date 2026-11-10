using CiudadInteligente.Domain.Entities;

namespace CiudadInteligente.Application.Interfaces;

/// <summary>Crea el token de sesion (JWT) que la app manda en cada peticion. Lo implementa la API.</summary>
public interface IGeneradorToken
{
    (string Token, DateTimeOffset ExpiraEn) Generar(Usuario usuario);
}
