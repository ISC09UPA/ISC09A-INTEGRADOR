namespace CiudadInteligente.Application.Interfaces;

/// <summary>Hash de contrasenas con sal (la contrasena en claro nunca se guarda).</summary>
public interface IHasherContrasenas
{
    string Hash(string contrasena);

    bool Verificar(string contrasena, string hash);
}
