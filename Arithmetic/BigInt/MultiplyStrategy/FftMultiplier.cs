// ============================================================
// FftMultiplier.cs (исправленное переполнение)
// ============================================================
using Arithmetic.BigInt.Interfaces;
using System.Numerics;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class FftMultiplier : IMultiplier
{
    // Три модуля для NTT (подходят для произведения цифр до 2^64)
    private static readonly long[] Mods = { 998244353, 1004535809, 469762049 };
    private static readonly long[] Roots = { 3, 3, 3 };

    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        if (IsZero(a) || IsZero(b))
            return new BetterBigInteger(new uint[] { 0 }, false);

        bool isNegative = a.IsNegative ^ b.IsNegative;
        var aDigits = a.GetDigits();
        var bDigits = b.GetDigits();

        // Преобразуем в long[]
        var x = aDigits.ToArray().Select(d => (long)d).ToArray();
        var y = bDigits.ToArray().Select(d => (long)d).ToArray();

        // Выполняем NTT для каждого модуля
        var results = new long[3][];
        for (int i = 0; i < 3; i++)
        {
            var fa = x.Select(v => v % Mods[i]).ToArray();
            var fb = y.Select(v => v % Mods[i]).ToArray();
            results[i] = MultiplyNTTMod(fa, fb, Mods[i], Roots[i]);
        }

        // Восстанавливаем результат через КТО с использованием BigInteger
        var product = ChineseRemainderBigInteger(results[0], results[1], results[2]);

        // Переносы для получения uint[] (без переполнения)
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

    private static long[] MultiplyNTTMod(long[] a, long[] b, long mod, long root)
    {
        int n = 1;
        int totalLen = a.Length + b.Length;
        while (n < totalLen) n <<= 1;
        var fa = new long[n];
        var fb = new long[n];
        Array.Copy(a, fa, a.Length);
        Array.Copy(b, fb, b.Length);
        NTT(fa, false, mod, root);
        NTT(fb, false, mod, root);
        for (int i = 0; i < n; i++)
            fa[i] = fa[i] * fb[i] % mod;
        NTT(fa, true, mod, root);
        var result = new long[totalLen];
        Array.Copy(fa, result, totalLen);
        return result;
    }

    private static void NTT(long[] a, bool invert, long mod, long root)
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
            long wlen = PowMod(root, (mod - 1) / len, mod);
            if (invert) wlen = PowMod(wlen, mod - 2, mod);
            for (int i = 0; i < n; i += len)
            {
                long w = 1;
                for (int j = 0; j < len / 2; j++)
                {
                    long u = a[i + j];
                    long v = a[i + j + len / 2] * w % mod;
                    a[i + j] = (u + v) % mod;
                    a[i + j + len / 2] = (u - v + mod) % mod;
                    w = w * wlen % mod;
                }
            }
        }
        if (invert)
        {
            long invN = PowMod(n, mod - 2, mod);
            for (int i = 0; i < n; i++)
                a[i] = a[i] * invN % mod;
        }
    }

    private static long PowMod(long a, long b, long mod)
    {
        long res = 1;
        while (b > 0)
        {
            if ((b & 1) == 1) res = res * a % mod;
            a = a * a % mod;
            b >>= 1;
        }
        return res;
    }

    private static BigInteger[] ChineseRemainderBigInteger(long[] r1, long[] r2, long[] r3)
    {
        int len = Math.Max(r1.Length, Math.Max(r2.Length, r3.Length));
        var result = new BigInteger[len];
        BigInteger mod12 = (BigInteger)Mods[0] * Mods[1];
        BigInteger mod123 = mod12 * Mods[2];
        for (int i = 0; i < len; i++)
        {
            BigInteger v1 = i < r1.Length ? r1[i] : 0;
            BigInteger v2 = i < r2.Length ? r2[i] : 0;
            BigInteger v3 = i < r3.Length ? r3[i] : 0;
            // КТО для двух модулей
            BigInteger t = (v2 - v1) * ModInverseBigInteger(Mods[0], Mods[1]) % Mods[1];
            if (t < 0) t += Mods[1];
            BigInteger x12 = v1 + Mods[0] * t;
            // КТО для трёх
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