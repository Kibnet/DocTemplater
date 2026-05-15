using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive.Linq;
using DocTemplater.Domain;
using DocTemplater.ViewModels;
using Xunit;

namespace DocTemplater.Tests.ViewModels;

/// <summary>
/// Тесты для PlaceholderFieldViewModel
/// </summary>
public class PlaceholderFieldViewModelTests
{
    [Fact]
    public void IntegerValue_BindsToValueForIntegerType()
    {
        // Arrange
        var placeholder = new Placeholder
        {
            Name = "Возраст",
            DataType = PlaceholderDataType.Integer,
            FormatPatterns = new List<string>()
        };
        
        var viewModel = new PlaceholderFieldViewModel(
            placeholder, 
            CultureInfo.GetCultureInfo("ru-RU"));

        // Act
        viewModel.IntegerValue = 25;

        // Assert
        Assert.Equal(25, viewModel.Value);
        Assert.Equal(25, viewModel.IntegerValue);
    }

    [Fact]
    public void IntegerValue_UpdatesPreview()
    {
        // Arrange
        var placeholder = new Placeholder
        {
            Name = "Количество",
            DataType = PlaceholderDataType.Integer,
            FormatPatterns = new List<string> { "N0" }
        };
        
        var viewModel = new PlaceholderFieldViewModel(
            placeholder, 
            CultureInfo.GetCultureInfo("ru-RU"));

        // Act
        viewModel.IntegerValue = 1000;

        // Assert
        Assert.NotEmpty(viewModel.PreviewTexts);
        Assert.Contains("1", viewModel.Preview); // Часть отформатированного числа
    }

    [Fact]
    public void IsIntegerType_ReturnsTrueForIntegerPlaceholder()
    {
        // Arrange
        var placeholder = new Placeholder
        {
            Name = "Число",
            DataType = PlaceholderDataType.Integer,
            FormatPatterns = new List<string>()
        };
        
        var viewModel = new PlaceholderFieldViewModel(
            placeholder, 
            CultureInfo.GetCultureInfo("ru-RU"));

        // Assert
        Assert.True(viewModel.IsIntegerType);
        Assert.False(viewModel.IsStringType);
        Assert.False(viewModel.IsDateTimeType);
    }

    [Fact]
    public void IsStringType_ReturnsTrueForStringPlaceholder()
    {
        // Arrange
        var placeholder = new Placeholder
        {
            Name = "Имя",
            DataType = PlaceholderDataType.String,
            FormatPatterns = new List<string>()
        };
        
        var viewModel = new PlaceholderFieldViewModel(
            placeholder, 
            CultureInfo.GetCultureInfo("ru-RU"));

        // Assert
        Assert.True(viewModel.IsStringType);
        Assert.False(viewModel.IsIntegerType);
        Assert.False(viewModel.IsDateTimeType);
    }

    [Fact]
    public void IsDateTimeType_ReturnsTrueForDateTimePlaceholder()
    {
        // Arrange
        var placeholder = new Placeholder
        {
            Name = "Дата",
            DataType = PlaceholderDataType.DateTimeOffset,
            FormatPatterns = new List<string>()
        };
        
        var viewModel = new PlaceholderFieldViewModel(
            placeholder, 
            CultureInfo.GetCultureInfo("ru-RU"));

        // Assert
        Assert.True(viewModel.IsDateTimeType);
        Assert.False(viewModel.IsStringType);
        Assert.False(viewModel.IsIntegerType);
    }

    [Fact]
    public void Preview_UpdatesAutomaticallyWhenValueChanges()
    {
        // Arrange
        var placeholder = new Placeholder
        {
            Name = "Текст",
            DataType = PlaceholderDataType.String,
            FormatPatterns = new List<string>()
        };
        
        var viewModel = new PlaceholderFieldViewModel(
            placeholder, 
            CultureInfo.GetCultureInfo("ru-RU"));

        // Act
        viewModel.Value = "Новое значение";

        // Assert
        Assert.Contains("Новое значение", viewModel.Preview);
    }

    [Fact]
    public void AddDayCommand_AddsOneDayToDateValue()
    {
        var viewModel = CreateDateViewModel();
        var initialValue = new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.FromHours(3));

        viewModel.DateValue = initialValue;
        viewModel.AddDayCommand.Execute().Subscribe();

        Assert.Equal(initialValue.AddDays(1), viewModel.DateValue);
    }

    [Fact]
    public void AddMonthCommand_UsesFrameworkMonthArithmetic()
    {
        var viewModel = CreateDateViewModel();
        var initialValue = new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.FromHours(3));

        viewModel.DateValue = initialValue;
        viewModel.AddMonthCommand.Execute().Subscribe();

        Assert.Equal(initialValue.AddMonths(1), viewModel.DateValue);
    }

    [Fact]
    public void AddYearCommand_UsesFrameworkYearArithmetic()
    {
        var viewModel = CreateDateViewModel();
        var initialValue = new DateTimeOffset(2024, 2, 29, 0, 0, 0, TimeSpan.FromHours(3));

        viewModel.DateValue = initialValue;
        viewModel.AddYearCommand.Execute().Subscribe();

        Assert.Equal(initialValue.AddYears(1), viewModel.DateValue);
    }

    [Fact]
    public void AddDayCommand_WhenDateValueIsEmpty_UsesCurrentLocalDate()
    {
        var viewModel = CreateDateViewModel();
        var startDate = DateTimeOffset.Now.Date;

        viewModel.AddDayCommand.Execute().Subscribe();

        var endDate = DateTimeOffset.Now.Date;
        Assert.NotNull(viewModel.DateValue);
        Assert.True(
            viewModel.DateValue.Value.Date == startDate.AddDays(1).Date ||
            viewModel.DateValue.Value.Date == endDate.AddDays(1).Date);
    }

    [Fact]
    public void AddDayCommand_UpdatesPreview()
    {
        var viewModel = CreateDateViewModel("d.MM.yyyy");

        viewModel.DateValue = new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.FromHours(3));
        viewModel.AddDayCommand.Execute().Subscribe();

        Assert.Contains("16.05.2026", viewModel.Preview);
    }

    private static PlaceholderFieldViewModel CreateDateViewModel(params string[] formats)
    {
        var placeholder = new Placeholder
        {
            Name = "Дата",
            DataType = PlaceholderDataType.DateTimeOffset,
            FormatPatterns = new List<string>(formats)
        };

        return new PlaceholderFieldViewModel(
            placeholder,
            CultureInfo.GetCultureInfo("ru-RU"));
    }
}
