using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Arithmetic.BigInt.Interfaces;
using Arithmetic.BigInt.MultiplyStrategy;

namespace Arithmetic.BigInt;

public sealed class BetterBigInteger : IBigInteger
{
    private int _signBit;
    private uint _smallValue;
    private uint[]? _data;

    public bool IsNegative => _signBit == 1;

    public BetterBigInteger(uint[] digits, bool isNegative = false)
    {
        int len = digits.Length;
        while (len > 0 && digits[len - 1] == 0) len--;
        if (len == 0)
        {
            _signBit = 0;
            _smallValue = 0;
            _data = null;
            return;
        }

        if (len == 1)
        {
            _signBit = isNegative ? 1 : 0;
            _smallValue = digits[0];
            _data = null;
        }
        else
        {
            _signBit = isNegative ? 1 : 0;
            _data = new uint[len];
            Array.Copy(digits, 0, _data, 0, len);
            _smallValue = 0;
        }
    }

    public BetterBigInteger(IEnumerable<uint> digits, bool isNegative = false) : this(digits.ToArray(), isNegative) { }

    public BetterBigInteger(string value, int radix)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be null or whitespace.");
        if (radix < 2 || radix > 36)
            throw new ArgumentOutOfRangeException(nameof(radix), "Radix must be between 2 and 36.");

        value = value.Trim();
        int startIndex = 0;
        bool negative = false;
        if (value[0] == '-')
        {
            negative = true;
            startIndex = 1;
        }
        else if (value[0] == '+')
        {
            startIndex = 1;
        }

        uint[] result = [0];
        for (int i = startIndex; i < value.Length; i++)
        {
            char c = value[i];
            int digit;
            if (c >= '0' && c <= '9')
                digit = c - '0';
            else if (c >= 'A' && c <= 'Z')
                digit = c - 'A' + 10;
            else if (c >= 'a' && c <= 'z')
                digit = c - 'a' + 10;
            else
                throw new ArgumentException($"Invalid character '{c}' in number.");

            if (digit >= radix)
                throw new ArgumentException($"Digit '{c}' is not valid for radix {radix}.");

            MultiplyByInt(ref result, radix);
            AddInt(ref result, digit);
        }

        int realLen = result.Length;
        while (realLen > 0 && result[realLen - 1] == 0) realLen--;
        if (realLen == 0)
        {
            _signBit = 0;
            _smallValue = 0;
            _data = null;
            return;
        }

        if (realLen == 1)
        {
            _signBit = negative ? 1 : 0;
            _smallValue = result[0];
            _data = null;
        }
        else
        {
            _signBit = negative ? 1 : 0;
            _data = new uint[realLen];
            Array.Copy(result, 0, _data, 0, realLen);
            _smallValue = 0;
        }
    }

    private static void MultiplyByInt(ref uint[] digits, int multiplier)
    {
        ulong carry = 0;
        for (int i = 0; i < digits.Length; i++)
        {
            carry += (ulong)digits[i] * (uint)multiplier;
            digits[i] = (uint)carry;
            carry >>= 32;
        }
        while (carry > 0)
        {
            Array.Resize(ref digits, digits.Length + 1);
            digits[^1] = (uint)carry;
            carry >>= 32;
        }
    }

    private static void AddInt(ref uint[] digits, int addend)
    {
        ulong sum = (ulong)digits[0] + (uint)addend;
        digits[0] = (uint)sum;
        if (sum >> 32 == 0) return;
        int i = 1;
        while (i < digits.Length && (digits[i] += 1) == 0) i++;
        if (i == digits.Length)
        {
            Array.Resize(ref digits, digits.Length + 1);
            digits[^1] = 1;
        }
    }

    public ReadOnlySpan<uint> GetDigits()
    {
        if (_data != null)
            return _data;
        return new[] { _smallValue };
    }

    public int CompareTo(IBigInteger? other)
    {
        if (other is null) return 1;
        if (ReferenceEquals(this, other)) return 0;

        var b = other as BetterBigInteger ?? throw new ArgumentException("Incompatible type.");
        if (IsNegative != b.IsNegative)
            return IsNegative ? -1 : 1;

        int cmp = CompareAbsolute(this, b);
        return IsNegative ? -cmp : cmp;
    }

    public bool Equals(IBigInteger? other)
    {
        return other is BetterBigInteger b && CompareTo(b) == 0;
    }

    public override bool Equals(object? obj) => Equals(obj as IBigInteger);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(_signBit);
        if (_data != null)
            foreach (uint d in _data) hash.Add(d);
        else
            hash.Add(_smallValue);
        return hash.ToHashCode();
    }

    private static int CompareAbsolute(BetterBigInteger a, BetterBigInteger b)
    {
        var aDigits = a.GetDigits();
        var bDigits = b.GetDigits();
        if (aDigits.Length != bDigits.Length)
            return aDigits.Length.CompareTo(bDigits.Length);
        for (int i = aDigits.Length - 1; i >= 0; i--)
            if (aDigits[i] != bDigits[i])
                return aDigits[i].CompareTo(bDigits[i]);
        return 0;
    }

    public static BetterBigInteger operator +(BetterBigInteger a, BetterBigInteger b)
    {
        if (a.IsNegative == b.IsNegative)
        {
            var abs = AddAbsolute(a, b);
            return new BetterBigInteger(abs.GetDigits().ToArray(), a.IsNegative);
        }
        else
        {
            int cmp = CompareAbsolute(a, b);
            if (cmp == 0) return new BetterBigInteger(new uint[] { 0 }, false);
            if (cmp > 0)
            {
                var abs = SubtractAbsolute(a, b);
                return new BetterBigInteger(abs.GetDigits().ToArray(), a.IsNegative);
            }
            else
            {
                var abs = SubtractAbsolute(b, a);
                return new BetterBigInteger(abs.GetDigits().ToArray(), b.IsNegative);
            }
        }
    }

    public static BetterBigInteger operator -(BetterBigInteger a, BetterBigInteger b)
        => a + (-b);

    public static BetterBigInteger operator -(BetterBigInteger a)
    {
        if (a.GetDigits().Length == 1 && a.GetDigits()[0] == 0)
            return a;
        return new BetterBigInteger(a.GetDigits().ToArray(), !a.IsNegative);
    }

    public static BetterBigInteger operator /(BetterBigInteger a, BetterBigInteger b)
    {
        if (b.IsZero()) throw new DivideByZeroException();
        var (quotient, _) = DivideRemainder(a, b);
        return quotient;
    }

    public static BetterBigInteger operator %(BetterBigInteger a, BetterBigInteger b)
    {
        if (b.IsZero()) throw new DivideByZeroException();
        var (_, remainder) = DivideRemainder(a, b);
        return remainder;
    }

    private static BetterBigInteger AddAbsolute(BetterBigInteger a, BetterBigInteger b)
    {
        var aDigits = a.GetDigits();
        var bDigits = b.GetDigits();
        int maxLen = Math.Max(aDigits.Length, bDigits.Length);
        uint[] result = new uint[maxLen + 1];
        ulong carry = 0;
        for (int i = 0; i < maxLen; i++)
        {
            ulong sum = carry;
            if (i < aDigits.Length) sum += aDigits[i];
            if (i < bDigits.Length) sum += bDigits[i];
            result[i] = (uint)sum;
            carry = sum >> 32;
        }
        if (carry > 0) result[maxLen] = (uint)carry;
        else Array.Resize(ref result, maxLen);
        return new BetterBigInteger(result, false);
    }

    private static BetterBigInteger SubtractAbsolute(BetterBigInteger a, BetterBigInteger b)
    {
        var aDigits = a.GetDigits();
        var bDigits = b.GetDigits();
        uint[] result = new uint[aDigits.Length];
        long borrow = 0;
        for (int i = 0; i < aDigits.Length; i++)
        {
            long diff = (long)aDigits[i] - borrow;
            if (i < bDigits.Length) diff -= bDigits[i];
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
        if (len == 0) return new BetterBigInteger(new uint[] { 0 }, false);
        Array.Resize(ref result, len);
        return new BetterBigInteger(result, false);
    }

    private static (BetterBigInteger quotient, BetterBigInteger remainder) DivideRemainder(BetterBigInteger a, BetterBigInteger b)
    {
        if (b.IsZero()) throw new DivideByZeroException();
        if (a.IsZero()) return (new BetterBigInteger(new uint[] { 0 }, false), new BetterBigInteger(new uint[] { 0 }, false));

        var absA = new BetterBigInteger(a.GetDigits().ToArray(), false);
        var absB = new BetterBigInteger(b.GetDigits().ToArray(), false);
        if (CompareAbsolute(absA, absB) < 0)
            return (new BetterBigInteger(new uint[] { 0 }, false), new BetterBigInteger(a.GetDigits().ToArray(), a.IsNegative));

        uint[] dividend = absA.GetDigits().ToArray();
        uint[] divisor = absB.GetDigits().ToArray();
        int n = divisor.Length;
        int m = dividend.Length - n;

        int shift = BitOperations.LeadingZeroCount(divisor[n - 1]);
        uint[] normDivisor = new uint[n];
        uint[] normDividend = new uint[dividend.Length + 1];
        ulong carry = 0;
        for (int i = 0; i < n; i++)
        {
            carry = ((ulong)divisor[i] << shift) | carry;
            normDivisor[i] = (uint)carry;
            carry >>= 32;
        }
        carry = 0;
        for (int i = 0; i < dividend.Length; i++)
        {
            carry = ((ulong)dividend[i] << shift) | carry;
            normDividend[i] = (uint)carry;
            carry >>= 32;
        }
        normDividend[dividend.Length] = (uint)carry;

        uint[] quotient = new uint[m + 1];
        ulong divHigh = normDivisor[n - 1];

        for (int i = m; i >= 0; i--)
        {
            ulong guess = ((ulong)normDividend[i + n] << 32) | normDividend[i + n - 1];
            guess /= divHigh;
            if (guess > uint.MaxValue) guess = uint.MaxValue;

            while (true)
            {
                ulong prodCarry = 0;
                bool tooBig = false;
                for (int j = 0; j < n; j++)
                {
                    prodCarry += (ulong)guess * normDivisor[j];
                    uint low = (uint)prodCarry;
                    prodCarry >>= 32;
                    if (low != normDividend[i + j])
                    {
                        if (low > normDividend[i + j]) tooBig = true;
                        break;
                    }
                }
                if (prodCarry != normDividend[i + n]) tooBig = prodCarry > normDividend[i + n];
                if (tooBig) guess--;
                else break;
            }

            long borrow = 0;
            for (int j = 0; j < n; j++)
            {
                ulong sub = (ulong)guess * normDivisor[j];
                long diff = (long)normDividend[i + j] - borrow - (long)(sub & 0xFFFFFFFFUL);
                normDividend[i + j] = (uint)diff;
                borrow = (long)(sub >> 32) - (diff >> 32);
            }
            long final = (long)normDividend[i + n] - borrow;
            normDividend[i + n] = (uint)final;
            if (final < 0)
            {
                guess--;
                long addCarry = 0;
                for (int j = 0; j < n; j++)
                {
                    long sum = (long)normDividend[i + j] + normDivisor[j] + addCarry;
                    normDividend[i + j] = (uint)sum;
                    addCarry = sum >> 32;
                }
                normDividend[i + n] += (uint)addCarry;
            }
            quotient[i] = (uint)guess;
        }

        int qLen = quotient.Length;
        while (qLen > 0 && quotient[qLen - 1] == 0) qLen--;
        if (qLen == 0) qLen = 1;
        Array.Resize(ref quotient, qLen);

        uint[] remainder = new uint[n];
        for (int i = 0; i < n; i++)
            remainder[i] = (uint)(((ulong)normDividend[i] >> shift) | ((ulong)normDividend[i + 1] << (32 - shift)));
        int rLen = n;
        while (rLen > 0 && remainder[rLen - 1] == 0) rLen--;
        if (rLen == 0) rLen = 1;
        Array.Resize(ref remainder, rLen);

        bool negQuot = a.IsNegative ^ b.IsNegative;
        var qAbs = new BetterBigInteger(quotient, false);
        var rAbs = new BetterBigInteger(remainder, false);

        return (new BetterBigInteger(qAbs.GetDigits().ToArray(), negQuot),
                new BetterBigInteger(rAbs.GetDigits().ToArray(), a.IsNegative && !rAbs.IsZero()));
    }

    private static IMultiplier GetMultiplierStrategy(BetterBigInteger a, BetterBigInteger b)
    {
        int len = Math.Max(a.GetDigits().Length, b.GetDigits().Length);
        if (len < 32)
            return new SimpleMultiplier();
        if (len < 1024)
            return new KaratsubaMultiplier();
        return new FftMultiplier();
    }

    public static BetterBigInteger operator *(BetterBigInteger a, BetterBigInteger b)
    {
        var multiplier = GetMultiplierStrategy(a, b);
        return multiplier.Multiply(a, b);
    }

    public static BetterBigInteger operator ~(BetterBigInteger a)
        => -a - new BetterBigInteger(new uint[] { 1 }, false);

    public static BetterBigInteger operator &(BetterBigInteger a, BetterBigInteger b)
    {
        var (aBits, bBits, _) = PrepareForBitwise(a, b);
        int len = Math.Max(aBits.Length, bBits.Length);
        uint[] result = new uint[len];
        for (int i = 0; i < len; i++)
        {
            uint av = i < aBits.Length ? aBits[i] : 0;
            uint bv = i < bBits.Length ? bBits[i] : 0;
            result[i] = av & bv;
        }
        return FromTwosComplement(result);
    }

    public static BetterBigInteger operator |(BetterBigInteger a, BetterBigInteger b)
    {
        var (aBits, bBits, _) = PrepareForBitwise(a, b);
        int len = Math.Max(aBits.Length, bBits.Length);
        uint[] result = new uint[len];
        for (int i = 0; i < len; i++)
        {
            uint av = i < aBits.Length ? aBits[i] : 0;
            uint bv = i < bBits.Length ? bBits[i] : 0;
            result[i] = av | bv;
        }
        return FromTwosComplement(result);
    }

    public static BetterBigInteger operator ^(BetterBigInteger a, BetterBigInteger b)
    {
        var (aBits, bBits, _) = PrepareForBitwise(a, b);
        int len = Math.Max(aBits.Length, bBits.Length);
        uint[] result = new uint[len];
        for (int i = 0; i < len; i++)
        {
            uint av = i < aBits.Length ? aBits[i] : 0;
            uint bv = i < bBits.Length ? bBits[i] : 0;
            result[i] = av ^ bv;
        }
        return FromTwosComplement(result);
    }

    public static BetterBigInteger operator <<(BetterBigInteger a, int shift)
    {
        if (shift < 0) throw new ArgumentOutOfRangeException(nameof(shift));
        if (shift == 0) return a;
        if (a.IsZero()) return a;

        int fullWords = shift / 32;
        int bits = shift % 32;
        var digits = a.GetDigits();
        uint[] result = new uint[digits.Length + fullWords + 1];
        ulong carry = 0;
        for (int i = 0; i < digits.Length; i++)
        {
            ulong val = ((ulong)digits[i] << bits) | carry;
            result[i + fullWords] = (uint)val;
            carry = val >> 32;
        }
        if (carry > 0)
            result[digits.Length + fullWords] = (uint)carry;
        int len = result.Length;
        while (len > 0 && result[len - 1] == 0) len--;
        if (len == 0) return new BetterBigInteger(new uint[] { 0 }, false);
        Array.Resize(ref result, len);
        return new BetterBigInteger(result, a.IsNegative);
    }

    public static BetterBigInteger operator >>(BetterBigInteger a, int shift)
    {
        if (shift < 0) throw new ArgumentOutOfRangeException(nameof(shift));
        if (shift == 0) return a;
        if (a.IsZero()) return a;

        if (a.IsNegative)
        {
            var divisor = new BetterBigInteger(new uint[] { 1 }, false) << shift;
            var absA = new BetterBigInteger(a.GetDigits().ToArray(), false);
            var absDivisor = new BetterBigInteger(divisor.GetDigits().ToArray(), false);
            var div = DivideRemainder(absA, absDivisor).quotient;
            var rem = DivideRemainder(absA, absDivisor).remainder;
            if (!rem.IsZero())
                return new BetterBigInteger(div.GetDigits().ToArray(), true) - new BetterBigInteger(new uint[] { 1 }, false);
            else
                return new BetterBigInteger(div.GetDigits().ToArray(), true);
        }
        else
        {
            var shifted = RightShiftUnsigned(a.GetDigits().ToArray(), shift);
            return new BetterBigInteger(shifted, false);
        }
    }

    private static uint[] RightShiftUnsigned(uint[] digits, int shift)
    {
        int fullWords = shift / 32;
        int bits = shift % 32;
        if (fullWords >= digits.Length)
            return new uint[] { 0 };
        int newLen = digits.Length - fullWords;
        uint[] result = new uint[newLen];
        if (bits == 0)
        {
            Array.Copy(digits, fullWords, result, 0, newLen);
        }
        else
        {
            for (int i = 0; i < newLen; i++)
            {
                ulong val = (ulong)digits[i + fullWords];
                if (i + fullWords + 1 < digits.Length)
                    val |= (ulong)digits[i + fullWords + 1] << 32;
                result[i] = (uint)(val >> bits);
            }
        }
        int len = result.Length;
        while (len > 0 && result[len - 1] == 0) len--;
        if (len == 0) return new uint[] { 0 };
        Array.Resize(ref result, len);
        return result;
    }

    private static (uint[] aBits, uint[] bBits, int length) PrepareForBitwise(BetterBigInteger a, BetterBigInteger b)
    {
        int len = Math.Max(a.GetDigits().Length, b.GetDigits().Length) + 1;
        var aBits = ToTwosComplement(a, len);
        var bBits = ToTwosComplement(b, len);
        return (aBits, bBits, Math.Max(aBits.Length, bBits.Length));
    }

    private static uint[] ToTwosComplement(BetterBigInteger num, int minLength)
    {
        var digits = num.GetDigits().ToArray();
        if (!num.IsNegative)
        {
            if (digits.Length < minLength)
                Array.Resize(ref digits, minLength);
            return digits;
        }

        int len = Math.Max(digits.Length, minLength);
        uint[] result = new uint[len];
        for (int i = 0; i < len; i++)
        {
            uint val = i < digits.Length ? digits[i] : 0u;
            result[i] = ~val;
        }
        ulong carry = 1;
        for (int i = 0; i < len && carry > 0; i++)
        {
            carry += result[i];
            result[i] = (uint)carry;
            carry >>= 32;
        }
        return result;
    }

    private static BetterBigInteger FromTwosComplement(uint[] bits)
    {
        int len = bits.Length;
        if (len == 0) return new BetterBigInteger(new uint[] { 0 }, false);
        bool negative = (bits[len - 1] & 0x80000000) != 0;
        if (!negative)
        {
            while (len > 0 && bits[len - 1] == 0) len--;
            if (len == 0) return new BetterBigInteger(new uint[] { 0 }, false);
            Array.Resize(ref bits, len);
            return new BetterBigInteger(bits, false);
        }
        else
        {
            uint[] inv = new uint[len];
            for (int i = 0; i < len; i++)
                inv[i] = ~bits[i];
            ulong carry = 1;
            for (int i = 0; i < len && carry > 0; i++)
            {
                carry += inv[i];
                inv[i] = (uint)carry;
                carry >>= 32;
            }
            while (len > 0 && inv[len - 1] == 0) len--;
            if (len == 0) return new BetterBigInteger(new uint[] { 0 }, false);
            Array.Resize(ref inv, len);
            return new BetterBigInteger(inv, true);
        }
    }

    public override string ToString() => ToString(10);

    public string ToString(int radix)
    {
        if (radix < 2 || radix > 36)
            throw new ArgumentOutOfRangeException(nameof(radix), "Radix must be between 2 and 36.");
        if (IsZero()) return "0";

        var sb = new StringBuilder();
        var temp = new BetterBigInteger(GetDigits().ToArray(), false);
        while (!temp.IsZero())
        {
            var (div, rem) = DivModByInt(temp, radix);
            char digit = rem < 10 ? (char)('0' + rem) : (char)('A' + rem - 10);
            sb.Insert(0, digit);
            temp = div;
        }
        if (IsNegative) sb.Insert(0, '-');
        return sb.ToString();
    }

    private static (BetterBigInteger quotient, int remainder) DivModByInt(BetterBigInteger a, int divisor)
    {
        var digits = a.GetDigits();
        ulong remainder = 0;
        uint[] result = new uint[digits.Length];
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            ulong val = (remainder << 32) | digits[i];
            result[i] = (uint)(val / (ulong)divisor);
            remainder = (uint)(val % (ulong)divisor);
        }
        int len = result.Length;
        while (len > 0 && result[len - 1] == 0) len--;
        if (len == 0) return (new BetterBigInteger(new uint[] { 0 }, false), (int)remainder);
        Array.Resize(ref result, len);
        return (new BetterBigInteger(result, false), (int)remainder);
    }

    private bool IsZero() => GetDigits().Length == 1 && GetDigits()[0] == 0;

    public static bool operator ==(BetterBigInteger a, BetterBigInteger b) => Equals(a, b);
    public static bool operator !=(BetterBigInteger a, BetterBigInteger b) => !Equals(a, b);
    public static bool operator <(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) < 0;
    public static bool operator >(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) > 0;
    public static bool operator <=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) <= 0;
    public static bool operator >=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) >= 0;
}