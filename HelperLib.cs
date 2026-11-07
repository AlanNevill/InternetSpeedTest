using Serilog;

using System;
using System.Runtime.CompilerServices;

namespace InternetSpeedTest;

public static class HelperLib
{
    /// <summary>
    /// Helper method to create method-named scopes for logging throughout the application
    /// </summary>
    public static IDisposable BeginMethodScope([CallerMemberName] string methodName = "")
    {
        return Serilog.Context.LogContext.PushProperty( "MethodName", methodName );
    }

    /// <summary>
    /// Push both Serilog SourceContext and MethodName into the LogContext for the duration of the scope
    /// </summary>
    public static IDisposable BeginMethodScopeLocal(string sourceContext, [CallerMemberName] string methodName = "")
    {
        var source = Serilog.Context.LogContext.PushProperty( "SourceContext", sourceContext );
        var method = Serilog.Context.LogContext.PushProperty( "MethodName", methodName );
        return new CompositeDisposable( source, method );
    }

}

// File-scoped helper class (C# 11+)
file sealed class CompositeDisposable(IDisposable first, IDisposable second) : IDisposable
{
    private bool _disposed;
    
    public void Dispose()
    {
        if ( _disposed ) return;
        _disposed = true;
        try { second.Dispose(); }
        finally { first.Dispose(); }
    }
}
