/*
* ==============================================================================
* Filename: ArbinVersion
* Description: 
*
* Version: 1.0
* Created: 2023/3/30 16:30:19
*
* Author: RuiSen
* ==============================================================================
*/

using System;
using System.Collections.Generic;
using System.Text;

namespace ArbinUtil
{
    public class ArbinVersion : IEquatable<ArbinVersion>, ICloneable
    {
        public const uint AnyNumber = uint.MaxValue;
        private const char Separator = '.';
        private const char DefaultSuffixSeparator = '-';
        private const char PathSeparator = '/';
        private const string AnyNumberText = "*";
        public const string PatchVersionSuffix = "patch";
        public const string StableVersionSuffix = "release";
        public const StringComparison TextComparision = StringComparison.OrdinalIgnoreCase;
        public static StringComparer TextCompar => StringComparer.OrdinalIgnoreCase;

        private string m_suffix = "";
        private string m_pathPrefix = "";
        private string m_products = "";

        public string PathPrefix
        {
            get => m_pathPrefix;
            set => m_pathPrefix = value ?? "";
        }
        public string Products
        {
            get => m_products;
            set => m_products = value ?? "";
        }
        public uint Major { get; set; }
        public uint Minor { get; set; }
        public uint Build { get; set; }
        public string Suffix
        {
            get => m_suffix;
            set => m_suffix = value ?? "";
        }
        public char SuffixSeparator { get; set; } = DefaultSuffixSeparator;
        public uint SpecialNumber { get; set; }
        public bool HasSuffix => !string.IsNullOrEmpty(Suffix);
        public bool HasPathPrefix => !string.IsNullOrEmpty(PathPrefix);
        public bool HasProducts => !string.IsNullOrEmpty(Products);
        public bool IsPatchVersion => PatchVersionSuffix.Equals(Suffix, StringComparison.OrdinalIgnoreCase);
        public bool IsStableVersion => StableVersionSuffix.Equals(Suffix, StringComparison.OrdinalIgnoreCase);
        public bool IsNormalVersion => !HasSuffix && SpecialNumber == 0;

        public static ArbinVersion CreateDefault()
        {
            ArbinVersion result = new ArbinVersion();
            result.Major = 1;
            return result;
        }

        public string ToString(bool appendPathPrefix, bool appendProducts = false)
        {
            StringBuilder sb = new StringBuilder();
            if(appendPathPrefix && HasPathPrefix)
            {
                sb.Append(PathPrefix);
                sb.Append(PathSeparator);
            }

            if(appendProducts && HasProducts)
            {
                sb.Append(Products);
                sb.Append(Separator);
            }

            string tempText;
            if(Major == AnyNumber)
                tempText = AnyNumberText;
            else
                tempText = Major.ToString();
            sb.Append(tempText);
            sb.Append(Separator);

            if(Minor == AnyNumber)
                tempText = AnyNumberText;
            else
                tempText = Minor.ToString();
            sb.Append(tempText);
            sb.Append(Separator);

            if(Build == AnyNumber)
                tempText = AnyNumberText;
            else
                tempText = Build.ToString();
            sb.Append(tempText);

            if(HasSuffix)
            {
                sb.Append(SuffixSeparator);
                sb.Append(Suffix);
            }
            if(SpecialNumber != 0)
            {
                sb.Append(Separator);
                if(SpecialNumber == AnyNumber)
                    tempText = AnyNumberText;
                else
                    tempText = SpecialNumber.ToString();
                sb.Append(tempText);
            }
            return sb.ToString();
        }

        public override string ToString()
        {
            return ToString(true, true);
        }

        public bool SamePathPrefix(string pathPrefix)
        {
            return PathPrefix.Equals(pathPrefix, TextComparision);
        }

        public bool SameSuffix(string suffix)
        {
            return Suffix.Equals(suffix, TextComparision);
        }

        public bool SameProducts(string products)
        {
            return Products.Equals(products, TextComparision);
        }

        //private bool int MatchNumber()
        //{

        //}

        private static ReadOnlySpan<char> MatchNext(ref ReadOnlySpan<char> span, char ch)
        {
            int index = span.IndexOf(ch);
            if(index < 0)
                return ReadOnlySpan<char>.Empty;
            var temp = span[..index];
            span = span[(index + 1)..];
            return temp;
        }

        public static bool Parse(string version, out ArbinVersion result)
        {
            result = new ArbinVersion();
            if(version == null)
                return false;
            var raw = version.AsSpan().Trim();
            if(raw.IsEmpty)
                return false;

            int temp = raw.IndexOf('.');
            if(temp < 0)
                return false;

            temp = raw[..temp].LastIndexOf(PathSeparator);
            if(temp == 0)
                return false;
            if(temp > 0)
            {
                result.PathPrefix = raw[..temp].ToString();
                raw = raw[(temp + 1)..];
            }
            if(raw.IsEmpty)
                return false;

            if(char.IsLetter(raw[0]))
            {
                result.Products = MatchNext(ref raw, Separator).ToString();
            }

            uint setMarjor, setMinor, setBuild;

            var block = MatchNext(ref raw, Separator);
            if(block.IsEmpty)
                return false;
            if(block.Equals(AnyNumberText.AsSpan(), StringComparison.InvariantCulture))
            {
                setMarjor = AnyNumber;
            }
            else
            {
                if(!uint.TryParse(block, out setMarjor))
                    return false;
            }

            block = MatchNext(ref raw, Separator);
            if(block.IsEmpty)
                return false;
            if(block.Equals(AnyNumberText.AsSpan(), StringComparison.InvariantCulture))
            {
                setMinor = AnyNumber;
            }
            else
            {
                if(!uint.TryParse(block, out setMinor))
                    return false;
            }


            if(raw.StartsWith(AnyNumberText))
            {
                setBuild = AnyNumber;
                raw = raw[AnyNumberText.Length..];
            }
            else
            {
                temp = 0;
                while(temp < raw.Length && char.IsDigit(raw[temp]))
                    ++temp;
                var tempText = raw[..temp];
                if(!uint.TryParse(tempText, out setBuild))
                    return false;
                raw = raw[temp..];
            }

            result.Major = setMarjor;
            result.Minor = setMinor;
            result.Build = setBuild;

            if(raw.IsEmpty)
                return true;

            temp = raw.LastIndexOf(Separator);
            if(temp > 0)
            {
                uint setSpecialNumber;
                var tempText = raw[(temp + 1)..];
                bool ok = false;
                if(tempText.Equals(AnyNumberText.AsSpan(), StringComparison.InvariantCulture))
                {
                    setSpecialNumber = AnyNumber;
                    ok = true;
                }
                else if(uint.TryParse(raw[(temp + 1)..], out setSpecialNumber))
                {
                    ok = true;
                }

                if(ok)
                {
                    result.SpecialNumber = setSpecialNumber;
                    raw = raw[..temp];
                    if(raw.IsEmpty)
                        return true;
                }
           }

            char suffixSeparator = raw[0];
            if(suffixSeparator != DefaultSuffixSeparator && suffixSeparator != '+' || raw.Length < 2)
                return false;
            result.SuffixSeparator = suffixSeparator;
            var suffix = raw[1..];
            if(suffix[0] == Separator)
                return false;
            result.Suffix = suffix.ToString();
            return true;
        }

        public static explicit operator ArbinVersion(string version)
        {
            if(!Parse(version, out ArbinVersion result))
                return new ArbinVersion();
            return result;
        }

        public int MaxMajorMinorBuild(ArbinVersion b)
        {
            if(Major > b.Major)
                return 1;
            else if(Major < b.Major)
                return -1;

            if(Minor > b.Minor)
                return 1;
            else if(Minor < b.Minor)
                return -1;

            if(Build > b.Build)
                return 1;
            else if(Build < b.Build)
                return -1;
            return 0;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ArbinVersion);
        }

        public bool Equals(ArbinVersion other)
        {
            return other != null &&
                   SamePathPrefix(other.PathPrefix) &&
                   SameSuffix(other.Suffix) &&
                   SameProducts(other.Products) &&
                   Major == other.Major &&
                   Minor == other.Minor &&
                   Build == other.Build &&
                   SpecialNumber == other.SpecialNumber;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(PathPrefix, TextCompar);
            hash.Add(Products, TextCompar);
            hash.Add(Major);
            hash.Add(Minor);
            hash.Add(Build);
            hash.Add(Suffix, TextCompar);
            hash.Add(SpecialNumber);
            return hash.ToHashCode();
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }

        public static bool operator ==(ArbinVersion left, ArbinVersion right)
        {
            return EqualityComparer<ArbinVersion>.Default.Equals(left, right);
        }

        public static bool operator !=(ArbinVersion left, ArbinVersion right)
        {
            return !(left == right);
        }
    }
}
