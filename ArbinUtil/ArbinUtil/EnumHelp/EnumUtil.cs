/*
* ==============================================================================
* Filename: CEnumUtil
* Description: 
*
* Version: 1.0
* Created: 2019/08/20 16:01:23
* Compiler: VS
*
* Author: RuiSen
* ==============================================================================
*/

using ArbinUtil.EnumHelp.Attributes;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace ArbinUtil.EnumHelp
{
    public static class EnumUtil
    {
        internal static class DescEnum<T>
        {
            #region Member

            public static Dictionary<string, T> s_descToEnum = null;
            public static Dictionary<T, string> s_EnumToDesc = null;
            public static List<string> s_descriptions = null;

            #endregion

            static DescEnum()
            {
                MakeDescriptionAndEnumItem(out s_descriptions, out s_descToEnum, out s_EnumToDesc);
            }
        }

        public static IReadOnlyDictionary<string, T> GetDescToEnumDictionary<T>()
        {
            return DescEnum<T>.s_descToEnum;
        }

        public static IReadOnlyDictionary<T, string> GetEnumToDescDictionary<T>()
        {
            return DescEnum<T>.s_EnumToDesc;
        }

        public static bool DescToEnum<T>(string str, out T result)
        {
            if (str == null)
            {
                result = default;
                return false;
            }

            if (!DescEnum<T>.s_descToEnum.TryGetValue(str, out result))
            {
                return false;
            }

            return true;
        }
        public static bool DescToEnum<T>(string str, out T result, T defaultValue)
        {
            if (str == null)
            {
                result = defaultValue;
                return false;
            }

            if (!DescEnum<T>.s_descToEnum.TryGetValue(str, out result))
            {
                result = defaultValue;
                return false;
            }

            return true;
        }

        public static string GetDescription<T>(this T item)
        {
            string value;
            if (!DescEnum<T>.s_EnumToDesc.TryGetValue(item, out value))
            {
                return string.Empty;
            }

            return value;
        }

        public static string FromLinearGetDesc<T>(int item)
        {
            var list = DescEnum<T>.s_descriptions;
            if ((uint)item >= list.Count)
                return string.Empty;
            return list[item];
        }

        public static IReadOnlyList<string> GetDescriptions<T>()
        {
            return DescEnum<T>.s_descriptions;
        }

        public static string MakeDescription(this Enum eValue)
        {
            FieldInfo fi = eValue.GetType().GetField(eValue.ToString());
            EnumDescriptionAttribute attr = (EnumDescriptionAttribute)fi.GetCustomAttribute(typeof(EnumDescriptionAttribute));

            if (attr != null)
            {
                return attr.Desc;
            }
            return string.Empty;
        }

        public static void AddEnumDictionary<T>(IDictionary<T, string> enumToStringName, IDictionary<string, T> stringNameToEnum)
        {
            Array values = Enum.GetValues(typeof(T));
            string[] names = Enum.GetNames(typeof(T));
            int i = 0;
            foreach (T item in values)
            {
                if (stringNameToEnum != null)
                    stringNameToEnum[names[i]] = item;
                if (enumToStringName != null)
                    enumToStringName[item] = names[i];
                ++i;
            }
        }

        internal static void MakeDescriptionAndEnumItem<T>(out List<string> descriptions, out Dictionary<string, T> descToEnum, out Dictionary<T, string> enumToDesc)
        {
            var enumInstruction = typeof(T).GetCustomAttribute<EnumInstructionAttribute>();
            StringComparer comparer = null;
            if (enumInstruction != null)
            {
                switch (enumInstruction.Comparison)
                {
                    case StringComparison.CurrentCulture:
                        comparer = StringComparer.CurrentCultureIgnoreCase;
                        break;
                    case StringComparison.CurrentCultureIgnoreCase:
                        comparer = StringComparer.CurrentCultureIgnoreCase;
                        break;
                    case StringComparison.InvariantCulture:
                        comparer = StringComparer.InvariantCulture;
                        break;
                    case StringComparison.InvariantCultureIgnoreCase:
                        comparer = StringComparer.InvariantCultureIgnoreCase;
                        break;
                    case StringComparison.Ordinal:
                        comparer = StringComparer.Ordinal;
                        break;
                    case StringComparison.OrdinalIgnoreCase:
                        comparer = StringComparer.OrdinalIgnoreCase;
                        break;
                }
            }


            Array values = Enum.GetValues(typeof(T));

            descriptions = new List<string>(values.Length);
            if (comparer != null)
                descToEnum = new Dictionary<string, T>(values.Length, comparer);
            else
                descToEnum = new Dictionary<string, T>(values.Length);
            enumToDesc = new Dictionary<T, string>(values.Length);

            foreach (object item in values)
            {
                string desc = ((Enum)item).MakeDescription();

                if (string.IsNullOrEmpty(desc))
                    continue;

                T itemValue = (T)item;

                descToEnum[desc] = itemValue;
                enumToDesc[itemValue] = desc;
                descriptions.Add(desc);
            }
        }
    }
}
