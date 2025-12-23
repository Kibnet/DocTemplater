using System;
using System.Collections.Generic;
using System.Globalization;
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
}
