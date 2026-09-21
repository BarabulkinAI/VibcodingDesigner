# VibcodingDesigner — графический дизайнер отчётов

Avalonia UI + FastReport.OpenSource.

## Концепция

FastReport.OpenSource — это движок отчётов **без встроенного дизайнера**. Весь WYSIWYG-редактор
(канвас, выделение, drag & drop, панель свойств) реализуется на Avalonia поверх API FastReport.

**Принцип:** `FastReport.Report` — единственный источник правды. Канвас и дерево объектов
получают лёгкий снимок `DesignSnapshot` и перестраивают его после каждого изменения
через `IFastReportService`. Снимок дёшев: документ дизайнера обычно содержит десятки объектов.

**Единицы:** канвас работает в логических пикселях при 96 DPI, FastReport — в юнитах
(1 юнит = 1/96 дюйма). Конвертация — `UnitConverter` / `DesignSnapshotBuilder.ToPx|ToUnits`.

## Жизненный цикл документа

Редактор не только открывает готовые `.frx`, но и **создаёт отчёты с нуля**. Документ имеет состояния:

```
New (без пути) ──мутации──► Modified (IsDirty)
Opened(path) ──мутации──►  Modified (IsDirty)
Modified ──Save(path)──►   Opened(path), IsDirty=false
```

- **New** — `CreateNew()` строит отчёт полностью программно (A4 + стандартный набор полос).
  Файла `.frx` ещё не существует, `CurrentFilePath = null`;
- **первое сохранение нового документа = Save As**: пользователь выбирает путь, далее он
  закрепляется за документом;
- **Modified (`IsDirty`)** — любой мутирующий вызов сервиса (`AddObject`, `MoveObject`,
  `AddBand`, …) выставляет флаг; `Load` и `Save`/`MarkSaved` сбрасывают;
- при командах «Новый / Открыть / Закрыть» поверх `IsDirty` UI обязан спрашивать
  подтверждение («Сохранить изменения?»).

Владелец состояния — UI-слой (`MainViewModel`); сервис лишь отражает его через
`CurrentFilePath` / `IsDirty` и выставляет `IsDirty` при мутациях. Системные диалоги
файлов — отдельный `IFilesService` (этап 1).

## Структура

```
src/ReportDesigner/
├── Models/                     # DTO для снимка (без зависимостей от FastReport)
│   ├── DesignSnapshot.cs       # страницы → полосы → объекты
│   ├── DesignObjectInfo.cs     # графический объект (bounds, текст, стили)
│   ├── BandSnapshot.cs / PageSnapshot.cs / Enums.cs
├── Services/
│   ├── FastReportService.cs    # единственная точка доступа к Report (мутации)
│   ├── IFastReportService.cs
│   ├── DesignSnapshotBuilder.cs # Report → DesignSnapshot
│   ├── UnitConverter.cs        # см ⇄ px (96 DPI)
│   ├── ResizeGeometry.cs       # ручки ресайза, hit-test, кламп в пределах полосы
│   ├── SnapshotHitTester.cs    # hit-test объекта по точке канваса
│   ├── IExportService.cs / ExportService.cs  # PNG / HTML
│   ├── IFilesService.cs / FilesService.cs     # диалоги Open/Save (IStorageProvider)
│   ├── IDialogService.cs / DialogService.cs   # подтверждение потери изменений
│   ├── IHostWindowProvider.cs / HostWindowProvider.cs # доступ к TopLevel из DI-сервисов
│   ├── IPreviewService.cs / PreviewService.cs # рендер страницы в Bitmap
│   └── IUndoRedoService.cs     # (этап 4) undo/redo на снимках
├── UI/
│   ├── Views/Controls/DesignSurface.cs        # канвас: рендер, зум (Ctrl+колесо), drag/resize/клавиши
│   ├── Views/Controls/DesignCanvasView.axaml(.cs) # линейки+ScrollViewer+DesignSurface как один UserControl (Dock-документ)
│   ├── Views/Controls/PreviewPanelView.axaml(.cs) # превью документа как отдельная Dock-панель
│   ├── Views/Dialogs/ConfirmDiscardChangesDialog.axaml(.cs)
│   ├── ViewModels/              # MainViewModel, DesignSurfaceViewModel, ObjectTreeViewModel,
│   │                             # PropertiesPanelViewModel
│   ├── Docking/                 # Dock.Avalonia: MainDockFactory + тонкие обёртки Tool/Document
│   │                             # (ObjectTreeTool, PropertiesTool, DataSourcesTool, PreviewTool,
│   │                             # DesignCanvasDocument) вокруг существующих ViewModel — см. «Дорожная карта»
│   └── App.axaml / MainWindow.axaml
tests/ReportDesigner.Tests/     # xUnit: конвертер, сервис отчёта и его свойства, геометрия/hit-test,
                                  # DesignSurfaceViewModel, ObjectTreeViewModel, PropertiesPanelViewModel,
                                  # потом undo/redo
```

## Дорожная карта

- **Этап 0 — скелет (готов):** модели снимка, UnitConverter, DesignSnapshotBuilder,
  FastReportService (CreateNew/Load/Save, полосы, объекты), ExportService, тесты.
- **Этап 1 — канвас-редактор (готов):** DesignSurface (лист, сетка, зум по Ctrl+колесо,
  выделение, 8 ручек ресайза, перемещение), клавиши (Del, Esc, стрелки) + жизненный цикл
  документа: New/Open/Save As (`IFilesService`), dirty-флаг, подтверждение
  потери изменений, заголовок окна с именем файла. Вертикально объект зажат в пределах
  своей полосы (band) — в сервисе нет репарентинга между полосами, это осознанное
  ограничение, как в большинстве band-based дизайнеров отчётов.
- **Этап 2 — палитра/дерево/свойства (готов):** Toolbox (Text/Line/Shape/Picture) — создание
  объекта кликом по канвасу; дерево объектов (полосы → объекты) с командами z-order (на
  передний/задний план, вперёд/назад); панель свойств (координаты/размер/видимость,
  текст/шрифт/выравнивание, рамка, заливка и вид фигуры, стиль линии, выбор/очистка
  изображения для Picture через `IFilesService.PickImagePathAsync`). Выделение
  синхронизировано между канвасом, деревом и панелью свойств во всех направлениях.
- **Этап 3 — данные и полосы (готов):**
  - *Управление полосами* — в дереве объектов: добавление полосы любого вида (кроме уже
    присутствующих одиночных — ReportTitle/PageHeader/ColumnHeader/ColumnFooter/PageFooter/
    ReportSummary/Overlay), удаление, переименование и правка высоты выделенной полосы
    (`IFastReportService.RenameBand`/`SetBandHeight`).
  - *Источники данных* — панель внизу окна: именованные примерные таблицы (CSV-текст:
    первая строка — столбцы, дальше — строки), привязываются к полосе `Data` через
    комбобокс в свойствах полосы (`IFastReportService.SetDataSource`/`AssignBandDataSource`).
    Данные — только для дизайна, в `.frx` не сохраняются (см. «Известные риски»).
  - *Выражения `[Field]`* — синтаксис `[Источник.Колонка]` пишется прямо в `TextObject.Text`
    (панель свойств уже писала туда текст с этапа 2, ничего дополнительно готовить не
    нужно); кнопка «Вставить поле» в панели свойств лишь дописывает выражение в конец текста
    по выбранным источнику/колонке. Вычисляется на `Report.Prepare()` — уже вызывался в
    `PreviewService`/`ExportService`.
- **Этап 4 — продукт (готов):**
  - *Экспорт* — PNG/HTML через меню «Файл → Экспорт» (`IExportService`, ранее был готов,
    но нигде не подключён).
  - *Статус-бар* — выделенный объект, позиция курсора в см, масштаб, dirty-индикатор.
  - *Недавние файлы* (`IRecentFilesService`, JSON в `%AppData%/ReportDesigner/recent.json`) +
    *«Создать из шаблона...»* (`IFastReportService.LoadAsTemplate` — грузит содержимое, но
    оставляет документ «Новым»).
  - *Копирование/вставка* (Ctrl+C/Ctrl+V) — без клонирования FastReport-объектов, через
    `AddObject` + существующие сеттеры свойств; картинки не копируются (см. известные риски).
  - *Undo/redo* (Ctrl+Z, Ctrl+Y/Ctrl+Shift+Z, меню «Правка») — снимок `Report` целиком
    (`Save`/`Load` в `MemoryStream`) + отдельно копия источников данных, единая точка
    интеграции — `DesignSurfaceViewModel.CommitChange()` → `IFastReportService.Checkpoint()`.
  - *Линейки* — `RulerView` (кастомная отрисовка, как `DesignSurface`) сверху/слева от
    канваса; тики считает чистая тестируемая `RulerTickCalculator` (шаг адаптируется под
    zoom по прогрессии 0.5-1-2-5-10-20-50-100 см); прокрутка синхронизируется вручную через
    `ScrollViewer.ScrollChanged` — биндируемого `Offset`-свойства у `ScrollViewer` нет.
  - Заодно закрыт пробел: `MainViewModelTests.cs` (New/Open/Save/SaveAs, диалог
    подтверждения, недавние файлы) не существовал вообще до этого этапа.
  - При ручном тестировании найдено и исправлено два реальных бага (см. известные риски,
    п.4, и историю коммитов) — оба в коде источников данных с этапа 3, всплывшие только
    сейчас при более активном использовании UI.
  - *Персистентность источников данных* (после этапа 4, отдельная сессия) — sidecar-файл
    `<path>.datasources.json` рядом с `.frx`, см. известные риски п.4 (переписан).
  - *Курсор при ресайзе объекта* (из бэклога, отдельная сессия) — `DesignSurface` показывает
    направленный курсор (`TopSide`/`LeftSide`/.../`TopLeftCorner`/.../`BottomRightCorner`) как
    при наведении на ручку выделенного объекта, так и всё время активного ресайза (не только
    при наведении — указатель мог уйти с ручки под захватом). Источник истины — новое свойство
    `DesignSurfaceViewModel.ActiveResizeHandle` (производное от уже существующего внутреннего
    состояния жеста), сам `DesignSurface` при каждом движении мыши либо берёт его, либо (вне
    жеста) делает тот же `ResizeGeometry.HitTest`, что и `OnPointerPressed`.
  - *Кнопка скрытия панели превью* (из бэклога, отдельная сессия) — меню «Вид» →
    флажок «Панель превью», бинд на `MainViewModel.IsPreviewVisible` (bool, по
    умолчанию `true`). Столбцы сплиттера и самой панели (последние два в
    `ColumnDefinitions="...,4,3*"` главного `Grid`) при скрытии не просто прячутся
    через `IsVisible` (это оставило бы пустой промежуток на месте звёздочного
    столбца), а схлопываются до `GridLength(0)` — но **не биндингом на
    `ColumnDefinition.Width`** (не проверено эмпирически, что `ColumnDefinition`
    в Avalonia надёжно наследует `DataContext` для обычных биндингов), а явно из
    `MainWindow.axaml.cs` (`UpdatePreviewColumnWidths()`, реагирует на
    `PropertyChanged` того же способа, что уже применён для `RecentFiles` —
    `x:Name` на `ColumnDefinition` не генерирует поле в code-behind, пришлось
    именовать сам `Grid` и индексировать `ColumnDefinitions[5]`/`[6]`).
  - **Баг, найденный пользователем сразу после пункта выше**: чекбокс переключался
    визуально, но панель не пряталась. Причина — `MenuItem.IsChecked="{Binding ...}"`
    без явного `Mode` биндится **OneWay** (в отличие от `CheckBox.IsChecked`, у
    `MenuItem` это не двунаправленное свойство по умолчанию) — клик менял галочку
    локально в контроле, но VM-свойство не менялось. Фикс — явный `Mode=TwoWay`.
    **Урок**: в этом проекте на `IsChecked`/подобных свойствах элементов, которые не
    являются `CheckBox`/`ToggleButton` напрямую, всегда указывать `Mode=TwoWay` явно.
  - *Настраиваемые размеры страницы* (из бэклога, отдельная сессия) — по явной
    просьбе пользователя ограничено двумя пресетами, **A4 и A3** (без произвольного
    ввода мм), плюс переключатель ориентации (книжная/альбомная). Меню «Файл →
    Параметры страницы...» → `PageSetupDialog` (обычный code-behind `Window`, как
    `ConfirmDiscardChangesDialog`) → `IDialogService.ChoosePageSizeAsync` →
    `MainViewModel.PageSetupCommand` → `IFastReportService.SetPageSize(PageSizePreset,
    bool landscape)` + `DesignSurface.CommitChange()` (undo-точка + обновление
    канваса/превью, как и у всех остальных мутаций). `GetPageSize()` — обратное
    сопоставление текущих `PaperWidth/PaperHeight/Landscape` пресету, для
    предзаполнения диалога; если размер не совпадает ни с одним пресетом (сторонний
    `.frx`) — по умолчанию A4 с реальной текущей ориентацией. Работает и для «Нового»,
    и для уже открытого документа (не привязано к моменту `CreateNew()`).
    Побочный фикс: `DesignSurface.OnViewModelPropertyChanged` теперь перемеряет канвас
    (`InvalidateMeasure()`) и при смене `Snapshot`, не только `Zoom` — раньше размер
    страницы никогда не менялся после создания документа, поэтому этот пробел не
    проявлялся. Осознанное ограничение: объекты, уже размещённые на странице, не
    переносятся/не клэмпятся при уменьшении размера — как и `ClampVertical` при
    ресайзе полос, это не сделано для координаты по ширине.
  - *Убрана кнопка «Обновить превью»* из тулбара — превью и так обновляется
    автоматически на каждую зафиксированную мутацию документа
    (`DesignSurfaceViewModel.DocumentChanged` → `MainViewModel.RefreshPreview()`,
    см. `OnDesignSurfaceDocumentChanged`), кнопка была ручным дублем, оставшимся
    с более ранних этапов.
  - *Живой предпросмотр шрифта в поле редактирования текста* — уточнение от
    пользователя переформулировало исходный пункт бэклога («индикация размера
    текста»): речь не про канвас (там текст и так рисуется актуальным шрифтом,
    `DesignSurface.DrawTextObject` уже читает `obj.FontSize` из снимка), а про
    сам `TextBox` со свойством `Text` на панели свойств («Текст») — он раньше
    всегда рисовался стандартным UI-шрифтом независимо от `FontName`/`FontSize`/
    `FontBold`/`FontItalic` объекта, поэтому при подборе размера/шрифта
    приходилось смотреть в отдельную панель превью. Теперь `FontFamily`/
    `FontSize`/`FontWeight`/`FontStyle` этого `TextBox` забинжены на те же
    свойства `PropertiesPanelViewModel` (для двух последних — новые
    `BoolToFontWeightConverter`/`BoolToFontStyleConverter` в
    `UI/Converters/FontStyleConverters.cs`, т.к. `FontBold`/`FontItalic` в VM —
    `bool`, сознательно без зависимости от Avalonia в самой VM).
  - **Баг, найденный пользователем ручным тестом сразу после пункта выше**:
    объект с `Left=0`/`Top=0` на канвасе рисовался в самом углу листа, а в
    превью/экспорте оказывался смещён. Причина — `ReportPage` по умолчанию
    задаёт **все 4 поля страницы (`Left/Top/Right/BottomMargin`) по 10мм**
    (подтверждено эмпирически: XML-документация FastReport значение по
    умолчанию не указывает; также эмпирически, через побайтовое сравнение
    экспортированного PNG, подтверждено, что `Report.Prepare()`/экспорт рисуют
    содержимое полос со сдвигом именно на эти поля от края бумаги), а канвас
    (`DesignSnapshotBuilder`/`DesignSurface`) вообще не знал о полях и рисовал
    полосы от (0,0) бумаги. Итог — координаты объекта, идентичные по числу
    (`Left=0,Top=0`), интерпретировались canvas'ом и реальным движком
    по-разному (расхождение = 10мм ≈ 38px при 96dpi).
    Фикс:
    - `PageSnapshot` получил `MarginLeft` (px) — левое поле страницы.
      Верхнее поле отдельным свойством не хранится: `DesignSnapshotBuilder.
      BuildPage` теперь стартует накопитель `topPx` (используемый для
      `BandSnapshot.Top`) не с `0f`, а с `TopMargin`-в-px — оно "запекается"
      в `Top` первой же полосы и дальше течёт по коду без изменений
      (`band.Top` и так уже everywhere читается как готовая page-space
      координата).
    - `DesignSurface.ToPageBounds` (рендер объектов, ручки ресайза, рамка
      выделения) и `SnapshotHitTester.FindObjectAt` (обратное преобразование
      клика в band-относительные координаты) теперь прибавляют/вычитают
      `page.MarginLeft`.
    - `DesignSurfaceViewModel.PlaceObjectAt` (создание объекта кликом
      инструмента) тоже вычитает `MarginLeft` — иначе после фикса рендера
      клик кладёл бы новый объект на 38px правее места клика (эта часть не
      была в исходной жалобе пользователя, но стала бы новым, более заметным
      багом, если её не поправить симметрично).
    - Перетаскивание/ресайз (`UpdateDrag`/`UpdateResize`) фикса не
      потребовали — они работают дельтами между двумя точками в ОДНОЙ и той
      же системе координат, константный сдвиг в дельте сокращается сам.
    - Подтверждено эмпирически (побайтовый анализ PNG, `ExportService`), что
      после фикса позиция на канвасе (в px) и позиция в экспортированном PNG
      совпадают в пределах 1px (разница — субпиксельное округление
      растеризатора, не позиционная ошибка).

- **Dock (пункт бэклога) реализован 2026-09-15** — раскладка рабочих зон
  (дерево объектов, канвас, свойства, превью, источники данных) переведена со
  статичного `Grid`+`GridSplitter` на `Dock.Avalonia`/`Dock.Model.Mvvm`
  (`12.1.0.6`, под `Avalonia 12.1.1`/`net10.0` — точное совпадение версий
  подтверждено по NuGet перед установкой). Панели теперь перетаскиваются,
  перестыковываются и открепляются в отдельные окна.
  - Существующие ViewModel-и (`ObjectTreeViewModel`, `PropertiesPanelViewModel`,
    `DataSourcesViewModel`, `DesignSurfaceViewModel`) не переписывались —
    `UI/Docking/*` содержит только тонкие обёртки (`ObjectTreeTool`,
    `PropertiesTool`, `DataSourcesTool`, `PreviewTool : Tool`,
    `DesignCanvasDocument : Document`), каждая хранит ссылку на «свою» VM в
    свойстве `ViewModel`; View подбирается через `Application.DataTemplates`
    в `App.axaml` (`DataType` = класс обёртки → соответствующий `UserControl`
    с `DataContext="{Binding ViewModel}"`).
  - Перед добавлением Dock-сложности две самые нестандартные части
    (кастомные lookless `Control` без DataContext-биндинга — `RulerView`,
    `DesignSurface`) стабилизированы в собственных `UserControl`:
    `DesignCanvasView` (линейки + `ScrollViewer` + `DesignSurface`, раньше
    инлайново в `MainWindow.axaml`) и `PreviewPanelView` (было инлайново,
    без отдельной VM — `DataContext` остался `MainViewModel`, как и раньше).
    Синхронизация скролла канваса с линейками (`ScrollViewer.ScrollChanged`,
    т.к. `ScrollViewer.Offset` не биндится) переехала из
    `MainWindow.axaml.cs` в `DesignCanvasView.axaml.cs` без изменений —
    чисто механический перенос кода-бихайнда вместе с владением именованными
    элементами.
  - `MainDockFactory : Factory` (`UI/Docking/MainDockFactory.cs`) строит
    дерево `RootDock → ProportionalDock(вертикальный: верхний ряд + сплиттер
    + ToolDock источников данных) → ProportionalDock(горизонтальный: дерево
    | канвас-Document | свойства | превью)` — пропорции подобраны по весам
    старых колонок Grid, `InitLayout` регистрирует `HostWindowLocator`
    (`Dock.Avalonia.Controls.HostWindow`) — без него открепление панели в
    плавающее окно не работает.
  - **Видимость панели превью НЕ переведена на встроенный крестик закрытия
    Dock** — решение принято сознательно, а не по ограничению API: `PreviewTool.
    CanClose = false`, чекбокс «Вид → Панель превью»
    (`MainViewModel.IsPreviewVisible`) остаётся единственным источником
    истины и управляет видимостью напрямую через
    `MainDockFactory.SetPreviewVisible(bool)` (add/remove из
    `PreviewToolDock.VisibleDockables`) — так чекбокс и фактическое
    состояние панели не могут разойтись (стандартный крестик закрытия дал бы
    пользователю второй, несинхронизированный способ прятать панель).
  - **Сохранение раскладки между запусками (2026-09-21):** `Dock.Serializer.
    Newtonsoft` 12.1.0.6, файл `%AppData%/ReportDesigner/layout.json`
    (`IDockLayoutStore`/`DockLayoutStore`, путь передаётся в конструктор, как у
    `RecentFilesService`). Сохраняется при закрытии окна
    (`MainWindow.OnClosing` → `MainViewModel.SaveLayout()`), читается в
    конструкторе `MainViewModel` (`layoutStore` — необязательный параметр,
    без него тесты ничего не пишут на диск). Файл — конверт
    `{Version, PreviewVisible, Layout}`, где `Layout` — JSON Dock-дерева.
    Нюансы:
    - обёртки `Tool`/`Document` получили публичный конструктор без параметров
      (нужен десериализатору), а `ViewModel` — `[JsonIgnore]` + `set`:
      после загрузки `MainDockFactory.RestoreLayout` привязывает VM заново;
    - скрытая панель превью на время сериализации возвращается в дерево
      (иначе её негде восстановить), видимость хранится флагом
      `PreviewVisible`;
    - при повреждённом файле, другой `Version` или отсутствии любой из пяти
      панелей `RestoreLayout` возвращает null и используется `CreateLayout()`;
      при изменении набора/структуры панелей нужно поднять `LayoutVersion`.
  - `dotnet test` — 216/216 без изменений (Dock-код — обвязка/UI, логика
    существующих VM не менялась). Приложение проверено ручным smoke-тестом
    (`dotnet run`, без падений при старте); полноценный ручной прогон
    (перетаскивание/открепление/сворачивание панелей) — за пользователем.

- **Несколько страниц в отчёте (2026-09-21):** канвас и дерево объектов показывают одну
  **активную страницу**, над канвасом — вкладки «Стр. N» с кнопками «+»/«×», те же действия
  в меню «Страница».
  - Активная страница — состояние `IFastReportService` (`ActivePageName`/`SetActivePage`/
    `AddPage`/`RemovePage`), потому что на ней работают `AddBand`, `SetPageSize`, `GetPageSize`.
    Хранится **по имени**, а не по индексу: Undo/Redo пересоздаёт `Report` из байтов, и если
    страницы с таким именем больше нет (отменили её добавление) — активной считается первая.
    `GetFirstPage()` заменён на `GetActivePage()`.
  - Имена компонентов уникальны на весь отчёт (`EnsureUniqueComponentName`), поэтому поиск
    полосы/объекта по имени идёт по всем страницам (`FindBandAndPage`), а `RegisterDataSourcesIntoReport`/
    `CaptureLiveBandAssignments` обходят полосы всех страниц — иначе привязка DataBand
    второй страницы терялась бы при правке источников и при Save/Load через sidecar.
  - Новая страница — как в новом документе: A4 и 5 стандартных полос (`CreateStandardPage`,
    общий с `CreateNew`). Последнюю страницу удалить нельзя. У каждой страницы свой размер и
    ориентация («Файл → Параметры страницы» действует на активную).
  - `DesignSnapshot.ActivePageName`/`ActivePage` вместо `Pages[0]` в `SnapshotHitTester`,
    `DesignSurface`, `DesignSurfaceViewModel`, `ObjectTreeViewModel`.
  - Вкладки — `DesignSurfaceViewModel.Pages.cs` (partial): `PageTabs`, `SelectPageCommand`,
    `AddPageCommand`, `RemovePageCommand`. Переключение вкладки — **не мутация документа**:
    без `Checkpoint`/`IsDirty`, поднимается отдельное событие `ActivePageChanged` (на него
    подписано дерево), а не `DocumentChanged` — иначе каждый клик по вкладке гонял бы
    `Report.Prepare()` для превью. Добавление/удаление страницы — обычные мутации через
    `CommitChange()` (попадают в Undo).
  - Превью/экспорт не менялись: `ImageExport{SeparateFiles=false}` склеивает все страницы в один
    PNG (подтверждено тестом `ExportPng_ContainsAllPages` — высота = 2 × высоты одной страницы).
  - Не сделано (осознанно): переименование/перестановка/копирование страниц, показ всех страниц
    стопкой на канвасе.
  - Тесты: `MultiPageTests` (+21), всего 244.

## Бэклог (кандидаты на будущие этапы)

Не запланированы по фазам, зафиксированы по просьбе пользователя после ручной
проверки персистентности источников данных:

- **Иерархия в дереве объектов (идея, 2026-09-21):** сейчас дерево показывает полосы и объекты
  только активной страницы. Нужно: корень `Report`, в нём узлы `Page` (все страницы), под ними
  полосы и объекты. Выбор страницы в дереве должен переключать активную страницу (сейчас это
  делают вкладки над канвасом — `DesignSurfaceViewModel.SelectPageCommand`); вероятно, узел
  `Report` станет местом для свойств отчёта в целом.
- **Расхождения модели полос с реальным движком FastReport (проверено 2026-09-21):** порядок печати
  проверен эмпирически — чёрный квадрат в каждой полосе нового документа, позиции в PNG
  (ReportTitle y=0, PageHeader y=76, Data y=152, ReportSummary y=227, PageFooter y=1048 у низа листа).
  1. **`ReportPage.TitleBeforeHeader`** (по умолчанию `True`: заголовок отчёта печатается выше
     заголовка страницы) в коде нигде не читается — канвас всегда ставит ReportTitle над
     PageHeader; при `False` (PageHeader выше) канвас разойдётся с превью.
  2. **`DataHeaderBand`/`DataFooterBand` не поддерживаются.** В FastReport они есть, в `BandKind`
     нет; создать их из дизайнера нельзя, а `DesignSnapshotBuilder.ToBandKind` (ветка `_ =>
     BandKind.Data`) молча считает такую полосу из открытого `.frx` полосой данных — в дереве она
     подписана «Данные», `AddBand`/привязка источника трактуют её как настоящую `DataBand`.
  3. **PageFooter и ReportSummary должны лежать внизу страницы (требование пользователя,
     2026-09-21).** Сейчас канвас стопкой ставит PageFooter сразу под ReportSummary, а в печати
     PageFooter прижат к низу листа — канвас и превью расходятся. Проверено эмпирически (A4,
     нулевые поля, высота страницы 1122 px, высота футера/сводки ≈ 76 px): PageFooter печатается
     на y=1048 (низ листа); ReportSummary по умолчанию идёт сразу за данными (y=227), а при
     `BandBase.PrintOnBottom = true` встаёт на y=972 — вплотную над PageFooter. План:
     - у новых страниц (`CreateStandardPage`) ставить `ReportSummary.PrintOnBottom = true`;
     - `DesignSnapshotBuilder.BuildPage`: PageFooter — на `page.Height − нижнее поле − высота`,
       ReportSummary с `PrintOnBottom` — вплотную над ним (над низом, если футера нет),
       остальные полосы — стопкой сверху как сейчас; при нехватке места — не заезжать на полосы
       выше (зажать по верху свободной области);
     - открытые файлы: положение ReportSummary определяется флагом `PrintOnBottom` из файла (без
       флага сводка идёт за данными, как в движке), сам файл не менять;
     - `PageSnapshot`/`BandSnapshot` уже хранят `Top` — hit-test, drag, размещение объектов и
       выделение полосы работают без изменений; проверить `ClampVertical`/подсветку полосы;
     - ColumnFooter в печати тоже уходит вниз колонки — уточнить эмпирически при реализации;
     - тесты: позиции полос в снимке + пиксельная проверка PNG по образцу
       `ExportPng_ObjectAtOrigin_IsRenderedAtSheetCorner`.
  4. **Порядок Data/Group-полос** на канвасе — порядок добавления в `page.Bands`, без логической
     вложенности GroupHeader → DataHeader → Data → DataFooter → GroupFooter.
- **Данные из базы данных (идея, 2026-09-21; решение не принято):** сейчас источник — образцовые
  данные, введённые CSV-текстом (`DataSourceDefinition` → `DataTable` → `RegisterData`), хранятся
  в sidecar `.datasources.json`, в `.frx` не попадают. Два пути:
  - **A. Снимок из БД в существующий механизм:** дизайнер сам выполняет SQL через ADO.NET
    (`Microsoft.Data.SqlClient`, `Npgsql`, `Microsoft.Data.Sqlite`…) и кладёт результат в
    `DataSourceDefinition`. Превью/sidecar/Undo/привязка к DataBand не меняются, провайдеры
    подключаются по одному. Минусы: в `.frx` нет подключения и запроса (чужой рантайм FastReport
    отчёт данными не наполнит), данные статичны до ручного обновления.
  - **B. Родное подключение FastReport:** `DataConnection` + `TableDataSource.SelectCommand`,
    сохраняются в `.frx`, `Report.Prepare()` сам ходит в БД. Плюсы: переносимый отчёт, параметры,
    живое превью. Минусы: пакеты провайдеров по СУБД (`FastReport.Data.MsSql/Postgres/MySql/SQLite…`
    2026.2.8), строка подключения с паролем открытым текстом в `.frx`, живой запрос на каждое
    превью (тормоза/падение без сети), заметная перестройка модели источников.
  - Гибрид: подключение и запрос в `.frx` (B), для превью — загруженный снимок (A).
  - **Открытые вопросы к пользователю:** кто использует `.frx` (только дизайнер или другое
    приложение с FastReport); какие СУБД; нужны ли параметры запроса; где хранить пароли (в файле /
    спрашивать / переменные окружения); нужны ли связи мастер-деталь и несколько запросов.

Известные ограничения из этапов 3-4, ещё не устранённые (см. «Известные риски» ниже):
поддержка GroupFooter/Child-полос, копирование/вставка `Picture`-объектов, PDF-экспорт.

## Модель полос FastReport (важно!)

Полосы страницы хранятся **не в одном месте** (проверено по исходникам `ReportPage.cs`):

| Полоса | Где хранится |
|---|---|
| ReportTitleBand | `page.ReportTitle` |
| PageHeaderBand | `page.PageHeader` |
| ColumnHeaderBand | `page.ColumnHeader` |
| DataBand, GroupHeader/GroupFooter, Child | `page.Bands` (коллекция) |
| ColumnFooterBand | `page.ColumnFooter` |
| PageFooterBand | `page.PageFooter` |
| ReportSummaryBand | `page.ReportSummary` |
| OverlayBand | `page.Overlay` |

Вертикальный порядок отображения: **ReportTitle → PageHeader → ColumnHeader → Bands(Data/Group) → ReportSummary → ColumnFooter → PageFooter → Overlay**
(метод `ReportPage.GetChildObjects`; сводка идёт *перед* футером страницы).

Отсюда правила:
- добавление полосы — через `FastReportService.AddBand`, который раскладывает её в нужное место (`AttachBandToPage`);
- удаление — через `page.RemoveChild(band)` (сам находит полосу в правильном свойстве);
- перечисление всех полос страницы — только через `EnumerateAllBands` / `DesignSnapshotBuilder.EnumerateBands`;
- при загрузке `.frx` FastReport сам раскладывает полосы по свойствам — снапшот собирает их обратно.

Прочие подтверждённые факты API 2026.x:
- `PaperWidth/PaperHeight` — в **миллиметрах**;
- `ReportPage.Landscape` свопает `PaperWidth`/`PaperHeight` (и поля) сама, но только когда
  значению реально присваивают ДРУГОЕ значение (подтверждено доками FastReport.xml: "When you
  change this property, it will automatically swap paper width and height"). Чтобы выставить
  произвольный пресет+ориентацию детерминированно независимо от текущего состояния —
  `Landscape = false` → абсолютные книжные `PaperWidth/PaperHeight` → `Landscape = target`
  (см. `FastReportService.SetPageSize`);
- **`new ReportPage()` по умолчанию задаёт все 4 поля страницы (`LeftMargin/TopMargin/
  RightMargin/BottomMargin`) равными 10мм** (подтверждено эмпирически — в XML-документации
  значение по умолчанию не указано). `Report.Prepare()`/экспорт рисуют содержимое полос со
  сдвигом именно на `LeftMargin`/`TopMargin` от края бумаги — это НЕ часть самих полос, а
  сдвиг страницы; учтено в `DesignSnapshotBuilder`/`DesignSurface`/`SnapshotHitTester`
  (см. «Сделано», Этап 4); **новые страницы дизайнера создаются с нулевыми полями**
  (`CreateStandardPage`), чтобы объект с `Left=0/Top=0` лежал в углу листа и на канвасе, и в превью
  (проверено тестом по пикселю PNG); открытые файлы со своими полями не меняются;
- `LineObject`: нет `StartPoint/EndPoint/LineWidth/LineColor`; линия настраивается через `Border.Width/Style/Color`, направление — `Diagonal`;
- PDF-экспорта в OpenSource нет (есть отдельный плагин `FastReport.OpenSource.Export.PdfSimple`);
- цвет текста — `TextObject.TextColor`, выравнивание — `HorzAlign`/`VertAlign`, скруглённый прямоугольник — `ShapeKind.RoundRectangle`.
- источник данных регистрируется через `Report.RegisterData(DataTable/DataSet/IEnumerable, string name)`,
  извлекается обратно — `Report.GetDataSource(name)`; привязывается к полосе —
  `DataBand.DataSource = source`;
- **зарегистрированный источник данных по умолчанию `Enabled == false`** — без явного
  `source.Enabled = true` присваивание `DataBand.DataSource` молча не сохраняется (геттер/
  сеттер откатывает на `null`). В официальной XML-документации это только предупреждение
  без деталей — подтверждено эмпирически (см. `FastReportServiceTests.AssignBandDataSource_*`);
- `Dictionary.ClearRegisteredData()` **только отключает данные от `TableDataSource`, но не
  убирает сам компонент из `Dictionary`** — `Report.GetDataSource(name)` после очистки всё
  ещё находит "отключённый" источник (полностью подтверждено эмпирически, официальная
  документация не уточняет это поведение — сначала предполагалось обратное, что и привело
  к найденному ниже багу). Чтобы действительно убрать источник из графа отчёта — нужен
  `DataSourceBase.Dispose()` на каждом элементе `Dictionary.DataSources` (см. известные риски,
  п. 4 — именно так исправлена отвязка полосы после `Load()`);
- выражения пишутся буквально как `[ИмяИсточника.Колонка]` внутри `TextObjectBase.Text`
  (`AllowExpressions` по умолчанию `true`, отдельно включать не нужно — проверено тестом);
  вычисляются только на `Report.Prepare()`, не в момент присвоения `.Text`.
- **`GroupFooterBand` и `ChildBand` — не самостоятельные полосы страницы.** В отличие от
  `GroupHeaderBand` (штатно добавляется в `page.Bands.Add(...)`, подтверждено примером в
  доке), `GroupFooterBand` привязывается только через `GroupHeaderBand.GroupFooter`, а
  `ChildBand` — через `BandBase.Child` любой другой полосы. Прямое добавление в
  `page.Bands` валится в рантайме с `FastReport.Utils.ParentException: Object of type
  ReportPage cannot contain objects of type GroupFooterBand` — не ловится на этапе
  компиляции. `FastReportService.AddBand` бросает `NotSupportedException` для этих двух
  видов, `ObjectTreeViewModel.BandKindOptions` исключает их из «Добавить полосу» —
  полноценная поддержка вложенных полос (выбор родительской полосы для футера группы/
  child-band) осталась за рамками этапа 3.

## Известные риски

1. Растеризация шрифтов на канвасе и в превью может отличаться на 1–3 px — допустимо
   для визуального дизайна.
2. Набор экспортеров OpenSource-версии: ImageExport, HTMLExport подтверждены; PDF — только через плагин.
3. UI-функции FastReport (диалоги данных) в OpenSource недоступны — реализуем свои.
4. Источники данных, заданные в дизайнере (этап 3), — это примерные данные для превью,
   а не подключение к реальному источнику. Сами данные (`.frx` может донести только
   привязку `DataBand.DataSource`/компонент `TableDataSource` как часть графа отчёта, но
   не строки) **персистентны с этапа устранения этого ограничения** — см. ниже.

   **Персистентность источников (решено).** `FastReportService.Save(path)` пишет рядом
   с `.frx` sidecar-файл `<path>.datasources.json` — имена/колонки/строки всех источников
   плюс привязки `DataBand → источник` (см. `DataSourcesSidecarDto`,
   `SaveDataSourcesSidecar`/`LoadDataSourcesSidecar`/`RestoreDataSourcesFromSidecar` в
   `FastReportService.cs`). `Load()` и `LoadAsTemplate()` читают этот файл и восстанавливают
   источники и привязки до того, как пересобрать `CurrentReport.Dictionary` — таким образом
   решается и сам сценарий «добавили источник → сохранили → закрыли → открыли», и его
   следствие ниже (устаревший `[Источник.Колонка]`, ссылающийся в никуда).
   Известные ограничения этого решения (осознанные, не баги):
   - sidecar — отдельный файл: если пользователь скопирует/переместит `.frx` без него
     (например, через проводник, а не через приложение), источники при следующем открытии
     будут пустыми — деградация к прежнему поведению, не краш (см. ниже про `Prepare()`
     без try/catch);
   - «Сохранить как» в новое место не переносит и не удаляет sidecar по старому пути —
     остаётся осиротевшим файлом;
   - отсутствие или повреждение sidecar-файла (битый JSON) не бросает исключение — тихо
     считается, что источников нет, ровно как до появления этого файла.

   **Историческая деталь (найдена на этапе 4 при ручном тестировании — реальный краш
   приложения, не гипотетический edge case; актуальна и после решения выше — как раз
   поэтому `Report.Prepare()` в приложении не должен вызываться без try/catch):** привязка
   `DataBand.DataSource` (и компонент `TableDataSource` без данных) сериализуется в `.frx`
   как часть графа отчёта независимо от sidecar. После `Load()` это давало
   `DataTableException: Table is not connected to the data` прямо в
   `DataBand.InitDataSource()` — падало вообще без единого `[Field]`-выражения, просто от
   факта, что полоса когда-то была привязана к источнику. Исправлено: `Load()`/
   `LoadAsTemplate()` явно отвязывают и удаляют (`Dispose()`) все унаследованные из файла
   источники (см. факт про `ClearRegisteredData()` выше) перед тем, как восстановить
   актуальные из sidecar. Если же sidecar недоступен (см. ограничения выше), а в тексте
   объекта остался буквальный `[Источник.Колонка]` — `Report.Prepare()` падает с
   `CompilerException: CS0103` (FastReport компилирует выражения в C#, необъявленный
   идентификатор — ошибка компиляции, а не мягкая деградация в текст плейсхолдера).
   `MainViewModel.RefreshPreview()`/экспорт ловят это исключение и показывают
   `PreviewErrorMessage` вместо падения всего приложения (превью просто недоступно, а не
   краш).