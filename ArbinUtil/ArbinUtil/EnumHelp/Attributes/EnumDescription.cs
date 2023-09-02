/*
* ==============================================================================
* Filename: EnumDescriptionAttribute
* Description: 
*
* Version: 1.0
* Created: 2019/08/20 16:30:21
* Compiler: VS
*
* Author: RuiSen
* ==============================================================================
*/

using System;

namespace ArbinUtil.EnumHelp.Attributes
{
    public sealed class EnumDescriptionAttribute : Attribute
    {
        public string Desc { get; set; } = null;

        public EnumDescriptionAttribute()
        {

        }
    }
}
