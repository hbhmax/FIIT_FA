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
            ulong prod = (ulong)x[0] * y[0];
            if (prod < (1UL << 32))                             // ~ if (prod < 2³²)
                return new uint[] { (uint)prod };
            else
                return new uint[] { (uint)prod, (uint)(prod >> 32) };
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

    private static uint[] Add(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        int maxLen = Math.Max(a.Length, b.Length);
        var result = new uint[maxLen + 1];
        ulong carry = 0;
        for (int i = 0; i < maxLen; i++)
        {
            ulong sum = carry;
            if (i < a.Length) sum += a[i];
            if (i < b.Length) sum += b[i];
            result[i] = (uint)sum;
            carry = sum >> 32;
        }
        if (carry != 0)
            result[maxLen] = (uint)carry;
        else
            Array.Resize(ref result, maxLen);
        return result;
    }

    private static uint[] Subtract(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        var result = new uint[a.Length];
        long borrow = 0;
        for (int i = 0; i < a.Length; i++)
        {
            long diff = (long)a[i] - borrow;
            if (i < b.Length) diff -= b[i];
            if (diff < 0)
            {
                diff += 1L << 32;
                borrow = 1;
            }
            else borrow = 0;
            result[i] = (uint)diff;
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
        var result = new uint[digits.Length + shift];
        digits.CopyTo(result.AsSpan(shift));
        return result;

        //Таким образом, элемент digits[0] копируется в result[shift], digits[1] – в result[shift+1], и так далее. Первые shift слов (индексы 0..shift-1) остаются нулевыми
    }

    private static bool IsZero(BetterBigInteger num)
    {
        var digits = num.GetDigits();
        return digits.Length == 1 && digits[0] == 0;
    }
}