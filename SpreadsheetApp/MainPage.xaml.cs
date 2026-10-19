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
    private bool isUpdatingUi;
    private bool hasUnsavedChanges;

    public MainPage()
    {
        InitializeComponent();
        controller = new SpreadsheetController(InitialRows, InitialColumns);
        BuildGridStructure(controller.Table.RowCount, controller.Table.ColumnCount);
        showTable(controller.Table, currentMode);
    }

    public void editCell(string address, string expression)
    {
        string currentExpr = controller.Table.getCell(address)?.getExpression() ?? string.Empty;
        string trimmedNew = expression?.Trim() ?? string.Empty;

        if (currentExpr == trimmedNew)
        {
            return;
        }

        if (controller.setCellExpression(address, trimmedNew))
        {
            hasUnsavedChanges = true;
            if (currentMode == DisplayMode.VALUE)
            {
                controller.calculate();
            }
            showTable(controller.Table, currentMode);
        }
    }

    public void clearCellBtnClick(string address)
    {
        controller.clearCell(address);
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
        showTable(controller.Table, currentMode);
        statusLabel.Text = $"Клітинку {address} очищено.";
    }

    public void calculateBtnClick()
    {
        CommitActiveInput();
        Spreadsheet table = controller.calculate();
        currentMode = DisplayMode.VALUE;
        UpdateModeButtonText();
        showTable(table, currentMode);

        Cell? firstErrorCell = table.Cells.FirstOrDefault(c => c.getError() != null);
        if (firstErrorCell != null)
        {
            ExpressionError err = firstErrorCell.getError()!;
            showErrorLocation(firstErrorCell.getAddress(), err.getPosition());
            showMessage($"Помилка у клітинці {firstErrorCell.getAddress()} (символ {err.getPosition() + 1}):\n{err.getMessage()}");
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
        showTable(controller.Table, currentMode);
        statusLabel.Text = $"Режим: {(currentMode == DisplayMode.EXPRESSION ? "ВИРАЗ" : "ЗНАЧЕННЯ")} | Активна клітинка: {selectedAddress}";
    }

    public void addRowBtnClick()
    {
        CommitActiveInput();
        controller.addRow();
        AddRowToGridUi(controller.Table.RowCount);
        hasUnsavedChanges = true;
        showTable(controller.Table, currentMode);
    }

    public void deleteRowBtnClick()
    {
        CommitActiveInput();
        if (!controller.deleteRow())
        {
            showMessage("Неможливо видалити останній рядок таблиці.");
            return;
        }

        RemoveLastRowFromGridUi();
        hasUnsavedChanges = true;

        if (!controller.Table.IsValidAddress(selectedAddress))
        {
            selectedAddress = "A1";
            activeCellLabel.Text = "A1";
        }

        if (currentMode == DisplayMode.VALUE)
        {
            controller.calculate();
        }
        showTable(controller.Table, currentMode);
    }

    public void addColumnBtnClick()
    {
        CommitActiveInput();
        controller.addColumn();
        AddColumnToGridUi(controller.Table.ColumnCount);
        hasUnsavedChanges = true;
        showTable(controller.Table, currentMode);
    }

    public void deleteColumnBtnClick()
    {
        CommitActiveInput();
        if (!controller.deleteColumn())
        {
            showMessage("Неможливо видалити останній стовпчик таблиці.");
            return;
        }

        RemoveLastColumnFromGridUi();
        hasUnsavedChanges = true;

        if (!controller.Table.IsValidAddress(selectedAddress))
        {
            selectedAddress = "A1";
            activeCellLabel.Text = "A1";
        }

        if (currentMode == DisplayMode.VALUE)
        {
            controller.calculate();
        }
        showTable(controller.Table, currentMode);
    }

    public void saveBtnClick(string filePath)
    {
        try
        {
            CommitActiveInput();
            controller.saveToFile(filePath);
            hasUnsavedChanges = false;
            showMessage($"Таблицю збережено у файл:\n{filePath}");
        }
        catch (Exception ex)
        {
            showMessage($"Помилка збереження файлу:\n{ex.Message}");
        }
    }

    public void openBtnClick(string filePath)
    {
        try
        {
            Spreadsheet loadedTable = controller.loadFromFile(filePath);
            BuildGridStructure(loadedTable.RowCount, loadedTable.ColumnCount);
            selectedAddress = "A1";
            activeCellLabel.Text = "A1";
            hasUnsavedChanges = false;
            showTable(loadedTable, currentMode);
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
            "Операнди: цілі числа довільної довжини (BigInteger), константи true/false, посилання на клітинки (A1, B2...).\n" +
            "Порожня клітинка має значення 0.";

        await DisplayAlert("Довідка", helpText, "OK");
    }

    public void showTable(Spreadsheet table, DisplayMode mode)
    {
        isUpdatingUi = true;
        try
        {
            for (int r = 1; r <= table.RowCount; r++)
            {
                for (int c = 1; c <= table.ColumnCount; c++)
                {
                    string address = Spreadsheet.FormatAddress(r, c);
                    Entry entry = cellEntries[r - 1][c - 1];
                    Cell? cell = table.getCell(address);

                    if (cell == null || string.IsNullOrWhiteSpace(cell.getExpression()))
                    {
                        if (!entry.IsFocused)
                        {
                            entry.Text = string.Empty;
                        }
                        entry.BackgroundColor = address == selectedAddress ? Colors.LightCyan : Colors.White;
                        continue;
                    }

                    ExpressionError? error = cell.getError();
                    if (mode == DisplayMode.EXPRESSION || entry.IsFocused)
                    {
                        entry.Text = cell.getExpression();
                    }
                    else
                    {
                        entry.Text = error != null
                            ? $"#ПОМИЛКА (поз. {error.getPosition() + 1})"
                            : (cell.getValue()?.ToString() ?? string.Empty);
                    }

                    if (error != null)
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

    public void showErrorLocation(string address, int position)
    {
        selectedAddress = address;
        activeCellLabel.Text = address;
        showTable(controller.Table, currentMode);

        Cell? cell = controller.Table.getCell(address);
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
            statusLabel.Text = $"Помилка у {address} (символ {position + 1}): {cell.getError()!.getMessage()}";
        }
    }

    public async void showMessage(string text)
    {
        await DisplayAlert("Повідомлення", text, "OK");
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
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            Label colHeader = CreateHeaderLabel(Spreadsheet.ColumnIndexToName(c));
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

        Label rowHeader = CreateHeaderLabel(rowNumber.ToString());
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
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

        Label colHeader = CreateHeaderLabel(Spreadsheet.ColumnIndexToName(colNumber));
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

    private static Label CreateHeaderLabel(string text)
    {
        return new Label
        {
            Text = text,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            FontAttributes = FontAttributes.Bold
        };
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

        // Оновлюємо підсвічування попередньої та поточної клітинки одразу при кліку
        UpdateSingleCellBackground(previousAddress);
        UpdateSingleCellBackground(selectedAddress);

        Cell? cell = controller.Table.getCell(address);
        string rawExpr = cell?.getExpression() ?? string.Empty;

        isUpdatingUi = true;
        entry.Text = rawExpr;
        textInput.Text = rawExpr;
        isUpdatingUi = false;

        if (cell?.getError() != null)
        {
            ExpressionError err = cell.getError()!;
            statusLabel.Text = $"Клітинка {address} — Помилка (символ {err.getPosition() + 1}): {err.getMessage()}";
        }
        else
        {
            statusLabel.Text = $"Активна клітинка: {address} | Режим: {(currentMode == DisplayMode.EXPRESSION ? "ВИРАЗ" : "ЗНАЧЕННЯ")}";
        }
    }

    private void UpdateSingleCellBackground(string address)
    {
        if (!Spreadsheet.TryParseAddress(address, out int r, out int c)) return;
        if (r < 1 || r > cellEntries.Count || c < 1 || c > columnHeaders.Count) return;

        Entry entry = cellEntries[r - 1][c - 1];
        Cell? cell = controller.Table.getCell(address);
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
        showTable(controller.Table, currentMode);
    }

    private void OnCellCompleted(object? sender, EventArgs e)
    {
        if (sender is not Entry entry || isUpdatingUi) return;
        editCell(entry.AutomationId, entry.Text ?? string.Empty);
        showTable(controller.Table, currentMode);
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
    private void OnAddRowClicked(object? sender, EventArgs e) => addRowBtnClick();
    private void OnDeleteRowClicked(object? sender, EventArgs e) => deleteRowBtnClick();
    private void OnAddColumnClicked(object? sender, EventArgs e) => addColumnBtnClick();
    private void OnDeleteColumnClicked(object? sender, EventArgs e) => deleteColumnBtnClick();
    private void OnHelpClicked(object? sender, EventArgs e) => helpBtnClick();
    private void OnExitClicked(object? sender, EventArgs e) => close();

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

        controller.Table.clearAll();
        hasUnsavedChanges = true;
        showTable(controller.Table, currentMode);
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
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
            saveBtnClick(file.Path);
        }
#else
        string defaultPath = Path.Combine(FileSystem.Current.AppDataDirectory, "table_variant67.json");
        saveBtnClick(defaultPath);
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