using System.Numerics;
using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class FftMultiplier : IMultiplier
{
    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        if (a.IsZero() || b.IsZero())
            return new BetterBigInteger(new uint[] { 0 }, false);

        bool resultSign = a.IsNegative ^ b.IsNegative;

        uint[] aDigits = a.GetDigits().ToArray();
        uint[] bDigits = b.GetDigits().ToArray();

        BigInteger bigA = BigIntegerFromDigits(aDigits);
        BigInteger bigB = BigIntegerFromDigits(bDigits);

        int aBitLen = GetBitLength(bigA);
        int bBitLen = GetBitLength(bigB);
        if (aBitLen == 0 || bBitLen == 0)
            return new BetterBigInteger(new uint[] { 0 }, false);

        const int K = 30;
        BigInteger mask = (BigInteger.One << K) - 1;

        int lenA = (aBitLen + K - 1) / K;
        int lenB = (bBitLen + K - 1) / K;
        int convLen = lenA + lenB - 1;

        int L = 1;
        while (L < convLen) L <<= 1;

        BigInteger maxCoeff = mask;
        BigInteger maxVal = new BigInteger(convLen) * maxCoeff * maxCoeff;

        int N = L;
        while ((BigInteger.One << N) + 1 <= maxVal)
            N <<= 1;

        BigInteger M = (BigInteger.One << N) + 1;

        BigInteger psi = BigInteger.ModPow(2, N / L, M);
        BigInteger omega = psi * psi % M;

        BigInteger[] aCoeffs = new BigInteger[L];
        BigInteger[] bCoeffs = new BigInteger[L];
        for (int i = 0; i < lenA; i++)
            aCoeffs[i] = (bigA >> (i * K)) & mask;
        for (int i = 0; i < lenB; i++)
            bCoeffs[i] = (bigB >> (i * K)) & mask;

        {
            BigInteger w = 1;
            for (int i = 0; i < L; i++)
            {
                aCoeffs[i] = aCoeffs[i] * w % M;
                bCoeffs[i] = bCoeffs[i] * w % M;
                w = w * psi % M;
            }
        }

        Fft(aCoeffs, false, omega, M);
        Fft(bCoeffs, false, omega, M);

        for (int i = 0; i < L; i++)
            aCoeffs[i] = aCoeffs[i] * bCoeffs[i] % M;

        Fft(aCoeffs, true, omega, M);

        BigInteger invL = ModInverse(new BigInteger(L), M);
        BigInteger invPsi = ModInverse(psi, M);
        {
            BigInteger wInv = 1;
            for (int i = 0; i < L; i++)
            {
                aCoeffs[i] = aCoeffs[i] * wInv % M * invL % M;
                wInv = wInv * invPsi % M;
            }
        }

        BigInteger carry = 0;
        for (int i = 0; i < convLen; i++)
        {
            BigInteger val = aCoeffs[i] + carry;
            carry = val >> K;
            aCoeffs[i] = val & mask;
        }
        int resultLength = convLen;
        while (carry > 0)
        {
            if (resultLength >= aCoeffs.Length)
                Array.Resize(ref aCoeffs, aCoeffs.Length * 2);
            aCoeffs[resultLength] = carry & mask;
            carry >>= K;
            resultLength++;
        }

        BigInteger product = 0;
        for (int i = 0; i < resultLength; i++)
            product += aCoeffs[i] << (i * K);

        return new BetterBigInteger(BigIntegerToUIntArray(product), resultSign);
    }

    private static void Fft(BigInteger[] data, bool invert, BigInteger omega, BigInteger M)
    {
        int n = data.Length;

        BitReverse(data);

        BigInteger omegaInv = ModInverse(omega, M);

        for (int len = 2; len <= n; len <<= 1)
        {
            int half = len >> 1;
            BigInteger wlen = invert
                ? BigInteger.ModPow(omegaInv, n / len, M)
                : BigInteger.ModPow(omega, n / len, M);

            for (int i = 0; i < n; i += len)
            {
                BigInteger w = 1;
                for (int j = 0; j < half; j++)
                {
                    BigInteger u = data[i + j];
                    BigInteger v = data[i + j + half] * w % M;
                    data[i + j] = (u + v) % M;
                    data[i + j + half] = (u - v + M) % M;
                    w = w * wlen % M;
                }
            }
        }
    }

    private static void BitReverse(BigInteger[] data)
    {
        int n = data.Length;
        int j = 0;
        for (int i = 0; i < n; i++)
        {
            if (i < j)
                (data[i], data[j]) = (data[j], data[i]);
            int bit = n >> 1;
            while (j >= bit && bit > 0)
            {
                j -= bit;
                bit >>= 1;
            }
            j += bit;
        }
    }

    private static BigInteger BigIntegerFromDigits(uint[] digits)
    {
        byte[] bytes = new byte[digits.Length * 4 + 1];
        Buffer.BlockCopy(digits, 0, bytes, 0, digits.Length * 4);
        return new BigInteger(bytes);
    }

    private static uint[] BigIntegerToUIntArray(BigInteger value)
    {
        if (value.IsZero)
            return new uint[] { 0 };

        byte[] bytes = value.ToByteArray();
        if (bytes[bytes.Length - 1] == 0)
            Array.Resize(ref bytes, bytes.Length - 1);

        int uintLength = (bytes.Length + 3) / 4;
        uint[] result = new uint[uintLength];
        Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
        return result;
    }

    private static int GetBitLength(BigInteger x)
    {
        if (x.IsZero) return 0;
        byte[] bytes = x.ToByteArray();
        int highByte = bytes[bytes.Length - 1];
        int bits = (bytes.Length - 1) * 8;
        while (highByte != 0)
        {
            bits++;
            highByte >>= 1;
        }
        return bits;
    }

    private static BigInteger ModInverse(BigInteger a, BigInteger mod)
    {
        BigInteger old_r = a, r = mod;
        BigInteger old_s = 1, s = 0;
        while (r != 0)
        {
            BigInteger q = old_r / r;
            BigInteger temp = r;
            r = old_r - q * r;
            old_r = temp;
            temp = s;
            s = old_s - q * s;
            old_s = temp;
        }
        if (old_r != 1)
            throw new ArithmeticException("Element is not invertible in the ring.");
        if (old_s < 0)
            old_s += mod;
        return old_s;
    }
}