namespace MSPAutomationControlPlane.Services;

public sealed class StubOperatorContext : IOperatorContext
{
    public string CurrentOperator => "operator@local.dev";
}
