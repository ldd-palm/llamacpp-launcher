namespace LlamaCppLauncher.Validation;

public sealed class ValidationResult
{
    public List<string> Errors { get; } = new();
    public bool IsValid => Errors.Count == 0;

    public void AddError(string message) => Errors.Add(message);
}
