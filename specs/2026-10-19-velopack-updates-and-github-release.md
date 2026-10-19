# Velopack: автообновление DocTemplater и первый GitHub Release

## 0. Метаданные
- Фаза: EXEC. 2026-10-20 пользователь подтвердил спецификацию: «Спеку подтверждаю. А затем установи из релиза». Реализация, push/tag, публикация и последующая установка из опубликованного GitHub Release разрешены.
- Исходное поручение: «Добавь в приложение автообновление через velopack и опубликуй релиз на гитхабе».
- Форма: Expanded по `C:/Users/Kibnet/.codex/agents/templates/specs/_template.md`. Риск high: установка/замена бинарников, сохранность пользовательских файлов, GitHub delivery и публичные артефакты.
- Профили: dotnet-desktop-client + ui-automation-testing; context testing-dotnet. Core: creator-vibe-lens, model-behavior-baseline, tool-execution-baseline, quest-governance/mode, collaboration/testing-baseline; governance: spec-linter/rubric, review-loops, commit-message/github-delivery/versioning policies. Локальный override не найден.
- Владелец результата/approval: пользователь; проектирование, реализация, проверки и уже заказанная публикация — агент.
- Surface: Codex desktop, Windows/PowerShell. Целевой behavior baseline каталога GPT-6 Astra; exact model/effort текущего host не предоставлены, переключение не выполнялось. Model eval не применим: меняется приложение.
- Baseline: чистая master, HEAD d99c3b2; origin `git@github.com:Kibnet/DocTemplater.git`. Предыдущая функция: build и 98 tests PASS; это не evidence будущего updater.
- План: рабочая ветка `feat/velopack-auto-update`, первый stable GitHub Release `v0.1.0`, target Windows x64, package ID `DocTemplater`, channel `win`. До tag проверить отсутствие конкурентного релиза/тега; существующие опубликованные версии не переписывать.
- Ссылки на первичные источники: [startup/update](https://docs.velopack.io/integrating/overview), [GitHub source](https://docs.velopack.io/integrating/update-sources), [сохранность файлов](https://docs.velopack.io/integrating/preserved-files), [pack CLI](https://docs.velopack.io/reference/cli/content/vpk-windows), [testing](https://docs.velopack.io/integrating/testing), [UpdateManager](https://docs.velopack.io/reference/cs/Velopack/UpdateManager), [Setup](https://docs.velopack.io/reference/cli/content/setup-windows).

## 1. Overview / Цель
Установленная версия сама проверяет GitHub и скачивает обновление. Пользователь видит прогресс/ошибку и применяет готовое обновление кнопкой с перезапуском; несохранённый ввод защищён явным подтверждением. Первый установщик, пакет обновления и feed публикуются в публичном репозитории Kibnet/DocTemplater.

Outcome contract: результат — работающий updater, воспроизводимая упаковка/доставка и опубликованный release с проверенными assets и ссылкой в итоговом сообщении. Исходники, тесты, changelog и release notes согласованы с tag/SHA. Подготовленный локальный installer или draft release сами по себе не закрывают поручение о публикации.

Stop rules: до approval меняется только эта SPEC; после approval выполнить реализацию, обязательные проверки, review и уже разрешённую публикацию без повторного запроса того же разрешения. При обязательном validation blocker не публиковать заведомо непроверенный релиз, не объявлять PASS; точно назвать невыполненную проверку. До публикации и резервного копирования не устанавливать/обновлять действующую пользовательскую копию приложения и не создавать искусственные публичные релизы для тестов.

## 2. Текущее состояние (AS-IS)
- В Program.Main нет Velopack startup; App создаёт сервисы и MainWindowViewModel, экран — ContentControl с выбором/заполнением шаблона. Общей панели версии/обновления нет.
- Приложение и UI tests — net9.0/Avalonia 11.3.8, unit tests — net10.0/xUnit/VSTest. Переход framework в эту задачу не входит.
- Saved values и настройки находятся в `%APPDATA%/DocTemplater/settings.json`; запись через один ValuePersistenceService уже защищена lock, публичные снимки изолированы. Изменение пути выполняется SaveTemplatesDirectoryAsync.
- App по умолчанию создаёт Templates рядом с EXE, AppSettings default — `./Templates`. Выходной каталог формы по умолчанию совпадает с каталогом шаблона. Такой default непригоден для updater-managed бинарного каталога: там могут оказаться и шаблоны, и готовые документы.
- DataInputViewModel уже различает SaveValues/SaveDocument и busy; статус после правки — «Есть несохранённые изменения». Полного признака несохранённых полей/выходных настроек пока нет.
- GitHub API подтвердил: публичный репозиторий, default master, релизов нет; локальных tags нет. gh авторизован как Kibnet, permissions push/admin=true. В репозитории не найден существующий release workflow/pack script/changelog. Персональные DOCX в исходниках не найдены; release assets всё равно проверяются по содержимому.
- `dotnet tool list --global` подтвердил установленный vpk 1.2.161; `vpk pack --help` прочитан. `vpk --version` не поддерживается, больше не использовать. Актуальные official reference/NuGet подтверждают stable SDK/CLI 1.2.161; обе версии фиксируются локальным tool manifest/package reference, не через обновление global tools.

## 3. Проблема
Нет воспроизводимой установки и канала обновления; простое подключение updater к текущим путям ставит пользовательские документы под замену вместе с бинарниками.

## 4. Цели дизайна
Один владелец update lifecycle для автоматической и ручной проверки; UI не блокируется сетью; отсутствие сети не мешает документам. Постоянные пользовательские данные вне install/content/packages directories. Явное управление перезапуском, фиксированные source/identity/version, воспроизводимый release и проверяемая цепочка tag → commit → package → GitHub assets.

## 5. Non-Goals
Linux/macOS/ARM64 installers, MSI/system-wide installation, beta channels, forced downgrade, платный signing/service, удалённый сервер обновлений, смена формата saved values или ключей шаблонов, переписывание редактора. Личные данные/шаблоны не публикуются. Не добавлять GitHub token в приложение. Изменение branch protection/прав аккаунта и посторонние dependency upgrades не заказаны. Установка из опубликованного релиза дополнительно разрешена 2026-10-20: до неё сохранить backup настроек и выбранных шаблонов, сверить скачанный installer по SHA256SUMS, после проверить installed version/path и сохранность данных.

## 6. Предлагаемое решение (TO-BE)
### 6.1 Распределение ответственности
- Program: ранний Velopack bootstrap перед Avalonia и обычными операциями с данными.
- Velopack adapter: единственный UpdateManager и реальные check/download/apply; native package types не выходят в VM.
- ApplicationUpdateService: application-scoped snapshot/state/progress, автоматическая и ручная проверка, сериализация/coalescing, ошибки, lifetime/cancellation.
- MainWindowViewModel + компактная панель: версия, статус, проверка, установка; оставляет доступ к текущему экрану.
- DataInputViewModel: отслеживание изменений относительно загруженного/сохранённого состояния для подтверждения перезапуска, включая выходные настройки; values-only по-прежнему не сохраняет выходные настройки.
- ApplicationDataPaths/bootstrap: постоянные каталоги, разрешение/перенос устаревших путей без потери файлов, инициализация сервисов до открытия рабочей формы.
- ApplicationInstanceCoordinator: один рабочий процесс на canonical data root, lifetime file lock до обычной инициализации, отдельный guard перед apply.
- IValuePersistenceService/ValuePersistenceService: при необходимости минимальный атомарный metadata update для миграции путей, под существующим lock; не передавать старый полный settings для изменения одного поля.
- Repo-local SDK/vpk manifests, scripts/Build-Release.ps1 и GitHub workflow: подготовка артефактов, проверка версии, выпуск с curated release notes.

### 6.2 Детальный дизайн
**Startup и update flow.** Вызвать `VelopackApp.Build().SetAutoApplyOnStartup(false).Run()` ровно один раз первой операцией entry point. Это сохраняет выполнение installer hooks без запуска обычного UI. Автоприменение на старте выключено: перезапуск/замена выполняются только после проверки файлов и явного действия пользователя. Не использовать отложенный helper, принудительно закрывающий приложение по timeout.

В production adapter источник фиксирован: GithubSource публичного `https://github.com/Kibnet/DocTemplater`, без токена и prereleases, без downgrade. SDK и CLI 1.2.161. После запуска готового окна установленной версии выполнить одну async check/download последовательность; дополнительных фоновых таймеров не нужно. Кнопка ручной проверки использует тот же service. Совпавшие запросы не создают второй check/download. Готовый локальный пакет распознаётся при следующем запуске и снова предлагает установку; после уже подтверждённого успешного apply готовность сбрасывается новой версией.

Для обычного bin/Debug или непакетированного publish режим Disabled с объяснением «Автообновление доступно в установленной версии»; не выполнять бессмысленные сетевые retry и не показывать необработанный NotInstalledException. Известные network/rate-limit/checksum/lock ошибки становятся статусом и допускают ручной retry; не показывать чужие exception/token payloads в UI. Версия — из package metadata, с assembly fallback в dev.

После Velopack.Run и до чтения/изменения settings/bootstrap нормальный процесс захватывает lifetime file lock в постоянном canonical data root (например .instance.lock с FileShare.None). Второй процесс с тем же data root не открывает новую редактируемую форму и не пишет настройки; сообщает о запущенной копии и завершается, не завершая первый процесс. Hooks должны проходить до single-instance guard. После Velopack.Run до любого mutable bootstrap и повторно перед apply проверять отсутствие другой неподготовленной копии DocTemplater, включая обнаруживаемую legacy-копию без нового lock; при её наличии/неопределимом состоянии не начинать копирование/миграцию/запись настроек и не запускать apply, попросить закрыть другую копию. Старый процесс не убивать. При первом переходе со старой версии документация требует закрыть её до запуска Setup. Общую синхронизацию с произвольным сторонним редактором файлов/переименованной legacy-программой не обещать. Native tests запускают второй процесс с той же fixture data root и подтверждают отказ без потери ввода/записи первой копии.

**Применение.** Кнопка «Установить и перезапустить» доступна только Ready и когда форма не busy. Перед запуском native apply проверить безопасность путей данных. При наличии несохранённых значений/выходных настроек показать диалог «Обновление перезапустит приложение. Несохранённые изменения будут потеряны» с безопасным default «Вернуться к документу» и явным «Обновить без сохранения». Cancel не завершает процесс и не записывает поля. Пользователь может вернуться, явно сохранить значения/документ и повторить. Не включать скрытое автосохранение. Apply вызывается один раз после разрешения; при ошибке до выхода приложение остаётся доступным.

**Постоянные файлы.** Новый default Templates — `%APPDATA%/DocTemplater/Templates`; settings.json остаётся на прежнем месте. Сначала асинхронно загрузить/разрешить настройки и пути, затем сформировать discovery/рабочую форму; не показывать список из временного default, пока bootstrap не закончен.

Существующий корректный внешний абсолютный каталог сохраняется. Относительный путь разрешать по фактическим прежним правилам текущего запуска и сохранять абсолютный, если это существующий пользовательский каталог. Старый `./Templates`, который не разрешился, и отсутствие настроек получают новый постоянный default. Если рядом с текущим EXE есть прежние пользовательские Templates/документы, копировать их содержимое, включая подкаталоги, в постоянную папку без удаления источника и перезаписи отличающегося файла. Совпавшие файлы сравнивать по содержимому; конфликт/недоступный файл требует видимого решения через выбор каталога, не автоматического overwrite. Перед записью миграции сохранить backup затронутых settings и read-back результата. Ошибка копирования/backup блокирует destructive apply и не выдаётся за успешный перенос.

Нельзя хранить активные Templates и зарегистрированные output directories внутри управляемых Velopack content/packages/root paths. Bootstrap и проверка перед apply обнаруживают такие пути. Безопасный перенос обслуживаемого каталога копирует все пользовательские файлы, затем атомарно обновляет связанные metadata; готовые DOCX не оставляются внутри удаляемого install дерева. Если путь выбран вручную позднее, тот же guard применяется при выборе или до любой замены; повторный запуск не обходит защиту, поскольку auto-apply отключён. Не удалять пользовательские данные при uninstall/rollback.

Перед миграцией существующий settings.json должен быть прочитан без fallback на пустые defaults: текущее LoadSettingsCoreAsync скрывает parse/read errors. Bootstrap должен отличать отсутствие файла от ошибки существующего файла и не перезаписывать unreadable/невалидный JSON настройками по умолчанию. Ошибка видима, backup исходника сохраняется, apply заблокирован до исправления. Для output directory, совпавшего с binary root, переносить известные пользовательские DOCX, а не всё дерево бинарников/packages; выделенная папка Templates копируется со своим пользовательским содержимым. Migration path containment должен учитывать нормализацию, границу сегмента и Windows case; junction/symlink не обходить с рекурсивной записью вне проверенных корней.

Проверять filesystem identity путей, а не только строки: внешний путь через junction/symlink может фактически указывать внутрь current. Canonicalize managed roots, Templates, все сохранённые и текущий output path с разрешением ancestor reparse points; для ещё не существующего output разрешать существующего родителя. Неразрешимый/неоднозначный путь блокирует apply с сообщением, а не считается внешним. При копировании обнаруженный reparse point не обходить рекурсивно: остановить миграцию с указанием пути и сохранить источник, до выбора безопасного каталога. Добавить Windows junction внешний→current и цикл/выход из дерева в negative tests. Корректный внешний обычный каталог не переносится.

Первый installer не умеет находить неизвестный каталог старой распакованной копии на компьютере. Release notes объясняют: сохранённые значения в AppData подхватываются, а старую внешнюю папку шаблонов можно выбрать существующей кнопкой `...`. Источник не удаляется; не сканировать весь диск и не обещать автоматический импорт неизвестной копии.

**Visual planning artifact — wireframe в SPEC:**
```text
┌ существующий экран выбора / заполнения, рабочая область ┐
│ поля, превью, выходной файл и кнопки остаются доступны   │
└────────────────────────────────────────────────────────┘
DocTemplater 0.1.0   [Проверить обновления]
Скачивается 0.1.1… 42%        [progress]
Готова версия 0.1.1          [Установить и перезапустить]
Ошибка проверки. Повторите позже. [Повторить]

При грязной форме: [Вернуться к документу] [Обновить без сохранения]
При bootstrap: «Подготовка данных…», без рабочего списка до разрешения путей.
```
Общая панель не заменяет StatusMessage текущего документа; статусы независимы. Проверить 800×600/900×600 и выбор/форму, error/progress/ready/dirty-dialog. Video fallback: существующий Headless harness имеет Skia PNG, но не recorder; сохранять/просмотреть реальные render PNG + test logs. Native updater smoke имеет отдельные receipts/logs; не называть его видео или полным тестом личной установленной копии.

**Packaging/delivery.** Первый release `v0.1.0` — Windows x64, self-contained publish существующего net9.0, без trimming/single-file, `DocTemplater.exe`, стабильные packId/channel. Installer без Authenticode, если signing не настроен; не покупать сертификат и не обещать подписанный издателем файл. Обязательные assets: Setup.exe, full nupkg, releases.win.json и SHA256SUMS.txt. Portable zip публиковать только если он сгенерирован/проверен; updater в обычном непакетированном zip не обещать. Delta первого релиза отсутствует закономерно; будущие релизы могут формировать delta, full fallback остаётся.

Build-Release.ps1 принимает SemVer, создаёт новые publish/releases каталоги внутри заранее проверенного artifact root и проверяет exit codes. Не использовать опасную рекурсивную очистку вычисленных путей. Версии assembly/package/tag должны согласовываться; vpk startup verification не отключать. Publish содержит только бинарники/необходимые ресурсы, не пользовательские DOCX/settings/backups/test artifacts. SHA256 фиксируется после упаковки.

Workflow на SemVer tag выполняет restore pinned tool/SDK, Release build, весь suite и pack; repo global.json фиксирует доступный stable SDK 10.0.402, CI явно обеспечивает net9 и net10 runtimes для обоих test projects; `contents: write` нужен только release job. Release notes хранятся в репозитории, включают новые возможности d99c3b2 и updater, требования/первую установку/сохранность шаблонов/unsigned installer/known limits. Не публиковать произвольные v-tags, не менять published assets при повторном запуске.

Разрешённая доставка после approval: подготовить и проверить код/notes/assets; commit и обычный fast-forward push проверенного результата в master, затем SemVer tag на этот commit; создать draft, загрузить полный комплект и проверить имена/размеры/хэши, затем опубликовать stable/latest. Live rules API сейчас вернул пустой список правил для master; перед доставкой перепроверить drift/правила и не обходить появившуюся защиту. Возможны gh/vpk или workflow, но один выбранный publisher, без параллельных upload flows. При существующем draft сверить tag/SHA и продолжить его; опубликованный tag не force-move. Финальный read-back release/assets и anonymous feed check обязателен. GitHub public release как итог уже разрешён текущим поручением.

### 6.3 User-Observable Scenarios
| Scenario | Trigger | Ожидаемый результат | Evidence | AC |
| --- | --- | --- | --- | --- |
| S1 | Запуск установленной версии | Проверка/скачивание без блокировки документов, одна общая операция | Service tests + UI progress + real package feed | AC1–3 |
| S2 | Готовое обновление, форма изменена | Отмена сохраняет ввод; явное подтверждение разрешает restart | Dirty-state/диалог UI tests, native fixture apply | AC4–5 |
| S3 | Нет сети/пакет повреждён/вторая операция | Ошибка/retry, документы работают, плохой пакет не применяется | Adapter/service negative tests | AC3/6 |
| S4 | Обновление версии с шаблонами/значениями | Файлы и metadata сохранны, новая версия запускается | Две реальные локальные версии, hashes/receipt | AC7–9 |
| S5 | Скачать первый release на GitHub | Доступны installer/full package/feed/checksums, версия соответствует tag | gh read-back + публичное получение feed/package | AC10–12 |

### 6.4 State / Interaction Matrix
| State | Trigger | Transition/result | Error/concurrent/empty |
| --- | --- | --- | --- |
| Disabled | Dev/raw launch | Версия/объяснение, документы доступны | Сеть updater не вызывается |
| Idle/UpToDate | Auto/manual check | Checking → UpToDate или Downloading | Запросы coalesce, нет дубля manager |
| Downloading | Progress/закрытие | Прогресс; закрытие без принудительного apply | Cancellation/lifetime handled, partial не считается Ready |
| Ready | Apply | Проверка путей + dirty/busy guard → Applying | Cancel остаётся Ready; busy не завершает процесс |
| Error | Retry | Повтор check/download через тот же service | Offline/rate limit не запускают бесконечный цикл |
| Applying | Native update | Процесс завершён, новая версия стартует | Неподтверждённый apply не вызывается; ошибка перед выходом видна |
| Bootstrap/migration | Unsafe path/conflict | Копия/backup/read-back либо блокировка apply | Ни источник, ни конфликтующий файл не удаляются |
| Release draft | Assets verified | Publish → stable/latest → read-back | Published tag immutable; неизвестный outcome сначала read-back |

### 6.5 Decision Ledger
| Decision | Owner | Chosen option | Confidence | Risk | Needs user before EXEC |
| --- | --- | --- | ---: | --- | --- |
| Update UX | agent, принимается SPEC | Авто check/download; apply кнопкой, без скрытого restart | 0.95 | Иначе потеря незаписанного ввода | Нет отдельного вопроса |
| Target/version | agent, принимается SPEC | Первый v0.1.0, Windows x64 stable | 0.9 | Другие OS требуют отдельной доставки | Нет отдельного вопроса |
| Source/identity | agent | Public GitHub, DocTemplater/win, SDK/CLI 1.2.161 | 0.98 | Несогласованный channel ломает обнаружение | Нет |
| Data location | agent | AppData Templates, внешний custom path сохраняется | 0.98 | Данные в current стираются update | Нет |
| Public actions | user | Release, необходимые source/tag push разрешены текущим поручением | 1.0 | Не распространяется на personal install | Уже разрешено |
| Signing | agent, принимается SPEC | Без Authenticode; указать в release notes | 0.9 | Не обещать подписанный installer | Нет отдельного вопроса |
| Second process | agent, принимается SPEC | Один нормальный процесс на data root; другой не пишет/не форсирует apply | 0.95 | Иначе updater может потерять ввод другой копии | Нет отдельного вопроса |
| Exact approval | user | `Спеку подтверждаю` для этой SPEC | 1.0 | Обязательный phase gate | Только фазовый gate |

### 6.6 Runtime / Config / Data Contract Matrix
| Area | Source of truth | Change | Compatibility / verification |
| --- | --- | --- | --- |
| Updater | Один application service/adapter | Velopack/GithubSource, channel win | Тесты concurrency/state, real feed |
| Version | Tag v0.1.0 → build/pack metadata | Repo release version/notes | Tag SHA/manifest/exe согласованы |
| Settings | Прежний AppData JSON | Узкое атомарное обновление путей при миграции | Backup/read-back, поля/metadata не теряются |
| Templates/output | Custom external или новый AppData default | Устранить хранение под binary root | Copy/hash/conflict tests + update smoke |
| Release | GitHub tag/assets | Setup/full/feed/SHA256 | Download/read-back, без embedded tokens |

## 7. Бизнес-правила / Алгоритмы
UpdateManager живёт только в adapter, один service на приложение. Не смешивать автоматические/ручные state owners. Не применять неподтверждённый, неполный или checksum-failed пакет. Startup не применяет pending update сам. UI busy и несохранённые данные проверяются до запуска native helper. Миграция не удаляет/не перезаписывает исходные файлы. Отсутствующий внешний custom path не заменять молча пустой папкой. До публикации artifact identity/feed/release notes должны совпасть с проверенным commit.

## 8. Точки интеграции и триггеры
Program.Run → обычный bootstrap → данные/discovery/MainWindow → auto check. Общая панель/ручная check → тот же service. Apply → current DataInput dirty/busy → path guard → native backend. Выбор/изменение TemplatesDirectory → защита managed roots. Tag workflow/локальный release script → pack/verify/draft/publish/read-back.

## 9. Изменения модели данных / состояния
Новые update state/version/progress/error и dirty state — runtime, без изменения сохранённых значений плейсхолдеров. Default path становится абсолютным постоянным. Если нужен маркер успешной миграции — добавочное backward-compatible поле, только после успешного copy/settings commit; не считать маркер заменой проверки актуального пути перед apply.

## 10. Миграция / Rollout / Rollback
Первый запуск portable/unpackaged сохраняет обычное заполнение и не выдаёт ложной готовности updater. Первый Setup нужно запустить вручную: прежняя версия без Velopack сама получить эту функцию не может. Существующий AppData JSON читается; неизвестный каталог старой копии выбирается пользователем, не угадывается. Исходники/копии миграции сохраняются.

Перед публичным release проверяются локальные fixture версии N → N+1 настоящего packaged DocTemplater: Program/App/bootstrap/MainWindow, production adapter/service и применение через обычную UI-команду, в отдельном временном install/data root. Нельзя установить production identity поверх рабочей копии. Fixture имеет уникальные packId/executable identity, явные temp data paths и local feed через test-only build/composition options; основная логика/Views и startup остаются кодом приложения. Эти отличия документировать. Публичный Release build не включает test overrides и фиксирован на production GitHub source/identity. Отдельный probe допустим как дополнительный backend test, но не вместо установленного приложения. Проверить обычное появление окна до/после, версию нового процесса и работоспособность заполнения после обновления. Receipt содержит версию/path/hash запущенного EXE и hashes settings/templates/output до/после. TestVelopackLocator/fake apply не заменяет native smoke. При невозможности такого безопасного запуска AC9 остаётся незакрытым и публикация не выдаётся за готовую.

Rollback кода — отдельный revert; опубликованный tag не переписывать. Ошибочный release можно снять с latest/убрать публикацию, сохраняя артефакты/evidence; установленной версии нужен новый исправленный patch, forced downgrade не включать. Backup/copy restore только в затронутые пути, без удаления новых пользовательских файлов. Установка из опубликованного релиза дополнительно разрешена 2026-10-20; автоматический rollback с перезаписью пользовательских данных не запускается.

## 11. Тестирование и критерии приёмки
Обязательны обычные `dotnet build DocTemplater.sln` и полный `dotnet test DocTemplater.sln`, а также Release build/tests для точного release head, publish/pack/manifest inspection, настоящий изолированный updater smoke и final GitHub read-back. Основание: startup/storage/инсталляция/новый UI и публичная доставка. Не повторять успешные проверки без изменения/нового риска. До долгих шагов сообщать команду и progress path; историю длительности текущего suite ~8–10 секунд не переносить на publish/native update.

### Acceptance-to-Test Matrix
| AC | Проверяемый результат | Evidence required |
| --- | --- | --- |
| AC1 | Run ровно один раз до Avalonia, hooks завершаются без обычного bootstrap | Startup contract test + vpk pack verification без skip |
| AC2 | Авто/ручная check/download используют один manager/service и общие status/progress | Service concurrency tests + UI mouse action |
| AC3 | Latest/no newer, готовый pending пакет, dev Disabled, progress/cancel/retry корректны | State/adapter tests и render states |
| AC4 | Dirty values/число/дата/очистка/выходные настройки требуют явного подтверждения; cancel сохраняет ввод | VM/UI tests, просмотр диалога PNG |
| AC5 | Busy/двойной apply не приводит к двум helper/закрытию во время записи | Guard tests; native smoke receipt |
| AC6 | Offline/rate limit/checksum/lock errors не мешают документам, плохой пакет не применяется | Негативные tests, error PNG/log |
| AC7 | Settings, saved values, скрытые keys, templates и ready DOCX сохранны после переноса/обновления; corrupt/unreadable исходник не заменяется defaults | Real temp files, raw-byte backup/hash/read-back, конфликт/отказ записи/invalid JSON |
| AC8 | Managed binary path защищён после ручной смены/нового запуска и через filesystem aliases | Boundary/case/relative/junction/ancestor/reparse tests, current+saved output guard; auto-apply off |
| AC9 | Настоящий packaged DocTemplater N→N+1 через обычный startup/UI flow запускает новую версию и сохраняет данные; второй процесс не пишет и не мешает apply | Installer/UI/update logs, PID/version/path/hash receipt; не отдельный probe/fake apply |
| AC10 | Release build/full suite/publish/pack green; версия/packId/channel/exe согласованы | Logs, manifests, package contents, hashes; нет personal DOCX/settings |
| AC11 | Stable/latest v0.1.0 на GitHub опубликован с полным комплектом assets | gh release/tag read-back и ссылки; не один draft |
| AC12 | Public GithubSource видит опубликованный feed и скачивает проверенный full package | Adapter probe с более старой test locator version, без установки в профиль пользователя |
| AC13 | UI не перекрывает форму при 800/900, независимы document/update statuses | Headless UI suite, реально просмотренные PNG |
| AC14 | Опубликованный установщик скачан из GitHub Release, проверен по SHA256SUMS и установлен в рабочий профиль после backup | Backup manifest/hash read-back; установленная version/path и сохранность profile/templates; разрешение пользователя 2026-10-20 |

Проверочные команды: `dotnet test ... --filter FullyQualifiedName~Update...` только после появления конкретных классов; обычные build/test; `dotnet build/test DocTemplater.sln -c Release`; `dotnet tool restore`; `./scripts/Build-Release.ps1 -Version 0.1.0`; изолированный smoke script; `git diff --check`; `gh release view v0.1.0 --repo Kibnet/DocTemplater` + tag/assets API. Точные пути logs/receipts и exit codes фиксируются в EXEC. Новый installer/публикация на SPEC не выполнялись. Незакрытый обязательный native/update/packaging AC нельзя скрыть unit mocks или отметить PASS по объективной причине отсутствия проверки.

## 12. Риски и edge cases
Потеря Templates/output под current, первый импорт неизвестной старой копии, конфликт копирования, dirty state, два окна/операции, недоступный GitHub, ограничение anonymous API, отсутствие следующей публичной версии для проверки первого релиза, unsigned installer, переименование asset/channel и публикация неполного draft. Mitigations перечислены в §6/10/11. У первого релиза невозможно показать переход с публичной старой Velopack-версии: старых релизов нет. Локальный native N→N+1 и live GitHub check/download разделяют проверку механизма замены и реального hosting; не выдавать их за уже состоявшееся обновление пользователей.

### Expected User Review Objections
| Замечание | Причина | Mitigation | Status |
| --- | --- | --- | --- |
| «Обновление закрыло несохранённый документ» | Background download ≠ разрешение закрыть | Auto-apply off, busy/dirty confirmation, cancel tests | mitigated |
| «Пропали шаблоны/готовые DOCX» | Раньше лежали у EXE | AppData default, managed-path guard, backup/copy/hash | mitigated |
| «На GitHub только исходники/не работает следующая версия» | Для update нужны package/feed | Setup/full/feed/checksums, live adapter probe | mitigated |
| «Старое приложение всё ещё не обновляется» | Baseline без Velopack | Первый Setup вручную, notes не обещают невозможного | mitigated |

Rework prevention: user scenarios/decision ledger/AC mapping/objections заполнены; применимые роли проверяются ниже; output — опубликованный проверенный release, не план/локальный exe. Существенных неразрешённых продуктовых вариантов нет; предложенные defaults принимаются exact approval.

## 13. План выполнения
После approval: branch и baseline → startup/data paths/guards → singleton update service/backend → общий UI/dirty confirmation → regressions/UI/real local update fixture → обычный и Release full gates → curated notes, publish/pack/hash/contents → post-EXEC review → уже разрешённые source/tag push и draft/assets verification → publish → live feed/package read-back → финальный URL/evidence. Если tag/release уже существует, сначала сверить фактический outcome и не создавать дубликат/force overwrite.

## 14. Открытые вопросы
Продуктовых blockers нет. Defaults: Windows x64/v0.1.0, unsigned installer, auto check/download, explicit apply/restart. Нужна точная фраза approval новой SPEC. Поддержка signing/других OS не нужна для первого релиза. Среда реального изолированного native smoke проверяется в EXEC; недоступность не будет выдаваться за PASS.

## 15. Соответствие профилю
Desktop/UI: async lifecycle, stable selectors, wireframe, runtime data safety, весь suite и визуальная проверка. Delivery: Conventional Commits/branch naming, stable SemVer tag, согласованные notes/manifest, один publisher, draft → assets verify → publish. Existing consumer tests и 98 baseline cases остаются обязательными. Public release разрешён; личная установка нет.

## 16. Таблица изменений файлов
| Файл/группа | Изменения | Причина |
| --- | --- | --- |
| Program.cs, App.axaml.cs, DocTemplater.csproj | Velopack/package version, bootstrap composition | Hooks, один updater, стабильные data paths |
| Domain/AppSettings.cs, Services/ApplicationDataPaths/Bootstrap*.cs | Постоянный default и безопасная миграция metadata | Сохранность данных |
| IValuePersistenceService/ValuePersistenceService и затронутые callers/fakes | Минимальные атомарные path updates | Не вернуть потерю конкурирующих записей |
| Services/*Update*.cs, Domain/*Update*.cs | Adapter/service/contracts/state | Один владелец lifecycle |
| MainWindowViewModel, DataInputViewModel, MainWindow/confirmation View | Общий статус, dirty/busy/restart UX | Видимый управляемый flow |
| Unit/UI tests + isolated native probe/script | Реальные и негативные сценарии, rendered states | AC1–13 |
| global.json, .config/dotnet-tools.json, scripts/Build-Release.ps1, release workflow, .gitignore | Pinned reproducible packaging, безопасные outputs | Windows release assets |
| CHANGELOG.md, release notes, README/UI_TESTS | Версия, установка, updater и границы | Согласованная публичная инструкция |
| Эта SPEC | Audit/validation/журнал | QUEST trace |

## 17. Таблица соответствий (было → стало)
| Область | Было | Стало |
| --- | --- | --- |
| Получение версии | Ручная пересборка | Setup и GitHub feed |
| Обновление | Нет | Auto check/download + explicit restart |
| Файлы по умолчанию | Templates у EXE | Постоянные Templates вне install root |
| Выпуск | Нет release pipeline/tag | Воспроизводимый pack + stable release/хэши |

## 18. Альтернативы и компромиссы
Прямая ссылка на latest download без feed не даёт надёжной update identity. Forced silent restart опасен для формы. Хранить пользовательские документы внутри current с исключениями pack недостаточно: заменяется дерево установленной версии. Собственный update server и несколько менеджеров не нужны публичному GitHub-проекту. Выбраны официальный GithubSource, один service и постоянные файлы. Первый release только Windows x64 ограничивает объём native acceptance.

## 19. Quality gate / review
### SPEC Linter Result
| № | Критерий | Статус | Evidence |
| --- | --- | --- | --- |
| 1 | Outcome | PASS | §1, опубликованный release и S1–S5 |
| 2 | AS-IS | PASS | Source/GitHub/tool inventory §2 |
| 3 | Проблема | PASS | §3, updater + data replacement |
| 4 | Дизайн | PASS | §4/6 |
| 5 | Границы | PASS | §5 |
| 6 | Ответственности | PASS | §6.1 |
| 7 | Интеграции | PASS | §8 |
| 8 | Инварианты | PASS | §7 |
| 9 | Ошибки/recovery | PASS | State matrix и AC6 |
| 10 | Performance | PASS | Async network, один startup check, без polling; benchmark не требуется |
| 11 | Данные | PASS | §6.6/9 |
| 12 | Совместимость | PASS | Legacy paths/settings и первый Setup §6/10 |
| 13 | Rollback | PASS | §10, без force tag/downgrade |
| 14 | Измеримые AC | PASS | AC1–13 |
| 15 | AC→evidence | PASS | Tests/native/hash/live release разделены |
| 16 | Команды/stop | PASS | §1/11 |
| 17 | План | PASS | §13, проверки до публикации |
| 18 | Решения/вопросы | PASS | §6.5/14 |
| 19 | Форма/масштаб | PASS | High-risk expanded §0 |
| 20 | Профили | PASS | §15 |
Итог: ГОТОВО к запросу exact approval после post-SPEC review ниже. Код, installer и release ещё не создавались.

### SPEC Rubric Result
| Критерий | 0/2/5 | Обоснование |
| --- | ---: | --- |
| Цель/границы | 5 | Явные release/output/non-goals |
| AS-IS | 5 | Код, live GitHub, CLI и official docs |
| Дизайн | 5 | State/source/paths/UI/pack определены |
| Безопасность/rollback | 5 | Data paths, backup, managed guard, dirty, immutable tags |
| Тестируемость | 5 | AC → unit/UI/native/public evidence |
| Автономность | 5 | Выбраны defaults, publication разрешена, exact gate сохранён |
30/30 — оценка спецификации, не подтверждение реализации/выпуска.

### Role-Based Review Result
| Role | Applicability | Review question | Verdict |
| --- | --- | --- | --- |
| Business analyst / domain | applicable | Сохраняются ли исходный документный flow и пользовательские данные? | PASS на уровне SPEC |
| UX / designer | applicable | Нет ли скрытого restart и общего статуса вместо статуса документа? | PASS на уровне SPEC |
| Tester / validation | applicable | Отличены ли mocks, native update, production package и public feed? | PASS на уровне SPEC |
| Developer / architect | applicable | Один ли manager, безопасны ли paths/startup/concurrency? | PASS на уровне SPEC |
| Delivery / operations / security | applicable | Верны ли tag/assets/permissions/signing/token boundaries? | PASS на уровне SPEC |

### Post-SPEC Review
Статус: PASS текущей фазы, можно запросить exact approval. Это оценка спецификации, не подтверждение updater/install/publish.

- Scope reviewed / Scope-Evidence pass: эта SPEC, canonical expanded template и выбранный stack; Program/App/MainWindow/MainWindowViewModel, DataInputViewModel/TemplateSelectionViewModel, AppSettings/SavedTemplateData/ValuePersistenceService/interface, csproj и repo inventory. Проверены чистая master/d99c3b2, GitHub public/default branch/no releases, gh permissions/rules API, installed vpk 1.2.161 и CLI help, SDK inventory с 10.0.402. Прочитаны official integration/source/preserved-files/testing/UpdateManager/Setup/pack docs; память использована только как навигация к принципу одного lifecycle owner, текущие контракты сверены заново.
- Contract pass: исходный output — auto-updater и public release — связан с S1–S5/AC1–13, границы публикации/личной установки названы. Выбранные product defaults, native validation и migration не скрыты за абстрактной «поддержкой Velopack». Решений пользователя кроме exact approval не требуется.
- Adversarial fallback: отдельный reviewer velopack_spec_review читал SPEC и исходники, без изменений/тестов/installer/publication. Фактический sandbox writable (danger-full-access), read-only override в доступном spawn API отсутствует; поэтому это отдельная adversarial-проверка без технической read-only изоляции, не independent read-only review. Main agent дополнительно проверил правила GitHub, startup catch/defaults, default output у Templates, неизвестную старую копию, повторный startup с pending package, mixed process writers, reparse containment и связь native fixture/production package.
- Role-Based pass: business/domain — шаблоны, готовые документы и saved values остаются вне замены; UX — нет скрытого restart, dirty cancel, независимые статусы/800–900 layout; tester — unit/Skia UI/native packaged application/live GitHub evidence разнесены; developer — один manager/service, guards до mutable bootstrap/apply, строгая загрузка/canonical paths; operations/security — stable tag/draft/assets/read-back, pinned SDK/tool, scopes токена, unsigned installer и ограничения фикстуры названы. Все применимые роли PASS по контракту.

| Severity | Area | Finding | Required action | Status |
| --- | --- | --- | --- | --- |
| HIGH | data | Current LoadSettings скрывает corrupt/unreadable JSON и может дать migration пустые defaults | Strict missing vs error, запрет rewrite/apply и raw-byte negative test | fixed в SPEC §6.2/AC7 |
| HIGH | processes | Второй/legacy процесс не соблюдает instance-local storage lock и может писать во время bootstrap/apply | Lifetime data-root lock и legacy guard до mutable bootstrap и перед apply; second-process native test | fixed в SPEC §6.2/AC9 |
| HIGH | paths | Внешний junction может физически указывать в current; lexical check недостаточен | Canonical filesystem identity/ancestor reparse + current/saved output paths, fail closed и negative tests | fixed в SPEC §6.2/AC8 |
| HIGH | evidence | Native probe отдельно от настоящего DocTemplater не доказывает его installed startup/restart | Actual packaged DocTemplater N→N+1 через обычную composition/UI, isolated identity/data/feed; probe только дополнительно | fixed в SPEC §10/AC9 |

- Fix and re-review: main agent уточнил corrupt-file guard, output-root copy policy, reparse и native fixture после первого прохода; отдельный re-review оставил gap legacy writer до migration. Guard перенесён также до mutable bootstrap. Финальный targeted re-review reviewer: все четыре HIGH закрыты, новых находок в изменённых требованиях нет; SDK/runtime pins согласованы. Код не исправлялся и тесты не выполнялись в SPEC.
- Evidence inspected: выводы Git/gh/dotnet-tools/SDK/CLI inventory, просмотренные source files и official docs, отдельные findings/re-review сообщения. UI wireframe находится в самой SPEC и доступен reviewer; новый реальный render появится в EXEC, не выдаётся за уже просмотренный.
- Depth checklist: scope не расширяется на signing purchase/OS ports/personal installation; AC верифицируют наблюдаемый результат и конкретные негативные случаи; decision ledger и expected objections заполнены; release публикация разрешена, startup/dirty guards не заменяются permission пользователя на публикацию; unsupported claims о migration старой неизвестной копии/production users update исключены; comments/docs/version/release notes обязаны согласоваться; hidden contract change single-instance/default Templates явен; manual-review challenge «почему мои документы не потеряются при update и как доказан restart самой программы?» закрыт AC7–9 и обязательным read-back.
- No-findings justification: не применимо — actionable findings были, перечислены и закрыты на уровне дизайна. Реализация каждого исправления и весь mandatory evidence ещё впереди.
- Residual risks: native fixture/CI среда ещё не проверена исполнением, signing отсутствует по выбранному плану, arbitrary external/renamed legacy writers не координируются, будущий public→public upgrade первого релиза ещё не мог состояться. Эти границы не дают считать выполненные AC заранее. При failure обязательного gate publication остаётся незавершённой, а не объявляется успешной.
- Stop decision: PASS/SPEC; запросить `Спеку подтверждаю`, затем выполнять уже разрешённую задачу до опубликованного проверенного release. Не создавать новый approval gate для уже разрешённой публикации.

### Post-EXEC Review
Выполнен отдельный adversarial read-only fallback в writable runtime; технической изоляции нет. Изначально NEEDS-FIX: повторный импорт stale adjacent Templates, незарегистрированные managed DOCX, живой rejected process, remote draft read-back, относительный внешний каталог, recovery chooser. Последующие recovery findings: сохранять все файлы прежних managed user trees и их исходные имена; durable registration до backup I/O и до смены settings. Все закрыты: once marker, pre-generation full-path guard, hash/size/path manifest, атомарная регистрация старых корней, строгая загрузка и blocking errors, bounded rejection, remote assets/tag/hash verification. Финальный targeted review PASS, новых HIGH/MEDIUM в изменённой поверхности нет. Reviewer tests/build не запускал.

Основной агент проверил unit/UI/native/packaging отдельно. Debug и Release: 96 unit/integration + 20 UI. Headless harness сериализован для общей Application; перед mouse hit-test принудительно завершает render/layout. Найденное зависание modal click без кадра исправлено в тестовом harness, UI-сценарий не ослаблен. PNG ready/dirty/download/error просмотрены. Native desktop заблокирован: screen capture показывает lock screen, поэтому visual evidence — честный Headless Skia fallback, native evidence — UIA receipts и Velopack logs, не видео.

Native packaged app 0.0.1→0.0.2: production Program/App/MainWindow/service с test-only identity/data/feed; normal UI check/download/cancel/confirm; второй процесс отклонён, pending package не применяется автоматически после обычного restart, новый PID/version, settings/template/generated-document hashes сохранены. Production nupkg: 234 entries, main EXE присутствует, personal DOCX/settings/fixture binaries отсутствуют; production DLL содержит фиксированный GithubSource и не содержит fixture override strings. Подпись не настроена, pack startup verification PASS. CI/publication/live GitHubSource/личная установка подтверждаются отдельным итоговым receipt после выпуска.

## Approval
2026-10-20: «Спеку подтверждаю. А затем установи из релиза». Реализация, необходимые Git push/tag, публикация GitHub Release и последующая установка именно скачанного release installer разрешены. Перед установкой создана и проверена отдельная резервная копия рабочего profile/settings и выбранных шаблонов; приватные пути и данные в Git/release не включать.

## 20. Журнал действий агента
| Фаза/событие | Решение/основание | Evidence / остаток | Следующий шаг | Решение человека | Артефакты |
| --- | --- | --- | --- | --- | --- |
| SPEC: исследование | Выбран Velopack 1.2.161, GitHub stable source, persistent Templates, explicit apply | Source/current GitHub/official docs/tool preflight; кода и релиза ещё нет | Post-SPEC review | Запрошены автообновление и публикация релиза | Только эта SPEC |
| SPEC: review и повторная проверка | Четыре HIGH закрыты в дизайне: corrupt settings, legacy writers, reparse, реальный packaged native update | Отдельный writable-runtime adversarial fallback и повторный review PASS; git status содержит только новую SPEC | Показать готовую SPEC и получить exact approval | Публикация уже разрешена; exact approval новой SPEC ожидается | Только эта SPEC |
| EXEC: approval | Реализация + release + установка из релиза | Подтверждение пользователя 2026-10-20 | Исполнить AC1–14 | Разрешено | Код, tests, manifests, scripts/workflow |
| EXEC: review/validation | Все findings закрыты, targeted re-review PASS | Unit 96, UI 20; native N→N+1 PASS; pack/content/override checks PASS; unsigned по плану | Exact-head CI, публикация, live source, установка | Повторное approval не требуется | artifacts/tests, artifacts/ui-final, artifacts/native-release-proof (ignored, приватные receipts не публикуются) |
