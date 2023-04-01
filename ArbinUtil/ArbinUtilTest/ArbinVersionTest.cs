using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArbinUtil;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ArbinUtilTest
{
    [TestClass]
    public class ArbinVersionTest
    {

        public static IEnumerable<object[]> CreateVersion()
        {
            var anyNumber = ArbinVersion.AnyNumber;
            yield return new object[] { "*.2.*-a.*", new ArbinVersion { Major = anyNumber, Minor = 2, Build = anyNumber, Suffix = "a", SpecialNumber = anyNumber } };
            yield return new object[] { "1.2.3", new ArbinVersion { Major = 1, Minor = 2, Build = 3 } };
            yield return new object[] { "1.2.3-a", new ArbinVersion { Major = 1, Minor = 2, Build = 3, Suffix = "a" } };
            yield return new object[] { "1.2.3-a.1", new ArbinVersion { Major = 1, Minor = 2, Build = 3, Suffix = "a", SpecialNumber = 1 } };
            yield return new object[] { "*.*.*-a.*", new ArbinVersion { Major = anyNumber, Minor = anyNumber, Build = anyNumber, Suffix = "a", SpecialNumber = anyNumber } };
            yield return new object[] { "release/1.2.3-a.1", new ArbinVersion { Major = 1, Minor = 2, Build = 3, Suffix = "a", SpecialNumber = 1, PathPrefix = "release" } };
            yield return new object[] { "release/mitstest.1.2.3-a.1", new ArbinVersion { Products = "mitstest", Major = 1, Minor = 2, Build = 3, Suffix = "a", SpecialNumber = 1, PathPrefix = "release" } };
        }

        [TestMethod]
        [DynamicData(nameof(CreateVersion), DynamicDataSourceType.Method)]
        public void ParseVersionTrue(string version, ArbinVersion expect)
        {
            Assert.IsTrue(ArbinVersion.Parse(version, out ArbinVersion temp));
            Assert.AreEqual(expect, temp);
        }

        [TestMethod]
        [DataRow("1.1.1-")]
        [DataRow(".")]
        [DataRow("..")]
        [DataRow("...")]
        [DataRow("....")]
        [DataRow(".1.1")]
        [DataRow("1.1.")]
        [DataRow("x.1.1")]
        [DataRow("12..")]
        [DataRow("..1")]
        [DataRow("1.1.1-.")]
        [DataRow("1.1.1-.zz")]
        [DataRow("2a.1.1.1-.zz")]
        public void ParseVersionFalse(string version)
        {
            Assert.IsFalse(ArbinVersion.Parse(version, out ArbinVersion _));
        }




    }
}
