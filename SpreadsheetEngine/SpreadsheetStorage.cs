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

    public void save(Spreadsheet table, string filePath)
    {
        var dto = new TableDto
        {
            RowCount = table.RowCount,
            ColumnCount = table.ColumnCount,
            Cells = table.Cells
                .Where(c => !string.IsNullOrWhiteSpace(c.getExpression()))
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
        File.WriteAllText(filePath, json);
    }

    public Spreadsheet load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Файл таблиці не знайдено.", filePath);
        }

        string json = File.ReadAllText(filePath);
        TableDto? dto = JsonSerializer.Deserialize<TableDto>(json)
            ?? throw new InvalidDataException("Некоректний формат файлу таблиці.");

        var table = new Spreadsheet(dto.RowCount, dto.ColumnCount);
        foreach (var cellDto in dto.Cells)
        {
            if (table.IsValidAddress(cellDto.Address))
            {
                table.setExpression(cellDto.Address, cellDto.Expression);
            }
        }

        return table;
    }
}