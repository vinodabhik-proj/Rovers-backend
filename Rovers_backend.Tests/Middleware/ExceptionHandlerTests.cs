using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Rovers_backend.Api.Middleware;
using Rovers_backend.Models;
using Xunit;

namespace Rovers_backend.Tests.Middleware;

public class ExceptionHandlerTests
{
    [Fact]
    public async Task Middleware_Returns500AndJsonResponse()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var logger = new Mock<ILogger<ExceptionHandler>>();
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        RequestDelegate next = _ => throw new Exception("Exception Thrown!");

        var middleware = new ExceptionHandler(next, logger.Object, env.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        context.Response.ContentType.Should().Be("application/json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();

        var error = JsonSerializer.Deserialize<ErrorResponse>(
            responseBody,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        error!.Message.Should().Be("Exception Thrown!");
        error.StatusCode.Should().Be(500);
    }
}
