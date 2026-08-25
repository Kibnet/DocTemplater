# Шаблон имени выходного файла

## 0. Метаданные
- Тип (профиль): `delivery-task`; stack profile `dotnet-desktop-client`; overlay profile `ui-automation-testing`
- Владелец: Codex
- Масштаб: medium
- Целевая модель: gpt-5.5
- Целевой релиз / ветка: текущая рабочая ветка
- Ограничения: до повторного утверждения нового scope изменяется только этот spec; публичный формат плейсхолдеров DOCX не меняется; существующие automation-id не ломать
- Связанные ссылки: `C:\Users\Kibnet\.codex\agents\AGENTS.md`, `instructions/core/quest-governance.md`, `instructions/profiles/dotnet-desktop-client.md`, `instructions/profiles/ui-automation-testing.md`

## 1. Overview / Цель
Добавить на экран ввода данных отдельное текстовое поле "Шаблон имени файла", в котором можно использовать плейсхолдеры параметров шаблона. Рядом с каждым editor-ом параметра добавить кнопку вставки плейсхолдера этого параметра в текущую позицию курсора шаблона имени. При изменении шаблона или значений параметров полный путь сохранения должен пересчитываться автоматически.

Outcome contract:
- Success means: пользователь может собрать имя выходного файла из текста и токенов параметров; итоговый `OutputFilePath` обновляется без ручного редактирования пути; сохранение использует пересчитанный путь; выбранный шаблон имени восстанавливается при следующем открытии этого DOCX-шаблона.
- Итоговый артефакт / output: изменения ViewModel, XAML, code-behind при необходимости фокуса/курсора, unit/UI regression tests.
- Stop rules: остановиться после прохода targeted tests, `dotnet build`, полного `dotnet test`, либо явно зафиксировать технический блокер проверки.

## 2. Текущее состояние (AS-IS)
- `DocTemplater/ViewModels/DataInputViewModel.cs` хранит `OutputDirectory`, `OutputFileName`, computed `OutputFilePath`.
- `OutputFileName` по умолчанию устанавливается в `{template.FileName}_заполненный`.
- `OutputFilePath` setter разбирает выбранный пользователем путь и записывает директорию и имя файла.
- `SaveDocumentAsync` формирует `outputPath` через `Path.Combine(OutputDirectory, OutputFileName + ".docx")`.
- `SavedTemplateData` хранит значения полей и `LastSaveDirectory`, но не хранит `OutputFileNameTemplate`.
- `ValuePersistenceService.SaveTemplateValuesAsync` сейчас сохраняет только field values и последний каталог.
- `DocTemplater/Views/DataInputView.axaml` показывает только поле полного пути `OutputFilePathTextBox`; отдельного поля шаблона имени нет.
- Динамические editor-ы параметров создаются через `ItemsControl` по `Fields`; сами поля представлены `PlaceholderFieldViewModel`.
- UI tests уже есть в `DocTemplater.UITests/DataInputViewTests.cs` на Avalonia Headless.
- Существующее dirty-состояние до задачи: `.gitignore` изменён не этой задачей.

## 3. Проблема
Имя файла сейчас является статичным текстом или ручным вводом полного пути, поэтому пользователь не может автоматически включить значения параметров документа в имя выходного файла.

## 4. Цели дизайна
- Разделение ответственности: форматирование имени файла и подстановка токенов живут во ViewModel, UI только вводит шаблон и вызывает команды вставки.
- Повторное использование: использовать значение и форматные правила `PlaceholderFieldViewModel`/`Placeholder`, но выбирать представление для имени файла детерминированно.
- Тестируемость: покрыть рендер имени unit-тестами и наличие/работу кнопок вставки UI-тестами.
- Консистентность: оставить выбор папки/файла через существующий `OutputFilePath` и `BrowseForSaveFileAsync`.
- Обратная совместимость: default остается визуально эквивалентным текущему имени `{template.FileName}_заполненный`, если пользователь не использует плейсхолдеры.

## 5. Non-Goals (чего НЕ делаем)
- Не меняем синтаксис плейсхолдеров внутри DOCX (`{Name:Type(format)}`).
- Не добавляем глобальный шаблон имени для всех DOCX-шаблонов.
- Не добавляем отдельный редактор форматов для имени файла.
- Не меняем сервис генерации DOCX.
- Не решаем подтверждение перезаписи файла.

## 6. Предлагаемое решение (TO-BE)
### 6.1 Распределение ответственности
- `DataInputViewModel` -> хранит `OutputFileNameTemplate`, рендерит `OutputFileName`, пересчитывает `OutputFilePath`, принимает запрос на вставку токена.
- `PlaceholderFieldViewModel` -> предоставляет `PlaceholderToken` и команду запроса вставки токена через делегат/observable owner ViewModel.
- `DataInputView.axaml` -> добавляет отдельное поле шаблона имени и кнопку вставки рядом с каждым editor-ом; новые UI elements получают стабильные `Name`.
- `DataInputView.axaml.cs` -> если нужно, отслеживает caret в `OutputFileNameTemplateTextBox` и передает позицию вставки во ViewModel.
- `DocTemplater.Tests` -> проверяет рендер шаблона и обновление пути.
- `SavedTemplateData` / `ValuePersistenceService` -> сохраняют и загружают шаблон имени файла для конкретного DOCX-шаблона.
- `DocTemplater.UITests` -> проверяет наличие кнопок вставки рядом с editor-ами и user flow вставки токена.

### 6.2 Детальный дизайн
- Синтаксис токена имени файла: `{ИмяПлейсхолдера}`. Он намеренно короче DOCX-плейсхолдера и не включает тип/формат.
- `OutputFileNameTemplate` по умолчанию: текущее имя шаблона как литерал плюс суффикс `_заполненный`, например `Договор_заполненный`. Специальный токен `{TemplateName}` не вводится.
- При загрузке сохраненных данных для шаблона, если `SavedTemplateData.OutputFileNameTemplate` не пустой, он становится текущим `OutputFileNameTemplate`; иначе используется default из имени шаблона.
- При успешном сохранении документа `OutputFileNameTemplate` сохраняется вместе с field values и `LastSaveDirectory` для текущего DOCX-шаблона.
- Если пользователь выбрал файл через `OutputFilePath`/диалог, обновленный `OutputFileNameTemplate` также сохраняется при следующем успешном сохранении документа.
- `OutputFileName` становится вычисляемым результатом шаблона, но setter сохраняет совместимость с выбором файла: при установке полного пути через `OutputFilePath` имя без расширения записывается обратно в `OutputFileNameTemplate`.
- Поле полного пути остается совместимым с текущим поведением: ручное изменение `OutputFilePath` меняет директорию и перезаписывает `OutputFileNameTemplate` именем файла без расширения. Основным источником имени после этого снова становится шаблон имени.
- Шаблон имени редактируется без расширения. Если пользователь все же ввел итоговое имя с `.docx`, один завершающий suffix `.docx` удаляется case-insensitive перед добавлением стандартного расширения, чтобы не получить `file.docx.docx`.
- При изменении `OutputFileNameTemplate` вызывается пересчет имени и `RaisePropertyChanged(nameof(OutputFileName))`, `RaisePropertyChanged(nameof(OutputFilePath))`.
- При изменении значения любого `PlaceholderFieldViewModel.Value` owner ViewModel пересчитывает `OutputFileName` и `OutputFilePath`.
- Подписки на `Value` полей должны пересоздаваться при замене `Fields` и освобождаться, чтобы не получать повторные пересчеты и не удерживать старые поля.
- Если значение плейсхолдера пустое или невалидное, токен подставляется пустой строкой; валидация полей при сохранении остается прежней.
- Для значения параметра использовать детерминированное filename-представление: если у параметра есть `FormatPatterns`, выбрать первый формат после `OrderBy(StringComparer.Ordinal)`, отформатировать значение с текущей culture; если форматов нет, использовать culture-aware default formatting для типа. Не полагаться на порядок `PreviewTexts[0]`, потому что порядок форматов из extraction pipeline не является контрактом.
- Недопустимые символы имени файла (`Path.GetInvalidFileNameChars()`) заменяются на `_`; итоговое имя `Trim()`.
- Если после подстановки имя пустое, `SaveDocumentAsync` показывает существующую ошибку "Укажите имя выходного файла".
- Вставка токена:
  - кнопка рядом с editor-ом вызывает команду с токеном поля;
  - если в `OutputFileNameTemplateTextBox` есть выделение, вставка заменяет выделенный текст;
  - иначе вставка идет в текущую позицию caret поля `OutputFileNameTemplateTextBox`;
  - если caret/selection неизвестны, токен добавляется в конец шаблона;
  - после вставки фокус возвращается в поле шаблона, caret ставится после токена.
- Нижняя панель сохранения не должна оставаться одной длинной горизонтальной строкой. Настройки сохранения нужно расположить вертикальным блоком внутри центральной колонки: строка шаблона имени, строка полного пути/обзора, строка checkbox при нехватке ширины. Это предотвращает обрезание на текущем design width 800.
- Стабильные UI selectors:
  - поле шаблона имени: `OutputFileNameTemplateTextBox`;
  - кнопка вставки токена в item template: `InsertPlaceholderTokenButton`;
  - контейнер настроек сохранения: `SaveSettingsPanel`;
  - существующие `Name` (`OutputFilePathTextBox`, `BackButton`, `ClearButton`, `SaveButton`, date quick buttons) не переименовывать.
- Visual planning artifact:

```text
Fields scroll area
┌ Field label ──────────────────────────────────────────────┐
│ [editor value ................] [ +token ]   [preview...] │
└───────────────────────────────────────────────────────────┘

Bottom save panel
[Back]   ┌ Имя файла:     [Договор_{Дата}_{Имя}.............] ┐   [Clear] [Save]
         │ Выходной файл: [C:\...\Договор_15.05.docx] [Обзор] │
         │ [ ] Открыть после сохранения                       │
         └────────────────────────────────────────────────────┘
```

- UI test video evidence: fallback. В текущем репозитории есть Avalonia Headless UI tests, но не найден runner/harness, сохраняющий видео. Evidence: Avalonia Headless assertions, при возможности diagnostic screenshots/logs.
- Производительность: пересчет имени линейный по количеству полей и длине шаблона; выполняется только на изменение шаблона/значения.

## 7. Бизнес-правила / Алгоритмы (если есть)
1. Токен параметра строится как `{Placeholder.Name}`.
2. Подстановка выполняется по точному имени параметра, case-sensitive.
3. Если несколько плейсхолдеров имеют пересекающиеся имена, замена должна работать по токенам целиком, а не по частичным подстрокам.
4. Символы, недопустимые в имени файла ОС, заменяются на `_`.
5. Расширение `.docx` не входит в шаблон имени и добавляется как сейчас при сохранении/preview пути.
6. Выбор файла через диалог обновляет `OutputDirectory` и `OutputFileNameTemplate` именем выбранного файла без `.docx`.
7. Токены существуют только для параметров документа; имя шаблона в default является обычным текстом, а не отдельным специальным плейсхолдером.
8. Завершающий `.docx` в результате шаблона нормализуется как расширение, а не как часть имени, чтобы расширение не дублировалось.

## 8. Точки интеграции и триггеры
- Setter `OutputFileNameTemplate` -> пересчет `OutputFileName` и `OutputFilePath`.
- Setter `OutputFilePath` -> обновление `OutputDirectory` и `OutputFileNameTemplate`.
- Создание/замена `Fields` -> переподписка на изменения `Value` и привязка команды вставки токена.
- Изменение `PlaceholderFieldViewModel.Value` -> пересчет `OutputFileName` и `OutputFilePath`.
- Кнопка вставки токена -> изменение `OutputFileNameTemplate`.
- `BrowseForSaveFileAsync` -> suggested file name берется из вычисленного `OutputFileName`.
- `SaveDocumentAsync` -> использует вычисленный `OutputFileName`.

## 9. Изменения модели данных / состояния
- Новое состояние ViewModel: `OutputFileNameTemplate`.
- Новое transient состояние: позиция caret и диапазон selection для шаблона имени, если реализация через code-behind.
- Persisted data: добавить nullable/string property `OutputFileNameTemplate` в `SavedTemplateData`.
- Совместимость JSON: отсутствие свойства в старом `settings.json` должно десериализоваться как `null`/empty и приводить к default template.

## 10. Миграция / Rollout / Rollback
- При первом запуске или старом `settings.json` без `OutputFileNameTemplate` default template дает прежнее имя `{template.FileName}_заполненный`.
- Существующие сохраненные значения полей и последний каталог не меняются.
- Rollback: удалить новое поле/команды и вернуть прямой ввод `OutputFileName`; сохраненное JSON-свойство `OutputFileNameTemplate` можно оставить игнорируемым или удалить из модели.

## 11. Тестирование и критерии приёмки
- Acceptance Criteria:
  - На экране есть отдельное текстовое поле шаблона имени файла.
  - Рядом с каждым editor-ом параметра есть кнопка вставки токена этого параметра.
  - Нажатие кнопки вставляет `{ИмяПараметра}` в текущую позицию курсора шаблона имени.
  - Если в поле шаблона имени выделен текст, кнопка вставки заменяет выделение токеном.
  - Изменение шаблона имени автоматически меняет полный путь сохранения.
  - Изменение значения параметра, использованного в шаблоне, автоматически меняет полный путь сохранения.
  - Выбор файла через "Обзор..." продолжает обновлять директорию и шаблон имени.
  - Сохранение использует пересчитанный путь.
  - После успешного сохранения текущий шаблон имени записывается в settings для этого DOCX-шаблона.
  - При повторном открытии того же DOCX-шаблона сохраненный шаблон имени восстанавливается.
  - Старые настройки без `OutputFileNameTemplate` продолжают открываться и получают default template.
  - Ввод шаблона, который уже заканчивается на `.docx`, не дает двойное расширение в `OutputFilePath`.
- Какие тесты добавить/изменить:
  - Unit: default template renders legacy name.
  - Unit: template with string/date/integer tokens updates `OutputFilePath` after value changes.
  - Unit: placeholder with multiple formats uses deterministic filename formatting independent of input order.
  - Unit: invalid filename chars are sanitized.
  - Unit: trailing `.docx` in rendered template is not duplicated in `OutputFilePath`.
  - Unit: setting `OutputFilePath` updates `OutputFileNameTemplate`.
  - Unit: replacing `Fields` does not duplicate value-change recalculations.
  - Unit: loading saved `OutputFileNameTemplate` restores it into `DataInputViewModel`.
  - Unit: saving document passes current `OutputFileNameTemplate` to persistence.
  - Service: `ValuePersistenceService` persists and reloads `SavedTemplateData.OutputFileNameTemplate`.
  - Service: missing `OutputFileNameTemplate` in existing JSON does not break loading.
  - UI: insert-token buttons exist for all field editor types.
  - UI: clicking insert-token button updates `OutputFileNameTemplateTextBox`/ViewModel.
  - UI: new controls are found through stable `Name` selectors, not text/position selectors.
  - UI: bottom save panel controls remain visible and non-overlapping at 800x600/900x600 headless layout.
- Characterization tests / contract checks: сохранить существующие date quick add tests.
- Visual acceptance: layout должен соответствовать wireframe: отдельное поле шаблона находится в панели сохранения, кнопка вставки токена находится в строке editor-а, существующие preview/error зоны остаются доступны, нижняя панель не обрезает поля/кнопки на design width 800.
- UI video evidence: fallback из-за отсутствия video recorder в текущем Avalonia Headless harness; evidence: `dotnet test .\DocTemplater.UITests\DocTemplater.UITests.csproj`.
- Команды для проверки:
  - `dotnet test .\DocTemplater.Tests\DocTemplater.Tests.csproj --filter FullyQualifiedName~DataInputViewModelTests`
  - `dotnet test .\DocTemplater.UITests\DocTemplater.UITests.csproj --filter FullyQualifiedName~DataInputViewTests`
  - `dotnet build`
  - `dotnet test`
- Stop rules для test/retrieval/tool/validation loops: не расширять поиск после подтверждения всех touched contracts; при падении unrelated tests зафиксировать и отделить от изменений задачи.

## 12. Риски и edge cases
- Caret может быть недоступен из чистой ViewModel; mitigation: code-behind передает позицию и возвращает фокус.
- Нажатие кнопки вставки забирает focus у поля шаблона; mitigation: code-behind хранит последнюю позицию caret/selection перед потерей focus и восстанавливает focus после вставки.
- У одного параметра может быть несколько форматов, порядок которых после извлечения не является надежным; mitigation: для имени файла сортировать `FormatPatterns` ordinal и использовать первый формат.
- Повторная замена `Fields` может оставить старые subscriptions; mitigation: хранить disposable subscriptions и пересоздавать их вместе со списком полей.
- Пользователь может ввести токен неизвестного поля; mitigation: оставлять неизвестный токен как текст или заменять только известные токены. Выбранный вариант: заменять только известные токены, затем sanitize итогового имени.
- Пользователь может ввести `.docx` в шаблон имени; mitigation: удалить один завершающий `.docx` suffix перед добавлением стандартного расширения.
- Добавление второго поля в текущую горизонтальную нижнюю панель может привести к обрезанию на 800px; mitigation: заменить центральную часть панели сохранения на вертикальный блок и покрыть headless layout test.
- Пустые/невалидные значения могут дать пустые части имени; mitigation: сохранение все равно блокируется существующей валидацией полей.
- Длинное имя файла не валидируется отдельно; mitigation: не расширять scope, оставить ошибку файловой системы как текущий общий catch.

## 13. План выполнения
1. Добавить failing unit tests для рендера шаблона, пересчета пути и sanitize.
2. Реализовать `OutputFileNameTemplate`, token rendering, подписки на поля и команду вставки.
3. Обновить XAML/code-behind для поля шаблона и кнопок вставки.
4. Добавить UI regression tests на кнопки и вставку.
5. Запустить targeted tests, затем `dotnet build`, затем полный `dotnet test`.
6. Выполнить post-EXEC review по diff/status/tests.

## 14. Открытые вопросы
Нет блокирующих вопросов. Предположение: синтаксис токена в имени файла `{ИмяПараметра}` подходит, так как пользователь просит плейсхолдер данного параметра, а не полный DOCX-синтаксис с типом и форматом.

## 15. Соответствие профилю
- Профиль: `dotnet-desktop-client`, `ui-automation-testing`
- Выполненные требования профиля:
  - UI-поток не блокируется, логика пересчета синхронная и легкая.
  - Automation ids существующих элементов сохраняются; новые элементы получают конкретные стабильные `Name`: `OutputFileNameTemplateTextBox`, `InsertPlaceholderTokenButton`, `SaveSettingsPanel`.
  - UI behavior покрывается существующим Avalonia Headless suite.
  - Video evidence fallback зафиксирован из-за отсутствия recorder в harness.
  - Перед завершением planned `dotnet build` и `dotnet test`.

## 16. Таблица изменений файлов
| Файл | Изменения | Причина |
| --- | --- | --- |
| `DocTemplater/ViewModels/DataInputViewModel.cs` | шаблон имени, рендер токенов, пересчет пути, команда вставки | основная логика фичи |
| `DocTemplater/ViewModels/PlaceholderFieldViewModel.cs` | token property/command hook | кнопка рядом с каждым editor-ом |
| `DocTemplater/Domain/SavedTemplateData.cs` | persisted `OutputFileNameTemplate` | восстановление шаблона имени между запусками |
| `DocTemplater/Services/IValuePersistenceService.cs` | optional save parameter для шаблона имени | контракт сохранения per-template данных |
| `DocTemplater/Services/ValuePersistenceService.cs` | запись/чтение `OutputFileNameTemplate` | persistence implementation |
| `DocTemplater/Views/DataInputView.axaml` | поле шаблона имени, кнопки вставки, layout панели сохранения | пользовательский UI |
| `DocTemplater/Views/DataInputView.axaml.cs` | caret/focus integration при необходимости | вставка в текущую позицию курсора |
| `DocTemplater.Tests/ViewModels/DataInputViewModelTests.cs` | unit regression coverage | поведение ViewModel |
| `DocTemplater.UITests/DataInputViewTests.cs` | UI regression coverage | presence/user flow |
| `specs/2026-05-15-output-file-name-template.md` | рабочая спецификация и journal | QUEST governance |

## 17. Таблица соответствий (было -> стало)
| Область | Было | Стало |
| --- | --- | --- |
| Имя файла | ручное/статичное `OutputFileName` | `OutputFileName` вычисляется из `OutputFileNameTemplate` |
| Полный путь | меняется при изменении имени/директории | меняется при изменении шаблона/значений/директории |
| Плейсхолдеры в имени | отсутствуют | `{ИмяПараметра}` |
| UI полей | editor + preview | editor + insert-token button + preview |
| Диалог сохранения | suggested name из `OutputFileName` | suggested name из вычисленного `OutputFileName` |
| Сохранение шаблона имени | отсутствует | `OutputFileNameTemplate` хранится per-template в `SavedTemplateData` |

## 18. Альтернативы и компромиссы
- Вариант: использовать полный DOCX-синтаксис `{Имя:Type(format)}`.
- Плюсы: однозначно связан с исходным шаблоном.
- Минусы: шумно для имени файла, требует выбора формата при нескольких форматах.
- Почему выбранное решение лучше в контексте этой задачи: короткий `{Имя}` проще вставлять кнопкой и читать в имени файла; тип/формат уже известны из поля и текущего preview.

- Вариант: оставить только поле полного пути и вставлять токены туда.
- Плюсы: меньше UI.
- Минусы: смешивает директорию, имя и расширение; риск поломать путь при подстановке значений.
- Почему выбранное решение лучше в контексте этой задачи: пользователь прямо попросил отдельное текстовое поле для шаблона имени.

## 19. Результат quality gate и review
### SPEC Linter Result

| Блок | Пункты | Статус | Комментарий |
|---|---|---|---|
| A. Полнота спеки | 1-5 | PASS | Цель, AS-IS, проблема, цели и Non-Goals зафиксированы |
| B. Качество дизайна | 6-10 | PASS | Ответственность, интеграции, алгоритм, состояние и rollback описаны |
| C. Безопасность изменений | 11-13 | PASS | Совместимость, rollback и edge cases описаны |
| D. Проверяемость | 14-16 | PASS | Acceptance, UI acceptance и команды проверки заданы |
| E. Готовность к автономной реализации | 17-19 | PASS | План, отсутствие блокирующих вопросов и file table есть |
| F. Соответствие профилю | 20 | PASS | `dotnet-desktop-client` и `ui-automation-testing` требования учтены |

Итог: ГОТОВО

### SPEC Rubric Result

| Критерий | Балл (0/2/5) | Обоснование |
|---|---:|---|
| 1. Ясность цели и границ | 5 | Цель и Non-Goals проверяемы |
| 2. Понимание текущего состояния | 5 | Указаны реальные ViewModel/XAML/tests точки |
| 3. Конкретность целевого дизайна | 5 | Заданы токены, пересчет, sanitize и вставка |
| 4. Безопасность (миграция, откат) | 5 | Persisted data не меняется, rollback прост |
| 5. Тестируемость | 5 | Есть unit/UI acceptance и команды |
| 6. Готовность к автономной реализации | 5 | Нет блокирующих вопросов, план и файлы определены |

Итоговый балл: 30 / 30
Зона: готово к автономному выполнению

### Post-SPEC Review
- Статус: PASS
- Scope reviewed: `specs/2026-05-15-output-file-name-template.md`; instruction stack `model-behavior-baseline`, `quest-governance`, `collaboration-baseline`, `testing-baseline`, `testing-dotnet`, `dotnet-desktop-client`, `ui-automation-testing`, `quest-mode`, `review-loops`; selected profile `dotnet-desktop-client` + `ui-automation-testing`; open questions none blocking; planned changed files listed in section 16
- Decision: можно запрашивать подтверждение

| Severity | Area | Finding | Required action | Status |
| --- | --- | --- | --- | --- |
| HIGH | design | Default `{TemplateName}_заполненный` конфликтовал с правилом "токены только для параметров" и мог стать буквальным именем файла | Сделать default литералом текущего имени шаблона и явно не вводить `{TemplateName}` | fixed |
| MEDIUM | UX contract | Поведение editable полного пути рядом с новым шаблоном имени было неоднозначным | Зафиксировать, что `OutputFilePath` остается совместимым editor-ом и при ручном вводе обновляет шаблон именем файла | fixed |
| MEDIUM | implementation | Замена `Fields` могла привести к повторным subscriptions на старые поля | Добавить требование освобождать и пересоздавать subscriptions | fixed |
| MEDIUM | determinism | `PreviewTexts[0]` зависел от порядка `FormatPatterns`, а порядок форматов после extraction не является контрактом | Использовать deterministic filename formatting: сортировать `FormatPatterns` ordinal и форматировать значение напрямую | fixed |
| HIGH | visual acceptance | Текущая нижняя панель уже горизонтальная, добавление второго поля в одну строку может обрезать controls на design width 800 | Зафиксировать вертикальный блок настроек сохранения и UI layout test на 800x600/900x600 | fixed |
| MEDIUM | UX edge case | Если пользователь введет `.docx` в шаблон имени, текущий контракт добавления расширения даст `file.docx.docx` | Нормализовать один завершающий `.docx` suffix перед добавлением стандартного расширения | fixed |
| MEDIUM | editor behavior | Вставка только по caret не описывала поведение при выделенном тексте и потерю focus после клика по кнопке | Зафиксировать replacement selection и хранение caret/selection в code-behind | fixed |
| MEDIUM | UI automation | Spec требовал стабильные selectors, но не задавал конкретные `Name` для новых controls | Зафиксировать `OutputFileNameTemplateTextBox`, `InsertPlaceholderTokenButton`, `SaveSettingsPanel` и selector-based UI tests | fixed |
| HIGH | scope change | Новый запрос требует сохранять шаблон имени, что противоречит прежнему Non-Goal | Обновить spec: хранить `OutputFileNameTemplate` в `SavedTemplateData`, добавить persistence tests, запросить повторное подтверждение перед кодом | fixed |
| LOW | evidence | UI video evidence недоступен в текущем Avalonia Headless harness | Использовать fallback: UI assertions + test command evidence | accepted-risk |

- Fixed before continuing: default template contract, full path edit contract, field subscription lifecycle, deterministic placeholder formatting, bottom panel layout contract, trailing `.docx` normalization, selection replacement behavior, stable UI selector contract
- Checks rerun: SPEC linter/rubric self-check completed after review fixes
- Needs human: после scope change снова требуется фраза `Спеку подтверждаю`
- Residual risks / follow-ups: нет блокирующих

### Post-EXEC Review
- Статус: PASS
- Scope reviewed: approved spec including persistence scope change, `git status --short`, `git diff --stat`, relevant diff, targeted tests, `dotnet build`, full `dotnet test`, docs/changelog impact
- Decision: можно завершать

| Severity | Area | Finding | Required action | Status |
| --- | --- | --- | --- | --- |
| LOW | evidence | UI video evidence недоступен в текущем Avalonia Headless harness | Использован зафиксированный fallback: UI assertions + test command evidence | accepted-risk |

- Fixed before final report: нет
- Checks rerun: `dotnet test .\DocTemplater.Tests\DocTemplater.Tests.csproj --filter "FullyQualifiedName~DataInputViewModelTests|FullyQualifiedName~ValuePersistenceServiceTests"`; `dotnet test .\DocTemplater.UITests\DocTemplater.UITests.csproj --filter FullyQualifiedName~DataInputViewTests`; `dotnet build`; `dotnet test`
- Validation evidence: targeted ViewModel/Persistence tests 22/22; targeted UI tests 5/5; build succeeded; final full `dotnet test` passed (`DocTemplater.UITests` 9/9, `DocTemplater.Tests` 59/59)
- Unrelated changes: `.gitignore` изменен до начала задачи и не трогался в рамках EXEC
- Needs human: нет
- Residual risks / follow-ups: existing environment warnings remain outside this task: .NET preview SDK message, NU1903 for `Tmds.DBus.Protocol`, Avalonia Accelerate telemetry banner

## Approval
Ожидается фраза: "Спеку подтверждаю"

## 20. Журнал действий агента
Заполняется инкрементально после каждого значимого блока работ. Одна строка = один завершённый значимый блок.

| Фаза (SPEC/EXEC) | Тип намерения/сценария | Уверенность в решении (0.0-1.0) | Каких данных не хватает | Следующее действие | Нужна ли передача управления/решения человеку | Было ли фактическое обращение к человеку / решение человека | Короткое объяснение выбора | Затронутые артефакты/файлы |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| SPEC | instruction routing | 0.95 | нет | собрать кодовый контекст | нет | нет | Центральный стек требует QUEST и desktop/UI profiles | `C:\Users\Kibnet\.codex\agents\AGENTS.md` |
| SPEC | code context discovery | 0.90 | нет | создать рабочую spec | нет | нет | Найдены реальные точки изменения `DataInputViewModel`, `PlaceholderFieldViewModel`, `DataInputView` и tests | `DocTemplater/ViewModels/DataInputViewModel.cs`, `DocTemplater/Views/DataInputView.axaml`, `DocTemplater.UITests/DataInputViewTests.cs` |
| SPEC | spec authoring | 0.90 | пользовательское подтверждение | ждать `Спеку подтверждаю` | да | ожидается | Спека содержит scope, design, visual artifact, tests и quality gate | `specs/2026-05-15-output-file-name-template.md` |
| SPEC | requested review | 0.92 | пользовательское подтверждение | ждать `Спеку подтверждаю` | да | пользователь запросил ревью | Исправлены найденные несостыковки default token, editable path contract и lifecycle subscriptions | `specs/2026-05-15-output-file-name-template.md` |
| SPEC | repeated requested review | 0.90 | пользовательское подтверждение | ждать `Спеку подтверждаю` | да | пользователь повторно запросил ревью | Уточнен deterministic formatting для filename placeholder values при нескольких форматах | `specs/2026-05-15-output-file-name-template.md` |
| SPEC | implementation-readiness review | 0.90 | пользовательское подтверждение | ждать `Спеку подтверждаю` | да | пользователь повторно запросил ревью | Уточнены layout нижней панели, handling `.docx` suffix и replacement selection при вставке токена | `specs/2026-05-15-output-file-name-template.md` |
| SPEC | selector-contract review | 0.92 | пользовательское подтверждение | ждать `Спеку подтверждаю` | да | пользователь повторно запросил ревью | Зафиксированы конкретные стабильные UI selectors для новых controls и UI tests | `specs/2026-05-15-output-file-name-template.md` |
| EXEC | approval received | 0.95 | нет | реализовать изменения по spec | нет | пользователь подтвердил фразой `Спеку подтверждаю` | Gate пройден, можно менять код в границах спеки | `specs/2026-05-15-output-file-name-template.md` |
| EXEC | implementation and targeted validation | 0.88 | нет | запустить build и full tests | нет | нет | Реализованы ViewModel/XAML/code-behind/test изменения; targeted ViewModel и UI tests проходят | `DocTemplater/ViewModels/DataInputViewModel.cs`, `DocTemplater/ViewModels/PlaceholderFieldViewModel.cs`, `DocTemplater/Views/DataInputView.axaml`, `DocTemplater/Views/DataInputView.axaml.cs`, `DocTemplater.Tests/ViewModels/DataInputViewModelTests.cs`, `DocTemplater.UITests/DataInputViewTests.cs` |
| EXEC | full validation and post-exec review | 0.92 | нет | финальный отчет | нет | нет | `dotnet build` и полный `dotnet test` прошли; post-EXEC review PASS, unrelated `.gitignore` отделен | `specs/2026-05-15-output-file-name-template.md`, validation output |
| SPEC | scope change requested | 0.92 | повторное подтверждение | ждать `Спеку подтверждаю` | да | пользователь попросил сохранять шаблон названия | Требование меняет прежний Non-Goal, поэтому spec обновлена до нового gate | `specs/2026-05-15-output-file-name-template.md`, `DocTemplater/Domain/SavedTemplateData.cs`, `DocTemplater/Services/ValuePersistenceService.cs` |
| EXEC | persistence approval received | 0.95 | нет | реализовать сохранение шаблона имени | нет | пользователь подтвердил фразой `Спеку подтверждаю` | Повторный gate пройден для scope change про persistence | `specs/2026-05-15-output-file-name-template.md` |
| EXEC | persistence implementation and targeted validation | 0.90 | нет | запустить build и full tests | нет | нет | Добавлено per-template сохранение `OutputFileNameTemplate`; targeted ViewModel/Persistence tests прошли 22/22 | `DocTemplater/Domain/SavedTemplateData.cs`, `DocTemplater/Services/IValuePersistenceService.cs`, `DocTemplater/Services/ValuePersistenceService.cs`, `DocTemplater/ViewModels/DataInputViewModel.cs`, `DocTemplater.Tests/ViewModels/DataInputViewModelTests.cs`, `DocTemplater.Tests/Services/ValuePersistenceServiceTests.cs` |
| EXEC | final validation and review after persistence | 0.93 | нет | финальный отчет | нет | нет | `dotnet build` и финальный `dotnet test` прошли; persistence scope включен в post-EXEC review | `specs/2026-05-15-output-file-name-template.md`, validation output |
