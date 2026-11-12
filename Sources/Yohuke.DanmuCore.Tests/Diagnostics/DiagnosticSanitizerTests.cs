using Yohuke.DanmuCore.Diagnostics;

namespace Yohuke.DanmuCore.Tests.Diagnostics;

/// <summary>验证公共诊断沿用已有日志凭据隐藏约定。</summary>
public sealed class DiagnosticSanitizerTests
{
    /// <summary>隐藏认证头、设备字段、URL 查询与已知敏感原文。</summary>
    [Fact]
    public void CredentialsAndExplicitSecretsAreRedactedWithTheExistingMarker()
    {
        var text = "https://example.com/path?token=one buvid4=device\nAuthorization: Bearer abc\nraw-known-secret";

        var sanitized = DiagnosticSanitizer.Sanitize(text, ["raw-known-secret"]);

        Assert.Contains("[已隐藏]", sanitized);
        Assert.DoesNotContain("one", sanitized);
        Assert.DoesNotContain("device", sanitized);
        Assert.DoesNotContain("abc", sanitized);
        Assert.DoesNotContain("raw-known-secret", sanitized);
        Assert.Equal(sanitized, DiagnosticSanitizer.Sanitize(sanitized));
    }
}
