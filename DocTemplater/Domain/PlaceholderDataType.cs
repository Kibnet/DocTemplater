namespace DocTemplater.Domain;

/// <summary>
/// Перечисление типов данных плейсхолдеров
/// </summary>
public enum PlaceholderDataType
{
    /// <summary>
    /// Строка
    /// </summary>
    String,
    
    /// <summary>
    /// Целое число
    /// </summary>
    Integer,
    
    /// <summary>
    /// Дата и время с часовым поясом
    /// </summary>
    DateTimeOffset
}
