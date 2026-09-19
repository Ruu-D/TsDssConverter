using Microsoft.Win32;
using TsDssConverter.Core;
using TsDssConverter.Tray;

namespace TsDssConverter.Tests;

/// <summary>Tests for the parts of the Tray project that can be tested without a person clicking.</summary>
public class TrayTests
{
    // ---------------------------------------------------------------- start with Windows (registry)

    /// <summary>A registry key of its own for a test, removed afterwards. The real Run key is never touched.</summary>
    private sealed class TestRegistryKey : IDisposable
    {
        public string Path { get; } = @"Software\TsDssConverterTests\Run-" + Guid.NewGuid().ToString("N");

        public void Dispose()
        {
            TestRegistry.Remove(Path);
        }
    }

    /// <summary>Removes a test key, and the empty parent key "TsDssConverterTests" when it was the last one.</summary>
    internal static class TestRegistry
    {
        public static void Remove(string keyPath)
        {
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);

            try
            {
                using var parent = Registry.CurrentUser.OpenSubKey(@"Software\TsDssConverterTests");
                if (parent != null && parent.SubKeyCount == 0)
                {
                    parent.Close();
                    Registry.CurrentUser.DeleteSubKey(@"Software\TsDssConverterTests", throwOnMissingSubKey: false);
                }
            }
            catch (Exception error) when (error is InvalidOperationException || error is UnauthorizedAccessException)
            {
                // Another test is using the parent key at the same moment: that test will remove it.
            }
        }
    }

    [Fact]
    public void StartWithWindows_IsOffByDefault_OnAfterEnabling_OffAfterDisabling()
    {
        using var key = new TestRegistryKey();
        var startup = new StartWithWindows(key.Path, @"C:\TsDssConverter\TsDssConverter.exe");

        Assert.False(startup.IsEnabled());

        startup.SetEnabled(true);
        Assert.True(startup.IsEnabled());

        startup.SetEnabled(false);
        Assert.False(startup.IsEnabled());
    }

    [Fact]
    public void StartWithWindows_WritesTheQuotedPathOfTheExe()
    {
        using var key = new TestRegistryKey();
        new StartWithWindows(key.Path, @"C:\Program Files\Some Folder\TsDssConverter.exe").SetEnabled(true);

        using var registryKey = Registry.CurrentUser.OpenSubKey(key.Path);
        Assert.Equal("\"C:\\Program Files\\Some Folder\\TsDssConverter.exe\"", registryKey!.GetValue("TsDssConverter"));
    }

    [Fact]
    public void StartWithWindows_ReadsTheRealRegistryState_AlsoWhenSomeoneChangedItByHand()
    {
        using var key = new TestRegistryKey();
        var startup = new StartWithWindows(key.Path, @"C:\x\TsDssConverter.exe");

        using (var registryKey = Registry.CurrentUser.CreateSubKey(key.Path))
        {
            registryKey.SetValue("TsDssConverter", "\"D:\\somewhere\\else.exe\""); // another path than ours
        }

        Assert.True(startup.IsEnabled()); // the checkbox shows what the registry says
    }

    [Fact]
    public void StartWithWindows_DisablingWhenNothingIsThere_IsFine()
    {
        using var key = new TestRegistryKey();

        new StartWithWindows(key.Path, @"C:\x\TsDssConverter.exe").SetEnabled(false); // key does not even exist
    }

    [Fact]
    public void StartWithWindows_DefaultsToTheRunKeyOfTheCurrentUser()
    {
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Run", StartWithWindows.DefaultKeyPath);
    }

    // ---------------------------------------------------------------- colour theme

    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            double c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    private static double Contrast(Color a, Color b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    [Fact]
    public void Palette_IsTheOneFromTheIconFiles()
    {
        Assert.Equal(Color.FromArgb(0x2C, 0xAB, 0xE2), Theme.Brand);
        Assert.Equal(Color.FromArgb(0x14, 0x80, 0xB5), Theme.DarkBlue);
        Assert.Equal(Color.FromArgb(0x7C, 0xCA, 0xED), Theme.LightBlue);
        Assert.Equal(Color.FromArgb(0xA3, 0xD7, 0xEF), Theme.PaleBlue);
        Assert.Equal(Color.FromArgb(0xD7, 0xEF, 0xFA), Theme.VeryPaleBlue);
        Assert.Equal(Color.FromArgb(0x0A, 0x3A, 0x56), Theme.Navy);
    }

    [Theory]
    [InlineData("white")]
    [InlineData("pale")]
    [InlineData("verypale")]
    [InlineData("brand")]
    public void NavyText_IsReadableOnEveryBackgroundItIsUsedOn(string background)
    {
        Color back = background switch
        {
            "white" => Theme.White,
            "pale" => Theme.PaleBlue,
            "verypale" => Theme.VeryPaleBlue,
            _ => Theme.Brand,
        };

        Assert.True(Contrast(Theme.Navy, back) >= 4.5, $"navy on {background} is {Contrast(Theme.Navy, back):0.0}");
    }

    [Fact]
    public void WhiteText_IsOnlyReadableOnDarkBlue_NotOnBrandBlue()
    {
        // The reason why primary buttons have navy text and only turn white on the dark blue hover colour.
        Assert.True(Contrast(Theme.White, Theme.Brand) < 3.0);
        Assert.True(Contrast(Theme.White, Theme.DarkBlue) >= 4.3);
        Assert.True(Contrast(Theme.White, Theme.Navy) >= 4.5); // pressed button
    }

    [Fact]
    public void ErrorRed_IsReadableOnWhite_AndDifferentFromTheBrandColours()
    {
        Assert.True(Contrast(Theme.Error, Theme.White) >= 4.5);
        Assert.NotEqual(Theme.Brand, Theme.Error);
    }

    // ---------------------------------------------------------------- built-in graphics

    [Fact]
    public void AllIconsAndTheBannerAreBuiltIntoTheExe()
    {
        foreach (TrayState state in Enum.GetValues<TrayState>())
        {
            using Icon icon = AppIcons.ForState(state);
            Assert.True(icon.Width > 0);
        }

        using Icon windowIcon = AppIcons.ForWindow();
        Assert.True(windowIcon.Width > 0);

        using Image banner = AppIcons.Banner();
        Assert.Equal(1080, banner.Width); // the @2x banner
    }

    [Fact]
    public void StatusIcons_AreFourDifferentPictures()
    {
        string media = System.IO.Path.Combine(TestPaths.SamplesFolder, "..", "media");

        var icons = new[] { "app.ico", "app-busy.ico", "app-error.ico", "app-paused.ico" }
            .Select(name => Convert.ToHexString(File.ReadAllBytes(System.IO.Path.Combine(media, name))))
            .ToList();

        Assert.Equal(4, icons.Distinct().Count());
    }

    // ---------------------------------------------------------------- which icon is shown

    [Theory]
    // state, paused, expected icon
    [InlineData(TrayState.Ok, false, TrayState.Ok)]        // A. normal: operating normally
    [InlineData(TrayState.Ok, true, TrayState.Paused)]     // B. paused: pause symbol at the bottom right
    [InlineData(TrayState.Busy, false, TrayState.Busy)]
    [InlineData(TrayState.Busy, true, TrayState.Busy)]     // a running conversion is really running
    [InlineData(TrayState.Error, false, TrayState.Error)]
    [InlineData(TrayState.Error, true, TrayState.Error)]   // an error is never hidden by the pause
    public void TheIconFollowsTheState_AndThePauseSwitch(TrayState state, bool paused, TrayState expected)
    {
        Assert.Equal(expected, TrayStateRules.Displayed(state, paused));
    }

    [Fact]
    public void ResumingAfterAPause_GivesTheNormalIconBack()
    {
        Assert.Equal(TrayState.Paused, TrayStateRules.Displayed(TrayState.Ok, paused: true));
        Assert.Equal(TrayState.Ok, TrayStateRules.Displayed(TrayState.Ok, paused: false));
    }
}
