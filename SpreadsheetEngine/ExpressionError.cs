namespace SpreadsheetEngine;

public class ExpressionError
{
    private readonly string type;
    private readonly int position;
    private readonly string message;

    public ExpressionError(string type, int position, string message)
    {
        this.type = type;
        this.position = position;
        this.message = message;
    }

    public string getType() => type;
    public int getPosition() => position;
    public string getMessage() => message;

    public override string ToString() => $"{type} (поз. {position + 1}): {message}";
}