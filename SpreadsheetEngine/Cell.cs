using System.Numerics;

namespace SpreadsheetEngine;

public class Cell
{
    private string address;
    private string expression = string.Empty;
    private Value? value;
    private ExpressionError? error;

    public Cell(string address)
    {
        this.address = address.ToUpperInvariant();
    }

    public string getAddress() => address;
    internal void setAddress(string newAddress) => address = newAddress.ToUpperInvariant();

    public string getExpression() => expression;

    public bool setExpression(string text)
    {
        expression = text?.Trim() ?? string.Empty;
        error = null;

        if (IsPlainUnsignedInteger(expression) && BigInteger.TryParse(expression, out BigInteger num))
        {
            value = new Value(num);
        }
        else
        {
            value = null;
        }

        return true;
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

    public string getDisplayText(DisplayMode mode)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return string.Empty;
        }

        if (mode == DisplayMode.EXPRESSION)
        {
            return expression;
        }

        if (error != null)
        {
            return $"#ПОМИЛКА [{error.getType()}, поз. {error.getPosition() + 1}]";
        }

        return value?.ToString() ?? string.Empty;
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