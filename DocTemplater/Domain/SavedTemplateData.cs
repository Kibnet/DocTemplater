namespace DocTemplater.Domain;

/// <summary>
/// Сохраненные данные шаблона для повторного использования
/// </summary>
public class SavedTemplateData
{
    /// <summary>
    /// Имя связанного файла шаблона
    /// </summary>
    public string TemplateFileName { get; set; } = string.Empty;

    /// <summary>
    /// Сопоставление имени плейсхолдера и значения
    /// </summary>
    public Dictionary<string, object> FieldValues { get; set; } = new();
    
    /// <summary>
    /// Время последнего заполнения этого шаблона
    /// </summary>
    public DateTimeOffset LastUsed { get; set; }

    /// <summary>
    /// Последний каталог сохранения файла для этого шаблона
    /// </summary>
    public string? LastSaveDirectory { get; set; }

    /// <summary>
    /// Последний шаблон имени выходного файла для этого шаблона
    /// </summary>
    public string? OutputFileNameTemplate { get; set; }
}
