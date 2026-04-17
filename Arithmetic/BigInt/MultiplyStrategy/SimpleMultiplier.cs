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
            uint aVal = aDigits[i];
            for (int j = 0; j < bDigits.Length; j++)
            {
                uint bVal = bDigits[j];
                MultiplyWords(aVal, bVal, out uint high, out uint low);

                int index = i + j;
                uint sum = result[index] + low;
                uint carry = sum < result[index] ? 1u : 0u;
                result[index] = sum;

                if (high != 0 || carry != 0)
                {
                    AddWordToResult(result, index + 1, high + carry);
                }
            }
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
    private static void MultiplyWords(uint a, uint b, out uint high, out uint low)
    {
        uint aLo = a & 0xFFFF;
        uint aHi = a >> 16;
        uint bLo = b & 0xFFFF;
        uint bHi = b >> 16;

        uint loLo = aLo * bLo;
        uint loHi = aLo * bHi;
        uint hiLo = aHi * bLo;
        uint hiHi = aHi * bHi;

        uint midSum = (loLo >> 16) + (loHi & 0xFFFF);
        bool carry1 = midSum < (loLo >> 16);
        midSum += (hiLo & 0xFFFF);
        if (midSum < (hiLo & 0xFFFF)) carry1 = true;

        low = (midSum << 16) | (loLo & 0xFFFF);

        uint carry2 = midSum >> 16;
        uint totalCarry = (carry1 ? 1u : 0u) + carry2;

        high = hiHi + (loHi >> 16) + (hiLo >> 16) + totalCarry;
    }

    private static void AddWordToResult(uint[] result, int startIndex, uint value)
    {
        if (value == 0) return;
        uint carry = value;
        int i = startIndex;
        while (carry != 0 && i < result.Length)
        {
            uint sum = result[i] + carry;
            carry = sum < result[i] ? 1u : 0u;
            result[i] = sum;
            i++;
        }
    }

    private static bool IsZero(ReadOnlySpan<uint> digits)
        => digits.Length == 1 && digits[0] == 0;
}