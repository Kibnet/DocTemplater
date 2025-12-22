namespace DocTemplater.Domain;

/// <summary>
/// Сущность шаблона документа
/// </summary>
public class Template
{
    /// <summary>
    /// Внутренний идентификатор шаблона
    /// </summary>
    public Guid TemplateId { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Имя DOCX файла
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Полный путь к файлу шаблона
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Извлеченные плейсхолдеры из документа
    /// </summary>
    public List<Placeholder> Placeholders { get; set; } = new();
    
    /// <summary>
    /// Временная метка изменения файла
    /// </summary>
    public DateTimeOffset LastModified { get; set; }

    /// <summary>
    /// Отображаемое имя (имя файла без расширения)
    /// </summary>
    public string DisplayName => Path.GetFileNameWithoutExtension(FileName);
}
