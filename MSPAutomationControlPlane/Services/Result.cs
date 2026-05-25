namespace MSPAutomationControlPlane.Services;

public sealed class Result<T>
{
    private Result(T? value, string? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public string? Error { get; }

    public bool Succeeded => Error is null;

    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Failure(string error) => new(default, error);
}
