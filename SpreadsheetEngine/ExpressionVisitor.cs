using System.Numerics;
using Antlr4.Runtime.Misc;

namespace SpreadsheetEngine;

public class EvaluationException : Exception
{
    public ExpressionError Error { get; }

    public EvaluationException(int position, string message) : base(message)
    {
        Error = new ExpressionError(position, message);
    }
}

public class ExpressionVisitor : ExpressionBaseVisitor<Value>
{
    private readonly Spreadsheet table;

    public ExpressionVisitor(Spreadsheet table)
    {
        this.table = table;
    }

    public Value visitCompileUnit(ExpressionParser.CompileUnitContext context) => VisitCompileUnit(context);
    public Value visitParenthesizedExpr(ExpressionParser.ParenthesizedExprContext context) => VisitParenthesizedExpr(context);
    public Value visitIncDecExpr(ExpressionParser.IncDecExprContext context) => VisitIncDecExpr(context);
    public Value visitUnaryExpr(ExpressionParser.UnaryExprContext context) => VisitUnaryExpr(context);
    public Value visitComparisonExpr(ExpressionParser.ComparisonExprContext context) => VisitComparisonExpr(context);
    public Value visitNotExpr(ExpressionParser.NotExprContext context) => VisitNotExpr(context);
    public Value visitAndExpr(ExpressionParser.AndExprContext context) => VisitAndExpr(context);
    public Value visitOrExpr(ExpressionParser.OrExprContext context) => VisitOrExpr(context);
    public Value visitBooleanExpr(ExpressionParser.BooleanExprContext context) => VisitBooleanExpr(context);
    public Value visitNumberExpr(ExpressionParser.NumberExprContext context) => VisitNumberExpr(context);
    public Value visitCellRefExpr(ExpressionParser.CellRefExprContext context) => VisitCellRefExpr(context);

    public override Value VisitCompileUnit([NotNull] ExpressionParser.CompileUnitContext context)
    {
        return Visit(context.expression());
    }

    public override Value VisitParenthesizedExpr([NotNull] ExpressionParser.ParenthesizedExprContext context)
    {
        return Visit(context.expression());
    }

    public override Value VisitIncDecExpr([NotNull] ExpressionParser.IncDecExprContext context)
    {
        Value val = Visit(context.expression());
        BigInteger num = RequireInteger(val, context.operatorToken.Column, context.operatorToken.Text);

        return context.operatorToken.Type == ExpressionLexer.INC
            ? new Value(num + BigInteger.One)
            : new Value(num - BigInteger.One);
    }

    public override Value VisitUnaryExpr([NotNull] ExpressionParser.UnaryExprContext context)
    {
        Value val = Visit(context.expression());
        BigInteger num = RequireInteger(val, context.operatorToken.Column, context.operatorToken.Text);

        return context.operatorToken.Type == ExpressionLexer.MINUS
            ? new Value(-num)
            : new Value(num);
    }

    public override Value VisitComparisonExpr([NotNull] ExpressionParser.ComparisonExprContext context)
    {
        Value leftVal = Visit(context.expression(0));
        Value rightVal = Visit(context.expression(1));

        int opCol = context.operatorToken.Column;
        string opText = context.operatorToken.Text;

        BigInteger left = RequireInteger(leftVal, opCol, opText);
        BigInteger right = RequireInteger(rightVal, opCol, opText);

        bool result = context.operatorToken.Type switch
        {
            ExpressionLexer.LE => left <= right,
            ExpressionLexer.GE => left >= right,
            ExpressionLexer.NE => left != right,
            _ => throw new EvaluationException(opCol, $"Невідомий оператор порівняння '{opText}'.")
        };

        return new Value(result);
    }

    public override Value VisitNotExpr([NotNull] ExpressionParser.NotExprContext context)
    {
        Value val = Visit(context.expression());
        bool flag = RequireLogical(val, context.Start.Column, "not");
        return new Value(!flag);
    }

    public override Value VisitAndExpr([NotNull] ExpressionParser.AndExprContext context)
    {
        Value leftVal = Visit(context.expression(0));
        Value rightVal = Visit(context.expression(1));

        int opCol = context.AND().Symbol.Column;
        bool left = RequireLogical(leftVal, opCol, "and");
        bool right = RequireLogical(rightVal, opCol, "and");

        return new Value(left && right);
    }

    public override Value VisitOrExpr([NotNull] ExpressionParser.OrExprContext context)
    {
        Value leftVal = Visit(context.expression(0));
        Value rightVal = Visit(context.expression(1));

        int opCol = context.OR().Symbol.Column;
        bool left = RequireLogical(leftVal, opCol, "or");
        bool right = RequireLogical(rightVal, opCol, "or");

        return new Value(left || right);
    }

    public override Value VisitBooleanExpr([NotNull] ExpressionParser.BooleanExprContext context)
    {
        bool isTrue = context.TRUE() != null;
        return new Value(isTrue);
    }

    public override Value VisitNumberExpr([NotNull] ExpressionParser.NumberExprContext context)
    {
        string text = context.NUMBER().GetText();
        if (!BigInteger.TryParse(text, out BigInteger number))
        {
            throw new EvaluationException(context.Start.Column, $"Некоректне ціле число '{text}'.");
        }
        return new Value(number);
    }

    public override Value VisitCellRefExpr([NotNull] ExpressionParser.CellRefExprContext context)
    {
        string address = context.IDENTIFIER().GetText().ToUpperInvariant();
        int col = context.Start.Column;

        if (!table.IsValidAddress(address))
        {
            throw new EvaluationException(col, $"Посилання на неіснуючу клітинку '{address}' (поза межами таблиці).");
        }

        Cell? cell = table.getCell(address);
        if (cell != null && cell.getError() != null)
        {
            throw new EvaluationException(col, $"Посилання на клітинку '{address}', яка містить помилку.");
        }

        return table.getValue(address);
    }

    private static BigInteger RequireInteger(Value val, int position, string opName)
    {
        if (val.IsLogical)
        {
            throw new EvaluationException(position, $"Операція '{opName}' вимагає цілого числа, але отримано логічне значення ({val}).");
        }
        return val.asInteger();
    }

    private static bool RequireLogical(Value val, int position, string opName)
    {
        if (!val.IsLogical)
        {
            throw new EvaluationException(position, $"Операція '{opName}' вимагає логічного значення, але отримано число ({val}).");
        }
        return val.asLogical();
    }
}