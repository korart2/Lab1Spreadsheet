using SpreadsheetEngine;
using Cell = SpreadsheetEngine.Cell;

namespace SpreadsheetApp;

public partial class MainPage : ContentPage
{
    private const int InitialRows = 10;
    private const int InitialColumns = 8;

    private readonly SpreadsheetController controller;
    private DisplayMode currentMode = DisplayMode.EXPRESSION;

    private readonly List<Label> columnHeaders = new();
    private readonly List<Label> rowHeaders = new();
    private readonly List<List<Entry>> cellEntries = new();

    private string selectedAddress = "A1";
    private int selectedRow = 1;
    private int selectedColumn = 1;
    private string? currentFilePath;
    private bool isUpdatingUi;
    private bool hasUnsavedChanges;

    public MainPage()
    {
        InitializeComponent();
        controller = new SpreadsheetController(InitialRows, InitialColumns);
        BuildGridStructure(controller.getTable().getRowCount(), controller.getTable().getColumnCount());
        showTable(controller.getTable(), currentMode);
    }

    public void editCell(string address, string text)
    {
        Spreadsheet table = controller.getTable();
        Cell? cell = table.getCell(address);
        if (cell == null) return;

        string currentExpr = cell.getExpression();
        string trimmedNew = text?.Trim() ?? string.Empty;

        if (currentExpr == trimmedNew)
        {
            return;
        }

        if (cell.setExpression(trimmedNew))
        {
            hasUnsavedChanges = true;
            if (currentMode == DisplayMode.VALUE)
            {
                controller.calculate();
            }
            showTable(table, currentMode);
        }
    }

    public void clearCellBtnClick(string address)
    {
        Spreadsheet table = controller.getTable();
        Cell? cell = table.getCell(address);
        cell?.clear();
        hasUnsavedChanges = true;

        if (selectedAddress == address)
        {
            isUpdatingUi = true;
            textInput.Text = string.Empty;
            isUpdatingUi = false;
        }

        if (currentMode == DisplayMode.VALUE)
        {
            controller.calculate();
        }
        showTable(table, currentMode);
        statusLabel.Text = $"Клітинку {address} очищено.";
    }

    public void calculateBtnClick()
    {
        CommitActiveInput();
        Spreadsheet table = controller.calculate();
        currentMode = DisplayMode.VALUE;
        UpdateModeButtonText();
        showTable(table, currentMode);

        Cell? firstErrorCell = table.getFilledCells().FirstOrDefault(c => c.getError() != null);
        if (firstErrorCell != null)
        {
            ExpressionError err = firstErrorCell.getError()!;
            showErrorLocation(firstErrorCell.getAddress(), err.getType(), err.getPosition());
            showMessage($"Клітинка {firstErrorCell.getAddress()} — {err.getType()} (символ {err.getPosition() + 1}):\n{err.getMessage()}");
        }
        else
        {
            statusLabel.Text = "Обчислення виконано успішно.";
        }
    }

    public void switchModeBtnClick()
    {
        CommitActiveInput();
        if (currentMode == DisplayMode.EXPRESSION)
        {
            currentMode = DisplayMode.VALUE;
            controller.calculate();
        }
        else
        {
            currentMode = DisplayMode.EXPRESSION;
        }

        UpdateModeButtonText();
        showTable(controller.getTable(), currentMode);
        statusLabel.Text = $"Режим: {(currentMode == DisplayMode.EXPRESSION ? "ВИРАЗ" : "ЗНАЧЕННЯ")} | Активна клітинка: {selectedAddress}";
    }

    public void addRowBtnClick(int index)
    {
        CommitActiveInput();
        Spreadsheet table = controller.getTable();
        table.addRow(index);
        AddRowToGridUi(table.getRowCount());
        hasUnsavedChanges = true;

        if (currentMode == DisplayMode.VALUE)
        {
            controller.calculate();
        }
        showTable(table, currentMode);
        statusLabel.Text = $"Вставлено рядок на позицію {index}. Усього рядків: {table.getRowCount()}.";
    }

    public void deleteRowBtnClick(int index)
    {
        CommitActiveInput();
        Spreadsheet table = controller.getTable();
        if (!table.deleteRow(index))
        {
            showMessage("Неможливо видалити останній рядок таблиці.");
            return;
        }

        RemoveLastRowFromGridUi();
        hasUnsavedChanges = true;

        selectedRow = Math.Clamp(selectedRow, 1, table.getRowCount());
        selectedAddress = Spreadsheet.FormatAddress(selectedRow, selectedColumn);
        activeCellLabel.Text = selectedAddress;

        if (currentMode == DisplayMode.VALUE)
        {
            controller.calculate();
        }
        showTable(table, currentMode);
        statusLabel.Text = $"Видалено рядок {index}. Формули та клітинки зміщено.";
    }

    public void addColumnBtnClick(int index)
    {
        CommitActiveInput();
        Spreadsheet table = controller.getTable();
        table.addColumn(index);
        AddColumnToGridUi(table.getColumnCount());
        hasUnsavedChanges = true;

        if (currentMode == DisplayMode.VALUE)
        {
            controller.calculate();
        }
        showTable(table, currentMode);
        statusLabel.Text = $"Вставлено стовпчик {Spreadsheet.ColumnIndexToName(index)}.";
    }

    public void deleteColumnBtnClick(int index)
    {
        CommitActiveInput();
        Spreadsheet table = controller.getTable();
        string deletedName = Spreadsheet.ColumnIndexToName(index);
        if (!table.deleteColumn(index))
        {
            showMessage("Неможливо видалити останній стовпчик таблиці.");
            return;
        }

        RemoveLastColumnFromGridUi();
        hasUnsavedChanges = true;

        selectedColumn = Math.Clamp(selectedColumn, 1, table.getColumnCount());
        selectedAddress = Spreadsheet.FormatAddress(selectedRow, selectedColumn);
        activeCellLabel.Text = selectedAddress;

        if (currentMode == DisplayMode.VALUE)
        {
            controller.calculate();
        }
        showTable(table, currentMode);
        statusLabel.Text = $"Видалено стовпчик {deletedName}. Формули та клітинки зміщено.";
    }

    public async void saveBtnClick()
    {
        if (!string.IsNullOrWhiteSpace(currentFilePath))
        {
            saveAsBtnClick(currentFilePath);
            return;
        }

        await PickAndSaveFileAsync();
    }

    public void saveAsBtnClick(string path)
    {
        try
        {
            CommitActiveInput();
            if (controller.saveTable(path))
            {
                currentFilePath = path;
                hasUnsavedChanges = false;
                statusLabel.Text = $"Збережено: {path}";
                showMessage($"Таблицю збережено у файл:\n{path}");
            }
        }
        catch (Exception ex)
        {
            showMessage($"Помилка збереження файлу:\n{ex.Message}");
        }
    }

    public void openBtnClick(string path)
    {
        try
        {
            Spreadsheet loadedTable = controller.openTable(path);
            currentFilePath = path;
            BuildGridStructure(loadedTable.getRowCount(), loadedTable.getColumnCount());
            selectedRow = 1;
            selectedColumn = 1;
            selectedAddress = "A1";
            activeCellLabel.Text = "A1";
            hasUnsavedChanges = false;
            showTable(loadedTable, currentMode);
            statusLabel.Text = $"Відкрито файл: {path}";
        }
        catch (Exception ex)
        {
            showMessage($"Помилка відкриття файлу:\n{ex.Message}");
        }
    }

    public async void helpBtnClick()
    {
        string helpText =
            "Варіант 67. Підтримувані операції (за спаданням пріоритету):\n" +
            "1. ( ) — круглі дужки\n" +
            "2. inc(x), dec(x) — інкремент та декремент\n" +
            "3. +x, -x — унарні плюс та мінус\n" +
            "4. <=, >=, <> — порівняння цілих чисел\n" +
            "5. not x — логічне заперечення\n" +
            "6. x and y — логічне І\n" +
            "7. x or y — логічне АБО\n\n" +
            "Клік по заголовку рядка (1, 2...) або стовпчика (A, B...) виділяє його для швидкої вставки чи видалення.";

        await DisplayAlert("Довідка", helpText, "OK");
    }

    public void showTable(Spreadsheet table, DisplayMode mode)
    {
        isUpdatingUi = true;
        try
        {
            UpdateHeadersHighlight();

            int rows = table.getRowCount();
            int cols = table.getColumnCount();

            for (int r = 1; r <= rows; r++)
            {
                for (int c = 1; c <= cols; c++)
                {
                    string address = Spreadsheet.FormatAddress(r, c);
                    Entry entry = cellEntries[r - 1][c - 1];
                    Cell? cell = table.getCell(address);

                    if (cell == null) continue;

                    if (!entry.IsFocused)
                    {
                        entry.Text = cell.getDisplayText(mode);
                    }

                    if (cell.getError() != null)
                    {
                        entry.BackgroundColor = address == selectedAddress ? Colors.LightCoral : Colors.MistyRose;
                    }
                    else
                    {
                        entry.BackgroundColor = address == selectedAddress ? Colors.LightCyan : Colors.White;
                    }
                }
            }

            Cell? activeCell = table.getCell(selectedAddress);
            if (!textInput.IsFocused)
            {
                textInput.Text = activeCell?.getExpression() ?? string.Empty;
            }
        }
        finally
        {
            isUpdatingUi = false;
        }
    }

    public void showErrorLocation(string address, string type, int position)
    {
        if (Spreadsheet.TryParseAddress(address, out int r, out int c))
        {
            selectedRow = r;
            selectedColumn = c;
        }
        selectedAddress = address;
        activeCellLabel.Text = address;
        showTable(controller.getTable(), currentMode);

        Cell? cell = controller.getTable().getCell(address);
        string expr = cell?.getExpression() ?? string.Empty;

        isUpdatingUi = true;
        textInput.Text = expr;
        isUpdatingUi = false;

        int safePos = Math.Clamp(position, 0, expr.Length);
        textInput.Focus();
        textInput.CursorPosition = safePos;
        textInput.SelectionLength = safePos < expr.Length ? 1 : 0;

        if (cell?.getError() != null)
        {
            statusLabel.Text = $"{address} — {type} (символ {position + 1}): {cell.getError()!.getMessage()}";
        }
    }

    public async void showMessage(string message)
    {
        await DisplayAlert("Повідомлення", message, "OK");
    }

    public async void close()
    {
        if (hasUnsavedChanges)
        {
            bool confirm = await DisplayAlert(
                "Вихід",
                "У таблиці є незбережені зміни. Ви дійсно бажаєте вийти?",
                "Так",
                "Ні");

            if (!confirm) return;
        }

        Application.Current?.Quit();
    }

    private void BuildGridStructure(int rows, int cols)
    {
        grid.Children.Clear();
        grid.RowDefinitions.Clear();
        grid.ColumnDefinitions.Clear();
        columnHeaders.Clear();
        rowHeaders.Clear();
        cellEntries.Clear();

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });

        grid.Add(new Label { Text = "" }, 0, 0);

        for (int c = 1; c <= cols; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(115) });
            Label colHeader = CreateColumnHeaderLabel(c);
            columnHeaders.Add(colHeader);
            grid.Add(colHeader, c, 0);
        }

        for (int r = 1; r <= rows; r++)
        {
            AddRowToGridUi(r);
        }
    }

    private void AddRowToGridUi(int rowNumber)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Label rowHeader = CreateRowHeaderLabel(rowNumber);
        rowHeaders.Add(rowHeader);
        grid.Add(rowHeader, 0, rowNumber);

        var rowEntryList = new List<Entry>();
        int cols = columnHeaders.Count;

        for (int c = 1; c <= cols; c++)
        {
            Entry entry = CreateCellEntry(rowNumber, c);
            rowEntryList.Add(entry);
            grid.Add(entry, c, rowNumber);
        }

        cellEntries.Add(rowEntryList);
    }

    private void RemoveLastRowFromGridUi()
    {
        int lastRowIndex = cellEntries.Count - 1;
        if (lastRowIndex < 0) return;

        foreach (Entry entry in cellEntries[lastRowIndex])
        {
            grid.Children.Remove(entry);
        }
        cellEntries.RemoveAt(lastRowIndex);

        Label lastRowHeader = rowHeaders[lastRowIndex];
        grid.Children.Remove(lastRowHeader);
        rowHeaders.RemoveAt(lastRowIndex);

        grid.RowDefinitions.RemoveAt(grid.RowDefinitions.Count - 1);
    }

    private void AddColumnToGridUi(int colNumber)
    {
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(115) });

        Label colHeader = CreateColumnHeaderLabel(colNumber);
        columnHeaders.Add(colHeader);
        grid.Add(colHeader, colNumber, 0);

        for (int r = 1; r <= cellEntries.Count; r++)
        {
            Entry entry = CreateCellEntry(r, colNumber);
            cellEntries[r - 1].Add(entry);
            grid.Add(entry, colNumber, r);
        }
    }

    private void RemoveLastColumnFromGridUi()
    {
        int lastColIndex = columnHeaders.Count - 1;
        if (lastColIndex < 0) return;

        for (int r = 0; r < cellEntries.Count; r++)
        {
            Entry entry = cellEntries[r][lastColIndex];
            grid.Children.Remove(entry);
            cellEntries[r].RemoveAt(lastColIndex);
        }

        Label lastColHeader = columnHeaders[lastColIndex];
        grid.Children.Remove(lastColHeader);
        columnHeaders.RemoveAt(lastColIndex);

        grid.ColumnDefinitions.RemoveAt(grid.ColumnDefinitions.Count - 1);
    }

    private Label CreateRowHeaderLabel(int rowNumber)
    {
        var label = new Label
        {
            Text = rowNumber.ToString(),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(4)
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            selectedRow = rowNumber;
            selectedAddress = Spreadsheet.FormatAddress(selectedRow, selectedColumn);
            activeCellLabel.Text = selectedAddress;
            UpdateHeadersHighlight();
            statusLabel.Text = $"Вибрано рядок {rowNumber}.";
        };
        label.GestureRecognizers.Add(tap);
        return label;
    }

    private Label CreateColumnHeaderLabel(int colNumber)
    {
        string colName = Spreadsheet.ColumnIndexToName(colNumber);
        var label = new Label
        {
            Text = colName,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(4)
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            selectedColumn = colNumber;
            selectedAddress = Spreadsheet.FormatAddress(selectedRow, selectedColumn);
            activeCellLabel.Text = selectedAddress;
            UpdateHeadersHighlight();
            statusLabel.Text = $"Вибрано стовпчик {colName}.";
        };
        label.GestureRecognizers.Add(tap);
        return label;
    }

    private void UpdateHeadersHighlight()
    {
        for (int r = 0; r < rowHeaders.Count; r++)
        {
            rowHeaders[r].BackgroundColor = (r + 1 == selectedRow) ? Colors.LightSteelBlue : Colors.Transparent;
        }
        for (int c = 0; c < columnHeaders.Count; c++)
        {
            columnHeaders[c].BackgroundColor = (c + 1 == selectedColumn) ? Colors.LightSteelBlue : Colors.Transparent;
        }
    }

    private Entry CreateCellEntry(int row, int col)
    {
        string address = Spreadsheet.FormatAddress(row, col);
        var entry = new Entry
        {
            Text = string.Empty,
            BackgroundColor = Colors.White,
            AutomationId = address
        };

        entry.Focused += OnCellFocused;
        entry.Unfocused += OnCellUnfocused;
        entry.Completed += OnCellCompleted;
        return entry;
    }

    private void OnCellFocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry || isUpdatingUi) return;

        string previousAddress = selectedAddress;
        string address = entry.AutomationId;
        selectedAddress = address;
        activeCellLabel.Text = address;

        if (Spreadsheet.TryParseAddress(address, out int r, out int c))
        {
            selectedRow = r;
            selectedColumn = c;
        }

        UpdateHeadersHighlight();
        UpdateSingleCellBackground(previousAddress);
        UpdateSingleCellBackground(selectedAddress);

        Cell? cell = controller.getTable().getCell(address);
        string rawExpr = cell?.getExpression() ?? string.Empty;

        isUpdatingUi = true;
        entry.Text = rawExpr;
        textInput.Text = rawExpr;
        isUpdatingUi = false;

        if (cell?.getError() != null)
        {
            ExpressionError err = cell.getError()!;
            statusLabel.Text = $"{address} — {err.getType()} (символ {err.getPosition() + 1}): {err.getMessage()}";
        }
        else
        {
            statusLabel.Text = $"Активна клітинка: {address} (рядок {selectedRow}, стовпчик {Spreadsheet.ColumnIndexToName(selectedColumn)})";
        }
    }

    private void UpdateSingleCellBackground(string address)
    {
        if (!Spreadsheet.TryParseAddress(address, out int r, out int c)) return;
        if (r < 1 || r > cellEntries.Count || c < 1 || c > columnHeaders.Count) return;

        Entry entry = cellEntries[r - 1][c - 1];
        Cell? cell = controller.getTable().getCell(address);
        if (cell?.getError() != null)
        {
            entry.BackgroundColor = address == selectedAddress ? Colors.LightCoral : Colors.MistyRose;
        }
        else
        {
            entry.BackgroundColor = address == selectedAddress ? Colors.LightCyan : Colors.White;
        }
    }

    private void OnCellUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry || isUpdatingUi) return;
        editCell(entry.AutomationId, entry.Text ?? string.Empty);
        showTable(controller.getTable(), currentMode);
    }

    private void OnCellCompleted(object? sender, EventArgs e)
    {
        if (sender is not Entry entry || isUpdatingUi) return;
        editCell(entry.AutomationId, entry.Text ?? string.Empty);
        showTable(controller.getTable(), currentMode);
    }

    private void OnFormulaBarCompleted(object? sender, EventArgs e)
    {
        if (isUpdatingUi) return;
        editCell(selectedAddress, textInput.Text ?? string.Empty);
    }

    private void OnFormulaBarUnfocused(object? sender, FocusEventArgs e)
    {
        if (isUpdatingUi) return;
        editCell(selectedAddress, textInput.Text ?? string.Empty);
    }

    private void OnApplyFormulaClicked(object? sender, EventArgs e)
    {
        editCell(selectedAddress, textInput.Text ?? string.Empty);
    }

    private void OnClearCellClicked(object? sender, EventArgs e) => clearCellBtnClick(selectedAddress);
    private void OnCalculateClicked(object? sender, EventArgs e) => calculateBtnClick();
    private void OnSwitchModeClicked(object? sender, EventArgs e) => switchModeBtnClick();
    private void OnHelpClicked(object? sender, EventArgs e) => helpBtnClick();
    private void OnExitClicked(object? sender, EventArgs e) => close();
    private void OnSaveClicked(object? sender, EventArgs e) => saveBtnClick();

    private async void OnSaveAsClicked(object? sender, EventArgs e)
    {
        await PickAndSaveFileAsync();
    }

    private async void OnAddRowClicked(object? sender, EventArgs e)
    {
        int totalRows = controller.getTable().getRowCount();
        var options = new List<string> { $"Перед вибраним рядком ({selectedRow})", $"У кінець таблиці ({totalRows + 1})" };
        for (int r = 1; r <= totalRows; r++)
        {
            options.Add($"Перед рядком {r}");
        }

        string? choice = await DisplayActionSheet("Куди вставити новий рядок?", "Скасувати", null, options.ToArray());
        if (string.IsNullOrEmpty(choice) || choice == "Скасувати") return;

        if (choice.StartsWith("Перед вибраним"))
        {
            addRowBtnClick(selectedRow);
        }
        else if (choice.StartsWith("У кінець"))
        {
            addRowBtnClick(totalRows + 1);
        }
        else if (int.TryParse(choice.Replace("Перед рядком ", ""), out int targetRow))
        {
            addRowBtnClick(targetRow);
        }
    }

    private async void OnDeleteRowClicked(object? sender, EventArgs e)
    {
        int totalRows = controller.getTable().getRowCount();
        var options = new List<string> { $"Вибраний рядок ({selectedRow})" };
        for (int r = 1; r <= totalRows; r++)
        {
            options.Add($"Рядок {r}");
        }

        string? choice = await DisplayActionSheet("Який рядок видалити?", "Скасувати", null, options.ToArray());
        if (string.IsNullOrEmpty(choice) || choice == "Скасувати") return;

        if (choice.StartsWith("Вибраний рядок"))
        {
            deleteRowBtnClick(selectedRow);
        }
        else if (int.TryParse(choice.Replace("Рядок ", ""), out int targetRow))
        {
            deleteRowBtnClick(targetRow);
        }
    }

    private async void OnAddColumnClicked(object? sender, EventArgs e)
    {
        int totalCols = controller.getTable().getColumnCount();
        string selColName = Spreadsheet.ColumnIndexToName(selectedColumn);
        var options = new List<string>
        {
            $"Перед вибраним стовпчиком ({selColName})",
            $"У кінець таблиці ({Spreadsheet.ColumnIndexToName(totalCols + 1)})"
        };
        for (int c = 1; c <= totalCols; c++)
        {
            options.Add($"Перед стовпчиком {Spreadsheet.ColumnIndexToName(c)}");
        }

        string? choice = await DisplayActionSheet("Куди вставити новий стовпчик?", "Скасувати", null, options.ToArray());
        if (string.IsNullOrEmpty(choice) || choice == "Скасувати") return;

        if (choice.StartsWith("Перед вибраним"))
        {
            addColumnBtnClick(selectedColumn);
        }
        else if (choice.StartsWith("У кінець"))
        {
            addColumnBtnClick(totalCols + 1);
        }
        else
        {
            string colName = choice.Replace("Перед стовпчиком ", "").Trim();
            if (Spreadsheet.TryParseAddress(colName + "1", out _, out int targetCol))
            {
                addColumnBtnClick(targetCol);
            }
        }
    }

    private async void OnDeleteColumnClicked(object? sender, EventArgs e)
    {
        int totalCols = controller.getTable().getColumnCount();
        string selColName = Spreadsheet.ColumnIndexToName(selectedColumn);
        var options = new List<string> { $"Вибраний стовпчик ({selColName})" };
        for (int c = 1; c <= totalCols; c++)
        {
            options.Add($"Стовпчик {Spreadsheet.ColumnIndexToName(c)}");
        }

        string? choice = await DisplayActionSheet("Який стовпчик видалити?", "Скасувати", null, options.ToArray());
        if (string.IsNullOrEmpty(choice) || choice == "Скасувати") return;

        if (choice.StartsWith("Вибраний стовпчик"))
        {
            deleteColumnBtnClick(selectedColumn);
        }
        else
        {
            string colName = choice.Replace("Стовпчик ", "").Trim();
            if (Spreadsheet.TryParseAddress(colName + "1", out _, out int targetCol))
            {
                deleteColumnBtnClick(targetCol);
            }
        }
    }

    private async void OnAboutClicked(object? sender, EventArgs e)
    {
        string aboutText =
            "Лабораторна робота №1\n" +
            "«Розробка програми для роботи з електронною таблицею»\n\n" +
            "Виконав: Корпачов Артем Андрійович, група К-27\n" +
            "Варіант: 67\n\n" +
            "Операції варіанту: унарні +, -; inc, dec; <=, >=, <>; not; or, and.";

        await DisplayAlert("Про програму", aboutText, "OK");
    }

    private async void OnClearAllClicked(object? sender, EventArgs e)
    {
        bool confirm = await DisplayAlert(
            "Очищення таблиці",
            "Ви дійсно бажаєте очистити всю таблицю?",
            "Так",
            "Ні");

        if (!confirm) return;

        controller.getTable().clearAll();
        hasUnsavedChanges = true;
        showTable(controller.getTable(), currentMode);
    }

    private async Task PickAndSaveFileAsync()
    {
#if WINDOWS
        var savePicker = new Windows.Storage.Pickers.FileSavePicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
            SuggestedFileName = "table_variant67"
        };
        savePicker.FileTypeChoices.Add("JSON таблиця", new List<string> { ".json" });

        if (Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView is MauiWinUIWindow window)
        {
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, window.WindowHandle);
        }

        Windows.Storage.StorageFile? file = await savePicker.PickSaveFileAsync();
        if (file != null)
        {
            saveAsBtnClick(file.Path);
        }
#else
        string defaultPath = Path.Combine(FileSystem.Current.AppDataDirectory, "table_variant67.json");
        saveAsBtnClick(defaultPath);
#endif
    }

    private async void OnOpenClicked(object? sender, EventArgs e)
    {
        if (hasUnsavedChanges)
        {
            bool proceed = await DisplayAlert(
                "Відкриття файлу",
                "Поточна таблиця має незбережені зміни. Продовжити?",
                "Так",
                "Ні");

            if (!proceed) return;
        }

        var customFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.WinUI, new[] { ".json" } }
        });

        PickOptions options = new()
        {
            PickerTitle = "Виберіть файл таблиці (.json)",
            FileTypes = customFileType
        };

        FileResult? result = await FilePicker.Default.PickAsync(options);
        if (result != null)
        {
            openBtnClick(result.FullPath);
        }
    }

    private void CommitActiveInput()
    {
        if (textInput.IsFocused)
        {
            editCell(selectedAddress, textInput.Text ?? string.Empty);
        }
        else if (Spreadsheet.TryParseAddress(selectedAddress, out int r, out int c)
                 && r >= 1 && r <= cellEntries.Count
                 && c >= 1 && c <= columnHeaders.Count)
        {
            Entry activeEntry = cellEntries[r - 1][c - 1];
            if (activeEntry.IsFocused)
            {
                editCell(selectedAddress, activeEntry.Text ?? string.Empty);
            }
        }
    }

    private void UpdateModeButtonText()
    {
        modeButton.Text = currentMode == DisplayMode.EXPRESSION
            ? "Режим: ВИРАЗ"
            : "Режим: ЗНАЧЕННЯ";
    }
}