﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using CampusEats.Api.Features.Upload.Handler;
using CampusEats.Api.Features.Upload.Request;

namespace CampusEats.Api.Tests.Features.Upload
{
    public class UploadImageHandlerTests : IDisposable
    {
        private readonly string _tempWebRoot;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<UploadImageHandler> _logger;
        private readonly UploadImageHandler _handler;

        public UploadImageHandlerTests()
        {
            _tempWebRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempWebRoot);

            _env = Substitute.For<IWebHostEnvironment>();
            _env.WebRootPath.Returns(_tempWebRoot);
            _env.ContentRootPath.Returns(_tempWebRoot);

            _logger = Substitute.For<ILogger<UploadImageHandler>>();

            _handler = new UploadImageHandler(_env, _logger);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempWebRoot))
                {
                    Directory.Delete(_tempWebRoot, recursive: true);
                }
            }
            catch
            {
                // ignore cleanup exceptions in tests
            }
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task Given_NullFile_When_Handle_Then_ReturnsBadRequest()
        {
            // Arrange
            var request = new UploadImageRequest(null!);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.GetType().Name.Should().Contain("BadRequest");
        }

        [Fact]
        public async Task Given_EmptyFile_When_Handle_Then_ReturnsBadRequest()
        {
            // Arrange
            var file = Substitute.For<IFormFile>();
            file.Length.Returns(0L);

            var request = new UploadImageRequest(file);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.GetType().Name.Should().Contain("BadRequest");
        }

        [Fact]
        public async Task Given_ValidImageFile_When_Handle_Then_ReturnsOkAndSavesFileToDisk()
        {
            // Arrange
            var fileName = "test.jpg";
            var content = new byte[] { 1, 2, 3, 4, 5 };

            var file = Substitute.For<IFormFile>();
            file.FileName.Returns(fileName);
            file.Length.Returns((long)content.Length);

            await using var sourceStream = new MemoryStream(content);
            file.CopyToAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var targetStream = ci.ArgAt<Stream>(0);
                    return sourceStream.CopyToAsync(targetStream, ci.ArgAt<CancellationToken>(1));
                });

            var request = new UploadImageRequest(file);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.GetType().Name.Should().Contain("Ok");

            var valueProp = result.GetType().GetProperty("Value");
            valueProp.Should().NotBeNull();
            var value = valueProp!.GetValue(result);
            value.Should().NotBeNull();

            var pathProp = value!.GetType().GetProperty("path");
            pathProp.Should().NotBeNull();
            var pathValue = pathProp!.GetValue(value) as string;
            pathValue.Should().NotBeNullOrWhiteSpace();
            pathValue!.Should().StartWith("/images/menuitems/");

            // Verify file exists on disk under temp web root
            var relativePath = pathValue.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(_tempWebRoot, relativePath);
            File.Exists(fullPath).Should().BeTrue();
        }

        [Fact]
        public async Task Given_ExceptionDuringCopy_When_Handle_Then_ReturnsProblem()
        {
            // Arrange
            var file = Substitute.For<IFormFile>();
            file.FileName.Returns("error.png");
            file.Length.Returns(10L);

            file.CopyToAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns<Task>(ci => throw new IOException("Simulated copy failure"));

            var request = new UploadImageRequest(file);

            // Act
            var result = await _handler.Handle(request, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.GetType().Name.Should().Contain("Problem");
        }
    }
}

