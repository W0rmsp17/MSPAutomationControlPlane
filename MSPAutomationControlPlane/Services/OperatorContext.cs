namespace MSPAutomationControlPlane.Services;

public sealed class OperatorContext : IOperatorContext
{
    private static readonly AsyncLocal<string?> Current = new();

    public string CurrentOperator => Current.Value ?? "system";

    public void SetCurrentOperator(string? operatorName)
    {
        Current.Value = string.IsNullOrWhiteSpace(operatorName) ? null : operatorName;
    }
}
