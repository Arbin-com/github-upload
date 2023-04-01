/*
* ==============================================================================
* Filename: Util
* Description: 
*
* Version: 1.0
* Created: 2023/4/1 10:18:36
*
* Author: RuiSen
* ==============================================================================
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace ArbinUtil
{
    public static class Util
    {

        public static string GetRelativePathPrefix(ArbinVersion version)
        {
            char ch = Path.AltDirectorySeparatorChar;
            return $"{version.Major}{ch}{version.Minor}";
        }

        public static int AzurePiplineGoodVersionCompareTo(ArbinVersion x, ArbinVersion y)
        {
            int temp = ArbinVersion.TextCompar.Compare(x.Suffix, y.Suffix);
            if (temp != 0)
                return temp;
            if (x.SpecialNumber != ArbinVersion.AnyNumber && y.SpecialNumber != ArbinVersion.AnyNumber)
            {
                temp = x.SpecialNumber.CompareTo(y.SpecialNumber);
                if (temp != 0)
                    return temp;
            }
            temp = MaxMajorMinorBuild(x, y);
            if (temp != 0)
                return temp;
            return 0;
        }

        private static int MaxMajorMinorBuild(ArbinVersion x, ArbinVersion y)
        {
            if (x.Major != ArbinVersion.AnyNumber && y.Major != ArbinVersion.AnyNumber)
            {
                if (x.Major > y.Major)
                    return 1;
                else if (x.Major < y.Major)
                    return -1;
            }
            if (x.Minor != ArbinVersion.AnyNumber && y.Minor != ArbinVersion.AnyNumber)
            {
                if (x.Minor > y.Minor)
                    return 1;
                else if (x.Minor < y.Minor)
                    return -1;
            }
            if (x.Build != ArbinVersion.AnyNumber && y.Build != ArbinVersion.AnyNumber)
            {
                if (x.Build > y.Build)
                    return 1;
                else if (x.Build < y.Build)
                    return -1;
            }
            return 0;
        }

        public static IEnumerable<Memory<char>> Split(Memory<char> src, char separator)
        {
            while (true)
            {
                int index = src.Span.IndexOf(separator);
                if (index != -1)
                {
                    yield return src.Slice(0, index);
                    src = src.Slice(index + 1);
                }
                else
                {
                    yield return src;
                    break;
                }
            }
        }


    }
}
