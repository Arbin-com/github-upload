/*
* ==============================================================================
* Filename: EnumInstructionAttribute
* Description: 
*
* Version: 1.0
* Created: 2021/9/1 13:00:24
*
* Author: RuiSen
* ==============================================================================
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArbinUtil.EnumHelp.Attributes
{
    public sealed class EnumInstructionAttribute : Attribute
    {
        public StringComparison Comparison { get; set; } = StringComparison.Ordinal;

        public EnumInstructionAttribute()
        {

        }
    }
}
