using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class SimpleMultiplier : IMultiplier
{
    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        var aDigits = a.GetDigits();
        var bDigits = b.GetDigits();

        if (IsZero(aDigits) || IsZero(bDigits))
            return new BetterBigInteger(new uint[] { 0 }, false);

        uint[] result = new uint[aDigits.Length + bDigits.Length];

        for (int i = 0; i < aDigits.Length; i++)
        {
            if (aDigits[i] == 0) continue;
            ulong carry = 0;
            for (int j = 0; j < bDigits.Length; j++)
            {
                ulong product = (ulong)aDigits[i] * bDigits[j] + result[i + j] + carry;
                result[i + j] = (uint)product;
                carry = product >> 32;
            }
            if (carry != 0)
                result[i + bDigits.Length] += (uint)carry;
        }

        int len = result.Length;
        while (len > 0 && result[len - 1] == 0)
            len--;
        if (len == 0)
            return new BetterBigInteger(new uint[] { 0 }, false);
        Array.Resize(ref result, len);

        bool isNegative = a.IsNegative ^ b.IsNegative;
        return new BetterBigInteger(result, isNegative);
    }

    private static bool IsZero(ReadOnlySpan<uint> digits)
        => digits.Length == 1 && digits[0] == 0;
}