using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ghumante.UI.Localization;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Ghumante.Tests.EditMode
{
    public class LocalizerTests
    {
        private const string LocalizationFolder = "Assets/Ghumante/UI/Localization";

        private static Localizer Create()
        {
            var l = new Localizer();
            l.AddTable("en", "{\"menu.explore\": \"Explore\", \"hud.coins\": \"{0} coins\", \"only.en\": \"E\"}");
            l.AddTable("ne", "{\"menu.explore\": \"घुम्नुहोस्\", \"hud.coins\": \"{0} सिक्का\"}");
            return l;
        }

        [Test]
        public void GetReturnsCurrentLocaleThenEnglishThenBracketedKey()
        {
            Localizer l = Create();
            l.SetLocale("ne");
            Assert.AreEqual("घुम्नुहोस्", l.Get("menu.explore"));
            Assert.AreEqual("E", l.Get("only.en"));
            Assert.AreEqual("[missing.key]", l.Get("missing.key"));
        }

        [Test]
        public void NumbersUseLakhGroupingAndDevanagariDigitsInNepali()
        {
            Localizer l = Create();
            Assert.AreEqual("1,234,567", l.FormatNumber(1234567));
            l.SetLocale("ne");
            Assert.AreEqual("१२,३४,५६७", l.FormatNumber(1234567));
            Assert.AreEqual("१,२५०", l.FormatNumber(1250));
            Assert.AreEqual("-१२३", l.FormatNumber(-123));
            l.NativeNumerals = false;
            Assert.AreEqual("12,34,567", l.FormatNumber(1234567));
        }

        [Test]
        public void FormatConvertsIntegerArguments()
        {
            Localizer l = Create();
            l.SetLocale("ne");
            Assert.AreEqual("१,२५० सिक्का", l.Format("hud.coins", 1250));
        }

        [Test]
        public void ChangedFiresOnLocaleAndNumeralSwitch()
        {
            Localizer l = Create();
            int count = 0;
            l.Changed += () => count++;
            l.SetLocale("ne");
            l.SetLocale("ne");
            l.NativeNumerals = false;
            Assert.AreEqual(2, count);
        }

        [Test]
        public void ApplyFillsBindingPathAndSetsLanguageClass()
        {
            Localizer l = Create();
            l.SetLocale("ne");
            var root = new VisualElement();
            var label = new Label("Explore") { bindingPath = "menu.explore" };
            var untouched = new Label("static");
            root.Add(label);
            root.Add(untouched);
            l.Apply(root);
            Assert.AreEqual("घुम्नुहोस्", label.text);
            Assert.AreEqual("static", untouched.text);
            Assert.IsTrue(root.ClassListContains("gh-lang-ne"));
            Assert.IsFalse(root.ClassListContains("gh-lang-en"));
        }

        [Test]
        public void FlatJsonRejectsNonStringValuesAndDuplicates()
        {
            Assert.Throws<FormatException>(() => FlatJson.Parse("{\"a\": 1}"));
            Assert.Throws<FormatException>(() => FlatJson.Parse("{\"a\": \"x\", \"a\": \"y\"}"));
            Assert.AreEqual("é\n", FlatJson.Parse("{\"k\": \"\\u00e9\\n\"}")["k"]);
        }

        [Test]
        public void ShippedTablesParseAndHaveIdenticalKeys()
        {
            Dictionary<string, string> en = FlatJson.Parse(File.ReadAllText(Path.Combine(LocalizationFolder, "strings.en.json")));
            Dictionary<string, string> ne = FlatJson.Parse(File.ReadAllText(Path.Combine(LocalizationFolder, "strings.ne.json")));
            CollectionAssert.AreEquivalent(en.Keys.ToList(), ne.Keys.ToList());
            Assert.GreaterOrEqual(en.Count, 30);
        }
    }
}
