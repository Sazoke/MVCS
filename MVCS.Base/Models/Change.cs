namespace MVCS.Base.Models;

public class Change
{
    public string PropertyName { get; set; } = null!;
    
    public string? Value { get; set; }
}