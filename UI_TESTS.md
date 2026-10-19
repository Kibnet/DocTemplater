# UI-тесты DocTemplater

Проект `DocTemplater.UITests` использует xUnit/VSTest и Avalonia.Headless.XUnit. Skia рисует настоящий интерфейс в памяти, без окна на рабочем столе.

## Запуск

```powershell
dotnet build DocTemplater.sln
dotnet test DocTemplater.sln
# Только UI:
dotnet test DocTemplater.UITests/DocTemplater.UITests.csproj
```

Для сохранения PNG задайте каталог артефактов:

```powershell
$env:DOCTEMPLATER_UI_ARTIFACTS = Join-Path $env:TEMP 'DocTemplater-ui-artifacts'
dotnet test DocTemplater.UITests/DocTemplater.UITests.csproj --filter "FullyQualifiedName~SavedValuesWorkflowTests"
```

Без переменной тесты выполняют те же проверки, но не сохраняют PNG. Артефакты находятся вне репозитория и не включаются в коммиты автоматически.

## Сценарии

- `TemplateSelectionViewTests`: элементы экрана выбора, загрузка и обновление.
- `DataInputViewTests`: поля дат и быстрые кнопки, вставка токенов в имя файла, доступность настроек при ширине 800/900, обработчик подтверждения перезаписи.
- `OverwriteConfirmationDialogTests`: явное подтверждение, отмена, Escape и закрытие диалога.
- `SavedValuesWorkflowTests`: нажатия через Headless mouse input, ошибки обязательных полей, сохранение значений в реальный временный JSON, отказ записи и повтор, загрузка через новый service/VM, отсутствие генерации DOCX и перехода назад при сохранении значений. После нового ввода проверяется статус «Есть несохранённые изменения», прежнее содержимое JSON до явного сохранения и новый результат после повторного сохранения. Отдельно проверяется сохранение документа. Открытие папки проверяется при пустом списке и после смены каталога, включая отказ shell.

Новые сценарии проверяют размещение действий при 800×600 и 900×600 и могут сохранять кадры успеха/ошибок. UI tests не используют пользовательский settings.json; shell opener подменён, Проводник при тестах не открывается.

## Селекторы

Существующие `Name` сохранены: `RefreshButton`, `SelectTemplateButton`, `TemplatesListBox`, `FieldsItemsControl`, `ValueTextBox`, `IntegerValueUpDown`, `DateValuePicker`, `OutputFilePathTextBox`, `BackButton`, `ClearButton`, `SaveButton`.

Новые: `SaveValuesButton`, `StatusMessageTextBlock`, `OpenTemplatesDirectoryButton`. `SaveButton` по-прежнему создаёт документ; сохранение только значений выполняет `SaveValuesButton`.

## Границы evidence

PNG получены из `CaptureRenderedFrame` с реальным Skia rendering. Их нужно просмотреть: успешные bounds assertions сами по себе не подтверждают качество отображения. Текущий harness не записывает видео; fallback — PNG и журнал тестов. Native запуск, появление окна Проводника и поведение установленной версии проверяются отдельно.
