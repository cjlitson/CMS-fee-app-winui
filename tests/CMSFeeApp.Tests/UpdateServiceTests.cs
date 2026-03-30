using System.Net;
using System.Net.Http;
using CMSFeeApp.Core.Services;
using Moq;
using Moq.Protected;
using Xunit;

namespace CMSFeeApp.Tests;

public class UpdateServiceTests
{
    private static HttpClient CreateMockHttpClient(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            });

        return new HttpClient(handler.Object);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenNewerVersionExists_ReturnsUpdateAvailable()
    {
        var json = """
            {
              "tag_name": "v99.0.0",
              "html_url": "https://github.com/cjlitson/CMS-fee-app-winui/releases/tag/v99.0.0",
              "name": "v99.0.0"
            }
            """;

        var httpClient = CreateMockHttpClient(json);
        var service = new UpdateService(httpClient);

        var result = await service.CheckForUpdateAsync();

        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("99.0.0", result.LatestVersion);
        Assert.NotNull(result.ReleaseUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenHttpFails_ReturnsFalse()
    {
        var httpClient = CreateMockHttpClient("", HttpStatusCode.NotFound);
        var service = new UpdateService(httpClient);

        var result = await service.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdateAsync_WhenSameVersion_ReturnsFalse()
    {
        // The current version will be "0.0.0" in tests (no entry assembly version)
        var json = """
            {
              "tag_name": "v0.0.0",
              "html_url": "https://github.com/cjlitson/CMS-fee-app-winui/releases/tag/v0.0.0",
              "name": "v0.0.0"
            }
            """;

        var httpClient = CreateMockHttpClient(json);
        var service = new UpdateService(httpClient);

        var result = await service.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
    }

    [Theory]
    [InlineData("2.0.0", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("1.0.0", "2.0.0", false)]
    [InlineData("1.1.0", "1.0.0", true)]
    [InlineData("1.0.1", "1.0.0", true)]
    public void IsVersionNewer_ReturnsExpectedResult(string latest, string current, bool expected)
    {
        Assert.Equal(expected, UpdateService.IsVersionNewer(latest, current));
    }
}
