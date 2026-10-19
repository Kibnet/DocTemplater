using System.Globalization;

namespace DocTemplater.Domain;

/// <summary>
/// Настройки приложения
/// </summary>
public class AppSettings
{
    /// <summary>
    /// Расположение файлов шаблонов
    /// </summary>
    public string TemplatesFolderPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DocTemplater", "Templates");
    
    /// <summary>
    /// Расположение по умолчанию для сгенерированных файлов
    /// </summary>
    public string DefaultOutputPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    
    /// <summary>
    /// Культура для форматирования дат и чисел
    /// </summary>
    public string CultureName { get; set; } = CultureInfo.CurrentCulture.Name;
    
    /// <summary>
    /// Состояние по умолчанию для флажка "Открыть после сохранения"
    /// </summary>
    public bool OpenAfterSaveDefault { get; set; } = false;
    
    /// <summary>
    /// Список сохраненных данных шаблонов
    /// </summary>
    public List<SavedTemplateData> Templates { get; set; } = new();
}
