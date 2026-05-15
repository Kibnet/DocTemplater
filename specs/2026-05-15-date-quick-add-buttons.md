# Быстрые кнопки добавления даты

## 0. Метаданные
- Тип (профиль): `delivery-task`; profiles: `dotnet-desktop-client`, `ui-automation-testing`
- Владелец: Codex
- Масштаб: small
- Целевая модель: gpt-5.5
- Целевой релиз / ветка: текущая рабочая ветка
- Ограничения: до подтверждения фразой `Спеку подтверждаю` менять только эту спецификацию; сохранить существующие `Name`/automation selectors; не менять публичные сервисные контракты
- Связанные ссылки: центральный stack `C:\Users\Kibnet\.codex\agents\AGENTS.md`

## 1. Overview / Цель
Добавить к редакторам даты в форме ввода три быстрые кнопки: `+1 день`, `+1 месяц`, `+1 год`.

Outcome contract:
- Success means: для каждого поля `DateTimeOffset` рядом с `DatePicker` есть три кнопки, каждая изменяет текущую дату на соответствующий интервал и обновляет превью.
- Итоговый артефакт / output: изменения в Avalonia view, `PlaceholderFieldViewModel`, unit-тесты и headless UI-тест.
- Stop rules: остановиться после реализации, если `dotnet build`, targeted tests или полный `dotnet test` падают по новому изменению; не выходить за рамки редакторов даты.

## 2. Текущее состояние (AS-IS)
- Код UI формы ввода: `DocTemplater/Views/DataInputView.axaml`.
- Динамические поля строятся через `ItemsControl ItemsSource="{Binding Fields}"`.
- Для даты используется `DatePicker` с `SelectedDate="{Binding DateValue, Mode=TwoWay}"` и `Name="DateValuePicker"`.
- Логика одного поля находится в `DocTemplater/ViewModels/PlaceholderFieldViewModel.cs`.
- `DateValue` проксирует `Value`; валидация принимает `DateTimeOffset` и `DateTime`.
- `Preview` вычисляется из `PreviewTexts`, но прямое уведомление `PropertyChanged` для `Preview` при изменении `PreviewTexts` сейчас не зафиксировано в коде.
- Unit-тесты для поля: `DocTemplater.Tests/ViewModels/PlaceholderFieldViewModelTests.cs`.
- UI suite: `DocTemplater.UITests` на `Avalonia.Headless` + xUnit, runner `dotnet test`.

## 3. Проблема
Пользователь может выбрать дату только через `DatePicker`; нет быстрого способа сдвинуть дату на типовые интервалы `+1 день`, `+1 месяц`, `+1 год`.

## 4. Цели дизайна
- Разделение ответственности: арифметика даты и команды живут во `ViewModel`, разметка только привязывает кнопки.
- Повторное использование: одна helper-логика для всех трех кнопок.
- Тестируемость: проверить дату на уровне `PlaceholderFieldViewModel` и наличие/клик в headless UI.
- Консистентность: сохранить текущий `DatePicker`, визуальную структуру формы и русскоязычные подписи.
- Обратная совместимость: не менять формат сохраняемых значений и генерацию документов.

## 5. Non-Goals (чего НЕ делаем)
- Не меняем `DatePicker` на другой календарный контрол.
- Не добавляем быстрые кнопки к строковым или числовым полям.
- Не меняем сервисы генерации, извлечения плейсхолдеров и сохранения значений.
- Не меняем бизнес-форматы дат и культуру форматирования.
- Не исправляем нерелевантные устаревшие строки в `UI_TESTS.md`.

## 6. Предлагаемое решение (TO-BE)
### 6.1 Распределение ответственности
- `DocTemplater/ViewModels/PlaceholderFieldViewModel.cs` -> команды `AddDayCommand`, `AddMonthCommand`, `AddYearCommand`; безопасное изменение `DateValue`; уведомление UI о зависимых свойствах даты/превью.
- `DocTemplater/Views/DataInputView.axaml` -> рядом с `DatePicker` добавить три кнопки, видимые только для `DateTimeOffset` полей.
- `DocTemplater.Tests/ViewModels/PlaceholderFieldViewModelTests.cs` -> unit coverage для `+1 день`, `+1 месяц`, `+1 год`, включая пустую дату.
- `DocTemplater.UITests/DataInputViewTests.cs` -> headless coverage, что date field содержит быстрые кнопки и клик меняет `DateValue`.

### 6.2 Детальный дизайн
- Data flow: Button click -> `ReactiveCommand` во `PlaceholderFieldViewModel` -> `DateValue` меняется -> `Value`, validation, preview и binding notifications обновляются.
- Контракты / API: публичный API сервисов не меняется; добавляются только публичные команды view model для binding.
- Output contract / evidence rules: тесты должны подтвердить арифметику дат, наличие UI controls и изменение значения после click.
- Visual planning artifact для UI-facing изменений:

```text
DateTime field row:

Дата подписания        Preview
[ 15.05.2026 v ] [+1 день] [+1 месяц] [+1 год]    d MMMM yyyy -> 15 мая 2026

States:
- buttons visible only when IsDateTimeType == true
- existing DateValuePicker Name remains unchanged
- quick buttons have stable Names: AddDayButton, AddMonthButton, AddYearButton
```

- UI test video evidence: `Avalonia.Headless` suite in this repo exposes assertions, not a configured video recorder. Fallback evidence for EXEC: targeted UI test command output plus optional screenshot/log if a UI assertion fails.
- Границы сохранения поведения: `DatePicker` remains the primary date editor; quick buttons only mutate the same `DateValue`.
- Обработка ошибок: date arithmetic uses .NET `DateTimeOffset.AddDays`, `AddMonths`, `AddYears`; end-of-month/leap-year behavior stays framework-defined.
- Производительность: O(1) UI command, no async work and no UI-thread blocking.

## 7. Бизнес-правила / Алгоритмы (если есть)
- If `DateValue` is set, use it as the base value.
- If `DateValue` is null, use the current local date at midnight with local offset as the base value, then apply the increment. This makes quick buttons useful on empty required date fields.
- `+1 день` applies `AddDays(1)`.
- `+1 месяц` applies `AddMonths(1)`.
- `+1 год` applies `AddYears(1)`.
- Time zone offset of an existing `DateTimeOffset` value is preserved by framework arithmetic.

## 8. Точки интеграции и триггеры
- `DataInputView.axaml` button `Command` bindings call the new commands on the current `PlaceholderFieldViewModel`.
- `DateValue` setter triggers validation and preview refresh through existing `Value` flow.
- For command-driven changes, binding notifications must include `DateValue`; for preview UI, notifications must include `Preview` when `PreviewTexts` changes.

## 9. Изменения модели данных / состояния
- Новых persisted fields нет.
- Runtime state: three `ReactiveCommand<Unit, Unit>` properties on each `PlaceholderFieldViewModel`.
- Saved values remain `DateTimeOffset` through existing `Value`/persistence flow.

## 10. Миграция / Rollout / Rollback
- Миграция данных не нужна.
- Rollout: обычная поставка приложения.
- Rollback: revert changes in planned files; existing templates and saved values remain compatible.

## 11. Тестирование и критерии приёмки
- Acceptance Criteria:
  - Date fields show `DateValuePicker`, `AddDayButton`, `AddMonthButton`, `AddYearButton`.
  - String and integer fields do not show the quick date button group.
  - `+1 день` changes `2026-05-15` to `2026-05-16`.
  - `+1 месяц` changes `2026-01-31` according to .NET `AddMonths(1)` behavior.
  - `+1 год` changes a date according to .NET `AddYears(1)` behavior.
  - Empty date field becomes current local date plus selected increment.
  - Preview updates after quick button action.
- Какие тесты добавить/изменить:
  - Add unit tests in `PlaceholderFieldViewModelTests`.
  - Add `DataInputViewTests` in `DocTemplater.UITests` using existing Avalonia.Headless pattern.
- Characterization tests / contract checks: no separate before-test needed; this is a new feature, existing date binding behavior is covered by current tests plus new regression tests.
- Visual acceptance: compare against wireframe in section 6.2 through headless UI assertions for controls and visibility.
- UI video evidence: fallback, because no video recorder is configured for the existing Avalonia.Headless runner; command output from UI tests is the required evidence.
- Базовые замеры performance: Не применимо, no meaningful performance tradeoff.
- Команды для проверки:
  - `dotnet build`
  - `dotnet test .\DocTemplater.Tests\DocTemplater.Tests.csproj`
  - `dotnet test .\DocTemplater.UITests\DocTemplater.UITests.csproj`
  - `dotnet test`
- Stop rules для validation loops: if targeted tests fail, inspect the failing test and fix within spec scope; run full suite after targeted pass; stop and report if failure is unrelated and pre-existing.

## 12. Риски и edge cases
- `DateValue` null behavior could be ambiguous; chosen behavior is useful default from local today and is test-covered.
- End-of-month and leap-year results may surprise users; using .NET built-in behavior avoids custom calendar bugs.
- Repeated controls inside `ItemsControl` will have repeated `Name`; tests should search within the date field visual subtree where possible.
- Adding buttons may widen the left input column; use compact horizontal layout and keep preview in the second column.

## 13. План выполнения
1. Add date increment commands and helper method in `PlaceholderFieldViewModel`.
2. Ensure dependent property notifications for `DateValue` and `Preview` are raised when command changes the value.
3. Update `DataInputView.axaml` to wrap date editor and quick buttons in one visible date-only horizontal layout.
4. Add/extend unit tests for command date arithmetic and preview notification.
5. Add headless UI test for buttons and date mutation.
6. Run targeted tests, then `dotnet build`, then full `dotnet test`.
7. Perform post-EXEC review against this spec.

## 14. Открытые вопросы
Нет блокирующих вопросов.

## 15. Соответствие профилю
- Профиль: `dotnet-desktop-client`, `ui-automation-testing`
- Выполненные требования профиля:
  - UI-thread blocking operations are not introduced.
  - Existing automation selector `DateValuePicker` remains stable.
  - New controls get stable `Name` selectors.
  - Existing UI test suite is used through Avalonia.Headless.
  - Visual planning artifact and UI evidence fallback are specified.
  - Validation commands include `dotnet build` and `dotnet test`.

## 16. Таблица изменений файлов
| Файл | Изменения | Причина |
| --- | --- | --- |
| `DocTemplater/ViewModels/PlaceholderFieldViewModel.cs` | Add quick date commands, helper, dependent property notifications | Keep date logic testable and reusable |
| `DocTemplater/Views/DataInputView.axaml` | Add date-only horizontal quick action buttons near `DatePicker` | User-facing feature |
| `DocTemplater.Tests/ViewModels/PlaceholderFieldViewModelTests.cs` | Add unit regression tests | Validate date arithmetic and notifications |
| `DocTemplater.UITests/DataInputViewTests.cs` | Add headless UI tests | Validate actual UI controls and command binding |
| `DocTemplater.UITests/AssemblyInfo.cs` | Add Avalonia headless test application attribute | Make Avalonia XAML resources available to UI tests |
| `DocTemplater.UITests/DocTemplater.UITests.csproj` | Align Avalonia Headless package versions with the app | Keep UI test runtime compatible with app XAML resources |

## 17. Таблица соответствий (было -> стало)
| Область | Было | Стало |
| --- | --- | --- |
| Date editor UI | Only `DatePicker` | `DatePicker` plus `+1 день`, `+1 месяц`, `+1 год` |
| Date field commands | No quick increment commands | Three explicit `ReactiveCommand`s |
| Empty date quick action | Not available | Uses current local date plus increment |
| UI tests | Template selection only | Adds DataInput date quick buttons coverage |

## 18. Альтернативы и компромиссы
- Вариант: implement click handlers in code-behind.
- Плюсы: minimal XAML binding work.
- Минусы: pushes field behavior into view, harder to unit test, less consistent with existing view model binding.
- Почему выбранное решение лучше в контексте этой задачи: commands in `PlaceholderFieldViewModel` keep behavior close to `DateValue`, validation and preview, and make the feature testable without UI.

- Вариант: disable quick buttons when date is empty.
- Плюсы: literal "add to selected date" semantics.
- Минусы: less useful for required empty fields; extra enable-state logic.
- Почему выбранное решение лучше в контексте этой задачи: defaulting from local today gives a practical shortcut and keeps validation simple.

## 19. Результат quality gate и review
### SPEC Linter Result

| Блок | Пункты | Статус | Комментарий |
|---|---|---|---|
| A. Полнота спеки | 1-5 | PASS | Цель, AS-IS, проблема, design goals и non-goals описаны |
| B. Качество дизайна | 6-10 | PASS | Ответственность, integration points, state, rollback и edge cases зафиксированы |
| C. Безопасность изменений | 11-13 | PASS | Scope small, public service contracts unchanged, rollback простой |
| D. Проверяемость | 14-16 | PASS | Acceptance criteria, unit/UI tests и команды проверки заданы |
| E. Готовность к автономной реализации | 17-19 | PASS | План и planned files есть, блокирующих вопросов нет |
| F. Соответствие профилю | 20 | PASS | .NET desktop и UI automation требования отражены |

Итог: ГОТОВО

### SPEC Rubric Result

| Критерий | Балл (0/2/5) | Обоснование |
|---|---:|---|
| 1. Ясность цели и границ | 5 | Конкретные кнопки, non-goals и scope указаны |
| 2. Понимание текущего состояния | 5 | Указаны реальные файлы, bindings, tests и текущий preview нюанс |
| 3. Конкретность целевого дизайна | 5 | Заданы команды, UI selectors, data flow и date rules |
| 4. Безопасность (миграция, откат) | 5 | Persisted data не меняется, rollback понятен |
| 5. Тестируемость | 5 | Есть unit, UI и full validation commands |
| 6. Готовность к автономной реализации | 5 | План пошаговый, открытых вопросов нет |

Итоговый балл: 30 / 30
Зона: готово к автономному выполнению

### Post-SPEC Review
- Статус: PASS
- Scope reviewed: `specs/2026-05-15-date-quick-add-buttons.md`, instruction stack `model-behavior-baseline + quest-governance + collaboration-baseline + testing-baseline + testing-dotnet + dotnet-desktop-client + ui-automation-testing`, selected profiles, open questions, planned changed files
- Decision: можно запрашивать подтверждение

| Severity | Area | Finding | Required action | Status |
| --- | --- | --- | --- | --- |
| LOW | evidence | UI video evidence unavailable in current Avalonia.Headless setup | Use documented fallback: targeted UI test output and diagnostics on failure | accepted-risk |

- Fixed before continuing: Не применимо
- Checks rerun: SPEC linter/rubric self-check after review
- Needs human: требуется только approval phrase
- Residual risks / follow-ups: no blocker; null-date base behavior is explicitly specified and test-covered

### Post-EXEC Review
- Статус: PASS
- Scope reviewed: approved spec, `git status --short`, `git diff --stat`, relevant diff, validation evidence, docs/changelog impact
- Decision: можно завершать

| Severity | Area | Finding | Required action | Status |
| --- | --- | --- | --- | --- |
| LOW | unrelated changes | `.gitignore` содержит unrelated изменение `**.csproj.lscache`, не относящееся к feature diff | Не трогать без явного запроса пользователя | accepted-risk |
| LOW | evidence | Video evidence отсутствует, так как текущий Avalonia.Headless runner не настроен на запись видео | Использован зафиксированный fallback: targeted/full UI test output | accepted-risk |

- Fixed before final report: removed accidental EOL-only worktree noise in `DocTemplater/DocTemplater.csproj`; synchronized UI test Avalonia package versions with app version; added `AvaloniaTestApplication` attribute for headless XAML loading
- Checks rerun: `dotnet build`; `dotnet test .\DocTemplater.Tests\DocTemplater.Tests.csproj`; `dotnet test .\DocTemplater.UITests\DocTemplater.UITests.csproj`; `dotnet test .\DocTemplater.UITests\DocTemplater.UITests.csproj --filter FullyQualifiedName~DataInputViewTests`; `dotnet test`
- Validation evidence: build passed with 0 errors; unit project passed 46/46; UI project passed 6/6; full solution test passed 52/52
- Unrelated changes: `.gitignore` has `**.csproj.lscache`; left untouched
- Needs human: Нет
- Residual risks / follow-ups: existing warnings remain: .NET preview SDK message, `Tmds.DBus.Protocol` NU1903 in test project, Avalonia Accelerate telemetry banner

## Approval
Получено подтверждение пользователя: "Спеку подтверждаю"

## 20. Журнал действий агента
Заполняется инкрементально после каждого значимого блока работ. Одна строка = один завершённый значимый блок.

| Фаза (SPEC/EXEC) | Тип намерения/сценария | Уверенность в решении (0.0-1.0) | Каких данных не хватает | Следующее действие | Нужна ли передача управления/решения человеку | Было ли фактическое обращение к человеку / решение человека | Короткое объяснение выбора | Затронутые артефакты/файлы |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| SPEC | собрать instruction stack и repo context | 0.95 | Нет | создать рабочую спецификацию | Нет | Нет | Central stack требует SPEC перед кодом; найдены реальные файлы даты и тестов | `C:\Users\Kibnet\.codex\agents\AGENTS.md`, `DocTemplater/Views/DataInputView.axaml`, `DocTemplater/ViewModels/PlaceholderFieldViewModel.cs`, tests |
| SPEC | оформить дизайн и quality gate | 0.92 | Нет | запросить подтверждение пользователя | Да | Да, ожидается фраза `Спеку подтверждаю` | Спека фиксирует scope, visual artifact, тесты и fallback evidence | `specs/2026-05-15-date-quick-add-buttons.md` |
| EXEC | реализовать feature и тесты | 0.86 | Результаты локальных проверок | запустить targeted tests | Нет | Нет | Добавлены команды даты, кнопки в XAML, unit tests и headless UI tests в рамках approved scope | `DocTemplater/ViewModels/PlaceholderFieldViewModel.cs`, `DocTemplater/Views/DataInputView.axaml`, `DocTemplater.Tests/ViewModels/PlaceholderFieldViewModelTests.cs`, `DocTemplater.UITests/DataInputViewTests.cs`, `specs/2026-05-15-date-quick-add-buttons.md` |
| EXEC | исправить headless test setup | 0.88 | Результат повторного UI test run | повторить UI tests | Нет | Нет | Targeted UI tests показали, что test assembly запускала пустое Avalonia-приложение без XAML resources; добавлен стандартный `AvaloniaTestApplication` attribute | `DocTemplater.UITests/AssemblyInfo.cs`, `specs/2026-05-15-date-quick-add-buttons.md` |
| EXEC | синхронизировать Avalonia test runtime | 0.87 | Результат повторного UI test run | повторить UI tests | Нет | Нет | XAML resources embedded в app assembly, но UI test runtime использовал Avalonia Headless `11.3.10` при app `11.3.8`; версии выровнены | `DocTemplater.UITests/DocTemplater.UITests.csproj`, `specs/2026-05-15-date-quick-add-buttons.md` |
| EXEC | выполнить проверки и post-EXEC review | 0.94 | Нет | завершить задачу | Нет | Нет | Build и полный test suite прошли; unrelated `.gitignore` diff отделён от feature diff | `specs/2026-05-15-date-quick-add-buttons.md`, validation output, git diff/status |
