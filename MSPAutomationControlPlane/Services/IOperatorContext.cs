namespace MSPAutomationControlPlane.Services;

public interface IOperatorContext
{
    string CurrentOperator { get; }

    void SetCurrentOperator(string? operatorName);
}
