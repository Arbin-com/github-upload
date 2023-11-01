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

using ArbinUtil.EnumHelp.Attributes;
using ArbinUtil.Git;
using ArbinUtil.Jira;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Xml.Linq;
using static ArbinUtil.PSCommand.GitGetJiraIssueReleaseNoteCommand;
using static System.Net.Mime.MediaTypeNames;

namespace ArbinUtil
{
    public static class Util
    {
        public const string BugfixName = "Bug fixes";
        public const string NewFeatureName = "New Features";
        public const string StyleName = "UI Improvements";

        public enum LikeLabelName
        {
            None = -1,
            [EnumDescription(Desc = NewFeatureName)]
            NewFeatures,
            [EnumDescription(Desc = BugfixName)]
            Fix,
            [EnumDescription(Desc = StyleName)]
            UI,
            MaxCount
        }

        public static LikeLabelName GetLikeLabel(string label)
        {
            switch (label.ToLower())
            {
            case "newfeatures":
            case "newfeature":
            case "new feature":
            case "new features":
            //case NewFeatureName:
            return LikeLabelName.NewFeatures;

            case "bug":
            case "fix":
            case "bug fixes":
            //case BugfixName:
            return LikeLabelName.Fix;

            case "ui":
            case "style":
            //case StyleName:
            return LikeLabelName.UI;

            default:
            return LikeLabelName.None;
            }
        }

        public static bool IsOpenVerbose(this PSCmdlet cmdlet)
        {
            return cmdlet.MyInvocation.BoundParameters.ContainsKey("Verbose");
        }

        public static Collection<PSObject> ExecOneScript(this PowerShell powerShell, string script)
        {
            powerShell.Commands.Clear();
            powerShell.AddScript(script);
            var result = powerShell.Invoke();
            powerShell.Commands.Clear();
            return result;
        }


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

        public static int MaxMajorMinorBuild(ArbinVersion x, ArbinVersion y)
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

        public static ArbinVersion StableOrPathTryFindPrevVersion(PowerShell powerShell, ArbinVersion version)
        {
            ArbinVersion tryFindLessVersion = (ArbinVersion)version.Clone();
            tryFindLessVersion.Suffix = ArbinVersion.PatchVersionSuffix;
            ArbinVersion findVersion = GitUtil.GetPrevVersion(powerShell, tryFindLessVersion);
            if (findVersion != null)
                return findVersion;
            tryFindLessVersion.Suffix = ArbinVersion.StableVersionSuffix;
            findVersion = GitUtil.GetPrevVersion(powerShell, tryFindLessVersion);
            return findVersion;
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


        public enum Aligin
        {
            Left,
            Center,
            Right,
        }

        private class FieldInfos
        {

            public FieldInfos(PropertyInfo[] properties)
            {
                GetFieldText = (object obj, int index) =>
                {
                    object value = properties[index].GetValue(obj);
                    return value == null ? "" : value.ToString();
                };
                FieldNames = properties.Select(x=>x.Name).ToArray();
            }

            public FieldInfos(PSObject pSObject)
            {
                GetFieldText = (object obj, int index) =>
                {
                    var temp = (PSObject)obj;
                    var value = temp.Members[FieldNames[index]].Value;
                    return value == null ? "" : value.ToString();
                };
                FieldNames = pSObject.Members.Where(x => x.MemberType == PSMemberTypes.NoteProperty).Select(x => x.Name).ToArray();
            }

            public string[] FieldNames { get; set; }
            public Func<object, int, string> GetFieldText { get; set; }
        }


        public static string ToMarkDownTable(IEnumerable<object> source)
        {
            return ToMarkDownTable(source, (colName) => Util.Aligin.Left);
        }

        public static string ToMarkDownTable(IEnumerable<object> source, Func<string, Aligin> colNameAligin)
        {
            var first = source.FirstOrDefault();
            if (first == null)
                return "";

            FieldInfos fieldInfos = null;
            if(first is PSObject tempPSObject)
            {
                fieldInfos = new FieldInfos(tempPSObject);
            }
            else
            {
                fieldInfos = new FieldInfos(first.GetType().GetRuntimeProperties().ToArray());
            }

            return ToMarkDownTable(source, fieldInfos, colNameAligin);
        }

        public static string ConvertToMarkDownCellText(string text)
        {
            return text.Replace("|", @"\|");
        }

        private static string ToMarkDownTable(IEnumerable<object> source, FieldInfos infos, Func<string, Aligin> colNameAligin)
        {
            const int MaxPad = 100;

            int colCount = infos.FieldNames.Length;
            int elementCount = source.Count();
            if (colCount <= 0 || elementCount <= 0)
                return "";

            string[,] table = new string[elementCount, colCount];
            int[] colMaxLength = new int[colCount];

            for (int i = 0; i < colCount; ++i)
            {
                string colName = infos.FieldNames[i];
                colMaxLength[i] = Math.Min(MaxPad, colName.Length);
            }

            int index = 0;
            foreach (var element in source)
            {
                for (int i = 0; i < colCount; ++i)
                {
                    object value = infos.GetFieldText(element, i);
                    string text = value == null ? "" : value.ToString();
                    table[index, i] = ConvertToMarkDownCellText(text);
                    colMaxLength[i] = Math.Min(MaxPad, Math.Max(colMaxLength[i], text.Length));
                }
                ++index;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("|  ");
            for (int i = 0; i < colCount; ++i)
            {
                string colName = infos.FieldNames[i];
                sb.Append(colName.PadLeft(colMaxLength[i]));
                sb.Append("  | ");
            }

            sb.AppendLine();
            sb.Append("|  ");
            for (int i = 0; i < colCount; ++i)
            {
                Aligin aligin = colNameAligin(infos.FieldNames[i]);
                sb.Append(aligin == Aligin.Center ? ':' : ' ');
                sb.Append('-', Math.Max(1, colMaxLength[i] - 1));
                sb.Append(aligin != Aligin.Left ? ':' : ' ');
                sb.Append(" | ");
            }


            for (int i = 0; i < elementCount; i++)
            {
                sb.AppendLine();
                sb.Append("|  ");
                for (int j = 0; j < colCount; j++)
                {
                    sb.Append(table[i, j].PadLeft(colMaxLength[j]));
                    sb.Append("  | ");
                }
            }

            return sb.ToString();
        }



    }
}
