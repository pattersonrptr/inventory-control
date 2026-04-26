using InventoryControl.Validators;
using Microsoft.AspNetCore.Http;
using Moq;

namespace InventoryControl.Tests.Unit.Validators;

public class ImageUploadValidatorTests
{
    private static IFormFile MakeFile(string fileName, long sizeBytes = 1024)
    {
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.FileName).Returns(fileName);
        mock.Setup(f => f.Length).Returns(sizeBytes);
        return mock.Object;
    }

    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.JPEG")]
    [InlineData("image.PNG")]
    [InlineData("anim.gif")]
    [InlineData("banner.webp")]
    public void Validate_AllowedExtensions_ReturnsNoErrors(string fileName)
    {
        var errors = ImageUploadValidator.Validate([MakeFile(fileName)]);
        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("script.php")]
    [InlineData("doc.pdf")]
    [InlineData("data.exe")]
    [InlineData("archive.zip")]
    [InlineData("noextension")]
    public void Validate_DisallowedExtension_ReturnsError(string fileName)
    {
        var errors = ImageUploadValidator.Validate([MakeFile(fileName)]);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains(fileName));
    }

    [Fact]
    public void Validate_FileTooLarge_ReturnsError()
    {
        var bigFile = MakeFile("photo.jpg", ImageUploadValidator.MaxFileSizeBytes + 1);
        var errors = ImageUploadValidator.Validate([bigFile]);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("photo.jpg"));
    }

    [Fact]
    public void Validate_FileSizeAtLimit_Passes()
    {
        var file = MakeFile("photo.jpg", ImageUploadValidator.MaxFileSizeBytes);
        var errors = ImageUploadValidator.Validate([file]);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MultipleFilesOneBad_ReturnsErrorForBadFile()
    {
        var files = new[]
        {
            MakeFile("good.jpg"),
            MakeFile("bad.exe")
        };
        var errors = ImageUploadValidator.Validate(files);
        Assert.Single(errors);
        Assert.Contains(errors, e => e.Contains("bad.exe"));
    }
}
