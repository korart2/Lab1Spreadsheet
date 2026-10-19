using System.Numerics;

namespace SpreadsheetEngine;

public class Cell
{
    private readonly string address;
    private string expression = string.Empty;
    private Value? value;
    private ExpressionError? error;

    public Cell(string address)
    {
        this.address = address.ToUpperInvariant();
    }

    public string getAddress() => address;
    public string getExpression() => expression;

    public void setExpression(string expression)
    {
        this.expression = expression?.Trim() ?? string.Empty;
        this.error = null;

        if (IsPlainUnsignedInteger(this.expression) && BigInteger.TryParse(this.expression, out BigInteger num))
        {
            this.value = new Value(num);
        }
        else
        {
            this.value = null;
        }
    }

    public Value? getValue() => value;
    public void setValue(Value? value) => this.value = value;

    public ExpressionError? getError() => error;
    public void setError(ExpressionError? error) => this.error = error;

    public bool isFormula()
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }
        return !IsPlainUnsignedInteger(expression);
    }

    public void clear()
    {
        expression = string.Empty;
        value = null;
        error = null;
    }

    private static bool IsPlainUnsignedInteger(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] < '0' || text[i] > '9') return false;
        }
        return true;
    }
}