using System.Numerics;

namespace SpreadsheetEngine;

public class Spreadsheet
{
    private int rowCount;
    private int columnCount;
    private readonly List<Cell> cells = new();

    public Spreadsheet(int rowCount = 10, int columnCount = 10)
    {
        this.rowCount = Math.Max(1, rowCount);
        this.columnCount = Math.Max(1, columnCount);
    }

    public int RowCount => rowCount;
    public int ColumnCount => columnCount;
    public IReadOnlyList<Cell> Cells => cells;

    public Cell? getCell(string address)
    {
        string normalized = NormalizeAddress(address);
        if (!IsValidAddress(normalized))
        {
            return null;
        }

        return cells.FirstOrDefault(c => c.getAddress() == normalized);
    }

    public void setExpression(string address, string expression)
    {
        string normalized = NormalizeAddress(address);
        if (!IsValidAddress(normalized))
        {
            throw new ArgumentOutOfRangeException(nameof(address), $"Клітинка {normalized} виходить за межі таблиці.");
        }

        Cell? cell = cells.FirstOrDefault(c => c.getAddress() == normalized);
        if (string.IsNullOrWhiteSpace(expression))
        {
            if (cell != null)
            {
                cell.clear();
                cells.Remove(cell);
            }
            return;
        }

        if (cell == null)
        {
            cell = new Cell(normalized);
            cells.Add(cell);
        }

        cell.setExpression(expression);
    }

    public Value getValue(string address)
    {
        string normalized = NormalizeAddress(address);
        Cell? cell = getCell(normalized);
        if (cell == null || string.IsNullOrWhiteSpace(cell.getExpression()))
        {
            return new Value(BigInteger.Zero);
        }

        return cell.getValue() ?? new Value(BigInteger.Zero);
    }

    public List<Cell> getFormulaCells()
    {
        return cells.Where(c => c.isFormula()).ToList();
    }

    public void addRow() => rowCount++;

    public bool deleteRow()
    {
        if (rowCount <= 1) return false;

        int targetRow = rowCount;
        cells.RemoveAll(c => TryParseAddress(c.getAddress(), out int r, out _) && r == targetRow);
        rowCount--;
        return true;
    }

    public void addColumn() => columnCount++;

    public bool deleteColumn()
    {
        if (columnCount <= 1) return false;

        int targetCol = columnCount;
        cells.RemoveAll(c => TryParseAddress(c.getAddress(), out _, out int col) && col == targetCol);
        columnCount--;
        return true;
    }

    public void clearAll()
    {
        foreach (var cell in cells)
        {
            cell.clear();
        }
        cells.Clear();
    }

    public void Resize(int newRows, int newCols)
    {
        rowCount = Math.Max(1, newRows);
        columnCount = Math.Max(1, newCols);
        cells.RemoveAll(c => !IsValidAddress(c.getAddress()));
    }

    public bool IsValidAddress(string address)
    {
        if (!TryParseAddress(address, out int row, out int col))
        {
            return false;
        }
        return row >= 1 && row <= rowCount && col >= 1 && col <= columnCount;
    }

    public static bool TryParseAddress(string address, out int row, out int col)
    {
        row = 0;
        col = 0;
        if (string.IsNullOrWhiteSpace(address)) return false;

        string s = address.Trim().ToUpperInvariant();
        int i = 0;
        while (i < s.Length && s[i] >= 'A' && s[i] <= 'Z')
        {
            col = col * 26 + (s[i] - 'A' + 1);
            i++;
        }

        if (i == 0 || i == s.Length) return false;

        string rowPart = s[i..];
        if (rowPart[0] == '0') return false;

        return int.TryParse(rowPart, out row) && row > 0;
    }

    public static string ColumnIndexToName(int col)
    {
        if (col < 1) throw new ArgumentOutOfRangeException(nameof(col));
        string name = string.Empty;
        while (col > 0)
        {
            col--;
            name = (char)('A' + (col % 26)) + name;
            col /= 26;
        }
        return name;
    }

    public static string FormatAddress(int row, int col) => $"{ColumnIndexToName(col)}{row}";

    private static string NormalizeAddress(string address) => address?.Trim().ToUpperInvariant() ?? string.Empty;
}