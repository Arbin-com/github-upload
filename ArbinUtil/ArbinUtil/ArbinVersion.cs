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
        private const char SuffixSeparator = '-';
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
        public uint SpecialNumber { get; set; }
        public bool HasSuffix => !string.IsNullOrEmpty(Suffix);
        public bool HasPathPrefix => !string.IsNullOrEmpty(PathPrefix);
        public bool HasProducts => !string.IsNullOrEmpty(Products);
        public bool IsPatchVersion => Suffix.Contains(PatchVersionSuffix, TextComparision);
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

            if (appendProducts && HasProducts)
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

            if (Minor == AnyNumber)
                tempText = AnyNumberText;
            else
                tempText = Minor.ToString();
            sb.Append(tempText);
            sb.Append(Separator);

            if (Build == AnyNumber)
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
                if (SpecialNumber == AnyNumber)
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

        public static bool Parse(string version, out ArbinVersion result)
        {
            result = new ArbinVersion();
            if (string.IsNullOrEmpty(version))
                return false;
            string[] blocks = version.Split(Separator);
            if (blocks.Length < 3 || blocks.Length > 5)
                return false;

            int nextIndex = 0;
            string first = blocks[nextIndex];
            int tempIndex = first.LastIndexOf(PathSeparator);
            if (tempIndex == 0)
                return false;
            if (tempIndex > 0)
            {
                result.PathPrefix = first.Substring(0, tempIndex);
                first = first.Substring(tempIndex + 1);
            }
            if (string.IsNullOrWhiteSpace(first))
                return false;

            string majorText;
            if (char.IsLetter(first[0]))
            {
                result.Products = first;
                ++nextIndex;
                majorText = blocks[nextIndex];
            }
            else
            {
                majorText = first;
            }

            int majorTextIndex = 0;
            uint setMarjor;
            uint temp;
            var tempText = majorText.AsSpan().Slice(majorTextIndex);
            if (tempText.Equals(AnyNumberText.AsSpan(), StringComparison.InvariantCulture))
            {
                setMarjor = AnyNumber;
            }
            else
            {
                if (!uint.TryParse(tempText, out setMarjor))
                    return false;
            }

            ++nextIndex;
            if (blocks[nextIndex] == AnyNumberText)
            {
                temp = AnyNumber;
            }
            else
            {
                if (!uint.TryParse(blocks[nextIndex], out temp))
                    return false;
            }

            result.Major = setMarjor;
            result.Minor = temp;

            ++nextIndex;
            if (nextIndex >= blocks.Length)
                return false;
            string buildText = blocks[nextIndex];
            if(buildText.StartsWith(AnyNumberText))
            {
                temp = AnyNumber;
                tempIndex = AnyNumberText.Length;
            }
            else
            {
                tempIndex = 0;
                while (tempIndex < buildText.Length && char.IsDigit(buildText[tempIndex]))
                    ++tempIndex;
                tempText = buildText.AsSpan().Slice(0, tempIndex);
                if (!uint.TryParse(tempText, out temp))
                    return false;
            }

            result.Build = temp;
            if (tempIndex < buildText.Length)
            {
                if (buildText[tempIndex] != SuffixSeparator)
                    return false;
                ++tempIndex;
                if (tempIndex >= buildText.Length)
                    return false;
                result.Suffix = buildText[tempIndex..];
            }

            ++nextIndex;
            if (nextIndex < blocks.Length)
            {
                if (blocks[nextIndex] == AnyNumberText)
                {
                    temp = AnyNumber;
                }
                else
                {
                    if (!uint.TryParse(blocks[nextIndex], out temp))
                        return false;
                }
                result.SpecialNumber = temp;
                ++nextIndex;
            }

            return nextIndex == blocks.Length;
        }

        public static explicit operator ArbinVersion(string version)
        {
            if (!Parse(version, out ArbinVersion result))
                return new ArbinVersion();
            return result;
        }

        public int MaxMajorMinorBuild(ArbinVersion b)
        {
            if (Major > b.Major)
                return 1;
            else if (Major < b.Major)
                return -1;

            if (Minor > b.Minor)
                return 1;
            else if (Minor < b.Minor)
                return -1;

            if (Build > b.Build)
                return 1;
            else if (Build < b.Build)
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
