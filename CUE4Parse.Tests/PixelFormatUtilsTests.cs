using CUE4Parse.UE4.Assets.Exports.Texture;

namespace CUE4Parse.Tests;

public class PixelFormatUtilsTests
{
    [Theory]
    [InlineData("PF_BC7", EPixelFormat.PF_BC7)]
    [InlineData("BC7", EPixelFormat.PF_BC7)]
    [InlineData("DXT5", EPixelFormat.PF_DXT5)]
    [InlineData("BC5", EPixelFormat.PF_BC5)]
    [InlineData("PF_BC6H_Signed", EPixelFormat.PF_BC6H)]
    public void TryGetUnrealPixelFormat_AcceptsEnumAndCookedShortNames(string name, EPixelFormat expected)
    {
        Assert.True(PixelFormatUtils.TryGetUnrealPixelFormat(name, out var actual));
        Assert.Equal(expected, actual);
    }
}
