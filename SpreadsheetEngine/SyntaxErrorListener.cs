using Antlr4.Runtime;

namespace SpreadsheetEngine;

public class SyntaxErrorListener : BaseErrorListener, IAntlrErrorListener<int>
{
    private readonly List<ExpressionError> errors = new();

    public void syntaxError(int line, int column, string message)
    {
        errors.Add(new ExpressionError(column, message));
    }

    public List<ExpressionError> getErrors() => errors;

    // Перехват лексических ошибок (неизвестные символы, например '@', '=')
    public void SyntaxError(
        TextWriter output,
        IRecognizer recognizer,
        int offendingSymbol,
        int line,
        int charPositionInLine,
        string msg,
        RecognitionException e)
    {
        syntaxError(line, charPositionInLine, $"Невідомий символ або лексична помилка ({msg})");
    }

    // Перехват синтаксических ошибок парсера
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
            ? "Неочікуваний кінець виразу (вираз незавершено)"
            : $"Синтаксична помилка біля '{tokenText}'";

        syntaxError(line, charPositionInLine, cleanMsg);
    }
}