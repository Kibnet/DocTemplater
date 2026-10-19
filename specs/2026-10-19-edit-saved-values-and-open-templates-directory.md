# Редактирование сохранённых значений и открытие каталога шаблонов

## 0. Метаданные
- Фаза: EXEC завершена после исправления двух замечаний пользовательского review. Exact approval сохранён; пользователь поручил «Исправь». Итог: build PASS, 80 unit/integration + 18 UI PASS. Предыдущие 73 unit/integration и 18 UI PASS не покрывали потерю параллельных записей и устаревший статус.
- Профили: `dotnet-desktop-client`, `ui-automation-testing`; тип задачи `delivery-task`.
- Владелец: пользователь — результат и approval; агент — проектирование и локальная реализация после approval.
- Масштаб: medium, локальное изменение двух связанных с работой над шаблонами действий.
- Форма: Expanded, canonical source `C:/Users/Kibnet/.codex/agents/templates/specs/_template.md`. Short не подходит: затронуто сохранение пользовательских данных и контракт получения активного каталога.
- Поверхность: Codex desktop, Windows/PowerShell. Behavior baseline каталога — GPT-6 Astra; точные effective model/effort/version не предоставлены runtime и для функции приложения не требуются. Настройки агента не меняются.
- Eval baseline: не применимо к моделям; baseline приложения — просмотренный исходный код на `master`, HEAD `107d986`, upstream `origin/master`, исходно чистый worktree.
- Целевой релиз: не задан. После реализации пользователь отдельно разрешил commit/push; доставка исходников — в текущую master/origin/master. Установка, release и deploy не запрошены.
- Instruction stack: central AGENTS → routing-matrix → creator-vibe-lens, model-behavior-baseline, tool-execution-baseline, quest-governance, quest-mode, collaboration-baseline, testing-baseline → указанные профили → spec-linter, spec-rubric, review-loops. Локальный AGENTS.override.md в репозитории и проверенных родительских каталогах не найден. Применён skill `creator-vibe` для выбора простого пользовательского потока.

## 1. Overview / Цель
Исходное поручение: «Я хочу добавить возможность редактировать сохранённые значения для подстановки документов и возможность открыть каталог с шаблонами».

Пользователь выбирает шаблон, исправляет ранее сохранённые значения в существующей форме и явно сохраняет их без создания документа. Из экрана выбора он открывает фактический каталог шаблонов в системном файловом менеджере.

Outcome contract:
- Success means: значения доступны после повторного открытия формы и нового экземпляра приложения; папка открывается независимо от наличия выбранного DOCX.
- Артефакт: изменения приложения, автоматические проверки, просмотренное визуальное evidence и обновлённая эта SPEC.
- Stop rules: до approval меняется только эта SPEC; после обязательного green и post-EXEC review работа завершается. Не выдавать сборку/Headless за native-проверку или установку.

## 2. Текущее состояние (AS-IS)
- `DataInputViewModel.LoadSavedValuesAsync` загружает один набор значений на имя шаблона, с преобразованием `JsonElement` в string/int/DateTimeOffset. Поля уже редактируемые.
- Обновление сохранённых значений вызывается только в `SaveDocumentAsync` после успешной генерации DOCX. Поэтому для исправления данных приходится создавать документ.
- `MainWindowViewModel` возвращает к выбору шаблона по `DocumentSaved`; отдельного события сохранения значений нет.
- `ValuePersistenceService` хранит все настройки и наборы в `%APPDATA%/DocTemplater/settings.json`, а не в отдельных файлах SavedValues, как утверждает README. Ключ записи: `Template.FileName + ".docx"`.
- `SaveTemplateValuesAsync` заменяет словарь целиком, меняет LastUsed, при null-параметрах сохраняет прежние LastSaveDirectory и OutputFileNameTemplate. Сейчас изменяет cached settings до записи на диск.
- `TemplateSelectionView` содержит путь, выбор папки `...`, редактирование DOCX, обновление и выбор шаблона. Открытия папки нет. `TemplatesDirectory` может содержать несуществующий вручную набранный путь, который сервис ещё не принял.
- `TemplateDiscoveryService` хранит активный путь приватно, мониторит DOCX и создаёт каталог при инициализации; интерфейс не предоставляет чтение активного пути.
- Приложение и UI-тесты: net9.0, Avalonia 11.3.8; unit-тесты: net10.0; runner xUnit/VSTest. Установлены SDK 10.x и 11 preview. Наличие net9 runtime ещё нужно проверить перед EXEC.
- Существуют Avalonia.Headless.XUnit тесты обоих экранов; в просмотренном harness нет записи видео. README/UI_TESTS.md местами расходятся с фактическими файлами; источником команд служат csproj и тесты.

## 3. Проблема
Управление исходными данными шаблонов требует лишних действий: обновление значений связано с выпуском документа, а доступ к файлам шаблонов приходится искать вручную.

## 4. Цели дизайна
Повторно использовать типизированные поля и существующее хранилище; явно различить сохранение значений и документа; сохранить старые данные, навигацию генерации и селекторы тестов; сделать платформенное открытие папки подменяемым в проверках.

## 5. Non-Goals
Несколько именованных наборов значений, история, удаление наборов, глобальные переменные, переименование ключей шаблонов, новый редактор DOCX, смена формата настроек, синхронизация нескольких процессов, переработка всего интерфейса. Не открывать/редактировать реальные пользовательские settings.json во время тестов. Установка, релиз и Git delivery не входят в scope.

## 6. Предлагаемое решение (TO-BE)
### 6.1 Распределение ответственности
- `DataInputViewModel`: отдельная `SaveValuesCommand`, проверка полей, подготовка и сохранение данных, статус и общий запрет параллельных операций.
- `DataInputView`: новая кнопка, постоянная область результата/ошибки, понятные подписи действий и сообщения занятости.
- `ValuePersistenceService` / `IValuePersistenceService`: надёжная запись без изменения схемы JSON; общий асинхронный lock, изолированные публичные снимки, отдельное атомарное обновление каталога. Cached данные считаются сохранёнными только после успешной записи.
- `ITemplateDiscoveryService` / `TemplateDiscoveryService`: readonly свойство фактически активного каталога, используемое новой командой.
- `TemplateSelectionViewModel` / View: команда открытия папки и небольшая платформенная граница, допускающая fake. Реальное открытие — системный shell, без запуска строки через cmd/PowerShell.

### 6.2 Детальный дизайн
**Сохранение значений.** В существующей форме добавить «Сохранить значения» (`SaveValuesButton`). Нынешнюю «Сохранить» переименовать в «Сохранить документ», сохранив `SaveButton` и `SaveCommand`. Подзаголовок объясняет оба действия. Новая команда не вызывает генератор, диалог сохранения, проверку выходного каталога/имени, подтверждение перезаписи, открытие DOCX и `DocumentSaved`. После успеха остаётся в форме, выводит «Значения сохранены».

Проверять все текущие поля по тем же требованиям типов и обязательности, что у документа, включая изначально пустые/нетронутые поля. При ошибках ничего не записывать, показывать ошибки полей и общий статус. Подготовка значений использует снимок на момент команды; повторное нажатие и генерация блокируются до окончания операции. На время записи редакторы также заблокированы, чтобы поздние правки не выглядели сохранёнными.

Для values-only сохранять текущие значения плейсхолдеров, объединённые с ранее сохранёнными ключами, которых в текущем DOCX нет. Не удалять такие ключи незаметно. Другие шаблоны и поля настроек сохраняются. LastUsed обновляется; прежние LastSaveDirectory и OutputFileNameTemplate сохраняются, даже если пользователь изменил выходные настройки на форме. Обычная генерация продолжает сохранять значения и свои выходные настройки по прежнему контракту.

Надёжность записи: формировать отдельный кандидат состояния вместо преждевременной мутации cached settings, писать JSON во временный файл рядом с целевым и заменять целевой только после успешной записи. При ошибке остаются прежний валидный файл и cached состояние; поля с правками остаются на форме для повтора. Без миграции и универсального рефакторинга storage.

**Открытие папки.** Добавить «Открыть папку» (`OpenTemplatesDirectoryButton`) непосредственно рядом с полем каталога и существующей кнопкой выбора `...`. Не требует выбора шаблона и работает при пустом списке. Источник пути — readonly свойство сервиса, не текстовый черновик. Привести активный относительный путь к абсолютному по тем же правилам, что у discovery. Перед shell-вызовом проверить существование; исчезнувший каталог не создавать этой командой, вывести понятную ошибку. При успешном shell-запросе сообщать «Отправлен запрос на открытие папки: …»; реальное появление окна подтверждается отдельной native-проверкой.

При изменении каталога через `...` или существующий валидный ручной ввод команда использует новый активный путь. При неверном черновике открывается прежний активный каталог, его полный путь показан в подсказке кнопки и статусе. На время загрузки/смены каталога кнопка недоступна. Изменение пути через новую кнопку отсутствует.

**Visual planning artifact — wireframe, часть SPEC:**
```text
Выбор шаблона документа
[существующий список DOCX, статус]
Каталог: [путь........................] [...] [Открыть папку]
                [Редактировать] [Обновить] [Выбрать]

Заполнение шаблона: Договор
Измените значения и сохраните их или создайте документ.
[существующие типизированные поля с превью и ошибками, прокрутка]
[существующие настройки выходного файла, отдельная строка]
[← Назад] [Очистить]   [Сохранить значения] [Сохранить документ]
[Значения сохранены / Ошибка сохранения значений: …]
```
Разделить область пути/выходных настроек и панель действий на строки, чтобы новые подписи помещались при 800×600 и 900×600. При необходимости переносить кнопки, сохранять доступ к полям и статусу. Сообщение занятости соответствует операции, например «Сохранение значений…». Не переносить существующие type-specific редакторы на новый экран.

Видео до: не применимо — самостоятельных новых действий ещё нет. Видео после: текущий Headless harness не имеет recorder; планируемый fallback — реальные render PNG обоих экранов (успех/ошибка), журнал и результаты UI tests, с самостоятельным просмотром PNG. Если при EXEC окажется доступна безопасная запись автоматизированного native run, использовать её; не выдавать PNG за видео. Артефакты local-only, вне Git по умолчанию.

### 6.3 User-Observable Scenarios
| Scenario | Действие | Видимый результат | Evidence required | AC |
| --- | --- | --- | --- | --- |
| S1 | Выбрать шаблон, изменить строку/число/дату, сохранить значения | Успех в форме; новый DOCX отсутствует; повторное открытие возвращает новые значения | UI action + реальный временный JSON + новый service/VM | AC1–3 |
| S2 | Сохранить с ошибкой поля или записи | Понятная ошибка; правки можно исправить/повторить; прежние данные сохранны | Негативные unit/integration/UI tests, PNG ошибки | AC4–5 |
| S3 | Открыть папку без выбранного шаблона, затем после смены каталога | Shell получает правильный абсолютный путь | UI action + fake opener; native smoke отдельно | AC6–8 |
| S4 | Создать документ привычной кнопкой | Сохраняется DOCX, значения и выходные настройки, остаётся подтверждение перезаписи | Существующие и затронутые тесты генерации | AC9 |

### 6.4 State / Interaction Matrix
| Состояние | Trigger | Результат | Empty/error/concurrent |
| --- | --- | --- | --- |
| Готовая форма | Сохранить значения | Проверка → запись → успех, остаёмся в форме | При отсутствии saved data создаётся запись для этого шаблона |
| Невалидное поле | Сохранить значения | Ошибка поля/общий статус | Никаких вызовов записи/генератора |
| Загрузка значений / запись / генерация | Любая вторая save-команда, Back, Clear, редактирование | Недоступны до завершения | Общий busy guard; отсутствие повторных записей |
| Ошибка записи | Повторить после устранения причины | Успех без потери введённых правок | Старые disk/cache остаются до успешного повтора |
| Пустой список | Открыть папку | Открывается активный каталог | Выбор DOCX не нужен |
| Неверный черновик пути | Открыть папку | Открывается активный путь сервиса | Подсказка/статус показывают фактический путь |
| Активная папка удалена / shell отказал | Открыть папку | Понятная ошибка на экране | Приложение остаётся работоспособным |

### 6.5 Decision Ledger
| Решение | Owner | Выбранный вариант | Confidence | Риск | Needs user before EXEC |
| --- | --- | --- | ---: | --- | --- |
| Место редактирования | agent | Существующая форма, без отдельного менеджера | 0.95 | Дополнительный экран был бы лишним | Нет |
| Значение «сохранённые» | agent | Существующий один набор на шаблон | 0.9 | История/несколько наборов потребуют новой задачи | Нет |
| Явное сохранение | agent | Отдельная кнопка без генерации, без автосохранения | 0.95 | Изменения без нажатия теряются как сейчас | Нет |
| Метаданные и скрытые ключи | agent | Сохранять прежние, обновлять только актуальные поля и LastUsed | 0.95 | Исключить скрытое удаление | Нет |
| Какую папку открыть | agent | Активную из discovery | 0.95 | Черновик может отличаться, путь виден | Нет |
| Approval | user | Exact `Спеку подтверждаю` | 1.0 | Код до фразы менять запрещено | Да, только фазовый gate |

### 6.6 Runtime / Config / Data Contract Matrix
| Область | Источник истины | Изменение | Совместимость | Проверка |
| --- | --- | --- | --- | --- |
| Saved values | settings.json, Templates/FieldValues | Новый явный путь записи | Та же схема и ключи | Fresh service round-trip строка/int/дата |
| Выходные настройки | LastSaveDirectory, OutputFileNameTemplate | Values-only их не меняет | Старый DOCX flow сохранён | До/после сравнение JSON |
| Сохранность файла/cache | ValuePersistenceService | Кандидат + временный файл и замена | Читается старой версией | Ошибка записи/повтор + существующие service tests |
| Каталог шаблонов | Активное состояние discovery | Readonly getter | Без изменения persisted настроек | Default/custom/relative/invalid draft tests |

## 7. Бизнес-правила / Алгоритмы
Уточнение по review в том же scope: все операции одного ValuePersistenceService используют общий асинхронный lock на полный цикл load/clone/update/write/cache. Публичное чтение возвращает изолированный снимок. SaveSettingsAsync остаётся явной полной заменой настроек (инициализация/восстановление снимка); для изменения только каталога вводится SaveTemplatesDirectoryAsync, обновляющий поле на актуальном состоянии под тем же lock. Оба места изменения пути в TemplateSelectionViewModel используют этот метод вместо передачи потенциально устаревшего полного settings. Новых schema, установок или межпроцессной синхронизации нет. После успешного сохранения последующая правка любого значения/очистка меняет статус на «Есть несохранённые изменения»; диск изменяется только явной save-командой.

Один набор на прежний ключ имени DOCX. Нельзя сохранять невалидные текущие поля, даже не тронутые пользователем. В values-only ключи из текущей формы перекрывают старые; отсутствующие в текущем шаблоне сохранённые ключи сохраняются. Успех выдаётся только после завершения записи. Создание DOCX не является побочным эффектом values-only.

## 8. Точки интеграции и триггеры
`SaveValuesButton` → `SaveValuesCommand` → validation/snapshot → persistence → status. Навигация по `DocumentSaved` остаётся только у генерации. `OpenTemplatesDirectoryButton` → active discovery path → existence check → shell opener → status. Старые команды, селекторы и FileSystemWatcher продолжают работать.

## 9. Изменения модели данных / состояния
Persisted схема не меняется. Добавляются команды и при необходимости observable состояние загрузки/общей занятости и busy-текст. Readonly getter discovery не является настройкой. Новых пользовательских файлов данных, кроме временного файла безопасной записи, нет.

## 10. Миграция / Rollout / Rollback
Миграции нет; старые settings читаются непосредственно. Тесты используют только временные каталоги и settings. Локальный откат — вернуть исходные файлы к baseline, сохранив эту SPEC/evidence; JSON совместим со старой версией. Установка/изменение действующего экземпляра не выполняются. Восстановление пользовательских данных из backup не требуется для разработки: реальные данные не трогаются.

## 11. Тестирование и критерии приёмки
Обязательны полный `dotnet build DocTemplater.sln` и `dotnet test DocTemplater.sln`: затронуты общий persistence и интерфейс discovery. Перед EXEC проверить `dotnet --list-runtimes`, assets/runner и реальный способ создания Headless screenshot; отсутствие зависимости классифицировать отдельно. SDK уже проверен, VSTest подтверждён csproj.

Staged validation: baseline/characterization существующих потоков → новые проверки наблюдаемого нового действия → targeted unit/UI → build → весь suite → visual/native evidence. На SPEC сборка и тесты не запускались. Перед долгим запуском указать команду и оценку на основании baseline run; прогресс читать из test output/log, не задавать произвольный общий timeout.

Команды targeted после реализации:
```powershell
dotnet test DocTemplater.Tests/DocTemplater.Tests.csproj --filter "FullyQualifiedName~DataInputViewModelTests|FullyQualifiedName~ValuePersistenceServiceTests|FullyQualifiedName~TemplateSelectionViewModelTests|FullyQualifiedName~TemplateDiscoveryServiceTests"
dotnet test DocTemplater.UITests/DocTemplater.UITests.csproj --filter "FullyQualifiedName~DataInputViewTests|FullyQualifiedName~TemplateSelectionViewTests"
dotnet build DocTemplater.sln
dotnet test DocTemplater.sln
```
Обычный запуск для native smoke: `dotnet run --project DocTemplater/DocTemplater.csproj`; только с изолированными тестовыми настройками/путями, не подставлять реальные данные автоматически. При невозможности изолированного native запуска явно оставить native boundary непроверенной; автоматический fake-shell не доказывает появление Проводника.

### Acceptance-to-Test Matrix
| AC | Automated test / обязательный результат | Visual/manual evidence | Статус |
| --- | --- | --- | --- |
| AC1 | SaveValues через действие формы записывает строку, int, дату без генератора, file picker, overwrite, DocumentSaved и нового DOCX | PNG успешной формы | PASS, evidence §19 |
| AC2 | Новый persistence instance и новая форма из того же временного файла читают изменённые значения | Повторное открытие; перезапуск заявлять только после проверки | PASS, evidence §19 |
| AC3 | Другие шаблоны, неизвестные сохранённые ключи, выходной каталог и шаблон имени сохранены; LastUsed обновлён | Сравнение содержимого временного JSON | PASS, evidence §19 |
| AC4 | Пустое нетронутое/невалидное поле не записывается; поля и общий статус показывают ошибку | PNG ошибки | PASS, evidence §19 |
| AC5 | Ошибка записи не меняет старый валидный JSON/cache; правки остаются; повтор успешен; повторные save и редактирование при busy заблокированы | UI состояние busy/error/retry | PASS, evidence §19 |
| AC6 | Кнопка доступна без выбора DOCX и с пустым списком; fake opener получает абсолютный активный путь | PNG выбора шаблона | PASS, evidence §19 |
| AC7 | После смены каталога используется новый путь; относительный путь и неверный черновик обрабатываются по контракту | Native shell smoke, если доступен изолированный запуск | PASS, evidence §19 |
| AC8 | Удалённая папка/shell exception показывают ошибку; не запускают необработанное исключение | Статус на экране, негативный тест | PASS, evidence §19 |
| AC9 | Генерация, отмена перезаписи, сохранение выходных настроек, даты/токены имени и навигация сохраняют поведение | Существующие regression tests + новые при необходимости | PASS, evidence §19 |
| AC10 | Сборка и весь unit/UI suite green; кнопки/настройки/статус видимы и не перекрываются при 800×600 и 900×600 | Самостоятельно просмотренные render PNG, не только bounds assertions | PASS, evidence §19 |
| AC11 | Параллельные сохранения разных шаблонов в одном service сохраняют все записи на диске и в cache | Regression RED→GREEN, временный JSON и fresh service | PASS, повторное review ниже |
| AC12 | Изменение только каталога согласовано с записью значений, не откатывает Templates; публичный снимок не меняет cache без записи; failed write отпускает lock | Service regression/contract tests и VM вызов узкого API | PASS, повторное review ниже |
| AC13 | После сохранения новая правка строки/числа/даты либо очистка убирает статус сохранения, показывает несохранённые изменения, не меняет JSON до следующего save | Unit tests + Headless mouse/typing UI flow + render PNG | PASS, повторное review ниже |

Video fallback: отсутствие recorder в текущем Avalonia.Headless harness; команды UI выше + render PNG и test logs. Настройку реального rendering проверить, при необходимости включить drawing backend в рамках test harness. Заглушечный headless bitmap не считается визуальной проверкой. Артефакты не коммитить автоматически. После green не расширять набор без новой причины. Timeout/нехватка runtime — blocker с диагностикой и новым обоснованным шагом, а не бесконечный retry.

## 12. Риски и edge cases
Сохранение при незавершённой загрузке блокируется. Отдельные ReactiveCommand сами по себе не исключают одновременный запуск разных операций — нужен общий guard. Ошибка cache-before-write закрывается кандидатом; частичная запись — временным файлом. Ранее удалённые из DOCX поля не должны исчезать из settings. Существующая коллизия одинаковых имён файлов из разных каталогов не меняется: новый способ ключевания вне scope.

### Expected User Review Objections
| Замечание | Почему вероятно | Решение | Статус |
| --- | --- | --- | --- |
| «Для правки значений опять создаётся документ» | Сейчас save совмещён | AC1 запрещает генерацию и навигацию | mitigated |
| «Сохранил, но после перезапуска всё старое» | Mock не подтверждает диск | AC2 требует нового service и повторной формы | mitigated |
| «Открылась другая папка» | Черновик пути расходится с активным | Readonly источник, подсказка и AC7 | mitigated |
| «Кнопки не помещаются / не видно ошибки» | Длинные подписи, нижняя панель уже плотная | Разделение строк, AC10 и просмотр PNG | mitigated |

Rework Prevention Checklist: исходный сценарий назван; S1–S4 связаны с AC; решения агента перечислены; objections закрыты планом; релевантные роли проверены ниже; AC описывают результат; EXEC имеет путь к disk/UI/visual evidence.

## 13. План выполнения
После approval: воспроизвести baseline и проверить runtime; реализовать и проверить новые команды/надёжность записи; связать UI и обновить сценарии тестов; пройти обязательные проверки и визуальный просмотр; выполнить post-EXEC review, обновить evidence и журнал. Последовательность записи должна сохранять инвариант disk-before-cache; прочий порядок внутри реализации свободный.

## 14. Открытые вопросы
Блокирующих вопросов нет. Exact approval получен. Runtime net9/net10 подтверждён. Реальный системный opener проверен с временной папкой; полный native flow приложения и установка не заявляются проверенными.

## 15. Соответствие профилю
Desktop: асинхронная запись, короткие UI операции, platform opener подменяем, стабильные selectors, обязательные build/test. UI automation: новые действия покрываются существующим Avalonia.Headless suite, wireframe связан с S1–S4, предусмотрены реально просмотренные render PNG и честно обозначенный video fallback. AppAutomation не внедряется: проект использует Avalonia.Headless.XUnit.

## 16. Таблица изменений файлов
| Файл | Изменения | Причина |
| --- | --- | --- |
| DocTemplater/ViewModels/DataInputViewModel.cs | SaveValuesCommand, validation/snapshot/busy/status | Редактирование с самостоятельным сохранением |
| DocTemplater/Views/DataInputView.axaml | Кнопка, названия, строки layout, feedback | Видимый доступный flow |
| DocTemplater/Services/ValuePersistenceService.cs, IValuePersistenceService.cs | Кандидат состояния, безопасная запись, async lock, снимки и SaveTemplatesDirectoryAsync | Не терять данные при ошибке и конкурирующих обновлениях |
| DocTemplater/Services/ITemplateDiscoveryService.cs, TemplateDiscoveryService.cs | Readonly активный путь | Открывать фактический каталог |
| DocTemplater/ViewModels/TemplateSelectionViewModel.cs, Views/TemplateSelectionView.axaml | Команда/кнопка, ошибки, компоновка | Открытие папки |
| DocTemplater/Services/* либо Views/TemplateSelectionView.axaml.cs | Минимальная подменяемая platform boundary | Shell без реальных запусков в unit/UI tests |
| DocTemplater.Tests/ViewModels/SavedValuesEditingTests.cs, TemplateSelectionViewModelTests.cs | Новые поведенческие проверки; прежние DataInputViewModelTests сохранены | AC1–8 |
| DocTemplater.Tests/Services/ValuePersistenceServiceTests.cs, TemplateDiscoveryServiceTests.cs | Round-trip и негативные сценарии | Совместимость и сохранность |
| DocTemplater.UITests/SavedValuesWorkflowTests.cs, AppBuilderTestPlatformHelper.cs | Новые UI действия, layout/feedback, Skia render и ReactiveUI scheduler; прежние UI tests сохранены | AC1/4–10 |
| README.md, UI_TESTS.md | Краткое описание новых действий и точных связанных путей/команд | Не оставлять ошибочную инструкцию использования |
| Эта SPEC | Evidence, review, журнал | Прослеживаемость |

## 17. Таблица соответствий (было → стало)
| Область | Было | Стало |
| --- | --- | --- |
| Правка saved values | Через выпуск DOCX | Отдельное сохранение в существующей форме |
| Открытие папки | Ручной поиск | Кнопка у пути, без выбора DOCX |
| Ошибка значения/записи | Нет отдельного flow | Feedback в форме, правки доступны для повтора |
| JSON/генерация | Один набор на имя файла | Прежний формат и путь генерации |

## 18. Альтернативы и компромиссы
Отдельный редактор JSON или менеджер наборов добавляет второй способ работы с типами/валидацией и усложняет задачу. Автосохранение меняет данные без явного намерения. Существующая форма с отдельной кнопкой даёт короткий и предсказуемый поток. Открытие текстового пути проще технически, но неверно при невалидном черновике; выбран активный путь сервиса.

## 19. Результат quality gate и review
### SPEC Linter Result
| № | Критерий | Статус | Evidence |
| --- | --- | --- | --- |
| 1 | Цель/outcome | PASS | §1, S1–S4 |
| 2 | Проверенный AS-IS | PASS | §2, просмотрены VM/View/service/csproj/tests |
| 3 | Проблема | PASS | §3, связь values с генерацией |
| 4 | Дизайн | PASS | §4 и §6 |
| 5 | Границы | PASS | §5, без дополнительных наборов/установки |
| 6 | Ответственности | PASS | §6.1 |
| 7 | Интеграция | PASS | §8, сохраняется DocumentSaved flow |
| 8 | Инварианты | PASS | §7, disk-before-cache |
| 9 | Ошибки/recovery | PASS | §6.2/6.4, AC4/5/8 |
| 10 | Performance | PASS | Малый JSON, асинхронная запись, no new polling; performance benchmark не применим |
| 11 | Данные/состояния | PASS | §6.6/9 |
| 12 | Совместимость | PASS | §10, без миграции |
| 13 | Rollback | PASS | §10, реальных данных тесты не касаются |
| 14 | Измеримые AC | PASS | AC1–10 |
| 15 | AC→evidence | PASS | §11, включая негативные случаи |
| 16 | Команды/stop | PASS | §1/11 |
| 17 | План | PASS | §13 |
| 18 | Решения/вопросы | PASS | §6.5/14, только exact approval |
| 19 | Масштаб/форма | PASS | §0, storage/interface исключают short |
| 20 | Профиль | PASS | §15, build/test/UI/render evidence planned |
Итог: ГОТОВО к запросу approval; фактическая реализация и проверки пока не выполнены.

### SPEC Rubric Result
| Критерий | Балл | Основание |
| --- | ---: | --- |
| Цель/границы | 5 | Исходное поручение, S1–S4, Non-Goals |
| AS-IS | 5 | Подтверждён кодом, README drift отмечен |
| Конкретность дизайна | 5 | Команды, wireframe, состояния и семантика сохранения |
| Безопасность/rollback | 5 | Временные данные тестов, кандидат/замена файла, совместимость |
| Проверяемость | 5 | AC1–10, UI/disk/negative, команды и visual criteria |
| Автономность | 5 | Продуктовых блокеров нет, environment preflight определён |
Итого: 30/30; готово к реализации только после exact approval. Это оценка SPEC, не evidence готовой функции.

### Role-Based Review Result
| Role | Applicability | Проверенный вопрос | Verdict | Required spec changes |
| --- | --- | --- | --- | --- |
| Business analyst / domain workflow | applicable | Можно ли исправить набор без выпуска документа? | PASS | Самостоятельная команда и AC1 |
| UX / designer | applicable | Понятно ли, что сохранится, где успех/ошибка и какая папка откроется? | PASS | Подписи, статус, путь и wireframe зафиксированы |
| Tester / validation | applicable | Отличают ли проверки реальный диск и shell от mocks/наличия кнопки? | PASS | Новый service instance, fake/native boundary, PNG inspection |
| Developer / architect | applicable | Нет ли потери скрытых ключей, cache mutation и разных save одновременно? | PASS | Merge, disk-before-cache, shared guard |
| Delivery / operations / security | applicable к сохранению локальных settings | Не затрагиваются ли реальные settings/установка? | PASS | Изолированные fixtures, локальный scope и rollback |

### Post-SPEC Review
- Статус / Stop decision: PASS текущей фазы; можно запросить exact approval. Не является разрешением реализации.
- Scope reviewed: эта SPEC, указанный central stack/профили/template; чтение DataInputViewModel/View, PlaceholderFieldViewModel, MainWindowViewModel, ValuePersistenceService/IValuePersistenceService/SavedTemplateData/AppSettings, TemplateDiscoveryService/interface, TemplateSelectionViewModel/View/code-behind; csproj, README/UI_TESTS, UI tests и relevant test assertions. Git preflight подтвердил master/107d986/clean до SPEC.
- Scope/Evidence pass: реальное хранилище установлено по сервису, а не README; отсутствие отдельного save/path getter подтверждено исходниками. Старый native интерфейс не запускался, его визуальный вид не заявлен просмотренным.
- Contract pass: обе части исходного поручения покрыты S1–S4; перезапуск/disk/read-back и негативные случаи имеют AC; CODE/README/test files не изменены в SPEC.
- Adversarial risk pass: проверены контрпримеры — нетронутое пустое поле; stale cache при ошибке; скрытый ключ старого шаблона; непригодный выходной каталог при valid values; невалидный черновик пути; пустой список; совпадающие имена DOCX в разных каталогах; два одновременных save.
- Role-Based pass: применимые роли представлены выше. Независимый reviewer не привлекался: scope medium, выполняется self-review; техническая read-only изоляция не заявляется.
- Findings / исправления при подготовке SPEC:

| Severity | Area | Finding | Required action | Status |
| --- | --- | --- | --- | --- |
| MEDIUM | storage | Старый save мутирует cache и заменяет весь словарь | Кандидат/безопасная запись, merge скрытых ключей для values-only, AC3/5 | fixed в дизайне; EXEC pending |
| MEDIUM | directory | Текст пути может отличаться от активного discovery | Getter сервиса, tooltip/status и AC7 | fixed в дизайне; EXEC pending |
| MEDIUM | UX/evidence | Статус формы вне busy сейчас не отображается; длинные кнопки могут не поместиться | Постоянный status, строки layout, 800/900 render evidence | fixed в дизайне; EXEC pending |

- Fix and re-review: повторно сопоставлены §6.2, state matrix и AC3/5/7/10 с найденными контрпримерами; требования друг другу не противоречат. Исходный обычный save не получает событие values-only.
- Evidence inspected: исходный текст перечисленных файлов, Git preflight и вывод SDK inventory; build/test/render на SPEC не запускались и не заявляются PASS.
- Depth checklist: scope не включает именованные наборы/установку; AC измеримы и mapped; решения и objections заполнены; validation план отличает mocks/disk/native; unsupported claims о текущем UI/test green отсутствуют; edge cases разобраны; связанные docs drift исправляются только в EXEC; additive interface/JSON semantics раскрыты; manual-review challenge — «сохранено ли после перезапуска и открылся ли Проводник на самом деле?» — закрывается AC2 и отдельной native boundary, не mock-утверждением.
- No-findings justification: не применимо — находки перечислены и устранены на уровне дизайна. Остаточный риск: ранее существующая коллизия filename-key и одновременные записи разных процессов вне scope; среда native/render ещё требует EXEC preflight.
- Needs human: только exact approval; выборов продукта, блокирующих утверждение, нет.

### Post-EXEC Review
- Статус / Stop decision: PASS. Обе функции реализованы локально, обязательные проверки green; дополнительных code/test циклов без новой причины не требуется.
- Scope reviewed: approved SPEC, `git status --short`, `git diff --numstat` и фактический diff всех изменённых tracked файлов; новые IDirectoryOpener/SystemDirectoryOpener, SavedValuesEditingTests, TemplateSelectionViewModelTests, SavedValuesWorkflowTests; README/UI_TESTS и перечисленные ниже артефакты. Изменённые production файлы соответствуют §16, unrelated changes не обнаружены.
- Scope/Evidence pass: baseline 63 unit/integration + 15 UI PASS; сохранён failing regression до изменения persistence (кэш «После» при отказе записи вместо «До»). После исправления тест закрывает прежний disk/cache, удаление temp и успешный retry. Сборка и полный suite подтверждены завершёнными командами с exit 0 и TRX.
- Contract pass: S1/AC1–3 — SavedValuesEditingTests.SaveValues_RoundTripWithoutDocument_PreservesMetadataHiddenKeysAndOtherTemplates и UI mouse-input save/reopen. S2/AC4–5 — untouched fields, locked-file retry, shared busy guard и отдельный UI test блокировки полей/кнопок. S3/AC6–8 — unit tests default/custom/relative/invalid draft/picker pending/ошибки, UI empty selection/custom path/shell error и реальный shell smoke. S4/AC9 — прежние generation/overwrite/date/token tests остались green, UI test отдельно вызывает SaveButton и получает DocumentSaved. AC10 — полный suite и просмотренные PNG двух размеров.
- Adversarial risk pass: проверены отсутствие выходного каталога/имени при values-only, OpenAfterSave=true без открытия документа, неизвестный сохранённый ключ, другой шаблон, даты после JSON deserialization, нетронутые пустые поля, failed write/cache/retry, concurrent SaveCommand, pending load, pending picker, исчезнувшая папка, shell exception, пустой список и смена пути. Ключ filename и multi-process sync остались прежними и не объявляются исправленными.
- Role-Based pass: business/domain — сохранение без выпуска документа и сохранение metadata подтверждены; UX — wireframe и фактические PNG сопоставлены, действия/статус доступны; tester — mouse input и реальный диск отличены от mocks/native; developer — review atomic replacement/candidate/cache, merge и shared busy guard; operations — реальные settings не тронуты, native проверка использовала новую пустую временную папку. Все применимые роли PASS как self-review, не независимый review.

| Severity | Area | Finding | Required action | Status |
| --- | --- | --- | --- | --- |
| MEDIUM | persistence | При заблокированном файле прежний сервис менял cache до отказа записи | Кандидат JSON + временный файл и замена; regression RED→GREEN | fixed |
| MEDIUM | UI test runtime | Асинхронная команда в старом Headless harness оставляла effective state кнопки устаревшим: harness не подключал ReactiveUI scheduler, в отличие от Program | `.UseReactiveUI()` как у приложения, `.UseSkia()` и настоящие mouse-input действия; UI retry проходит без принудительного включения кнопок | fixed |
| MEDIUM | folder interaction | Открытие могло быть доступно при незавершённой смене каталога; прежний picker путь вызывал повторное сохранение через setter | Состояние смены каталога с учётом pending операций, прямое уведомление после picker, тест pending/error | fixed |
| LOW | UX | Watermark строки показывал имя класса DocTemplater.Domain.Placeholder | Использовать Label; просмотр PNG empty/error | fixed in scope |
| LOW | formatting | Три изменённых XAML строки содержали trailing whitespace | Убрать пробелы; `git diff --check` повторён | fixed |

- Fix and re-review: после persistence исправления targeted 20/20, после folder/busy дополнений targeted 21 unit + 3 UI PASS; затем обычный build и полный suite PASS. После финальной очистки трёх trailing spaces повторён только static diff check: семантика XAML не менялась. Принудительный CanExecute, ослабление проверок и подмена screenshot assertions не использовались. UI test ожидает реальную готовность кнопки и кликает мышью.
- Evidence inspected (local-only root): `C:/Users/Kibnet/.codex/visualizations/2026/10/19/01a153de-cede-7b71-9985-fcb863b4c66c/doctemplater/`.
  - `build.log`: `dotnet build DocTemplater.sln --nologo`, exit 0, 0 errors, около 2 секунд.
  - `all-tests.log` и `test-results/all-tests_net9.0_20261019144822.trx`, `all-tests_net10.0_20261019144822.trx`: `dotnet test DocTemplater.sln --nologo --logger 'trx;LogFilePrefix=all-tests' --results-directory <artifact-root>/test-results`, exit 0; 18/18 UI и 73/73 unit/integration, 0 skipped/failed, около 8 секунд вместе со сборкой.
  - PNG самостоятельно просмотрены через view_image: `values-saved-800.png`, `values-saved-900.png`, `values-validation-error-800.png`, `values-write-error-800.png`, `values-saving-800.png`, `templates-folder-800.png`, `templates-folder-900.png`, `templates-folder-error-900.png`. Wireframe соблюдён: настройки отдельной строкой, кнопки/ошибки доступны без перекрытия, busy блокирует редакторы. Длинные пути прокручиваются внутри TextBox, статус переносится.
  - `native-shell-smoke.txt`: загружен реально собранный DocTemplater.dll и вызван SystemDirectoryOpener.Open; через Shell.Application.Windows проверен точный LocationURL Проводника на новой пустой временной папке. Закрыто только новое окно, временная папка удалена. PASS. Полный native app flow не запускался.
- Depth checklist: scope/Non-Goals соблюдены; AC1–10 имеют фактическое evidence; исходное поручение не заменено отчётом; schemas/старые selectors сохранены; старые tests green; comments/docs соответствуют двум save-действиям и реальному settings path; platform boundary остаётся маленькой; JSON/cache и picker concurrency проверены; ручной review challenge «будут ли новые значения после повторной загрузки и откроется ли правильная папка?» закрыт fresh service/VM и native shell observation.
- No-findings justification: не применимо — findings перечислены и исправлены; незакрытых in-scope нарушений обязательных AC не осталось.
- Validation boundaries: PNG — настоящий Skia Headless render, не native screenshot; видео отсутствует из-за отсутствия recorder в harness, fallback обоснован §6.2/11 и содержит logs/PNG. Полный native UI flow, process restart с пользовательскими settings и установка не выполнялись. Persistence подтверждён новым service/VM из реального временного JSON, реальные пользовательские данные не менялись.
- Residual risks: в baseline и финальной сборке присутствует прежний NU1903 для транзитивного Tmds.DBus.Protocol 0.21.2; зависимости этой задачей не изменены. Выбранный SDK по умолчанию 11 preview выдаёт NETSDK1057. Старые filename-key collisions и sync между процессами вне scope. Commit/push/установка не выполнялись.
- Needs human: нет; дополнительное разрешение понадобилось бы для установки/публикации, которые не входят в результат этой задачи.

### Повторный Post-EXEC Review после пользовательских замечаний
- Статус / Stop decision: PASS для исходной функции и исправлений P1/P2. AC1–13 закрыты текущим evidence. Предыдущий self-review пропустил два подтверждённых дефекта; новые проверки сохраняются в репозитории.
- Scope/Evidence pass: просмотрены актуальные изменения ValuePersistenceService/IValuePersistenceService, двух path-update callers в TemplateSelectionViewModel, подписки на значения в DataInputViewModel, service/VM regressions, UI workflow и fakes, README/UI_TESTS; актуальные status/diff и `git diff --check`. Новые contract изменения входят в тот же результат и риск; schema/установка/межпроцессные операции не добавлены.
- Contract pass: AC11 — ConcurrentTemplateSaves_PreserveEverySuccessfulUpdateOnDiskAndInCache, 16 saves + seed, новый service и cache сохраняют все 17 записей. AC12 — ConcurrentDirectoryAndTemplateUpdates_PreserveBothChanges, InitialFullSettingsSave_IsOrderedBeforeConcurrentFieldUpdates, PublicReadsAndSaveArguments_AreDetachedFromTheCommittedCache, FailedDirectoryWrite_KeepsCommittedStateAndReleasesLockForRetry и DirectoryChanges_UseAtomicFieldUpdateInsteadOfSavingAFullSettingsSnapshot. AC13 — EditingAfterSuccessfulSave_UpdatesStatusWithoutSavingAgain для string/int/date/clear, а также UI мышь → сохранение → keyboard input → несохранённый статус → проверка прежнего JSON новым service → повторный save → новый JSON/статус.
- Adversarial risk pass: lock охватывает чтение текущего состояния, копирование, изменение, запись и commit cache. Внутри него используются только Core-методы без повторного захвата. Публичные чтения/аргументы не дают изменять committed cache. Исключения записи освобождают lock через finally и сохраняют прежний файл. Полная SaveSettingsAsync остаётся документированной явной заменой снимка; в runtime-коде UI её больше не вызывает — оба изменения каталога используют узкий метод на актуальном состоянии. Проверено сохранение CLR string/int/DateTimeOffset в текущем сеансе при изоляции снимков, чтобы не сломать старый Service_Integration_SmokeTest. Новая правка меняет только feedback, не запускает запись/генерацию.
- Role-Based pass: business/domain — данные разных шаблонов и общие настройки не теряются при обновлении из приложения; UX — статус соответствует видимому несохранённому полю, action labels/layout сохранены; tester — оба дефекта имели RED до исправлений, затем regression/полный suite green; developer — единый lock на чтение/запись, без reentrant await и без утечки mutable cache; operations — только временный JSON, предыдущая native shell проверка не переобъявляется новым полным native UI run. Это self-review, не независимая read-only проверка.

| Severity | Area | Finding | Required action | Status |
| --- | --- | --- | --- | --- |
| HIGH / P1 | persistence | Два успешных параллельных save теряли записи; первый RED ожидал 17, получил 6 | Сериализовать read-modify-write и path-only update под одним lock; изолировать публичные снимки; regressions | fixed, full suite green |
| MEDIUM / P2 | UX feedback | После правки оставался статус «Значения сохранены»; второй RED подтвердил старую строку вместо ожидаемой | На изменение поля/очистку после save показать несохранённые изменения; проверить отсутствие записи до повторного save | fixed, unit/UI/PNG PASS |

- Fix and re-review: целевой прогон исходных двух regressions RED 2/2; после исправлений GREEN с проверкой failed-write retry; расширенный targeted scope 31 unit/integration PASS (storage, VM, smoke) и 3 UI PASS. После финального code change выполнены обязательные build/full tests, без ослабления assertions или прежних проверок. Последующие изменения — только SPEC/docs/evidence; повторный build/test не требуется без новой причины.
- Evidence inspected, local-only: `C:/Users/Kibnet/.codex/visualizations/2026/10/19/01a153de-cede-7b71-9985-fcb863b4c66c/doctemplater/review-fixes/`.
  - `regression-before-summary.txt` — сводка фактически наблюдавшихся RED и команды запуска (не сырой test log).
  - `build.log`: `dotnet build DocTemplater.sln --nologo`, exit 0, 0 errors.
  - `all-tests.log`, `test-results/review-fixes_net9.0_20261019152551.trx`, `test-results/review-fixes_net10.0_20261019152552.trx`: `dotnet test DocTemplater.sln --nologo --logger 'trx;LogFilePrefix=review-fixes' --results-directory <review-fixes>/test-results`, exit 0; 80/80 unit/integration и 18/18 UI, 0 failed/skipped.
  - `values-unsaved-900.png` самостоятельно просмотрен после полного прогона: в поле «ООО После правки», внизу «Есть несохранённые изменения», кнопки/настройки доступны. UI test подтверждает на этом шаге прежнее значение в JSON и новое значение только после повторного save. Прежние rendering scenarios 800/900 также прошли и сохранили PNG.
- Depth checklist: обе пользовательские review-находки закрыты новым evidence; исходные AC1–10 по-прежнему в полном green наборе; matrix/decision ledger/schema scopes учтены; все runtime path callers проверены через rg и VM test; типы/metadata/неизвестные сохранённые JSON keys сохраняются; snapshots и concurrency не опираются только на mocks; прежний `SaveSettingsAsync` больше не используется для patch из UI; README/UI_TESTS уточнены; manual-review challenge «не затрёт ли запоздалое изменение каталога недавно сохранённые значения?» покрыт смешанным test и field-update API.
- No-findings justification: не применимо — исходные HIGH/MEDIUM находки перечислены и исправлены; новых незакрытых in-scope нарушений обязательных AC при повторном review не найдено.
- Остаточные границы: lock одного service, несколько процессов/независимых service над одним файлом вне scope; full SaveSettingsAsync намеренно заменяет весь снимок и не должен использоваться для устаревшего patch. Видео по-прежнему отсутствует: recorder не поддержан harness, fallback — Headless PNG/TRX/logs. Native UI/установка не выполнялись. Прежние NU1903 и NETSDK1057 сохраняются, зависимости не менялись.
- Needs human: нет. Не выполнять дополнительные успешные validation loops без нового изменения/риска; commit/push/installation не входят в scope.

## Approval
Получена фраза пользователя: «Спеку подтверждаю». Scope не расширялся; изменения в пределах утверждённых действий и их сохранности/проверяемости.

## 20. Журнал действий агента
| Фаза / событие | Решение и основание | Evidence / остаток работы | Следующее действие | Фактическое решение человека | Затронутые артефакты |
| --- | --- | --- | --- | --- | --- |
| SPEC: исследование | Существующая форма + отдельный save; открытие активной папки | Прочитан код и инструкции, исходно чистый master/107d986; реализация не начата | Создать и проверить SPEC | Исходное поручение, exact approval отсутствует | Только эта SPEC |
| SPEC: post-SPEC review | Уточнены failure/cache, hidden keys, active path и layout/feedback | Дизайн/AC повторно проверены; post-SPEC PASS, тесты/рендер pending EXEC | Показать спецификацию и получить exact approval | Ожидается `Спеку подтверждаю` | Только эта SPEC |
| EXEC: approval | Получена точная фраза, scope прежний | .NET 9/10 runtime доступны; исходный код ещё не менялся | Baseline, реализация, tests/visual review | `Спеку подтверждаю` | Код, тесты, docs и эта SPEC в утверждённых границах |
| EXEC: реализация и review | Добавлены два действия, безопасная запись, UI feedback/busy и совместимый active path | 73 unit/integration + 18 UI PASS, build PASS, 8 render PNG просмотрены, native shell PASS, diff check PASS | Передать локальный результат | Approval сохранён; новых решений не требовалось | Код/тесты/docs/SPEC; evidence вне Git |
| EXEC: пользовательское review | Найдены P1: потеря A при двух успешных save A/B; P2: статус сохранения остаётся после правки | Воспроизведены на временных JSON; предыдущий post-EXEC PASS не закрывает AC11–13 | Regression RED, исправление и повторные обязательные checks | «Исправь»; исходный scope сохранён | Persistence/VM, interface/fakes, tests, эта SPEC |
| EXEC: исправления P1/P2 завершены | Общий lock и изолированные снимки, atomic path update, feedback после новых правок | RED→GREEN, 80 unit/integration + 18 UI PASS, build/diff check PASS, новый PNG просмотрен | Передать исправленный локальный результат | «Исправь» выполнено в прежнем scope | Код, interface/fakes, tests, docs/SPEC, local-only evidence |
| Delivery: commit/push разрешены | Пользователь явно поручил закоммитить и запушить подготовленные изменения | Git preflight: master, origin git@github.com:Kibnet/DocTemplater.git; fetch выполнен; build и 98 tests ранее PASS, после них менялись только docs/SPEC | Scoped commit и обычный push без force, затем проверка remote SHA/clean state | «Закоммить и запушь» | Только файлы этой задачи; PNG/logs/TRX остаются local-only вне Git |
