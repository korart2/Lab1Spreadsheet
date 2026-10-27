using System.Numerics;
using System.Text;
using Antlr4.Runtime;

namespace SpreadsheetEngine;

public class Spreadsheet
{
    private int rowCount;
    private int columnCount;
    private readonly List<Cell> cells = new();

    public Spreadsheet(int rowCount = 10, int columnCount = 8)
    {
        this.rowCount = Math.Max(1, rowCount);
        this.columnCount = Math.Max(1, columnCount);
    }

    public int getRowCount() => rowCount;
    public int getColumnCount() => columnCount;

    public List<Cell> getFilledCells()
    {
        return cells.Where(c => !string.IsNullOrWhiteSpace(c.getExpression())).ToList();
    }

    public List<Cell> getFormulaCells()
    {
        return cells.Where(c => c.isFormula()).ToList();
    }

    public Cell? getCell(string address)
    {
        string normalized = NormalizeAddress(address);
        if (!IsValidAddress(normalized))
        {
            return null;
        }

        Cell? cell = cells.FirstOrDefault(c => c.getAddress() == normalized);
        if (cell == null)
        {
            cell = new Cell(normalized);
            cells.Add(cell);
        }
        return cell;
    }

    public Value getValue(string address)
    {
        string normalized = NormalizeAddress(address);
        if (!IsValidAddress(normalized))
        {
            return new Value(BigInteger.Zero);
        }

        Cell? cell = cells.FirstOrDefault(c => c.getAddress() == normalized);
        if (cell == null || string.IsNullOrWhiteSpace(cell.getExpression()))
        {
            return new Value(BigInteger.Zero);
        }

        return cell.getValue() ?? new Value(BigInteger.Zero);
    }

    public void addRow(int index = -1)
    {
        int insertAt = (index < 1 || index > rowCount + 1) ? rowCount + 1 : index;
        rowCount++;

        foreach (Cell cell in cells)
        {
            if (TryParseAddress(cell.getAddress(), out int r, out int c) && r >= insertAt)
            {
                cell.setAddress(FormatAddress(r + 1, c));
            }
        }

        UpdateReferencesOnRowChange(insertAt, isDelete: false);
    }

    public bool deleteRow(int index = -1)
    {
        if (rowCount <= 1) return false;

        int targetRow = (index < 1 || index > rowCount) ? rowCount : index;

        cells.RemoveAll(c => TryParseAddress(c.getAddress(), out int r, out _) && r == targetRow);

        foreach (Cell cell in cells)
        {
            if (TryParseAddress(cell.getAddress(), out int r, out int c) && r > targetRow)
            {
                cell.setAddress(FormatAddress(r - 1, c));
            }
        }

        rowCount--;
        UpdateReferencesOnRowChange(targetRow, isDelete: true);
        return true;
    }

    public void addColumn(int index = -1)
    {
        int insertAt = (index < 1 || index > columnCount + 1) ? columnCount + 1 : index;
        columnCount++;

        foreach (Cell cell in cells)
        {
            if (TryParseAddress(cell.getAddress(), out int r, out int c) && c >= insertAt)
            {
                cell.setAddress(FormatAddress(r, c + 1));
            }
        }

        UpdateReferencesOnColumnChange(insertAt, isDelete: false);
    }

    public bool deleteColumn(int index = -1)
    {
        if (columnCount <= 1) return false;

        int targetCol = (index < 1 || index > columnCount) ? columnCount : index;

        cells.RemoveAll(c => TryParseAddress(c.getAddress(), out _, out int col) && col == targetCol);

        foreach (Cell cell in cells)
        {
            if (TryParseAddress(cell.getAddress(), out int r, out int c) && c > targetCol)
            {
                cell.setAddress(FormatAddress(r, c - 1));
            }
        }

        columnCount--;
        UpdateReferencesOnColumnChange(targetCol, isDelete: true);
        return true;
    }

    public void clearAll()
    {
        foreach (Cell cell in cells)
        {
            cell.clear();
        }
        cells.Clear();
    }

    public bool IsValidAddress(string address)
    {
        if (!TryParseAddress(address, out int row, out int col))
        {
            return false;
        }
        return row >= 1 && row <= rowCount && col >= 1 && col <= columnCount;
    }

    private void UpdateReferencesOnRowChange(int targetRow, bool isDelete)
    {
        foreach (Cell cell in getFormulaCells())
        {
            string expr = cell.getExpression();
            var lexer = new ExpressionLexer(new AntlrInputStream(expr));
            lexer.RemoveErrorListeners();
            IList<IToken> tokens = lexer.GetAllTokens();

            var sb = new StringBuilder();
            int lastPos = 0;

            foreach (IToken token in tokens)
            {
                sb.Append(expr, lastPos, token.StartIndex - lastPos);

                if (token.Type == ExpressionLexer.IDENTIFIER && TryParseAddress(token.Text, out int r, out int c))
                {
                    if (isDelete && r == targetRow)
                    {
                        sb.Append("#REF!");
                    }
                    else if (isDelete && r > targetRow)
                    {
                        sb.Append(FormatAddress(r - 1, c));
                    }
                    else if (!isDelete && r >= targetRow)
                    {
                        sb.Append(FormatAddress(r + 1, c));
                    }
                    else
                    {
                        sb.Append(token.Text);
                    }
                }
                else
                {
                    sb.Append(token.Text);
                }

                lastPos = token.StopIndex + 1;
            }

            sb.Append(expr, lastPos, expr.Length - lastPos);
            cell.setExpression(sb.ToString());
        }
    }

    private void UpdateReferencesOnColumnChange(int targetCol, bool isDelete)
    {
        foreach (Cell cell in getFormulaCells())
        {
            string expr = cell.getExpression();
            var lexer = new ExpressionLexer(new AntlrInputStream(expr));
            lexer.RemoveErrorListeners();
            IList<IToken> tokens = lexer.GetAllTokens();

            var sb = new StringBuilder();
            int lastPos = 0;

            foreach (IToken token in tokens)
            {
                sb.Append(expr, lastPos, token.StartIndex - lastPos);

                if (token.Type == ExpressionLexer.IDENTIFIER && TryParseAddress(token.Text, out int r, out int c))
                {
                    if (isDelete && c == targetCol)
                    {
                        sb.Append("#REF!");
                    }
                    else if (isDelete && c > targetCol)
                    {
                        sb.Append(FormatAddress(r, c - 1));
                    }
                    else if (!isDelete && c >= targetCol)
                    {
                        sb.Append(FormatAddress(r, c + 1));
                    }
                    else
                    {
                        sb.Append(token.Text);
                    }
                }
                else
                {
                    sb.Append(token.Text);
                }

                lastPos = token.StopIndex + 1;
            }

            sb.Append(expr, lastPos, expr.Length - lastPos);
            cell.setExpression(sb.ToString());
        }
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