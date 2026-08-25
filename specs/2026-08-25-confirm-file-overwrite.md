# Подтверждение перезаписи выходного файла

## 0. Метаданные
- Тип (профиль): `delivery-task`; stack profile `dotnet-desktop-client`; overlay profile `ui-automation-testing`
- Владелец: Codex
- Масштаб: small
- Целевое семейство / behavior baseline: GPT-5.6
- Поверхность: Work / Codex desktop
- Effective runtime: текущий runtime Codex; точный model ID не влияет на контракт приложения
- Eval baseline / evidence: автоматические unit- и Avalonia Headless UI-тесты; model-eval не применим
- Целевой релиз / ветка: `master`
- Ограничения: до фразы `Спеку подтверждаю` изменяется только эта spec; существующие automation-id не ломать; файл нельзя перезаписывать без явного согласия пользователя
- Связанные ссылки: `C:\Users\Kibnet\.codex\agents\AGENTS.md`, `instructions/core/quest-governance.md`, `instructions/profiles/dotnet-desktop-client.md`, `instructions/profiles/ui-automation-testing.md`

## 1. Overview / Цель
Перед генерацией документа проверять существование итогового `.docx` и запрашивать явное подтверждение перезаписи.

Outcome contract:
- Success means: существующий файл не изменяется, пока пользователь явно не выбрал «Перезаписать»; при отмене save-flow безопасно завершается без побочных эффектов.
- Итоговый артефакт / output: тестируемый async-контракт подтверждения во ViewModel, модальный Avalonia-диалог и regression-тесты.
- Stop rules: остановиться после green targeted tests, `dotnet build` и полного `dotnet test`; при блокере проверки зафиксировать его отдельно.

## 2. Текущее состояние (AS-IS)
- `DataInputViewModel.SaveDocumentAsync` вычисляет `outputPath` и вызывает `File.Exists(outputPath)`.
- Если файл существует, ViewModel только устанавливает `StatusMessage = "Файл будет перезаписан..."`, после чего без ожидания решения вызывает `GenerateDocumentAsync`.
- В проекте нет общего confirm-dialog сервиса или стороннего MessageBox-пакета.
- `DataInputView` уже назначает UI callback `BrowseForSaveFileAsync`, поэтому тот же seam подходит для подтверждения перезаписи без зависимости ViewModel от Avalonia `Window`.
- После успешной генерации выполняются persistence, возможное открытие файла и событие `DocumentSaved`; при отказе пользователя ни один из этих шагов выполняться не должен.

## 3. Проблема
Существующий выходной файл перезаписывается без явного согласия пользователя, что создаёт риск необратимой потери данных.

## 4. Цели дизайна
- Разделение ответственности: ViewModel управляет save-flow, View показывает модальный диалог.
- Повторное использование: async callback по образцу существующего file-picker callback.
- Тестируемость: ответы «Перезаписать» и «Отмена» подменяются в unit-тестах без настоящего окна.
- Консистентность: использовать русские подписи и owner-window приложения.
- Безопасность: отсутствие UI handler/owner трактовать как отказ, а не как разрешение.

## 5. Non-Goals (чего НЕ делаем)
- Не добавляем глобальный dialog service или сторонний MessageBox-пакет.
- Не меняем генерацию DOCX, шаблоны имён файлов и persistence-формат.
- Не добавляем режим «больше не спрашивать» или автоматическое версионирование имени.
- Не спрашиваем подтверждение, если итогового файла ещё нет.
- Не меняем поведение системного `SaveFilePicker`; проверка выполняется непосредственно перед генерацией по фактическому итоговому пути.

## 6. Предлагаемое решение (TO-BE)
### 6.1 Распределение ответственности
- `DataInputViewModel` -> публичный async callback `ConfirmFileOverwriteAsync`; проверка файла и ранний выход при неподтверждённом overwrite.
- `DataInputView` -> назначение/снятие callback вместе с `DataContext`; открытие модального диалога относительно текущего owner-window.
- `OverwriteConfirmationDialog` -> показ вопроса, полного пути и двух однозначных действий.
- `DocTemplater.Tests` -> unit regression на approve/cancel/no-handler и отсутствие запроса для нового файла.
- `DocTemplater.UITests` -> структура диалога, стабильные имена кнопок и видимый текст.

### 6.2 Детальный дизайн
- Контракт ViewModel: `Func<string, Task<bool>>? ConfirmFileOverwriteAsync`, где аргумент — полный `outputPath`, `true` означает явное разрешение.
- После валидации и вычисления `outputPath`, но до подготовки необратимых side effects:
  1. если файла нет — продолжить без диалога;
  2. если файл есть — установить нейтральный статус ожидания и вызвать callback;
  3. если callback отсутствует или вернул `false` — установить `StatusMessage = "Сохранение отменено"` и завершить команду;
  4. если вернул `true` — продолжить обычную генерацию.
- Callback должен быть awaited; `IsSaving` остаётся `true`, поэтому повторный запуск Save и уход назад заблокированы на время диалога.
- `DataInputView` назначает callback при подключении ViewModel и очищает только свой callback при смене `DataContext`, чтобы ViewModel не удерживала View.
- Диалог модальный, с owner-window. Закрытие через крестик/Escape эквивалентно `false`.
- Заголовок: «Файл уже существует».
- Вопрос: «Файл по указанному пути уже существует. Перезаписать его?».
- Полный путь показывается отдельным переносимым `TextBlock`, чтобы пользователь видел цель операции.
- Кнопки: безопасная «Отмена» и акцентная «Перезаписать»; default/fallback result — `false`.
- Если `TopLevel.GetTopLevel(this)` не является `Window`, callback возвращает `false` без перезаписи.
- Ошибка открытия/работы диалога проходит через существующий `catch` save-flow и не разрешает генерацию.

Visual planning artifact (lightweight wireframe, local spec artifact):

```text
┌─────────────────────────────────────────────────────────┐
│ Файл уже существует                                  ×  │
├─────────────────────────────────────────────────────────┤
│ Файл по указанному пути уже существует.                 │
│ Перезаписать его?                                       │
│                                                         │
│ C:\…\Итоговый документ.docx                            │
│                                                         │
│                         [ Отмена ] [ Перезаписать ]      │
└─────────────────────────────────────────────────────────┘
```

- UI video evidence: `Не применимо` — текущий Avalonia Headless harness не содержит механизма записи видео. Fallback: headless UI assertions, test logs и визуальная инспекция локально отрендеренного окна, если harness позволит получить screenshot.
- Производительность: одна синхронная проверка `File.Exists` уже существует; новый async wait возникает только при конфликте имени.

### 6.3 User-Observable Scenarios
| Scenario | User action / trigger | Expected visible result / output | Evidence required | Covered by AC |
| --- | --- | --- | --- | --- |
| Новый файл | Нажать «Сохранить», итогового файла нет | Документ сохраняется без вопроса | unit test | AC1 |
| Согласие | Нажать «Сохранить», файл есть; выбрать «Перезаписать» | Диалог закрывается, файл генерируется по тому же пути | unit + UI test | AC2, AC4 |
| Отказ | Нажать «Сохранить», файл есть; выбрать «Отмена» или закрыть окно | Сохранение прекращается, существующий файл и сохранённые значения не изменяются | unit + UI test | AC3, AC4 |
| Нет UI handler | Save-flow запущен без подключённой View | Перезапись запрещена fail-closed | unit test | AC3 |

### 6.4 State / Interaction Matrix
| Current state | Trigger | Expected transition/result | Empty/error/disabled/concurrent case | Notes |
| --- | --- | --- | --- | --- |
| Valid, target absent | Save | generation -> persistence -> success | dialog не вызывается | прежний happy path |
| Valid, target exists | Save | waiting for confirmation | Save/Back disabled через `IsSaving` | один modal dialog |
| Confirmation open | Overwrite | generation continues | generation failure handled as before | explicit consent |
| Confirmation open | Cancel/close/Escape | command ends, `IsSaving` resets | no generation/persistence/open/event | safe default |
| Target exists, callback unavailable | Save | command ends with cancellation status | no overwrite | fail-closed |

### 6.5 Decision Ledger
| Decision | Owner | Default / chosen option | Confidence | Risk if assumed | Needs user before EXEC |
| --- | --- | --- | ---: | --- | --- |
| Действия диалога | agent | «Отмена» / «Перезаписать» | 0.95 | низкий; формулировки однозначны | Нет |
| Закрытие окна | agent | трактовать как отказ | 1.0 | только более безопасное поведение | Нет |
| Архитектура | agent | callback ViewModel -> modal View | 0.95 | низкий; повторяет существующий seam | Нет |
| Показ полного пути | agent | показывать в диалоге | 0.9 | небольшой рост окна, снижает риск ошибки | Нет |

### 6.6 Runtime / Config / Data Contract Matrix
Не применимо: runtime config, API и persisted data не меняются.

## 7. Бизнес-правила / Алгоритмы
| Target exists | Callback available | User result | Generate | Persist values | Open file / event |
| --- | --- | --- | --- | --- | --- |
| Нет | любое | не спрашивать | Да | Да при success | как сейчас |
| Да | Да | `true` | Да | Да при success | как сейчас |
| Да | Да | `false`/close | Нет | Нет | Нет |
| Да | Нет | safe default `false` | Нет | Нет | Нет |

Явное подтверждение действительно только для пути, переданного в callback в рамках текущего выполнения команды.

## 8. Точки интеграции и триггеры
- `SaveCommand` -> `SaveDocumentAsync` -> `File.Exists(outputPath)` -> `ConfirmFileOverwriteAsync(outputPath)`.
- `DataInputView.OnDataContextChanged` назначает и освобождает UI callback.
- `OverwriteConfirmationDialog.ShowDialog<bool>(owner)` возвращает решение ViewModel.

## 9. Изменения модели данных / состояния
- Добавляется только неперсистентный callback ViewModel.
- Формат `SavedTemplateData` и пользовательские настройки не меняются.

## 10. Миграция / Rollout / Rollback
- Миграция не требуется.
- Изменение обратно совместимо для сохранения по новому пути.
- Rollback: удалить диалог/callback и вернуть прежнюю ветку проверки; данные не требуют отката.

## 11. Тестирование и критерии приёмки
Acceptance Criteria:
- AC1: при отсутствующем output-файле callback не вызывается, генерация выполняется.
- AC2: при существующем output-файле генерация выполняется только после результата `true`; callback получает точный полный путь.
- AC3: `false`, закрытие диалога или отсутствие callback не вызывают generation, persistence, open и `DocumentSaved`; статус сообщает об отмене.
- AC4: диалог содержит вопрос, полный путь, `CancelOverwriteButton` и `OverwriteButton`; безопасное действие доступно и неакцентно, overwrite — явно подписан.
- AC5: существующие save-flow тесты остаются зелёными.

Characterization/TDD: первым добавить падающий unit-тест, доказывающий текущий вызов генерации при существующем файле и ответе `false`, затем реализовать fix.

Visual acceptance: структура и подписи соответствуют wireframe; путь переносится, кнопки не перекрываются при типичном длинном пути.

Команды проверки:

```powershell
dotnet test DocTemplater.Tests/DocTemplater.Tests.csproj --filter "FullyQualifiedName~DataInputViewModelTests"
dotnet test DocTemplater.UITests/DocTemplater.UITests.csproj --filter "FullyQualifiedName~OverwriteConfirmationDialogTests|FullyQualifiedName~DataContext_AssignsOverwriteConfirmationHandler"
dotnet build DocTemplater.sln
dotnet test DocTemplater.sln
```

Фактический runner — VSTest-compatible xUnit. Установленный preview SDK 11 не поддержал форму `dotnet test --project`; команды скорректированы на `dotnet test <csproj>`. Из-за запущенного экземпляра приложения build/test outputs перенаправляются через `BaseOutputPath` во временный каталог.

### Acceptance-to-Test Matrix
| Acceptance criterion | Automated test | Manual / visual / log check | Evidence artifact | If not tested, why |
| --- | --- | --- | --- | --- |
| AC1 | VM test: target absent | Не требуется | test output | — |
| AC2 | VM test: existing target + approve | callback path assertion | test output | — |
| AC3 | VM tests: cancel + missing handler | verify mocks/event count/status | test output | OS close maps to dialog default and UI test |
| AC4 | Headless dialog structure/action tests | compare to wireframe; screenshot if supported | test output / optional local-only screenshot | video unsupported by harness |
| AC5 | full solution tests | inspect summary | console log | — |

## 12. Риски и edge cases
- TOCTOU: файл может появиться после `File.Exists` и до записи. Полное устранение требует атомарного create/replace-контракта генератора и вне scope; задача устраняет штатное тихое перезаписывание.
- Путь может быть длинным: текст должен переноситься, окно иметь разумные min/max dimensions.
- Callback может выбросить исключение: общий `catch` показывает ошибку и не продолжает генерацию.
- Headless View без owner: fail-closed.

### Expected User Review Objections
| Likely objection | Why likely | Mitigation in spec/code plan | Status |
| --- | --- | --- | --- |
| «Отмена всё равно перезаписывает файл» | это исходный дефект | mock-verification запрещает generation/persistence/event | mitigated |
| «Непонятно, какой файл будет заменён» | имя может строиться из шаблона | полный путь показан в диалоге | mitigated |
| «Закрытие крестиком считается согласием» | типичная опасная ошибка dialog result | default result строго `false` | mitigated |

### Rework Prevention Checklist
- [x] Видимый пользователю flow и текст диалога описаны.
- [x] Каждый user-visible scenario имеет evidence.
- [x] Все принятые решения перечислены.
- [x] Вероятные замечания закрыты.
- [x] Роли UX, tester и developer рассмотрены.
- [x] Acceptance criteria являются проверяемыми результатами.
- [x] Для EXEC указан путь доказательства сценариев.

## 13. План выполнения
1. Добавить failing regression unit test на отказ при существующем файле.
2. Добавить callback и fail-closed ветку в ViewModel; довести targeted VM tests до green.
3. Добавить модальный диалог и подключить его в `DataInputView`.
4. Добавить/обновить Avalonia Headless UI tests для структуры и безопасных действий диалога.
5. Выполнить targeted tests, build, full tests, визуальную проверку доступным fallback-способом и post-EXEC review.

## 14. Открытые вопросы
Нет блокирующих вопросов.

## 15. Соответствие профилю
- Профиль: `dotnet-desktop-client` + `ui-automation-testing`.
- Выполненные требования профиля: async UI без блокировки потока; стабильные automation names; unit/UI coverage; visual planning artifact; video fallback обоснован; запланированы build и полный test run.

## 16. Таблица изменений файлов
| Файл | Изменения | Причина |
| --- | --- | --- |
| `DocTemplater/ViewModels/DataInputViewModel.cs` | callback и ранний выход | запрет тихой перезаписи |
| `DocTemplater/Views/DataInputView.axaml.cs` | подключение confirm callback | связь ViewModel с modal UI |
| `DocTemplater/Views/OverwriteConfirmationDialog.axaml` | layout, текст, stable names | пользовательский вопрос |
| `DocTemplater/Views/OverwriteConfirmationDialog.axaml.cs` | modal result и safe close | явное решение пользователя |
| `DocTemplater.Tests/ViewModels/DataInputViewModelTests.cs` | save-flow regression tests | AC1-AC3 |
| `DocTemplater.UITests/DataInputViewTests.cs` или отдельный dialog test | headless UI checks | AC4 |

## 17. Таблица соответствий (было -> стало)
| Область | Было | Стало |
| --- | --- | --- |
| Existing output | status и немедленная перезапись | modal вопрос перед generation |
| Cancel/close | отсутствует | safe early return без side effects |
| Testability | нет decision seam | async callback с точным путем |

## 18. Альтернативы и компромиссы
- Сторонний MessageBox package: меньше собственного XAML, но новая зависимость несоразмерна одному диалогу.
- Dialog service: масштабируемее, но проект пока не имеет других confirm dialogs; это лишняя абстракция.
- Автоматически добавлять суффикс: сохраняет данные, но не выполняет явную просьбу спросить о перезаписи.
- Выбранное решение: небольшой собственный modal dialog и callback дают ясный UX, fail-closed безопасность и изолированные тесты без нового пакета.

## 19. Результат quality gate и review
### SPEC Linter Result
| Блок | Пункты | Статус | Комментарий |
|---|---|---|---|
| A. Полнота спеки | 1-5 | PASS | цель, AS-IS, проблема и границы определены |
| B. Качество дизайна | 6-10 | PASS | ответственность, flow, ошибки и perf описаны |
| C. Безопасность изменений | 11-13 | PASS | данные не меняются, fail-closed и rollback определены |
| D. Проверяемость | 14-16 | PASS | AC, matrices и команды заданы |
| E. Готовность к автономной реализации | 17-19 | PASS | план полный, открытых вопросов нет |
| F. Соответствие профилю | 20 | PASS | desktop/UI automation требования учтены |

Итог: ГОТОВО

### SPEC Rubric Result
| Критерий | Балл (0/2/5) | Обоснование |
|---|---:|---|
| 1. Ясность цели и границ | 5 | одна корневая проблема и явные non-goals |
| 2. Понимание текущего состояния | 5 | проверен реальный save-flow и отсутствие dialog infra |
| 3. Конкретность целевого дизайна | 5 | callback, dialog result, copy и states определены |
| 4. Безопасность (миграция, откат) | 5 | fail-closed, без миграции, rollback простой |
| 5. Тестируемость | 5 | AC покрыты unit/UI/full suite |
| 6. Готовность к автономной реализации | 5 | решений пользователя и блокеров нет |

Итоговый балл: 30 / 30
Зона: готово к автономному выполнению

### Role-Based Review Result
| Role | Applicability | Review question | Verdict | Required spec changes |
| --- | --- | --- | --- | --- |
| Business analyst / domain workflow | applicable | Защищает ли flow от потери существующего файла? | PASS | нет |
| UX / designer | applicable | Ясны ли цель, путь и безопасное действие? | PASS | нет |
| Tester / validation | applicable | Покрыты ли approve, cancel, close, absent handler и no-conflict? | PASS | нет |
| Developer / architect | applicable | Изолирован ли Avalonia UI от ViewModel? | PASS | нет |
| Delivery / operations / security | not applicable | Нет config/deploy/secrets изменений | PASS | нет |

### Post-SPEC Review
- Статус: PASS
- Scope reviewed: эта spec, выбранные профили, текущий `SaveDocumentAsync`, `DataInputView`, unit/UI suites.
- Decision: можно запрашивать подтверждение.
- Review passes:
  - Scope/Evidence pass: тихая перезапись подтверждена по коду; scope ограничен confirm-flow.
  - Contract pass: explicit `true` — единственное разрешение; все остальные результаты fail-closed.
  - Adversarial risk pass: проверены close/Escape, отсутствующий owner/callback, exception и TOCTOU.
  - Role-Based pass: PASS для BA/UX/tester/developer.
  - Re-review after fixes / Fix and re-review: дополнительных исправлений не потребовалось.
  - Stop decision: minimum sufficient evidence достигнут.
- Evidence inspected: `DataInputViewModel.SaveDocumentAsync`, `DataInputView.OnDataContextChanged`, `DataInputViewModelTests`, `DataInputViewTests`, project dependencies.
- Depth checklist:
  - Scope drift / unrelated changes: нет.
  - Acceptance criteria: AC1-AC5 измеримы.
  - User-observable scenarios / Decision ledger / Expected objections: заполнены.
  - Validation evidence: mapped в Acceptance-to-Test Matrix.
  - Unsupported claims: video fallback ограничен фактическим headless harness.
  - Regression / edge case: approve/cancel/close/no handler/no conflict/exception/TOCTOU рассмотрены.
  - Comments/docs/changelog: удаляется устаревший TODO; changelog не требуется.
  - Hidden contract change: callback непубличен за границами UI/VM слоя и не влияет на persisted data.
  - Manual-review challenge: отдельно проверить, что отмена не вызывает persistence, `OpenAfterSave` и `DocumentSaved`.
- No-findings justification: дизайн использует существующий callback pattern и имеет безопасный default с полным acceptance mapping.

| Severity | Area | Finding | Required action | Status |
| --- | --- | --- | --- | --- |
| LOW | risk | TOCTOU между проверкой и записью остаётся | зафиксировать как non-goal текущего генератора | accepted-risk |

- Fixed before continuing: не требовалось.
- Checks rerun: ручная сверка linter/rubric/rework gate.
- Needs human: только formal QUEST approval.
- Residual risks / follow-ups: атомарная replace-семантика вне scope.

### Post-EXEC Review
- Статус: PASS
- Scope reviewed: утверждённая spec, `git status --short`, полный diff production/test файлов, targeted/full test evidence.
- Decision: реализация соответствует spec и может быть передана пользователю.
- Review passes:
  - Scope/Evidence pass: изменены только save-flow, новый confirm dialog, тесты и эта spec.
  - Contract pass: только явный `true` продолжает generation; cancel/close/Escape/no handler возвращают `false`.
  - Adversarial risk pass: проверены новый файл, существующий файл, approve, decline, отсутствующий handler, закрытие окна и Escape.
  - Role-Based pass: BA/UX/tester/developer PASS; delivery не применим.
  - Re-review after fixes / Fix and re-review: исправлены missing `Task` import и deprecated headless keyboard overload; после финальной правки повторно прошли build и полный UI suite.
  - Stop decision: build и полный suite green, minimum sufficient evidence достигнут.
- Evidence inspected:
  - TDD red: existing output вызвал `GenerateDocumentAsync` 1 раз при ожидании `Times.Never`.
  - Targeted VM: 15/15 passed.
  - Targeted overwrite UI/integration: 6/6 passed.
  - `dotnet build DocTemplater.sln`: 0 errors, 1 pre-existing `NU1903` dependency warning.
  - Full tests: `DocTemplater.Tests` 63/63; `DocTemplater.UITests` 15/15.
  - Headless screenshot probe: fallback подтверждён; capture требует `.UseSkia()` и отключения `UseHeadlessDrawing`, чего текущий harness не делает.
- Depth checklist:
  - Scope drift / unrelated changes: нет.
  - Acceptance criteria: AC1-AC5 выполнены.
  - User-observable scenarios / Acceptance-to-test matrix / Expected objections: approve/cancel/close/no-conflict доказаны тестами.
  - Validation evidence: targeted, build и full suite green.
  - Unsupported claims: screenshot/video не заявляются как полученные.
  - Regression / edge case: fail-closed default и точный confirmed path проверены.
  - Comments/docs/changelog: устаревший TODO удалён; changelog не требуется.
  - Hidden contract change: persisted data/API генератора не изменены.
  - Manual-review challenge: проверено, что decline не вызывает generation, persistence и `DocumentSaved`, а исходный файл сохраняет содержимое.
- No-findings justification: production diff ограничен callback seam, одной guard-веткой и отдельным modal Window; все ветви покрыты green tests.

| Severity | Area | Finding | Required action | Status |
| --- | --- | --- | --- | --- |
| LOW | evidence | screenshot/video недоступны в текущей headless-конфигурации | использовать UI assertions и test logs; не менять harness вне scope | accepted-risk |
| LOW | dependency | `Tmds.DBus.Protocol 0.21.2` даёт существующий `NU1903` | отдельный dependency update вне этой задачи | follow-up |

- Fixed before final report: актуальный keyboard overload, точные runner-команды в spec.
- Checks rerun: solution build и full test suite после финальной правки.
- Validation evidence: 78/78 tests, build 0 errors.
- Unrelated changes: нет.
- Needs human: нет.
- Residual risks / follow-ups: TOCTOU и dependency warning остаются вне утверждённого scope.

## Approval
Ожидается фраза: "Спеку подтверждаю"

## 20. Журнал действий агента
| Фаза (SPEC/EXEC) | Тип намерения/сценария | Уверенность в решении (0.0-1.0) | Каких данных не хватает | Следующее действие | Нужна ли передача управления/решения человеку | Было ли фактическое обращение к человеку / решение человека | Короткое объяснение выбора | Затронутые артефакты/файлы |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| SPEC | Классификация и preflight | 1.0 | нет | изучить save-flow и dialog infra | Нет | Нет | изменение поведения требует QUEST; текущая spec исключала overwrite confirmation | `specs/2026-08-25-confirm-file-overwrite.md` |
| SPEC | Дизайн и acceptance mapping | 0.95 | нет | выполнить quality gate | Нет | Нет | callback + modal dialog сохраняют MVVM-границу и позволяют fail-closed тестирование | `specs/2026-08-25-confirm-file-overwrite.md` |
| SPEC | Post-SPEC review | 0.95 | formal approval | запросить `Спеку подтверждаю` | Да | Да — запрос подготовлен | linter PASS, rubric 30/30, блокирующих решений нет | `specs/2026-08-25-confirm-file-overwrite.md` |
| EXEC | Approval получен | 1.0 | нет | добавить reproducing regression test | Нет | Да — пользователь ответил `Спеку подтверждаю` | разрешён переход к реализации утверждённого scope | `specs/2026-08-25-confirm-file-overwrite.md` |
| EXEC | TDD и реализация | 0.98 | нет | выполнить targeted validation | Нет | Нет | reproducing test упал на реальном вызове генератора; добавлены fail-closed callback и modal dialog | ViewModel, View, dialog, unit/UI tests |
| EXEC | Validation | 0.98 | screenshot недоступен в headless drawing | выполнить post-EXEC review | Нет | Нет | targeted 15/15 и 6/6, build green, full suite 78/78; visual fallback подтверждён технически | test output, временные isolated outputs |
| EXEC | Post-EXEC review | 0.98 | нет | передать результат пользователю | Нет | Нет | AC1-AC5 выполнены, findings только вне scope | spec и полный рабочий diff |
