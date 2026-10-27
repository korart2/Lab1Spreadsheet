using System.Numerics;
using SpreadsheetEngine;

namespace SpreadsheetEngine.Tests;

public class Variant67Tests
{
    [Fact]
    public void SequenceDiagramScenario_WorksExactlyAsDesigned()
    {
        var ctrl = new SpreadsheetController(10, 10);
        Spreadsheet table = ctrl.getTable();

        table.getCell("A1")!.setExpression("70");
        table.getCell("B1")!.setExpression("67");
        table.getCell("C1")!.setExpression("1000");

        table.getCell("D1")!.setExpression("A1 >=");
        ctrl.calculate();

        Cell? d1 = table.getCell("D1");
        Assert.NotNull(d1);
        Assert.NotNull(d1.getError());
        Assert.Equal("Синтаксична помилка", d1.getError()!.getType());
        Assert.Equal(5, d1.getError()!.getPosition());

        table.getCell("D1")!.setExpression("A1 >= B1 and C1 >= 1");
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
        ctrl.getTable().getCell("B2")!.setExpression(expr);
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
        Spreadsheet table = ctrl.getTable();
        table.getCell("A1")!.setExpression("999999999999999999999999999999999999999999");
        table.getCell("A2")!.setExpression("inc(A1)");
        table.getCell("A3")!.setExpression("A2 >= A1 and A2 <> A1");
        ctrl.calculate();

        Assert.Equal(BigInteger.Parse("1000000000000000000000000000000000000000000"), table.getCell("A2")!.getValue()!.asInteger());
        Assert.Equal(new Value(true), table.getCell("A3")!.getValue());
    }

    [Fact]
    public void DeleteArbitraryRowAndColumn_ShiftsCellsAndUpdatesFormulaReferences()
    {
        var ctrl = new SpreadsheetController(5, 5);
        Spreadsheet table = ctrl.getTable();

        table.getCell("A1")!.setExpression("10");
        table.getCell("A2")!.setExpression("20");
        table.getCell("A3")!.setExpression("70");
        table.getCell("A4")!.setExpression("inc(A3)");

        Assert.True(table.deleteRow(2));
        ctrl.calculate();

        Assert.Equal("70", table.getCell("A2")!.getExpression());
        Assert.Equal("inc(A2)", table.getCell("A3")!.getExpression());
        Assert.Equal(new Value(new BigInteger(71)), table.getCell("A3")!.getValue());

        Assert.True(table.deleteRow(2));
        ctrl.calculate();

        Cell? shiftedFormulaCell = table.getCell("A2");
        Assert.NotNull(shiftedFormulaCell!.getError());
        Assert.Equal("Помилка посилання", shiftedFormulaCell.getError()!.getType());
    }

    [Fact]
    public void InsertArbitraryColumn_ShiftsCellsAndUpdatesFormulaReferences()
    {
        var ctrl = new SpreadsheetController(5, 5);
        Spreadsheet table = ctrl.getTable();

        table.getCell("B1")!.setExpression("50");
        table.getCell("C1")!.setExpression("B1 >= 40");

        table.addColumn(2);
        ctrl.calculate();

        Assert.Equal("50", table.getCell("C1")!.getExpression());
        Assert.Equal("C1 >= 40", table.getCell("D1")!.getExpression());
        Assert.Equal(new Value(true), table.getCell("D1")!.getValue());
    }

    [Fact]
    public void CycleDetection_ReportsCycleErrorTypeInBothCells()
    {
        var ctrl = new SpreadsheetController(5, 5);
        Spreadsheet table = ctrl.getTable();
        table.getCell("A1")!.setExpression("B1 <= 5");
        table.getCell("B1")!.setExpression("inc(A1)");

        ctrl.calculate();

        Assert.Equal("Циклічне посилання", table.getCell("A1")!.getError()!.getType());
        Assert.Equal("Циклічне посилання", table.getCell("B1")!.getError()!.getType());
    }

    [Theory]
    [InlineData("5 and 3", "Помилка типів")]
    [InlineData("not A1", "Помилка типів")]
    [InlineData("inc(true)", "Помилка типів")]
    [InlineData("Z99 <= 5", "Помилка посилання")]
    [InlineData("A1 = 5", "Лексична помилка")]
    public void InvalidExpressions_ProduceSpecificErrorTypeAndPosition(string invalidExpr, string expectedType)
    {
        var ctrl = new SpreadsheetController(5, 5);
        ctrl.getTable().getCell("B1")!.setExpression(invalidExpr);
        Spreadsheet table = ctrl.calculate();

        Cell? b1 = table.getCell("B1");
        Assert.NotNull(b1?.getError());
        Assert.Equal(expectedType, b1!.getError()!.getType());
        Assert.True(b1.getError()!.getPosition() >= 0);
    }

    [Fact]
    public void SaveAndLoad_PreservesDimensionsExpressionsAndRecalculates()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"test_v67_{Guid.NewGuid()}.json");
        try
        {
            var ctrl = new SpreadsheetController(6, 4);
            ctrl.getTable().getCell("A1")!.setExpression("70");
            ctrl.getTable().getCell("B1")!.setExpression("67");
            ctrl.getTable().getCell("C1")!.setExpression("A1 >= B1 and not (inc(B1) <= A1)");
            ctrl.saveTable(tempFile);

            var loadedCtrl = new SpreadsheetController(2, 2);
            Spreadsheet loadedTable = loadedCtrl.openTable(tempFile);

            Assert.Equal(6, loadedTable.getRowCount());
            Assert.Equal(4, loadedTable.getColumnCount());
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