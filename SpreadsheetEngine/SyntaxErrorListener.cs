using Antlr4.Runtime;

namespace SpreadsheetEngine;

public class SyntaxErrorListener : BaseErrorListener, IAntlrErrorListener<int>
{
    private readonly List<ExpressionError> errors = new();

    public void syntaxError(int line, int position, string message)
    {
        errors.Add(new ExpressionError("Синтаксична помилка", position, message));
    }

    public List<ExpressionError> getErrors() => errors;

    public void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        int offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        string input = recognizer.InputStream?.ToString() ?? string.Empty;
        if (charPositionInLine >= 0
            && charPositionInLine < input.Length
            && input[charPositionInLine..].StartsWith("#REF!", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new ExpressionError("Помилка посилання", charPositionInLine, "Посилання на видалену клітинку (#REF!)."));
            return;
        }

        errors.Add(new ExpressionError("Лексична помилка", charPositionInLine, $"Невідомий символ у виразі ({msg})."));
    }

    public override void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        IToken offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        string tokenText = offendingSymbol?.Text ?? "<EOF>";
        string cleanMsg = tokenText == "<EOF>"
            ? "Неочікуваний кінець виразу (вираз незавершено)."
            : $"Неочікуваний токен '{tokenText}'.";

        syntaxError(line, charPositionInLine, cleanMsg);
    }
}