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

   private static void MultiplyByInt(ref uint[] digits, int multiplier)
   {
       uint mult = (uint)multiplier;
       uint carry = 0;
       for (int i = 0; i < digits.Length; i++)
       {
           MultiplyWords(digits[i], mult, out uint high, out uint low);
           uint sum = low + carry;
           uint newCarry = high + (sum < low ? 1u : 0u);
           digits[i] = sum;
           carry = newCarry;
       }
       while (carry != 0)
       {
           Array.Resize(ref digits, digits.Length + 1);
           digits[^1] = carry;
           carry = 0;
       }
   }

   private static void AddInt(ref uint[] digits, int addend)
   {
       uint add = (uint)addend;
       uint sum = digits[0] + add;
       digits[0] = sum;
       if (sum >= add) return;
       uint carry = 1;
       int i = 1;
       while (i < digits.Length && carry != 0)
       {
           sum = digits[i] + carry;
           digits[i] = sum;
           carry = sum < carry ? 1u : 0u;
           i++;
       }
       if (carry != 0)
       {
           Array.Resize(ref digits, digits.Length + 1);
           digits[^1] = carry;
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
       uint carry = 0;
       for (int i = 0; i < maxLen; i++)
       {
           uint av = i < aDigits.Length ? aDigits[i] : 0u;
           uint bv = i < bDigits.Length ? bDigits[i] : 0u;
           uint sum = av + carry;
           bool carry1 = sum < av;
           sum += bv;
           bool carry2 = sum < bv;
           result[i] = sum;
           carry = (carry1 || carry2) ? 1u : 0u;
       }
       if (carry != 0) result[maxLen] = carry;
       else Array.Resize(ref result, maxLen);
       return new BetterBigInteger(result, false);
   }

   private static BetterBigInteger SubtractAbsolute(BetterBigInteger a, BetterBigInteger b)
   {
       var aDigits = a.GetDigits();
       var bDigits = b.GetDigits();
       uint[] result = new uint[aDigits.Length];
       uint borrow = 0;
       for (int i = 0; i < aDigits.Length; i++)
       {
           uint av = aDigits[i];
           uint bv = i < bDigits.Length ? bDigits[i] : 0u;

           uint diff = av - borrow;
           bool borrow1 = diff > av;
           uint diff2 = diff - bv;
           bool borrow2 = diff2 > diff;

           result[i] = diff2;
           borrow = (borrow1 || borrow2) ? 1u : 0u;
       }
       int len = result.Length;
       while (len > 0 && result[len - 1] == 0) len--;
       if (len == 0) return new BetterBigInteger(new uint[] { 0 }, false);
       Array.Resize(ref result, len);
       return new BetterBigInteger(result, false);
   }

   private static (BetterBigInteger quotient, BetterBigInteger remainder) DivideRemainder(BetterBigInteger dividend, BetterBigInteger divisor)
   {
       if (divisor.IsZero()) throw new DivideByZeroException();
       if (dividend.IsZero()) return (new BetterBigInteger(new uint[] { 0 }, false), new BetterBigInteger(new uint[] { 0 }, false));

       uint[] a = dividend.GetDigits().ToArray();
       uint[] b = divisor.GetDigits().ToArray();

       int aBitLen = GetBitLength(a);
       int bBitLen = GetBitLength(b);

       if (aBitLen < bBitLen)
       {
           return (
               new BetterBigInteger(new uint[] { 0 }, false),
               new BetterBigInteger(a, dividend.IsNegative)
           );
       }

       int quotientBitCapacity = aBitLen - bBitLen + 1;
       uint[] quotientBits = new uint[(quotientBitCapacity + 31) / 32];

       uint[] remainder = Array.Empty<uint>();

       for (int i = aBitLen - 1; i >= 0; i--)
       {
           remainder = ShiftLeftOne(remainder);
           if (GetBit(a, i))
           {
               remainder = AddOne(remainder);
           }

           if (CompareAbsolute(remainder, b) >= 0)
           {
               remainder = SubtractAbsolute(remainder, b);
               SetBit(quotientBits, i);
           }
       }

       uint[] quot = TrimLeadingZeros(quotientBits);
       uint[] rem = TrimLeadingZeros(remainder);

       BetterBigInteger q = new(quot, false);
       BetterBigInteger r = new(rem, false);

       bool quotientNegative = dividend.IsNegative != divisor.IsNegative;
       bool remainderNegative = dividend.IsNegative;

       return (
           quotientNegative ? -q : q,
           remainderNegative ? -r : r
       );
   }


   private static int GetBitLength(uint[] value)
   {
       int len = value.Length;
       while (len > 0 && value[len - 1] == 0) len--;
       if (len == 0) return 0;
       uint highWord = value[len - 1];
       int bitPos = 31;
       while (bitPos >= 0 && (highWord & (1u << bitPos)) == 0) bitPos--;
       return (len - 1) * 32 + bitPos + 1;
   }

   private static bool GetBit(uint[] value, int bitIndex)
   {
       int wordIndex = bitIndex / 32;
       int bitInWord = bitIndex % 32;
       if (wordIndex >= value.Length) return false;
       return (value[wordIndex] & (1u << bitInWord)) != 0;
   }

   private static void SetBit(uint[] arr, int bitIndex)
   {
       int wordIndex = bitIndex / 32;
       int bitInWord = bitIndex % 32;
       arr[wordIndex] |= (1u << bitInWord);
   }

   private static uint[] ShiftLeftOne(uint[] value)
   {
       if (value.Length == 0) return new uint[] { 0 };
       uint[] result = new uint[value.Length + 1];
       uint carry = 0;
       for (int i = 0; i < value.Length; i++)
       {
           uint newVal = (value[i] << 1) | carry;
           carry = value[i] >> 31;
           result[i] = newVal;
       }
       if (carry != 0) result[value.Length] = carry;
       else Array.Resize(ref result, value.Length);
       return result;
   }

   private static uint[] AddOne(uint[] value)
   {
       uint[] result = new uint[value.Length + 1];
       uint carry = 1;
       for (int i = 0; i < value.Length; i++)
       {
           uint sum = value[i] + carry;
           carry = sum < value[i] ? 1u : 0u;
           result[i] = sum;
       }
       if (carry != 0) result[value.Length] = carry;
       else Array.Resize(ref result, value.Length);
       return result;
   }

   private static int CompareAbsolute(uint[] a, uint[] b)
   {
       int aLen = a.Length;
       while (aLen > 0 && a[aLen - 1] == 0) aLen--;
       int bLen = b.Length;
       while (bLen > 0 && b[bLen - 1] == 0) bLen--;

       if (aLen != bLen) return aLen.CompareTo(bLen);
       for (int i = aLen - 1; i >= 0; i--)
           if (a[i] != b[i]) return a[i].CompareTo(b[i]);
       return 0;
   }

   private static uint[] SubtractAbsolute(uint[] a, uint[] b)
   {
       int aLen = a.Length;
       while (aLen > 0 && a[aLen - 1] == 0) aLen--;
       int bLen = b.Length;
       while (bLen > 0 && b[bLen - 1] == 0) bLen--;

       uint[] result = new uint[aLen];
       uint borrow = 0;
       for (int i = 0; i < aLen; i++)
       {
           uint av = a[i];
           uint bv = i < bLen ? b[i] : 0;

           uint diff = av - borrow;
           bool borrow1 = diff > av;
           uint diff2 = diff - bv;
           bool borrow2 = diff2 > diff;

           result[i] = diff2;
           borrow = (borrow1 || borrow2) ? 1u : 0u;
       }
       return TrimLeadingZeros(result);
   }

   private static uint[] TrimLeadingZeros(uint[] arr)
   {
       int len = arr.Length;
       while (len > 0 && arr[len - 1] == 0) len--;
       if (len == arr.Length) return arr;
       if (len == 0) return new uint[] { 0 };
       uint[] result = new uint[len];
       Array.Copy(arr, 0, result, 0, len);
       return result;
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
       uint carry = 0;
       for (int i = 0; i < digits.Length; i++)
       {
           uint val = digits[i];
           uint low = (val << bits) | carry;
           uint high = bits == 0 ? 0 : val >> (32 - bits);
           result[i + fullWords] = low;
           carry = high;
       }
       if (carry != 0)
           result[digits.Length + fullWords] = carry;
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
               uint low = digits[i + fullWords] >> bits;
               uint high = (i + fullWords + 1 < digits.Length) ? digits[i + fullWords + 1] << (32 - bits) : 0;
               result[i] = low | high;
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
       uint carry = 1;
       for (int i = 0; i < len && carry != 0; i++)
       {
           uint sum = result[i] + carry;
           carry = sum < carry ? 1u : 0u;
           result[i] = sum;
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
           uint carry = 1;
           for (int i = 0; i < len && carry != 0; i++)
           {
               uint sum = inv[i] + carry;
               carry = sum < carry ? 1u : 0u;
               inv[i] = sum;
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
       uint div = (uint)divisor;
       uint rem = 0;
       uint[] result = new uint[digits.Length];
       for (int i = digits.Length - 1; i >= 0; i--)
       {
           uint high = rem;
           uint low = digits[i];
           uint q = Divide64By32(high, low, div, out uint r);
           result[i] = q;
           rem = r;
       }
       int len = result.Length;
       while (len > 0 && result[len - 1] == 0) len--;
       if (len == 0) return (new BetterBigInteger(new uint[] { 0 }, false), (int)rem);
       Array.Resize(ref result, len);
       return (new BetterBigInteger(result, false), (int)rem);
   }

   private static uint Divide64By32(uint high, uint low, uint divisor, out uint remainder)
   {
       if (high == 0)
       {
           remainder = low % divisor;
           return low / divisor;
       }

       uint q = 0;
       uint r = 0;
       for (int i = 31; i >= 0; i--)
       {
           r = (r << 1) | ((high >> i) & 1);
           if (r >= divisor)
           {
               r -= divisor;
               q |= (1u << i);
           }
       }
       for (int i = 31; i >= 0; i--)
       {
           r = (r << 1) | ((low >> i) & 1);
           if (r >= divisor)
           {
               r -= divisor;
               q = (q << 1) | 1;
           }
           else
           {
               q = q << 1;
           }
       }
       remainder = r;
       return q;
   }

   private bool IsZero() => GetDigits().Length == 1 && GetDigits()[0] == 0;

   public static bool operator ==(BetterBigInteger a, BetterBigInteger b) => Equals(a, b);
   public static bool operator !=(BetterBigInteger a, BetterBigInteger b) => !Equals(a, b);
   public static bool operator <(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) < 0;
   public static bool operator >(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) > 0;
   public static bool operator <=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) <= 0;
   public static bool operator >=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) >= 0;
}

