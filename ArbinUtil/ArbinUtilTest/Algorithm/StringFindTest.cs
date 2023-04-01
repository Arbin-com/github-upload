using ArbinUtil.Algorithm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace ArbinUtilTest.Algorithm
{
    [TestClass]
    public class StringFindTest
    {
        [TestMethod]
        public void BaseTest()
        {
            StringFind.StringFindBuilder builder = new StringFind.StringFindBuilder();
            builder.AddString("QA-");
            builder.AddString("QSS-");
            builder.AddString("QWQE-");
            builder.AddString("WQ-");
            var find = builder.Build();

            List<string> matchs = new List<string>()
            {
                "QA- 0",
                "QSS- 3",
                "WQ- 10",
                "WQ- 16"
            };

            find.Search("QA-QSS-,,,WQ-,WQWQ-", new StringFind.SearchTextDelegate((text, index, len) => {
                string subText = text.Substring(index, len);
                string match = $"{subText} {index}";
                Assert.IsTrue(matchs.Contains(match));
            }));
        }

    }
}
