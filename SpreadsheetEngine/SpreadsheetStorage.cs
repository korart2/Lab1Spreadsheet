using System.Text.Encodings.Web;
using System.Text.Json;

namespace SpreadsheetEngine;

public class SpreadsheetStorage
{
    private sealed class CellDto
    {
        public string Address { get; set; } = string.Empty;
        public string Expression { get; set; } = string.Empty;
    }

    private sealed class TableDto
    {
        public int RowCount { get; set; }
        public int ColumnCount { get; set; }
        public List<CellDto> Cells { get; set; } = new();
    }

    public bool save(Spreadsheet table, string path)
    {
        var dto = new TableDto
        {
            RowCount = table.getRowCount(),
            ColumnCount = table.getColumnCount(),
            Cells = table.getFilledCells()
                .Select(c => new CellDto
                {
                    Address = c.getAddress(),
                    Expression = c.getExpression()
                })
                .ToList()
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        string json = JsonSerializer.Serialize(dto, options);
        File.WriteAllText(path, json);
        return true;
    }

    public Spreadsheet load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Файл таблиці не знайдено.", path);
        }

        string json = File.ReadAllText(path);
        TableDto? dto = JsonSerializer.Deserialize<TableDto>(json)
            ?? throw new InvalidDataException("Некоректний формат файлу таблиці.");

        var table = new Spreadsheet(dto.RowCount, dto.ColumnCount);
        foreach (var cellDto in dto.Cells)
        {
            table.getCell(cellDto.Address)?.setExpression(cellDto.Expression);
        }

        return table;
    }
}