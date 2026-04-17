using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class KaratsubaMultiplier : IMultiplier
{
    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        if (IsZero(a) || IsZero(b))
            return new BetterBigInteger(new uint[] { 0 }, false);

        bool isNegative = a.IsNegative ^ b.IsNegative;
        var aDigits = a.GetDigits();
        var bDigits = b.GetDigits();
        var product = MultiplyCore(aDigits, bDigits);

        int len = product.Length;
        while (len > 0 && product[len - 1] == 0) len--;
        if (len == 0)
            return new BetterBigInteger(new uint[] { 0 }, false);
        if (len < product.Length)
            Array.Resize(ref product, len);

        return new BetterBigInteger(product, isNegative);
    }

    private static uint[] MultiplyCore(ReadOnlySpan<uint> x, ReadOnlySpan<uint> y)
    {
        if (x.Length == 0 || y.Length == 0 || (x.Length == 1 && x[0] == 0) || (y.Length == 1 && y[0] == 0))
            return new uint[] { 0 };

        if (x.Length == 1 && y.Length == 1)
        {
            MultiplyWords(x[0], y[0], out uint high, out uint low);
            if (high == 0)
                return new uint[] { low };
            else
                return new uint[] { low, high };
        }

        int n = Math.Max(x.Length, y.Length);
        int m = n / 2;

        var x0 = x.Length <= m ? ReadOnlySpan<uint>.Empty : x[0..Math.Min(m, x.Length)];
        var x1 = x.Length <= m ? ReadOnlySpan<uint>.Empty : x[Math.Min(m, x.Length)..];
        var y0 = y.Length <= m ? ReadOnlySpan<uint>.Empty : y[0..Math.Min(m, y.Length)];
        var y1 = y.Length <= m ? ReadOnlySpan<uint>.Empty : y[Math.Min(m, y.Length)..];

        var z0 = MultiplyCore(x0, y0);                    // A0*B0
        var z2 = MultiplyCore(x1, y1);                    // A1*B1
        var xSum = Add(x0, x1);                           // A0+A1
        var ySum = Add(y0, y1);                           // B0+B1
        var z1 = MultiplyCore(xSum, ySum);                // (A0+A1)*(B0+B1)

        z1 = Subtract(z1, z0);
        z1 = Subtract(z1, z2);

        var result = Add(z0, ShiftLeft(z1, m));
        result = Add(result, ShiftLeft(z2, 2 * m));
        return result;
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

    private static uint[] Add(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        int maxLen = Math.Max(a.Length, b.Length);
        uint[] result = new uint[maxLen + 1];
        uint carry = 0;
        for (int i = 0; i < maxLen; i++)
        {
            uint av = i < a.Length ? a[i] : 0u;
            uint bv = i < b.Length ? b[i] : 0u;

            uint sum = av + carry;
            bool carry1 = sum < av;
            sum += bv;
            bool carry2 = sum < bv;

            result[i] = sum;
            carry = (carry1 || carry2) ? 1u : 0u;
        }
        if (carry != 0)
            result[maxLen] = carry;
        else
            Array.Resize(ref result, maxLen);
        return result;
    }

    private static uint[] Subtract(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        uint[] result = new uint[a.Length];
        uint borrow = 0;
        for (int i = 0; i < a.Length; i++)
        {
            uint av = a[i];
            uint bv = i < b.Length ? b[i] : 0u;

            uint diff = av - borrow;
            bool borrow1 = diff > av;
            uint diff2 = diff - bv;
            bool borrow2 = diff2 > diff;

            result[i] = diff2;
            borrow = (borrow1 || borrow2) ? 1u : 0u;
        }

        int len = result.Length;
        while (len > 0 && result[len - 1] == 0) len--;
        if (len == 0) return new uint[] { 0 };
        if (len < result.Length) Array.Resize(ref result, len);
        return result;
    }

    private static uint[] ShiftLeft(ReadOnlySpan<uint> digits, int shift)
    {
        if (shift == 0) return digits.ToArray();
        uint[] result = new uint[digits.Length + shift];
        digits.CopyTo(result.AsSpan(shift));
        return result;
    }

    private static bool IsZero(BetterBigInteger num)
    {
        var digits = num.GetDigits();
        return digits.Length == 1 && digits[0] == 0;
    }
}