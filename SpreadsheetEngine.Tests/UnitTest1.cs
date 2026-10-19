using System.Numerics;
using SpreadsheetEngine;

namespace SpreadsheetEngine.Tests;

public class Variant67Tests
{
    [Fact]
    public void SequenceDiagramScenario_WorksExactlyAsDesigned()
    {
        var ctrl = new SpreadsheetController(10, 10);
        ctrl.setCellExpression("A1", "70");
        ctrl.setCellExpression("B1", "67");
        ctrl.setCellExpression("C1", "1000");

        ctrl.setCellExpression("D1", "A1 >=");
        Spreadsheet table = ctrl.calculate();

        Cell? d1 = table.getCell("D1");
        Assert.NotNull(d1);
        Assert.NotNull(d1.getError());
        Assert.Equal(5, d1.getError()!.getPosition());

        ctrl.setCellExpression("D1", "A1 >= B1 and C1 >= 1");
        ctrl.calculate();

        Assert.Null(d1.getError());
        Assert.Equal(new Value(true), d1.getValue());
    }

    [Theory]
    [InlineData("inc(5) <= 6", true)]
    [InlineData("dec 10 >= 10", false)]
    [InlineData("-5 <= +3", true)]
    [InlineData("not 5 <= 3", true)]
    [InlineData("true or false and false", true)]
    [InlineData("10 <> 10", false)]
    [InlineData("INC(a1) >= 1", true)]
    public void Variant67_OperationsAndPrecedence_EvaluateCorrectly(string expr, bool expected)
    {
        var ctrl = new SpreadsheetController(5, 5);
        ctrl.setCellExpression("B2", expr);
        Spreadsheet table = ctrl.calculate();

        Cell? b2 = table.getCell("B2");
        Assert.NotNull(b2);
        Assert.Null(b2.getError());
        Assert.Equal(new Value(expected), b2.getValue());
    }

    [Fact]
    public void BigInteger_ArbitraryLength_EvaluatesCorrectly()
    {
        var ctrl = new SpreadsheetController(5, 5);
        ctrl.setCellExpression("A1", "999999999999999999999999999999999999999999");
        ctrl.setCellExpression("A2", "inc(A1)");
        ctrl.setCellExpression("A3", "A2 >= A1 and A2 <> A1");
        Spreadsheet table = ctrl.calculate();

        Assert.Equal(BigInteger.Parse("1000000000000000000000000000000000000000000"), table.getCell("A2")!.getValue()!.asInteger());
        Assert.Equal(new Value(true), table.getCell("A3")!.getValue());
    }

    [Fact]
    public void ChainedFormulas_EvaluatedInTopologicalOrder()
    {
        var ctrl = new SpreadsheetController(5, 5);
        ctrl.setCellExpression("A1", "not B1");
        ctrl.setCellExpression("B1", "C1 <= 10");
        ctrl.setCellExpression("C1", "inc(9)");

        Spreadsheet table = ctrl.calculate();

        Assert.Equal(new Value(new BigInteger(10)), table.getCell("C1")!.getValue());
        Assert.Equal(new Value(true), table.getCell("B1")!.getValue());
        Assert.Equal(new Value(false), table.getCell("A1")!.getValue());
    }

    [Fact]
    public void CycleDetection_ReportsCycleErrorInBothCells()
    {
        var ctrl = new SpreadsheetController(5, 5);
        ctrl.setCellExpression("A1", "B1 <= 5");
        ctrl.setCellExpression("B1", "inc(A1)");

        Spreadsheet table = ctrl.calculate();

        Assert.NotNull(table.getCell("A1")!.getError());
        Assert.Contains("Циклічне посилання", table.getCell("A1")!.getError()!.getMessage());
        Assert.NotNull(table.getCell("B1")!.getError());
        Assert.Contains("Циклічне посилання", table.getCell("B1")!.getError()!.getMessage());
    }

    [Theory]
    [InlineData("5 and 3")]
    [InlineData("not A1")]
    [InlineData("inc(true)")]
    [InlineData("Z99 <= 5")]
    [InlineData("A1 = 5")]
    public void InvalidExpressions_ProduceLocalizedErrors(string invalidExpr)
    {
        var ctrl = new SpreadsheetController(5, 5);
        ctrl.setCellExpression("B1", invalidExpr);
        Spreadsheet table = ctrl.calculate();

        Cell? b1 = table.getCell("B1");
        Assert.NotNull(b1);
        Assert.NotNull(b1.getError());
        Assert.True(b1.getError()!.getPosition() >= 0);
    }

    [Fact]
    public void TableResize_AddAndDeleteRowColumn_RespectsMinimumSize()
    {
        var ctrl = new SpreadsheetController(2, 2);
        ctrl.setCellExpression("B2", "inc(5)");

        Assert.True(ctrl.deleteRow());
        Assert.Equal(1, ctrl.Table.RowCount);
        Assert.Null(ctrl.Table.getCell("B2"));
        Assert.False(ctrl.deleteRow()); // Нельзя удалить единственную строку

        Assert.True(ctrl.deleteColumn());
        Assert.Equal(1, ctrl.Table.ColumnCount);
        Assert.False(ctrl.deleteColumn()); // Нельзя удалить единственный столбец

        ctrl.addRow();
        ctrl.addColumn();
        Assert.Equal(2, ctrl.Table.RowCount);
        Assert.Equal(2, ctrl.Table.ColumnCount);
    }

    [Fact]
    public void SaveAndLoad_PreservesDimensionsExpressionsAndRecalculates()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_v67_{Guid.NewGuid()}.json");
        try
        {
            var ctrl = new SpreadsheetController(6, 4);
            ctrl.setCellExpression("A1", "70");
            ctrl.setCellExpression("B1", "67");
            ctrl.setCellExpression("C1", "A1 >= B1 and not (inc(B1) <= A1)");
            ctrl.saveToFile(tempFile);

            var loadedCtrl = new SpreadsheetController(2, 2);
            Spreadsheet loadedTable = loadedCtrl.loadFromFile(tempFile);

            Assert.Equal(6, loadedTable.RowCount);
            Assert.Equal(4, loadedTable.ColumnCount);
            Assert.Equal(new Value(false), loadedTable.getCell("C1")!.getValue());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}