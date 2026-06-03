namespace MySQLConfigurationAndSsh.Parser;

public class TrackedProperty<T>
{
    public T OriginalValue { get; }
    public T CurrentValue { get; set; }
    public bool IsModified => !Equals(OriginalValue, CurrentValue);

    public TrackedProperty(T originalValue)
    {
        OriginalValue = originalValue;
        CurrentValue = originalValue;
    }
}
