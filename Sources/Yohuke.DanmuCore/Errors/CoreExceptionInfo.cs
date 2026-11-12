using System.Diagnostics.CodeAnalysis;

namespace Yohuke.DanmuCore.Errors;

/// <summary>为真实异常类型附加共享错误语义，供宿主按当前语言显示。</summary>
public static class CoreExceptionInfo
{
    /// <summary>创建带固定英文诊断和结构化语义的异常。</summary>
    public static TException Create<TException>(CoreErrorCode code, Func<string, TException> create,
        params object?[] arguments) where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(create);
        var error = new CoreErrorInfo(code, arguments);
        return Attach(create(error.DiagnosticMessage), error);
    }

    /// <summary>向原异常附加错误描述，保留其实际类型、内部异常和其他元数据。</summary>
    public static TException Attach<TException>(TException exception, CoreErrorInfo error) where TException : Exception
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(error);
        exception.Data[typeof(CoreExceptionInfo)] = error;
        return exception;
    }

    /// <summary>读取附加在原异常上的共享错误描述。</summary>
    public static bool TryGet(Exception exception, [NotNullWhen(true)] out CoreErrorInfo? error)
    {
        ArgumentNullException.ThrowIfNull(exception);
        error = exception.Data[typeof(CoreExceptionInfo)] as CoreErrorInfo;
        return error is not null;
    }
}
