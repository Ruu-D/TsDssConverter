using System.Reflection;
using TsDssConverter.Core;
using TsDssConverter.Tray;

namespace TsDssConverter.Tests;

/// <summary>Sets the language for one test and puts Dutch back afterwards (Dutch is the default).</summary>
public sealed class LanguageScope : IDisposable
{
    public LanguageScope(AppLanguage language)
    {
        Localizer.Current = language;
    }

    public void Dispose()
    {
        Localizer.Current = AppLanguage.Dutch;
    }
}

public class LocalizationTests
{
    private static readonly AppLanguage[] AllLanguages = { AppLanguage.Dutch, AppLanguage.French, AppLanguage.English };

    // ---------------------------------------------------------------- the switch itself

    [Fact]
    public void Dutch_IsTheDefaultLanguage()
    {
        Assert.Equal(AppLanguage.Dutch, Localizer.Current);
        Assert.Equal("nl", new AppSettings().Language);
    }

    [Theory]
    [InlineData(AppLanguage.Dutch, "Opslaan")]
    [InlineData(AppLanguage.French, "Enregistrer")]
    [InlineData(AppLanguage.English, "Save")]
    public void T_GivesTheTextOfTheSelectedLanguage(AppLanguage language, string expected)
    {
        using var scope = new LanguageScope(language);

        Assert.Equal(expected, Localizer.T("Opslaan", "Enregistrer", "Save"));
    }

    [Theory]
    [InlineData("nl", AppLanguage.Dutch)]
    [InlineData("fr", AppLanguage.French)]
    [InlineData("en", AppLanguage.English)]
    [InlineData(" FR ", AppLanguage.French)]   // spaces and capitals are forgiven
    [InlineData("En", AppLanguage.English)]
    public void TryParse_ReadsTheLanguageCodes(string code, AppLanguage expected)
    {
        Assert.True(Localizer.TryParse(code, out AppLanguage language));
        Assert.Equal(expected, language);
    }

    [Theory]
    [InlineData("")]
    [InlineData("de")]
    [InlineData("dutch")]
    [InlineData(null)]
    public void TryParse_RejectsUnknownCodes_AndFromCodeGivesDutch(string? code)
    {
        Assert.False(Localizer.TryParse(code, out _));
        Assert.Equal(AppLanguage.Dutch, Localizer.FromCode(code));
    }

    [Fact]
    public void ToCode_AndFromCode_AreEachOthersInverse()
    {
        foreach (AppLanguage language in AllLanguages)
        {
            Assert.Equal(language, Localizer.FromCode(Localizer.ToCode(language)));
        }
    }

    // ---------------------------------------------------------------- the language in settings.json

    [Fact]
    public void Language_IsKeptInSettingsJson()
    {
        using var folder = new TempFolder();
        string path = folder.File("settings.json");

        SettingsStore.Save(path, new AppSettings { Language = "fr" });
        var result = SettingsStore.Load(path);

        Assert.Contains("\"Language\": \"fr\"", File.ReadAllText(path));
        Assert.Equal("fr", result.Settings.Language);
        Assert.Empty(result.Problems);
    }

    [Fact]
    public void Language_ACapitalOrSpaceIsCleanedUp_AnUnknownLanguageBecomesDutch()
    {
        var clean = new AppSettings { Language = " EN " };
        Assert.Empty(clean.Normalize());
        Assert.Equal("en", clean.Language);

        var unknown = new AppSettings { Language = "klingon" };
        var notes = unknown.Normalize();
        Assert.Equal("nl", unknown.Language);
        Assert.Contains(notes, n => n.Contains("Language") && n.Contains("klingon"));
    }

    [Fact]
    public void OldSettingsJsonWithoutALanguage_KeepsWorking_AsDutch()
    {
        using var folder = new TempFolder();
        string path = folder.File("settings.json");
        File.WriteAllText(path, "{ \"RescanSeconds\": 45 }");

        var result = SettingsStore.Load(path);

        Assert.Empty(result.Problems);
        Assert.Equal("nl", result.Settings.Language);
    }

    // ---------------------------------------------------------------- every text exists in all three languages

    /// <summary>Every user-visible text of a class (properties without arguments and methods with arguments).</summary>
    private static List<(string Name, string[] Markers, Func<string> Get)> AllTexts(Type type)
    {
        var texts = new List<(string, string[], Func<string>)>();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (PropertyInfo property in type.GetProperties(flags).Where(p => p.PropertyType == typeof(string)))
        {
            texts.Add((property.Name, Array.Empty<string>(), () => (string)property.GetValue(null)!));
        }

        foreach (MethodInfo method in type.GetMethods(flags).Where(m => m.ReturnType == typeof(string) && !m.IsSpecialName))
        {
            var markers = new List<string>();
            object[] arguments = method.GetParameters().Select(parameter => SampleArgument(parameter, markers)).ToArray();
            texts.Add((method.Name, markers.ToArray(), () => (string)method.Invoke(null, arguments)!));
        }

        return texts;
    }

    /// <summary>A value for a method argument. Text arguments get a marker that must come back in the result.</summary>
    private static object SampleArgument(ParameterInfo parameter, List<string> markers)
    {
        Type type = parameter.ParameterType;

        if (type == typeof(string))
        {
            string marker = "MARK_" + parameter.Name;
            markers.Add(marker);
            return marker;
        }

        if (type == typeof(IEnumerable<string>))
        {
            markers.Add("MARK_list");
            return new[] { "MARK_list" };
        }

        if (type == typeof(int)) return 3;
        if (type == typeof(double)) return 1.5;
        if (type == typeof(bool)) return true;
        if (type == typeof(DateTime)) return new DateTime(2026, 9, 19, 14, 32, 0);

        throw new NotSupportedException("Give this test a sample value for " + type.Name);
    }

    // Texts that are the same in another language on purpose (a name, or a word that is the same).
    private static bool MayBeSameAsDutch(string textName, AppLanguage language)
    {
        return textName == "ResultOk" || (textName == "HistoryProject" && language == AppLanguage.English);
    }

    [Theory]
    [InlineData(typeof(Messages))]
    [InlineData(typeof(Strings))]
    public void EveryText_ExistsInAllThreeLanguages_AndUsesItsArguments(Type textClass)
    {
        var texts = AllTexts(textClass);
        Assert.True(texts.Count > 30, "The test found only " + texts.Count + " texts in " + textClass.Name);

        foreach (var (name, markers, get) in texts)
        {
            var results = new Dictionary<AppLanguage, string>();
            foreach (AppLanguage language in AllLanguages)
            {
                using var scope = new LanguageScope(language);
                results[language] = get();

                Assert.False(string.IsNullOrWhiteSpace(results[language]), $"{textClass.Name}.{name} is empty in {language}");
                foreach (string marker in markers)
                {
                    Assert.True(results[language].Contains(marker), $"{textClass.Name}.{name} does not use {marker} in {language}: {results[language]}");
                }
            }

            foreach (AppLanguage other in new[] { AppLanguage.French, AppLanguage.English })
            {
                if (!MayBeSameAsDutch(name, other))
                {
                    Assert.False(results[other] == results[AppLanguage.Dutch], $"{textClass.Name}.{name} is not translated to {other}: {results[other]}");
                }
            }

            // "Message" is the same word in French and English; "OK" everywhere.
            bool mayBeSameInFrenchAndEnglish = name == "ResultOk" || name == "HistoryMessage";
            Assert.False(results[AppLanguage.French] == results[AppLanguage.English] && !mayBeSameInFrenchAndEnglish,
                $"{textClass.Name}.{name} is the same in French and English: {results[AppLanguage.French]}");
        }
    }

    // ---------------------------------------------------------------- the conversion speaks the language too

    [Fact]
    public void ConversionErrors_FollowTheLanguage()
    {
        string header = "TopSolidMaterial;DssMaterial;Thickness;Grain\n";

        using (new LanguageScope(AppLanguage.Dutch))
        {
            Assert.Contains("staat niet in materials.csv", Assert.Throws<ConversionException>(() => MaterialTable.FromText(header).Find("Oak_18")).Message);
        }

        using (new LanguageScope(AppLanguage.French))
        {
            Assert.Contains("ne figure pas dans materials.csv", Assert.Throws<ConversionException>(() => MaterialTable.FromText(header).Find("Oak_18")).Message);
        }

        using (new LanguageScope(AppLanguage.English))
        {
            Assert.Contains("is not in materials.csv", Assert.Throws<ConversionException>(() => MaterialTable.FromText(header).Find("Oak_18")).Message);
        }
    }

    [Fact]
    public void LabelOutsideTheSheet_InFrench_NamesThePartAndKeepsTheCommaDecimals()
    {
        using var scope = new LanguageScope(AppLanguage.French);

        var error = Assert.Throws<ConversionException>(
            () => LabelPositionCalculator.Calculate(3050, 1300, 5000.5, 100, 0, false, false, "White_18#01 / Front - 1"));

        Assert.Contains("en dehors du panneau", error.Message);
        Assert.Contains("5000,5", error.Message);            // numbers stay the Duivestein way, in every language
        Assert.Contains("White_18#01 / Front - 1", error.Message);
    }

    [Fact]
    public void Warnings_OfTheWholeSampleConversion_AreInEnglish_WhenEnglishIsSelected()
    {
        using var scope = new LanguageScope(AppLanguage.English);
        using var folder = new TempFolder();
        string outFolder = folder.File("out");
        Directory.CreateDirectory(outFolder);
        var settings = new ConverterSettings { TopSolidExportPath = folder.File("does-not-exist") };

        var result = new Converter().Convert(
            TestPaths.InfoFile, TestPaths.PositionFile, TestPaths.MaterialsFile, outFolder, outFolder, settings, TestPaths.GoldenPlanDate);

        Assert.Single(result.Warnings);
        Assert.Contains("TopSolid export folder not reachable", result.Warnings[0]);
    }

    [Fact]
    public void TheFilesForDuivestein_AreNotTranslated()
    {
        // The label CSV and the XML are data for the warehouse, not interface: same in every language.
        byte[] dutch, french;

        using (new LanguageScope(AppLanguage.Dutch)) dutch = ConvertAndReadCsv();
        using (new LanguageScope(AppLanguage.French)) french = ConvertAndReadCsv();

        Assert.True(dutch.SequenceEqual(french));

        static byte[] ConvertAndReadCsv()
        {
            using var folder = new TempFolder();
            new Converter().Convert(
                TestPaths.InfoFile, TestPaths.PositionFile, TestPaths.MaterialsFile, folder.Path, folder.Path, new ConverterSettings(), TestPaths.GoldenPlanDate);
            return File.ReadAllBytes(folder.File("Verschuren-P-20_002.csv")); // contains the grain text "Geen"
        }
    }
}
