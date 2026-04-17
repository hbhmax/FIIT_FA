using Arithmetic.BigInt.Interfaces;
using System.Numerics;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class FftMultiplier : IMultiplier
{
    private static readonly BigInteger[] Mods = { 998244353, 1004535809, 469762049 };
    private static readonly BigInteger[] Roots = { 3, 3, 3 };

    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        if (IsZero(a) || IsZero(b))
            return new BetterBigInteger(new uint[] { 0 }, false);

        bool isNegative = a.IsNegative ^ b.IsNegative;
        var aDigits = a.GetDigits();
        var bDigits = b.GetDigits();

        var x = aDigits.ToArray().Select(d => (BigInteger)d).ToArray();
        var y = bDigits.ToArray().Select(d => (BigInteger)d).ToArray();

        var results = new BigInteger[3][];
        for (int i = 0; i < 3; i++)
        {
            var fa = x.Select(v => v % Mods[i]).ToArray();
            var fb = y.Select(v => v % Mods[i]).ToArray();
            results[i] = MultiplyNTTMod(fa, fb, Mods[i], Roots[i]);
        }

        var product = ChineseRemainderBigInteger(results[0], results[1], results[2]);

        var resultDigits = new List<uint>();
        BigInteger carry = 0;
        for (int i = 0; i < product.Length; i++)
        {
            carry += product[i];
            resultDigits.Add((uint)(carry & 0xFFFFFFFF));
            carry >>= 32;
        }
        while (carry > 0)
        {
            resultDigits.Add((uint)(carry & 0xFFFFFFFF));
            carry >>= 32;
        }
        while (resultDigits.Count > 1 && resultDigits[^1] == 0)
            resultDigits.RemoveAt(resultDigits.Count - 1);

        return new BetterBigInteger(resultDigits.ToArray(), isNegative);
    }

    private static BigInteger[] MultiplyNTTMod(BigInteger[] a, BigInteger[] b, BigInteger mod, BigInteger root)
    {
        int n = 1;
        int totalLen = a.Length + b.Length;
        while (n < totalLen) n <<= 1;
        var fa = new BigInteger[n];
        var fb = new BigInteger[n];
        Array.Copy(a, fa, a.Length);
        Array.Copy(b, fb, b.Length);
        NTT(fa, false, mod, root);
        NTT(fb, false, mod, root);
        for (int i = 0; i < n; i++)
            fa[i] = (fa[i] * fb[i]) % mod;
        NTT(fa, true, mod, root);
        var result = new BigInteger[totalLen];
        Array.Copy(fa, result, totalLen);
        return result;
    }

    private static void NTT(BigInteger[] a, bool invert, BigInteger mod, BigInteger root)
    {
        int n = a.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; j >= bit; bit >>= 1) j -= bit;
            j += bit;
            if (i < j) (a[i], a[j]) = (a[j], a[i]);
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            BigInteger wlen = PowMod(root, (mod - 1) / len, mod);
            if (invert) wlen = PowMod(wlen, mod - 2, mod);
            for (int i = 0; i < n; i += len)
            {
                BigInteger w = 1;
                for (int j = 0; j < len / 2; j++)
                {
                    BigInteger u = a[i + j];
                    BigInteger v = (a[i + j + len / 2] * w) % mod;
                    a[i + j] = (u + v) % mod;
                    a[i + j + len / 2] = (u - v + mod) % mod;
                    w = (w * wlen) % mod;
                }
            }
        }
        if (invert)
        {
            BigInteger invN = PowMod(n, mod - 2, mod);
            for (int i = 0; i < n; i++)
                a[i] = (a[i] * invN) % mod;
        }
    }

    private static BigInteger PowMod(BigInteger a, BigInteger b, BigInteger mod)
    {
        BigInteger res = 1;
        while (b > 0)
        {
            if ((b & 1) == 1) res = (res * a) % mod;
            a = (a * a) % mod;
            b >>= 1;
        }
        return res;
    }

    private static BigInteger[] ChineseRemainderBigInteger(BigInteger[] r1, BigInteger[] r2, BigInteger[] r3)
    {
        int len = Math.Max(r1.Length, Math.Max(r2.Length, r3.Length));
        var result = new BigInteger[len];
        BigInteger mod12 = Mods[0] * Mods[1];
        for (int i = 0; i < len; i++)
        {
            BigInteger v1 = i < r1.Length ? r1[i] : 0;
            BigInteger v2 = i < r2.Length ? r2[i] : 0;
            BigInteger v3 = i < r3.Length ? r3[i] : 0;
            BigInteger t = (v2 - v1) * ModInverseBigInteger(Mods[0], Mods[1]) % Mods[1];
            if (t < 0) t += Mods[1];
            BigInteger x12 = v1 + Mods[0] * t;
            t = (v3 - x12 % Mods[2]) * ModInverseBigInteger(mod12 % Mods[2], Mods[2]) % Mods[2];
            if (t < 0) t += Mods[2];
            result[i] = x12 + mod12 * t;
        }
        return result;
    }

    private static BigInteger ModInverseBigInteger(BigInteger a, BigInteger mod)
    {
        BigInteger t = 0, newT = 1;
        BigInteger r = mod, newR = a;
        while (newR != 0)
        {
            var quotient = r / newR;
            (t, newT) = (newT, t - quotient * newT);
            (r, newR) = (newR, r - quotient * newR);
        }
        if (r > 1) throw new InvalidOperationException("Not invertible");
        if (t < 0) t += mod;
        return t;
    }

    private static bool IsZero(BetterBigInteger num)
    {
        var digits = num.GetDigits();
        return digits.Length == 1 && digits[0] == 0;
    }
}