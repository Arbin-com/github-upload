/*
* ==============================================================================
* Filename: FixedSizeSlidingSetTest
* Description: 
*
* Version: 1.0
* Created: 2023/6/19 10:07:20
*
* Author: RuiSen
* ==============================================================================
*/

using ArbinUtil.Algorithm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace ArbinUtilTest
{
    [TestClass]
    public class FixedSizeSlidingSetTest
    {


        [TestMethod]
        public void BaseTest()
        {
            FixedSizeSlidingSet<string> check = new FixedSizeSlidingSet<string>(3);

            int index = 0;
            for (; index != 10; ++index)
                check.Add(index.ToString());

            for (; index != 1000; ++index)
            {
                check.Add(index.ToString());
                Assert.IsFalse(check.Contains((index - 3).ToString()));
                Assert.IsTrue(check.Contains((index - 1).ToString()));
            }
            Assert.AreEqual(3, check.Count);
        }

    }
}
