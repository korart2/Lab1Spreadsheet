namespace SpreadsheetEngine;

public class ExpressionError
{
    private readonly int position;
    private readonly string message;

    public ExpressionError(int position, string message)
    {
        this.position = position;
        this.message = message;
    }

    public int getPosition() => position;
    public string getMessage() => message;

    public override string ToString() => $"Позиція {position + 1}: {message}";
}