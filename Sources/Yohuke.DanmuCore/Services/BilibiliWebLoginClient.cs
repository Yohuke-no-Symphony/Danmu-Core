using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Yohuke.DanmuCore.Apis;
using Yohuke.DanmuCore.Diagnostics;
using Yohuke.DanmuCore.Models.Accounts;

namespace Yohuke.DanmuCore.Services;

/// <summary>隔离单次登录的 Cookie、平台挑战和刷新令牌，不依赖浏览器或持久化。</summary>
public sealed class BilibiliWebLoginClient : IDisposable
{
    private const string PASSPORT_ORIGIN = "https://passport.bilibili.com";
    private const string REFRESH_MODULUS = "cb81dd8e02470656da04dd38544446e2a3412051cfe9adc6a330a5ef90228509684960970b91c3360ca29c49e1690ff8fa068cb9dfc6179d1e9585cb9424e847db1ef59f33e37dd4dca8ccfb7631ee9b4a92640d00c8204300152a0ab7cd802889d3445aec69918fe6022b534912e7b095be3424dad1ba81145e969b533181f1";
    private readonly CookieContainer cookies = new();
    private readonly HttpClient client;
    private readonly bool ownsClient;
    private string refreshToken = "";

    /// <summary>为每个登录流程创建独立客户端；注入客户端须禁止自动重定向。</summary>
    public BilibiliWebLoginClient(HttpClient? httpClient = null, bool disposeClient = false)
    {
        ownsClient = httpClient is null || disposeClient;
        client = httpClient ?? new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
    }

    /// <summary>创建官方二维码内容和轮询键。</summary>
    public async Task<BilibiliQrLogin> BeginQrAsync(CancellationToken cancellationToken = default)
    {
        var data = await GetDataAsync("/x/passport-login/web/qrcode/generate?source=main_web", cancellationToken);
        return new(Required(data, "url"), Required(data, "qrcode_key"));
    }

    /// <summary>轮询二维码状态，并在确认后收集后台凭据。</summary>
    public async Task<BilibiliWebLoginResult> PollQrAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var data = await GetDataAsync($"/x/passport-login/web/qrcode/poll?qrcode_key={Uri.EscapeDataString(key)}&source=main_web", cancellationToken);
        return data.Value<int>("code") switch
        {
            86101 => new("Waiting"),
            86090 => new("Scanned"),
            86038 => new("Expired"),
            0 => await CompleteAsync(data, cancellationToken),
            _ => new("Failed", Message: DiagnosticSanitizer.Sanitize(data.Value<string>("message") ?? "平台登录失败。"))
        };
    }

    /// <summary>获取官方极验或图片验证码挑战。</summary>
    public async Task<BilibiliLoginChallenge> GetChallengeAsync(CancellationToken cancellationToken = default)
    {
        var data = await GetDataAsync("/x/passport-login/captcha?source=main_web", cancellationToken);
        var type = data.Value<string>("type") ?? data.Value<string>("recaptcha_type") ?? "";
        var token = data.Value<string>("token") ?? data.Value<string>("recaptcha_token") ?? "";
        var gt = data["geetest"]?.Value<string>("gt") ?? data.Value<string>("gee_gt") ?? "";
        var challenge = data["geetest"]?.Value<string>("challenge") ?? data.Value<string>("gee_challenge") ?? "";
        if (string.IsNullOrWhiteSpace(token) || type is not ("geetest" or "geetest4" or "img"))
        {
            throw new InvalidDataException("平台返回了未知的验证码挑战。");
        }
        return new(type, token, gt, challenge, type == "img" ? $"https://api.bilibili.com/x/recaptcha/img?token={Uri.EscapeDataString(token)}" : null);
    }

    /// <summary>提交用户完成的挑战并请求短信，返回平台短信会话键。</summary>
    public async Task<string> SendSmsAsync(string countryCode, string phone, BilibiliLoginChallenge challenge, BilibiliChallengeProof proof, CancellationToken cancellationToken = default)
    {
        ValidatePhone(countryCode, phone);
        if (proof.Token != challenge.Token)
        {
            throw new ArgumentException("验证码挑战已变更，请重新完成验证。", nameof(proof));
        }
        var form = new Dictionary<string, string>
        {
            ["cid"] = countryCode,
            ["tel"] = phone,
            ["source"] = "main_web",
            ["go_url"] = "https://www.bilibili.com",
            ["token"] = proof.Token
        };
        switch (challenge.Type)
        {
            case "geetest":
                form["challenge"] = RequiredProof(proof.Challenge);
                form["validate"] = RequiredProof(proof.Validate);
                form["seccode"] = RequiredProof(proof.Seccode);
                break;
            case "geetest4":
                form["lot_number"] = RequiredProof(proof.LotNumber);
                form["captcha_output"] = RequiredProof(proof.CaptchaOutput);
                form["pass_token"] = RequiredProof(proof.PassToken);
                form["gen_time"] = RequiredProof(proof.GenTime);
                break;
            case "img":
                form["captcha"] = RequiredProof(proof.Captcha);
                break;
            default:
                throw new ArgumentException("不支持的验证码挑战。", nameof(challenge));
        }
        var data = await PostDataAsync("/x/passport-login/web/sms/send", form, cancellationToken);
        return Required(data, "captcha_key");
    }

    /// <summary>提交平台短信验证码，支持继续安全验证的返回状态。</summary>
    public async Task<BilibiliWebLoginResult> CompleteSmsAsync(string countryCode, string phone, string captchaKey, string code, CancellationToken cancellationToken = default)
    {
        ValidatePhone(countryCode, phone);
        if (code.Length != 6 || !code.All(char.IsAsciiDigit) || string.IsNullOrWhiteSpace(captchaKey))
        {
            throw new ArgumentException("请输入六位短信验证码。", nameof(code));
        }
        var data = await PostDataAsync("/x/passport-login/web/login/sms", new Dictionary<string, string>
        {
            ["cid"] = countryCode, ["tel"] = phone, ["captcha_key"] = captchaKey, ["code"] = code,
            ["source"] = "main_web", ["go_url"] = "https://www.bilibili.com"
        }, cancellationToken);
        return await CompleteAsync(data, cancellationToken);
    }

    /// <summary>按平台检查结果刷新凭据；确认请求失败时保留新凭据和待确认令牌。</summary>
    public async Task<BilibiliLoginCredentials> RefreshAsync(BilibiliLoginCredentials credentials, CancellationToken cancellationToken = default)
    {
        ImportCredentials(credentials);
        if (!string.IsNullOrWhiteSpace(credentials.PendingConfirmationToken))
        {
            await ConfirmRefreshAsync(credentials.PendingConfirmationToken, cancellationToken);
        }
        var csrf = ApiCookieHelper.GetValue(CurrentCookie(), "bili_jct");
        var info = await GetDataAsync($"/x/passport-login/web/cookie/info?csrf={Uri.EscapeDataString(csrf)}", cancellationToken);
        if (info.Value<bool?>("refresh") != true)
        {
            return credentials with { Cookie = CurrentCookie(), PendingConfirmationToken = "" };
        }
        if (string.IsNullOrWhiteSpace(credentials.RefreshToken))
        {
            throw new InvalidOperationException("账号缺少刷新令牌，需要管理员重新登录。");
        }
        var timestamp = info.Value<long>("timestamp");
        using var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters { Modulus = Convert.FromHexString(REFRESH_MODULUS), Exponent = [1, 0, 1] });
        var path = Convert.ToHexString(rsa.Encrypt(Encoding.UTF8.GetBytes($"refresh_{timestamp}"), RSAEncryptionPadding.OaepSHA256)).ToLowerInvariant();
        using var response = await SendAsync(HttpMethod.Get, $"https://www.bilibili.com/correspond/1/{path}", null, cancellationToken);
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var match = Regex.Match(html, "<div\\b[^>]*\\bid=[\"']1-name[\"'][^>]*>\\s*([^<]+?)\\s*</div>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (!match.Success)
        {
            throw new InvalidDataException("平台没有返回刷新挑战，请重新登录。");
        }
        var refreshed = await PostDataAsync("/x/passport-login/web/cookie/refresh", new Dictionary<string, string>
        {
            ["csrf"] = csrf, ["refresh_csrf"] = WebUtility.HtmlDecode(match.Groups[1].Value), ["source"] = "main_web", ["refresh_token"] = credentials.RefreshToken
        }, cancellationToken);
        refreshToken = Required(refreshed, "refresh_token");
        var result = CreateCredentials() with { PendingConfirmationToken = credentials.RefreshToken };
        try
        {
            await ConfirmRefreshAsync(credentials.RefreshToken, cancellationToken);
            return result with { PendingConfirmationToken = "" };
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or OperationCanceledException)
        {
            return result;
        }
    }

    /// <summary>释放由当前流程持有的 HTTP 客户端。</summary>
    public void Dispose()
    {
        if (ownsClient)
        {
            client.Dispose();
        }
    }

    private async Task<BilibiliWebLoginResult> CompleteAsync(JObject data, CancellationToken cancellationToken)
    {
        var url = data.Value<string>("url");
        if (data.Value<int?>("status") is { } status && status != 0)
        {
            return new("VerificationRequired", VerificationUrl: IsAllowedUrl(url) ? url : null, Message: "请在 B站官方页面完成额外安全验证后重新登录。");
        }
        refreshToken = data.Value<string>("refresh_token") ?? refreshToken;
        if (string.IsNullOrWhiteSpace(ApiCookieHelper.GetValue(CurrentCookie(), "SESSDATA")) && !string.IsNullOrWhiteSpace(url))
        {
            await FollowCallbackAsync(url, cancellationToken);
        }
        var credentials = CreateCredentials();
        if (string.IsNullOrWhiteSpace(ApiCookieHelper.GetValue(credentials.Cookie, "SESSDATA")) || string.IsNullOrWhiteSpace(ApiCookieHelper.GetValue(credentials.Cookie, "bili_jct")))
        {
            return new("Failed", Message: "平台未返回完整登录凭据，请重新登录。");
        }
        return new("Complete", credentials);
    }

    private async Task FollowCallbackAsync(string url, CancellationToken cancellationToken)
    {
        for (var count = 0; count < 8; count++)
        {
            if (!IsAllowedUrl(url))
            {
                throw new InvalidDataException("平台登录回跳地址无效。");
            }
            using var response = await SendAsync(HttpMethod.Get, url, null, cancellationToken, allowRedirect: true);
            if ((int)response.StatusCode is < 300 or >= 400)
            {
                return;
            }
            var location = response.Headers.Location ?? throw new InvalidDataException("登录回跳缺少目标地址。");
            url = location.IsAbsoluteUri ? location.AbsoluteUri : new Uri(new Uri(url), location).AbsoluteUri;
        }
        throw new InvalidDataException("登录回跳次数超过限制。");
    }

    private async Task ConfirmRefreshAsync(string previousToken, CancellationToken cancellationToken) =>
        _ = await PostDataAsync("/x/passport-login/web/confirm/refresh", new Dictionary<string, string> { ["csrf"] = ApiCookieHelper.GetValue(CurrentCookie(), "bili_jct"), ["refresh_token"] = previousToken }, cancellationToken);

    private async Task<JObject> GetDataAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, PASSPORT_ORIGIN + path, null, cancellationToken);
        return ParseData(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private async Task<JObject> PostDataAsync(string path, IReadOnlyDictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, PASSPORT_ORIGIN + path, form, cancellationToken);
        return ParseData(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, IReadOnlyDictionary<string, string>? form, CancellationToken cancellationToken, bool allowRedirect = false)
    {
        var uri = new Uri(url);
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation("User-Agent", BaseApi.DEFAULT_USER_AGENT);
        request.Headers.TryAddWithoutValidation("Referer", PASSPORT_ORIGIN + "/login");
        request.Headers.TryAddWithoutValidation("Origin", PASSPORT_ORIGIN);
        var cookie = cookies.GetCookieHeader(uri);
        if (cookie.Length > 0)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }
        if (form is not null)
        {
            request.Content = new FormUrlEncodedContent(form);
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        try
        {
            if (response.Headers.TryGetValues("Set-Cookie", out var values))
            {
                foreach (var value in values)
                {
                    cookies.SetCookies(uri, value);
                }
            }
            if (!allowRedirect || (int)response.StatusCode is < 300 or >= 400)
            {
                response.EnsureSuccessStatusCode();
            }
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private void ImportCredentials(BilibiliLoginCredentials credentials)
    {
        refreshToken = credentials.RefreshToken;
        foreach (var pair in ApiCookieHelper.Parse(credentials.Cookie))
        {
            if (pair.Value.Length > 0)
            {
                cookies.Add(new Cookie(pair.Key, pair.Value, "/", ".bilibili.com"));
            }
        }
    }

    private string CurrentCookie() => ApiCookieHelper.CreateBilibiliCookie(cookies.GetAllCookies().Cast<Cookie>());
    private BilibiliLoginCredentials CreateCredentials() => new() { Cookie = CurrentCookie(), RefreshToken = refreshToken };

    private static JObject ParseData(string json)
    {
        var root = JObject.Parse(json);
        if (root.Value<int>("code") != 0)
        {
            throw new InvalidOperationException($"B站请求失败（{root.Value<int>("code")}）：{DiagnosticSanitizer.Sanitize(root.Value<string>("message") ?? "平台拒绝请求。")}");
        }
        return root["data"] as JObject ?? new JObject();
    }

    private static string Required(JObject data, string key) => !string.IsNullOrWhiteSpace(data.Value<string>(key)) ? data.Value<string>(key)! : throw new InvalidDataException($"平台返回数据缺少 {key}。");
    private static string RequiredProof(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 16384 ? value : throw new ArgumentException("请完成平台验证码挑战。");

    private static bool IsAllowedUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo) && (IsDomain(uri.Host, "bilibili.com") || IsDomain(uri.Host, "biligame.com"));
    private static bool IsDomain(string host, string domain) => host.Equals(domain, StringComparison.OrdinalIgnoreCase) || host.EndsWith('.' + domain, StringComparison.OrdinalIgnoreCase);

    private static void ValidatePhone(string countryCode, string phone)
    {
        if (countryCode.Length is < 1 or > 5 || !countryCode.All(char.IsAsciiDigit) || phone.Length is < 5 or > 20 || !phone.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("请输入有效的国际区号和手机号。");
        }
    }
}
