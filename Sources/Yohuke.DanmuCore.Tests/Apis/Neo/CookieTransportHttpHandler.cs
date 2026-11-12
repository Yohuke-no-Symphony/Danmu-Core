using System.Net;

namespace Yohuke.DanmuCore.Tests.Apis.Neo;

internal sealed class CookieTransportHttpHandler : HttpMessageHandler
{
    internal string ResponseJson { get; set; } = "";
    internal int RequestCount { get; private set; }
    internal List<IReadOnlyDictionary<string, string>> Forms { get; } = [];
    internal List<string> Cookies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RequestCount++;
        Cookies.Add(request.Headers.TryGetValues("Cookie", out var values) ? string.Join(";", values) : "");
        var form = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Forms.Add(form.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(parameter => parameter.Split('=', 2))
            .ToDictionary(pair => WebUtility.UrlDecode(pair[0]), pair => WebUtility.UrlDecode(pair[1])));
        return new(HttpStatusCode.OK) { Content = new StringContent(ResponseJson) };
    }
}
