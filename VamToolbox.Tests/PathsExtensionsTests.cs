using System.IO.Abstractions.TestingHelpers;
using System.IO.Abstractions;
using VamToolbox.Helpers;
using Xunit;

namespace VamToolbox.Tests;
public class PathsExtensionsTests
{
    private readonly IFileSystem _fileSystem;

    public PathsExtensionsTests()
    {
        _fileSystem = new MockFileSystem();
    }

    [Theory]
    [InlineData("folder", "file.txt", "folder/file.txt")]
    [InlineData("folder/sub", "file.txt", "folder/sub/file.txt")]
    [InlineData("", "file.txt", "file.txt")]
    [InlineData("folder/", "sub/file.txt", "folder/sub/file.txt")]
    [InlineData("folder\\sub", "file.txt", "folder/sub/file.txt")]
    [InlineData("folder//sub", "file.txt", "folder/sub/file.txt")]
    public void SimplifyRelativePath_SimpleCases_ReturnsExpected(string localFolder, string assetPath, string expected)
    {
        // Act
        var result = _fileSystem.SimplifyRelativePath(localFolder, assetPath);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("folder/sub", "../file.txt", "folder/file.txt")]
    [InlineData("folder/sub", "../../file.txt", "file.txt")]
    [InlineData("folder/sub/sub2", "../../../file.txt", "file.txt")]
    public void SimplifyRelativePath_RelativeSegments_ReturnsExpected(string localFolder, string assetPath, string expected)
    {
        // Act
        var result = _fileSystem.SimplifyRelativePath(localFolder, assetPath);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("C:/folder", "file.txt", "C:/folder/file.txt")]
    [InlineData("Z:/folder", "file.txt", "folder/file.txt")]
    [InlineData("D:/folder/sub", "../file.txt", "D:/folder/file.txt")]
    [InlineData("Z:/folder/sub", "../../file.txt", "file.txt")]
    public void SimplifyRelativePath_AbsolutePaths_ReturnsExpected(string localFolder, string assetPath, string expected)
    {
        // Act
        var result = _fileSystem.SimplifyRelativePath(localFolder, assetPath);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("folder", "C:/file.txt", "C:/file.txt")]
    [InlineData("folder", "/absolute/file.txt", "absolute/file.txt")]
    [InlineData("", "/absolute/file.txt", "absolute/file.txt")]
    public void SimplifyRelativePath_AssetPathIsAbsolute_ReturnsExpected(string localFolder, string assetPath, string expected)
    {
        // Act
        var result = _fileSystem.SimplifyRelativePath(localFolder, assetPath);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("folder with spaces", "file name.txt", "folder with spaces/file name.txt")]
    [InlineData("folder!@#", "file$%^.txt", "folder!@#/file$%^.txt")]
    public void SimplifyRelativePath_SpecialCharacters_ReturnsExpected(string localFolder, string assetPath, string expected)
    {
        // Act
        var result = _fileSystem.SimplifyRelativePath(localFolder, assetPath);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SimplifyRelativePath_AssetPathPointsOutsideBase_ReturnsExpected()
    {
        // Arrange
        string localFolder = "folder/sub";
        string assetPath = "../../file.txt";
        string expected = "file.txt";

        // Act
        var result = _fileSystem.SimplifyRelativePath(localFolder, assetPath);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Z:/folder", "file.txt", "folder/file.txt")]
    [InlineData("Z:/folder/sub", "file.txt", "folder/sub/file.txt")]
    public void SimplifyRelativePath_BasePathIsZSlash_ReturnsExpected(string localFolder, string assetPath, string expected)
    {
        // Act
        var result = _fileSystem.SimplifyRelativePath(localFolder, assetPath);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SimplifyRelativePath_PathAlreadyStartsWithZSlash_ReplaceOccurs()
    {
        // Arrange
        string localFolder = "Z:/folder";
        string assetPath = "sub/file.txt";
        string expected = "folder/sub/file.txt";

        // Act
        var result = _fileSystem.SimplifyRelativePath(localFolder, assetPath);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("folder", "", "folder")]
    [InlineData("folder/", "", "folder")]
    public void SimplifyRelativePath_EmptyAssetPath_ReturnsExpected(string localFolder, string assetPath, string expected)
    {
        // Act
        var result = _fileSystem.SimplifyRelativePath(localFolder, assetPath);

        // Assert
        Assert.Equal(expected, result);
    }
}
