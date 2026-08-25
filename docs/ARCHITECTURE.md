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
│   ├── IExportService.cs / ExportService.cs  # PNG / HTML
│   ├── IPreviewService.cs / PreviewService.cs # рендер страницы в Bitmap
│   └── IUndoRedoService.cs     # (этап 4) undo/redo на снимках
├── UI/
│   ├── Views/Controls/         # DesignSurface, Ruler и т.п. (этап 1+)
│   ├── ViewModels/             # MainViewModel, DesignSurfaceVM, PropertiesVM…
│   └── App.axaml / MainWindow.axaml
tests/ReportDesigner.Tests/     # xUnit: конвертер, сервис отчёта, потом undo/redo
```

## Дорожная карта

- **Этап 0 — скелет (готов):** модели снимка, UnitConverter, DesignSnapshotBuilder,
  FastReportService (CreateNew/Load/Save, полосы, объекты), ExportService, тесты.
- **Этап 1 — канвас-редактор:** DesignSurface (лист, сетка, зум, выделение,
  8 ручек ресайза, перемещение), клавиши (Del, Esc, стрелки).
- **Этап 2 — палитра/дерево/свойства:** Toolbox (Text/Line/Shape/Picture), дерево
  объектов с z-order, панель свойств (координаты, текст, шрифт, цвета, рамки).
- **Этап 3 — данные и полосы:** управление полосами, источники данных, выражения `[Field]`.
- **Этап 4 — продукт:** меню Файл, undo/redo, копирование/вставка, статус-бар, линейки,
  экспорт, unit-тесты ViewModel.

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

## Известные риски

1. Растеризация шрифтов на канвасе и в превью может отличаться на 1–3 px — допустимо
   для визуального дизайна.
2. Набор экспортеров OpenSource-версии: ImageExport, HTMLExport подтверждены; PDF — только через плагин.
3. UI-функции FastReport (диалоги данных) в OpenSource недоступны — реализуем свои.