﻿using FluentAssertions;
using FluentValidation.TestHelper;
using NSubstitute;
using Microsoft.AspNetCore.Http;
using CampusEats.Api.Features.Upload.Request;
using CampusEats.Api.Validators.Upload;

namespace CampusEats.Api.Tests.Validators.Upload
{
    public class UploadImageValidatorTests : IDisposable
    {
        private UploadImageValidator _validator;

        public UploadImageValidatorTests()
        {
            _validator = CreateSUT();
        }

        private UploadImageValidator CreateSUT() => new();

        public void Dispose()
        {
            _validator = null!;
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void Given_ValidImageFile_When_Validate_Then_ShouldReturnValidResult()
        {
            // Arrange
            var file = Substitute.For<IFormFile>();
            file.Length.Returns(1024L);
            file.FileName.Returns("test.jpg");
            var request = new UploadImageRequest(file);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public void Given_NullFile_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var request = new UploadImageRequest(null);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.File);
            result.Errors.Should().Contain(e => e.PropertyName == "File" && e.ErrorMessage.Contains("No file uploaded"));
        }

        [Fact]
        public void Given_EmptyFile_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var file = Substitute.For<IFormFile>();
            file.Length.Returns(0L);
            file.FileName.Returns("test.jpg");
            var request = new UploadImageRequest(file);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.File.Length);
            result.Errors.Should().Contain(e => e.PropertyName == "File.Length" && e.ErrorMessage.Contains("File is empty"));
        }

        [Fact]
        public void Given_FileExceedsMaxLimit_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var file = Substitute.For<IFormFile>();
            file.Length.Returns(5 * 1024 * 1024 + 1L); // 5MB + 1
            file.FileName.Returns("test.png");
            var request = new UploadImageRequest(file);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.File.Length);
            result.Errors.Should().Contain(e => e.PropertyName == "File.Length" && e.ErrorMessage.Contains("exceed 5MB"));
        }
        
        [Fact]
        public void Given_FileIsExactlyMaxLimit_When_Validate_Then_ShouldReturnValidResult()
        {
            // Arrange
            var file = Substitute.For<IFormFile>();
            file.Length.Returns(5 * 1024 * 1024);
            file.FileName.Returns("test.jpg");
            var request = new UploadImageRequest(file);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeTrue(); 
        }

        [Fact]
        public void Given_InvalidExtension_When_Validate_Then_ShouldReturnInvalidResult()
        {
            // Arrange
            var file = Substitute.For<IFormFile>();
            file.Length.Returns(100L);
            file.FileName.Returns("document.pdf");
            var request = new UploadImageRequest(file);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeFalse();
            result.ShouldHaveValidationErrorFor(x => x.File.FileName);
            result.Errors.Should().Contain(e => e.PropertyName == "File.FileName" && e.ErrorMessage.Contains("Invalid file type"));
        }
        
        [Fact]
        public void Given_UpperCaseExtension_When_Validate_Then_ShouldReturnValidResult()
        {
            // Arrange
            var file = Substitute.For<IFormFile>();
            file.Length.Returns(1024L);
            file.FileName.Returns("TEST.JPG"); 
            var request = new UploadImageRequest(file);

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.IsValid.Should().BeTrue(); 
        }
    }
}

