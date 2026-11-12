namespace Yohuke.DanmuCore.Tests.Apis;

internal sealed class ApiTestHttpHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handle) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return handle(request, cancellationToken);
    }
}
