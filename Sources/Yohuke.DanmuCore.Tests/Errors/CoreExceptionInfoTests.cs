using System.Net;
using Newtonsoft.Json;
using Yohuke.DanmuCore.Errors;

namespace Yohuke.DanmuCore.Tests.Errors;

/// <summary>验证跨应用错误契约的快照、脱敏与真实异常类型。</summary>
public sealed class CoreExceptionInfoTests
{
    /// <summary>外部参数数组变更不能修改保存的描述，凭据文本先脱敏。</summary>
    [Fact]
    public void ErrorInfoSnapshotsArgumentsAndRejectsMutableObjects()
    {
        object?[] arguments = [42, "SESSDATA=secret"];
        var error = new CoreErrorInfo(CoreErrorCode.HistoryRequestFailed, arguments);
        arguments[0] = 99;

        Assert.Equal(42, error.Arguments[0]);
        Assert.DoesNotContain("secret", error.Arguments[1]!.ToString());
        Assert.Throws<NotSupportedException>(() => ((IList<object?>)error.Arguments)[0] = 5);
        Assert.Throws<ArgumentException>(() => new CoreErrorInfo(CoreErrorCode.ApiEmptyJsonResult, new object()));
    }

    /// <summary>附着描述不改变异常对象、HTTP 状态或内部异常。</summary>
    [Fact]
    public void AttachPreservesTheOriginalExceptionAndHttpStatus()
    {
        var inner = new IOException("network");
        var exception = new HttpRequestException("failed", inner, HttpStatusCode.TooManyRequests);
        var attached = CoreExceptionInfo.Attach(exception, new(CoreErrorCode.RoomSearchFailed));

        Assert.Same(exception, attached);
        Assert.Same(inner, attached.InnerException);
        Assert.Equal(HttpStatusCode.TooManyRequests, attached.StatusCode);
        Assert.True(CoreExceptionInfo.TryGet(exception, out var error));
        Assert.Equal(CoreErrorCode.RoomSearchFailed, error.Code);
    }

    /// <summary>JSON 失败保持可捕获的原异常类型，不需要初始化任何语言资源。</summary>
    [Fact]
    public void CreateKeepsTheRequestedExceptionTypeAndDiagnosticFallback()
    {
        var exception = CoreExceptionInfo.Create(CoreErrorCode.ApiEmptyJsonResult,
            message => new JsonSerializationException(message));

        Assert.IsType<JsonSerializationException>(exception);
        Assert.Equal("The JSON response contains no result.", exception.Message);
        Assert.True(CoreExceptionInfo.TryGet(exception, out var error));
        Assert.Equal(CoreErrorCode.ApiEmptyJsonResult, error.Code);
        Assert.False(CoreExceptionInfo.TryGet(new InvalidOperationException(), out _));
    }
}
