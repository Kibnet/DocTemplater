namespace DocTemplater.Domain;

/// <summary>
/// Значение плейсхолдера, введенное пользователем
/// </summary>
public class PlaceholderValue
{
    /// <summary>
    /// Имя плейсхолдера
    /// </summary>
    public string PlaceholderName { get; set; } = string.Empty;
    
    /// <summary>
    /// Введенное пользователем значение (строка, целое число или дата)
    /// </summary>
    public object? RawValue { get; set; }
    
    /// <summary>
    /// Образцы форматированного вывода для каждого шаблона формата
    /// </summary>
    public List<string> FormattedPreviews { get; set; } = new();
}
