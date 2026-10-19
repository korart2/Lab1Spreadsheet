using System.Numerics;

namespace SpreadsheetEngine;

public class Value : IEquatable<Value>
{
    private readonly bool isLogical;
    private readonly BigInteger number;
    private readonly bool flag;

    public Value(BigInteger number)
    {
        isLogical = false;
        this.number = number;
        flag = false;
    }

    public Value(bool flag)
    {
        isLogical = true;
        number = BigInteger.Zero;
        this.flag = flag;
    }

    public bool IsLogical => isLogical;

    public BigInteger asInteger()
    {
        if (isLogical)
        {
            throw new InvalidOperationException("Очікувалося ціле число, але отримано логічне значення.");
        }
        return number;
    }

    public bool asLogical()
    {
        if (!isLogical)
        {
            throw new InvalidOperationException($"Очікувалося логічне значення, але отримано число {number}.");
        }
        return flag;
    }

    public override string ToString() => isLogical ? (flag ? "true" : "false") : number.ToString();

    public bool Equals(Value? other)
    {
        if (other is null) return false;
        return isLogical == other.isLogical && number == other.number && flag == other.flag;
    }

    public override bool Equals(object? obj) => Equals(obj as Value);
    public override int GetHashCode() => HashCode.Combine(isLogical, number, flag);
}