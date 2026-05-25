namespace MSPAutomationControlPlane.Services;

public sealed class Result<T>
{
    private Result(T? value, IReadOnlyList<string> errors)
    {
        Value = value;
        Errors = errors;
    }

    public T? Value { get; }

    public IReadOnlyList<string> Errors { get; }

    public string? Error => Errors.Count == 0 ? null : string.Join(" ", Errors);

    public bool Succeeded => Errors.Count == 0;

    public static Result<T> Success(T value) => new(value, []);

    public static Result<T> Failure(string error) => new(default, [error]);

    public static Result<T> Failure(IReadOnlyList<string> errors) => new(default, errors);
}
