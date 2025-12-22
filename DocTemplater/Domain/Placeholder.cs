namespace DocTemplater.Domain;

/// <summary>
/// Сущность плейсхолдера, представляющая поле подстановки в шаблоне
/// </summary>
public class Placeholder
{
    /// <summary>
    /// Уникальное имя плейсхолдера (например, "Дата начала")
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Тип данных плейсхолдера
    /// </summary>
    public PlaceholderDataType DataType { get; set; }
    
    /// <summary>
    /// Все уникальные строки формата, найденные для этого плейсхолдера
    /// </summary>
    public List<string> FormatPatterns { get; set; } = new();
    
    /// <summary>
    /// Количество раз, когда этот плейсхолдер встречается в шаблоне
    /// </summary>
    public int OccurrenceCount { get; set; }
}
