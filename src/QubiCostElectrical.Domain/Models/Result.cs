namespace QubiCostElectrical;

/// <summary>
/// Resultado explícito para operaciones de dominio/aplicación.
/// Evita usar excepciones como flujo normal y entrega errores testeables a Presenters/Views.
/// </summary>
public readonly record struct Result<TValue, TError>(bool IsSuccess, TValue? Value, TError? Error)
{
    public bool IsFailure => !IsSuccess;

    public static Result<TValue, TError> Success(TValue value) => new(true, value, default);
    public static Result<TValue, TError> Failure(TError error) => new(false, default, error);
}

public readonly record struct Result(bool IsSuccess, string? Error)
{
    public bool IsFailure => !IsSuccess;

    public static Result Success() => new(true, null);
    public static Result Failure(string error) => new(false, error);
}

public sealed record ValidationError(string Code, string Message, string Field = "");
