using Xunit;
namespace Yohuke.DanmuCore.Tests.Services;

internal sealed class AvatarHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return send(request, cancellationToken);
    }
}
