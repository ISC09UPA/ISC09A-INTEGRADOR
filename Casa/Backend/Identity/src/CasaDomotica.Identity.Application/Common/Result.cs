namespace CasaDomotica.Identity.Application.Common;

public abstract record Result<T>
{
    private Result() { }

    public sealed record Success(T Value) : Result<T>;

    public sealed record Failure(string Code, string Message) : Result<T>;
}
