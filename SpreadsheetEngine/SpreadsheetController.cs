using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace SpreadsheetEngine;

public class SpreadsheetController
{
    private Spreadsheet table;
    private readonly SpreadsheetStorage storage;
    private IParseTree? currentTree;

    private enum VisitState
    {
        Unvisited,
        Visiting,
        Visited
    }

    public SpreadsheetController(int rows = 10, int cols = 8)
    {
        table = new Spreadsheet(rows, cols);
        storage = new SpreadsheetStorage();
    }

    public Spreadsheet getTable() => table;

    public Spreadsheet calculate()
    {
        List<Cell> formulaCells = table.getFormulaCells();
        var parsedTrees = new Dictionary<string, IParseTree>();
        var states = new Dictionary<string, VisitState>();

        foreach (Cell cell in formulaCells)
        {
            cell.setValue(null);
            cell.setError(null);

            List<ExpressionError> syntaxErrors = checkSyntax(cell.getExpression());
            if (syntaxErrors.Count > 0)
            {
                cell.setError(syntaxErrors[0]);
                states[cell.getAddress()] = VisitState.Visited;
            }
            else if (currentTree != null)
            {
                parsedTrees[cell.getAddress()] = currentTree;
                states[cell.getAddress()] = VisitState.Unvisited;
            }
        }

        foreach (Cell cell in formulaCells)
        {
            string addr = cell.getAddress();
            if (states.TryGetValue(addr, out VisitState state) && state == VisitState.Unvisited)
            {
                EvaluateCellDfs(cell, parsedTrees, states);
            }
        }

        return table;
    }

    public bool saveTable(string path) => storage.save(table, path);

    public Spreadsheet openTable(string path)
    {
        table = storage.load(path);
        return calculate();
    }

    private List<ExpressionError> checkSyntax(string expression)
    {
        var inputStream = new AntlrInputStream(expression);
        var lexer = new ExpressionLexer(inputStream);
        var listener = new SyntaxErrorListener();

        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(listener);

        var tokenStream = new CommonTokenStream(lexer);
        var parser = new ExpressionParser(tokenStream);

        parser.RemoveErrorListeners();
        parser.AddErrorListener(listener);

        currentTree = parser.compileUnit();
        return listener.getErrors();
    }

    private Value evaluate()
    {
        if (currentTree == null)
        {
            throw new InvalidOperationException("Дерево розбору відсутнє.");
        }
        var visitor = new ExpressionVisitor(table);
        return visitor.Visit(currentTree);
    }

    private void EvaluateCellDfs(
        Cell cell,
        Dictionary<string, IParseTree> parsedTrees,
        Dictionary<string, VisitState> states)
    {
        string addr = cell.getAddress();
        states[addr] = VisitState.Visiting;

        IParseTree tree = parsedTrees[addr];
        var references = new List<(string TargetAddress, int Position)>();
        CollectCellReferences(tree, references);

        foreach (var (targetAddr, pos) in references)
        {
            if (!table.IsValidAddress(targetAddr))
            {
                cell.setError(new ExpressionError("Помилка посилання", pos, $"Посилання на неіснуючу клітинку '{targetAddr}' (поза межами таблиці)."));
                states[addr] = VisitState.Visited;
                return;
            }

            if (states.TryGetValue(targetAddr, out VisitState targetState))
            {
                if (targetState == VisitState.Visiting)
                {
                    cell.setError(new ExpressionError("Циклічне посилання", pos, $"Циклічне посилання на клітинку '{targetAddr}'."));
                    states[addr] = VisitState.Visited;
                    return;
                }

                if (targetState == VisitState.Unvisited)
                {
                    Cell? targetCell = table.getCell(targetAddr);
                    if (targetCell != null)
                    {
                        EvaluateCellDfs(targetCell, parsedTrees, states);
                    }
                }
            }

            Cell? resolvedTarget = table.getCell(targetAddr);
            if (resolvedTarget != null && resolvedTarget.getError() != null)
            {
                ExpressionError targetErr = resolvedTarget.getError()!;
                if (targetErr.getType() == "Циклічне посилання")
                {
                    cell.setError(new ExpressionError("Циклічне посилання", pos, $"Циклічне посилання через клітинку '{targetAddr}'."));
                }
                else
                {
                    cell.setError(new ExpressionError("Помилка посилання", pos, $"Посилання на клітинку '{targetAddr}', яка містить помилку."));
                }
                states[addr] = VisitState.Visited;
                return;
            }
        }

        try
        {
            currentTree = tree;
            Value result = evaluate();
            cell.setValue(result);
        }
        catch (EvaluationException ex)
        {
            cell.setError(ex.Error);
        }

        states[addr] = VisitState.Visited;
    }

    private static void CollectCellReferences(IParseTree node, List<(string TargetAddress, int Position)> refs)
    {
        if (node is ExpressionParser.CellRefExprContext cellRef)
        {
            string addr = cellRef.IDENTIFIER().GetText().ToUpperInvariant();
            int pos = cellRef.Start.Column;
            refs.Add((addr, pos));
        }

        for (int i = 0; i < node.ChildCount; i++)
        {
            CollectCellReferences(node.GetChild(i), refs);
        }
    }
}