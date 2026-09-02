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
│   ├── Views/Dialogs/ConfirmDiscardChangesDialog.axaml(.cs)
│   ├── ViewModels/              # MainViewModel, DesignSurfaceViewModel, ObjectTreeViewModel,
│   │                             # PropertiesPanelViewModel
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
- **Этап 4 — продукт:** расширенное меню Файл (недавние файлы, шаблоны нового документа),
  undo/redo, копирование/вставка, статус-бар, линейки, экспорт, unit-тесты ViewModel.

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
- `Dictionary.ClearRegisteredData()` — штатный способ снять все зарегистрированные источники
  разом перед их пересборкой (используется вместо точечного `UnregisterData`);
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
   а не подключение к реальному источнику: они не сохраняются в `.frx` и теряются при
   `CreateNew()`/`Load()`. После повторного открытия файла источники нужно завести заново
   (сами выражения `[Источник.Колонка]` в тексте объектов при этом сохраняются, т.к. это
   просто строка `TextObject.Text`). Появление импорта/подключения к реальным данным —
   вне рамок этапа 3.