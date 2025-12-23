using System.Globalization;
using DocTemplater.Domain;
using ReactiveUI;

namespace DocTemplater.ViewModels;

/// <summary>
/// ViewModel для одного поля плейсхолдера
/// </summary>
public class PlaceholderFieldViewModel : ViewModelBase
{
    private readonly Placeholder _placeholder;
    private readonly CultureInfo _culture;
    
    private object? _value;
    private List<string> _previewTexts = new();
    private string? _validationError;

    public PlaceholderFieldViewModel(Placeholder placeholder, CultureInfo culture, object? savedValue = null)
    {
        _placeholder = placeholder;
        _culture = culture;

        // Установить сохраненное значение, если есть
        if (savedValue != null)
        {
            Value = savedValue;
        }

        // Подписаться на изменения значения для обновления превью
        this.WhenAnyValue(x => x.Value)
            .Subscribe(_ => UpdatePreview());
    }

    /// <summary>
    /// Плейсхолдер
    /// </summary>
    public Placeholder Placeholder => _placeholder;

    /// <summary>
    /// Введенное значение
    /// </summary>
    public object? Value
    {
        get => _value;
        set
        {
            this.RaiseAndSetIfChanged(ref _value, value);
            Validate();
        }
    }

    /// <summary>
    /// Список текстов превью для каждого формата
    /// </summary>
    public List<string> PreviewTexts
    {
        get => _previewTexts;
        set => this.RaiseAndSetIfChanged(ref _previewTexts, value);
    }

    /// <summary>
    /// Ошибка валидации
    /// </summary>
    public string? ValidationError
    {
        get => _validationError;
        set => this.RaiseAndSetIfChanged(ref _validationError, value);
    }

    /// <summary>
    /// Проверить, валидно ли значение
    /// </summary>
    public bool IsValid => string.IsNullOrEmpty(ValidationError);

    /// <summary>
    /// Название плейсхолдера для отображения
    /// </summary>
    public string Label => _placeholder.Name;

    /// <summary>
    /// Является ли поле датой
    /// </summary>
    public bool IsDateTimeType => _placeholder.DataType == PlaceholderDataType.DateTimeOffset;

    /// <summary>
    /// Является ли поле целым числом
    /// </summary>
    public bool IsIntegerType => _placeholder.DataType == PlaceholderDataType.Integer;

    /// <summary>
    /// Является ли поле строкой
    /// </summary>
    public bool IsStringType => _placeholder.DataType == PlaceholderDataType.String;

    /// <summary>
    /// Значение в текстовом формате (для TextBox)
    /// </summary>
    public string ValueText
    {
        get => Value?.ToString() ?? string.Empty;
        set
        {
            if (_placeholder.DataType == PlaceholderDataType.Integer)
            {
                if (int.TryParse(value, out var intValue))
                {
                    Value = intValue;
                }
                else
                {
                    Value = null;
                }
            }
            else
            {
                Value = value;
            }
        }
    }

    /// <summary>
    /// Значение даты (для DatePicker)
    /// </summary>
    public DateTimeOffset? DateValue
    {
        get => Value as DateTimeOffset? ?? (Value as DateTime?)?.ToUniversalTime();
        set => Value = value;
    }

    /// <summary>
    /// Значение целого числа (для NumericUpDown)
    /// </summary>
    public int? IntegerValue
    {
        get => Value as int?;
        set => Value = value;
    }

    /// <summary>
    /// Превью форматированного значения (объединенные строки)
    /// </summary>
    public string Preview => string.Join("\n", PreviewTexts);

    /// <summary>
    /// Обновить превью форматированных значений
    /// </summary>
    private void UpdatePreview()
    {
        var previews = new List<string>();

        if (Value == null)
        {
            PreviewTexts = previews;
            return;
        }

        foreach (var format in _placeholder.FormatPatterns)
        {
            try
            {
                var formatted = FormatValue(Value, format);
                var previewLine = string.IsNullOrEmpty(format)
                    ? formatted
                    : $"{format} → {formatted}";
                previews.Add(previewLine);
            }
            catch (Exception ex)
            {
                previews.Add($"{format} → Ошибка: {ex.Message}");
            }
        }

        // Если нет форматов, просто показать значение
        if (previews.Count == 0 && Value != null)
        {
            previews.Add(Value.ToString() ?? string.Empty);
        }

        PreviewTexts = previews;
    }

    /// <summary>
    /// Форматировать значение
    /// </summary>
    private string FormatValue(object value, string format)
    {
        return value switch
        {
            DateTimeOffset dto => string.IsNullOrEmpty(format)
                ? dto.ToString(_culture)
                : dto.ToString(format, _culture),
            DateTime dt => string.IsNullOrEmpty(format)
                ? dt.ToString(_culture)
                : dt.ToString(format, _culture),
            int i => string.IsNullOrEmpty(format)
                ? i.ToString(_culture)
                : i.ToString(format, _culture),
            string s => s,
            _ => value.ToString() ?? string.Empty
        };
    }

    /// <summary>
    /// Валидация значения
    /// </summary>
    private void Validate()
    {
        ValidationError = null;

        if (Value == null)
        {
            ValidationError = "Значение не может быть пустым";
            return;
        }

        // Валидация по типу
        switch (_placeholder.DataType)
        {
            case PlaceholderDataType.Integer:
                if (Value is not int)
                {
                    ValidationError = "Значение должно быть целым числом";
                }
                break;

            case PlaceholderDataType.DateTimeOffset:
                if (Value is not DateTimeOffset and not DateTime)
                {
                    ValidationError = "Значение должно быть датой";
                }
                break;

            case PlaceholderDataType.String:
                if (Value is not string || string.IsNullOrWhiteSpace(Value.ToString()))
                {
                    ValidationError = "Значение не может быть пустой строкой";
                }
                break;
        }
    }
}
