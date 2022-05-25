// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.OutputCaching.Tests;

public class OutputCachingPolicyProviderTests
{
    public static TheoryData<string> CacheableMethods
    {
        get
        {
            return new TheoryData<string>
                {
                    HttpMethods.Get,
                    HttpMethods.Head
                };
        }
    }

    public static TheoryData<string> NonCacheableMethods
    {
        get
        {
            return new TheoryData<string>
                {
                    HttpMethods.Post,
                    HttpMethods.Put,
                    HttpMethods.Delete,
                    HttpMethods.Trace,
                    HttpMethods.Connect,
                    HttpMethods.Options,
                    "",
                    null
                };
        }
    }

    [Theory]
    [MemberData(nameof(CacheableMethods))]
    public async Task AttemptOutputCaching_CacheableMethods_Allowed(string method)
    {
        var sink = new TestSink();
        var context = TestUtils.CreateTestContext(sink);
        var options = new OutputCachingOptions { DefaultPolicy = new OutputCachePolicyBuilder().Default().Enable().Build() };
        context.HttpContext.Request.Method = method;

        await new OutputCachingPolicyProvider(Options.Create(options)).OnRequestAsync(context);

        Assert.True(context.AttemptOutputCaching);
        Assert.Empty(sink.Writes);
    }

    [Theory]
    [MemberData(nameof(NonCacheableMethods))]
    public async Task AttemptOutputCaching_UncacheableMethods_NotAllowed(string method)
    {
        var sink = new TestSink();
        var context = TestUtils.CreateTestContext(sink);
        var options = new OutputCachingOptions { DefaultPolicy = new OutputCachePolicyBuilder().Default().Enable().Build() };
        context.HttpContext.Request.Method = method;

        await new OutputCachingPolicyProvider(Options.Create(options)).OnRequestAsync(context);

        Assert.False(context.AttemptOutputCaching);
        TestUtils.AssertLoggedMessages(
            sink.Writes,
            LoggedMessage.RequestMethodNotCacheable);
    }

    [Fact]
    public async Task AttemptResponseCaching_AuthorizationHeaders_NotAllowed()
    {
        var sink = new TestSink();
        var context = TestUtils.CreateTestContext(sink);
        context.HttpContext.Request.Method = HttpMethods.Get;
        context.HttpContext.Request.Headers.Authorization = "Placeholder";

        var options = new OutputCachingOptions { DefaultPolicy = new OutputCachePolicyBuilder().Default().Enable().Build() };

        await new OutputCachingPolicyProvider(Options.Create(options)).OnRequestAsync(context);

        Assert.False(context.AttemptOutputCaching);

        TestUtils.AssertLoggedMessages(
            sink.Writes,
            LoggedMessage.RequestWithAuthorizationNotCacheable);
    }

    [Fact]
    public async Task AllowCacheStorage_NoStore_Allowed()
    {
        var sink = new TestSink();
        var context = TestUtils.CreateTestContext(sink);
        context.HttpContext.Request.Method = HttpMethods.Get;
        context.HttpContext.Request.Headers.CacheControl = new CacheControlHeaderValue()
        {
            NoStore = true
        }.ToString();

        var options = new OutputCachingOptions { DefaultPolicy = new OutputCachePolicyBuilder().Default().Enable().Build() };
        await new OutputCachingPolicyProvider(Options.Create(options)).OnRequestAsync(context);

        Assert.True(context.AllowCacheStorage);
        Assert.Empty(sink.Writes);
    }

    [Fact]
    public async Task AllowCacheLookup_LegacyDirectives_OverridenByCacheControl()
    {
        var sink = new TestSink();
        var context = TestUtils.CreateTestContext(sink);
        context.HttpContext.Request.Method = HttpMethods.Get;
        context.HttpContext.Request.Headers.Pragma = "no-cache";
        context.HttpContext.Request.Headers.CacheControl = "max-age=10";

        var options = new OutputCachingOptions { DefaultPolicy = new OutputCachePolicyBuilder().Default().Enable().Build() };
        await new OutputCachingPolicyProvider(Options.Create(options)).OnRequestAsync(context);

        Assert.True(context.AllowCacheLookup);
        Assert.Empty(sink.Writes);
    }

    [Fact]
    public async Task IsResponseCacheable_NoPublic_Allowed()
    {
        var sink = new TestSink();
        var context = TestUtils.CreateTestContext(sink);

        var options = new OutputCachingOptions { DefaultPolicy = new OutputCachePolicyBuilder().Default().Enable().Build() };
        await new OutputCachingPolicyProvider(Options.Create(options)).OnServeResponseAsync(context);

        Assert.True(context.IsResponseCacheable);
        Assert.Empty(sink.Writes);
    }

    [Fact]
    public async Task IsResponseCacheable_Public_Allowed()
    {
        var sink = new TestSink();
        var context = TestUtils.CreateTestContext(sink);
        context.HttpContext.Response.Headers.CacheControl = new CacheControlHeaderValue()
        {
            Public = true
        }.ToString();

        var options = new OutputCachingOptions { DefaultPolicy = new OutputCachePolicyBuilder().Default().Enable().Build() };
        await new OutputCachingPolicyProvider(Options.Create(options)).OnServeResponseAsync(context);

        Assert.True(context.IsResponseCacheable);
        Assert.Empty(sink.Writes);
    }

    [Fact]
    public async Task IsResponseCacheable_NoCache_Allowed()
    {
        var sink = new TestSink();
        var context = TestUtils.CreateTestContext(sink);
        context.HttpContext.Response.Headers.CacheControl = new CacheControlHeaderValue()
        {
            Public = true,
            NoCache = true
        }.ToString();

        var options = new OutputCachingOptions { DefaultPolicy = new OutputCachePolicyBuilder().Default().Enable().Build() };
        await new OutputCachingPolicyProvider(Options.Create(options)).OnServeResponseAsync(context);

        Assert.True(context.IsResponseCacheable);
        Assert.Empty(sink.Writes);
    }

    [Fact]
    public async Task IsResponseCacheable_ResponseNoStore_Allowed()
    {
        var sink = new TestSink();
        var context = TestUtils.CreateTestContext(sink);
        context.HttpContext.Response.Headers.CacheControl = new CacheControlHeaderValue()
        {
            Public = true,
            NoStore = true
        }.ToString();

        var options = new OutputCachingOptions { DefaultPolicy = new OutputCachePolicyBuilder().Default().Enable().Build() };
        await new OutputCachingPolicyProvider(Options.Create(options)).OnServeResponseAsync(context);

        Assert.True(context.IsResponseCacheable);
        Assert.Empty(sink.Writes);
    }

    [Fact]
    public async Task IsResponseCacheable_SetCookieHeader_NotAllowed()
    {
        var sink = new TestSink();
        var context = TestUtils.CreateTestContext(sink);
        context.HttpContext.Response.Headers.CacheControl = new CacheControlHeaderValue()
        {
            Public = true
        }.ToString();
        context.HttpContext.Response.Headers.SetCookie = "cookieName=cookieValue";

        var options = new OutputCachingOptions { DefaultPolicy = new OutputCachePolicyBuilder().Default().Enable().Build() };
        await new OutputCachingPolicyProvider(Options.Create(options)).OnServeResponseAsync(context);

        Assert.False(context.IsResponseCacheable);
        TestUtils.AssertLoggedMessages(
            sink.Writes,
            LoggedMessage.ResponseWithSetCookieNotCacheable);
    }

    [Fact]
    public async Task IsResponseCacheable_VaryHeaderByStar_Allowed()
    {
        var sink = new TestSink();
        var context = TestUtils.CreateTestContext(sink);
        context.HttpContext.Response.Headers.CacheControl = new CacheControlHeaderValue()
        {
            Public = true
        }.ToString();
        context.HttpContext.Response.Headers.Vary = "*";

        var options = new OutputCachingOptions { DefaultPolicy = new OutputCachePolicyBuilder().Default().Enable().Build() };
        await new OutputCachingPolicyProvider(Options.Create(options)).OnServeResponseAsync(context);

        Assert.True(context.IsResponseCacheable);
        Assert.Empty(sink.Writes);
    }

    [Fact]
    public async Task IsResponseCacheable_Private_Allowed()
    {
        var sink = new TestSink();
        var context = TestUtils.CreateTestContext(sink);
        context.HttpContext.Response.Headers.CacheControl = new CacheControlHeaderValue()
        {
            Public = true,
            Private = true
        }.ToString();

        var options = new OutputCachingOptions { DefaultPolicy = new OutputCachePolicyBuilder().Default().Enable().Build() };
        await new OutputCachingPolicyProvider(Options.Create(options)).OnServeResponseAsync(context);

        Assert.True(context.IsResponseCacheable);
        Assert.Empty(sink.Writes);
    }

    [Theory]
    [InlineData(StatusCodes.Status200OK)]
    public async Task IsResponseCacheable_SuccessStatusCodes_Allowed(int statusCode)
    {
        var sink = new TestSink();
        var context = TestUtils.CreateTestContext(sink);
        context.HttpContext.Response.StatusCode = statusCode;
        context.HttpContext.Response.Headers.CacheControl = new CacheControlHeaderValue()
        {
            Public = true
        }.ToString();

        var options = new OutputCachingOptions { DefaultPolicy = new OutputCachePolicyBuilder().Default().Enable().Build() };
        await new OutputCachingPolicyProvider(Options.Create(options)).OnServeResponseAsync(context);

        Assert.True(context.IsResponseCacheable);
        Assert.Empty(sink.Writes);
    }

    [Theory]
    [InlineData(StatusCodes.Status100Continue)]
    [InlineData(StatusCodes.Status101SwitchingProtocols)]
    [InlineData(StatusCodes.Status102Processing)]
    [InlineData(StatusCodes.Status201Created)]
    [InlineData(StatusCodes.Status202Accepted)]
    [InlineData(StatusCodes.Status203NonAuthoritative)]
    [InlineData(StatusCodes.Status204NoContent)]
    [InlineData(StatusCodes.Status205ResetContent)]
    [InlineData(StatusCodes.Status206PartialContent)]
    [InlineData(StatusCodes.Status207MultiStatus)]
    [InlineData(StatusCodes.Status208AlreadyReported)]
    [InlineData(StatusCodes.Status226IMUsed)]
    [InlineData(StatusCodes.Status300MultipleChoices)]
    [InlineData(StatusCodes.Status301MovedPermanently)]
    [InlineData(StatusCodes.Status302Found)]
    [InlineData(StatusCodes.Status303SeeOther)]
    [InlineData(StatusCodes.Status304NotModified)]
    [InlineData(StatusCodes.Status305UseProxy)]
    [InlineData(StatusCodes.Status306SwitchProxy)]
    [InlineData(StatusCodes.Status307TemporaryRedirect)]
    [InlineData(StatusCodes.Status308PermanentRedirect)]
    [InlineData(StatusCodes.Status400BadRequest)]
    [InlineData(StatusCodes.Status401Unauthorized)]
    [InlineData(StatusCodes.Status402PaymentRequired)]
    [InlineData(StatusCodes.Status403Forbidden)]
    [InlineData(StatusCodes.Status404NotFound)]
    [InlineData(StatusCodes.Status405MethodNotAllowed)]
    [InlineData(StatusCodes.Status406NotAcceptable)]
    [InlineData(StatusCodes.Status407ProxyAuthenticationRequired)]
    [InlineData(StatusCodes.Status408RequestTimeout)]
    [InlineData(StatusCodes.Status409Conflict)]
    [InlineData(StatusCodes.Status410Gone)]
    [InlineData(StatusCodes.Status411LengthRequired)]
    [InlineData(StatusCodes.Status412PreconditionFailed)]
    [InlineData(StatusCodes.Status413RequestEntityTooLarge)]
    [InlineData(StatusCodes.Status414RequestUriTooLong)]
    [InlineData(StatusCodes.Status415UnsupportedMediaType)]
    [InlineData(StatusCodes.Status416RequestedRangeNotSatisfiable)]
    [InlineData(StatusCodes.Status417ExpectationFailed)]
    [InlineData(StatusCodes.Status418ImATeapot)]
    [InlineData(StatusCodes.Status419AuthenticationTimeout)]
    [InlineData(StatusCodes.Status421MisdirectedRequest)]
    [InlineData(StatusCodes.Status422UnprocessableEntity)]
    [InlineData(StatusCodes.Status423Locked)]
    [InlineData(StatusCodes.Status424FailedDependency)]
    [InlineData(StatusCodes.Status426UpgradeRequired)]
    [InlineData(StatusCodes.Status428PreconditionRequired)]
    [InlineData(StatusCodes.Status429TooManyRequests)]
    [InlineData(StatusCodes.Status431RequestHeaderFieldsTooLarge)]
    [InlineData(StatusCodes.Status451UnavailableForLegalReasons)]
    [InlineData(StatusCodes.Status500InternalServerError)]
    [InlineData(StatusCodes.Status501NotImplemented)]
    [InlineData(StatusCodes.Status502BadGateway)]
    [InlineData(StatusCodes.Status503ServiceUnavailable)]
    [InlineData(StatusCodes.Status504GatewayTimeout)]
    [InlineData(StatusCodes.Status505HttpVersionNotsupported)]
    [InlineData(StatusCodes.Status506VariantAlsoNegotiates)]
    [InlineData(StatusCodes.Status507InsufficientStorage)]
    [InlineData(StatusCodes.Status508LoopDetected)]
    [InlineData(StatusCodes.Status510NotExtended)]
    [InlineData(StatusCodes.Status511NetworkAuthenticationRequired)]
    public async Task IsResponseCacheable_NonSuccessStatusCodes_NotAllowed(int statusCode)
    {
        var sink = new TestSink();
        var context = TestUtils.CreateTestContext(sink);
        context.HttpContext.Response.StatusCode = statusCode;
        context.HttpContext.Response.Headers.CacheControl = new CacheControlHeaderValue()
        {
            Public = true
        }.ToString();

        var options = new OutputCachingOptions { DefaultPolicy = new OutputCachePolicyBuilder().Default().Enable().Build() };
        await new OutputCachingPolicyProvider(Options.Create(options)).OnServeResponseAsync(context);

        Assert.False(context.IsResponseCacheable);
        TestUtils.AssertLoggedMessages(
            sink.Writes,
            LoggedMessage.ResponseWithUnsuccessfulStatusCodeNotCacheable);
    }

    [Fact]
    public async Task IsResponseCacheable_NoExpiryRequirements_IsAllowed()
    {
        var sink = new TestSink();
        var context = TestUtils.CreateTestContext(sink);
        context.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
        context.HttpContext.Response.Headers.CacheControl = new CacheControlHeaderValue()
        {
            Public = true
        }.ToString();

        var utcNow = DateTimeOffset.UtcNow;
        context.HttpContext.Response.Headers.Date = HeaderUtilities.FormatDate(utcNow);
        context.ResponseTime = DateTimeOffset.MaxValue;

        var options = new OutputCachingOptions { DefaultPolicy = new OutputCachePolicyBuilder().Default().Enable().Build() };
        await new OutputCachingPolicyProvider(Options.Create(options)).OnServeResponseAsync(context);

        Assert.True(context.IsResponseCacheable);
        Assert.Empty(sink.Writes);
    }

    [Fact]
    public async Task IsResponseCacheable_MaxAgeOverridesExpiry_ToAllowed()
    {
        var utcNow = DateTimeOffset.UtcNow;
        var sink = new TestSink();
        var context = TestUtils.CreateTestContext(sink);
        context.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
        context.HttpContext.Response.Headers.CacheControl = new CacheControlHeaderValue()
        {
            Public = true,
            MaxAge = TimeSpan.FromSeconds(10)
        }.ToString();
        context.HttpContext.Response.Headers.Expires = HeaderUtilities.FormatDate(utcNow);
        context.HttpContext.Response.Headers.Date = HeaderUtilities.FormatDate(utcNow);
        context.ResponseTime = utcNow + TimeSpan.FromSeconds(9);

        var options = new OutputCachingOptions { DefaultPolicy = new OutputCachePolicyBuilder().Default().Enable().Build() };
        await new OutputCachingPolicyProvider(Options.Create(options)).OnServeResponseAsync(context);

        Assert.True(context.IsResponseCacheable);
        Assert.Empty(sink.Writes);
    }

    [Fact]
    public async Task IsResponseCacheable_SharedMaxAgeOverridesMaxAge_ToAllowed()
    {
        var utcNow = DateTimeOffset.UtcNow;
        var sink = new TestSink();
        var context = TestUtils.CreateTestContext(sink);
        context.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
        context.HttpContext.Response.Headers.CacheControl = new CacheControlHeaderValue()
        {
            Public = true,
            MaxAge = TimeSpan.FromSeconds(10),
            SharedMaxAge = TimeSpan.FromSeconds(15)
        }.ToString();
        context.HttpContext.Response.Headers.Date = HeaderUtilities.FormatDate(utcNow);
        context.ResponseTime = utcNow + TimeSpan.FromSeconds(11);

        var options = new OutputCachingOptions { DefaultPolicy = new OutputCachePolicyBuilder().Default().Enable().Build() };
        await new OutputCachingPolicyProvider(Options.Create(options)).OnServeResponseAsync(context);

        Assert.True(context.IsResponseCacheable);
        Assert.Empty(sink.Writes);
    }

    [Fact]
    public async Task IsCachedEntryFresh_NoExpiryRequirements_IsFresh()
    {
        var utcNow = DateTimeOffset.UtcNow;
        var sink = new TestSink();
        var context = TestUtils.CreateTestContext(sink);
        context.ResponseTime = DateTimeOffset.MaxValue;
        context.CachedEntryAge = TimeSpan.MaxValue;

        var options = new OutputCachingOptions { DefaultPolicy = new OutputCachePolicyBuilder().Default().Enable().Build() };
        await new OutputCachingPolicyProvider(Options.Create(options)).OnServeFromCacheAsync(context);

        Assert.True(context.IsCacheEntryFresh);
        Assert.Empty(sink.Writes);
    }

    [Fact]
    public async Task IsCachedEntryFresh_AtExpiry_IsNotFresh()
    {
        var utcNow = DateTimeOffset.UtcNow;
        var sink = new TestSink();
        var context = TestUtils.CreateTestContext(sink);
        context.ResponseTime = utcNow;
        context.CachedEntryAge = TimeSpan.Zero;

        var options = new OutputCachingOptions { DefaultPolicy = new OutputCachePolicyBuilder().Default().Enable().Build() };
        await new OutputCachingPolicyProvider(Options.Create(options)).OnServeFromCacheAsync(context);

        Assert.False(context.IsCacheEntryFresh);
        TestUtils.AssertLoggedMessages(
            sink.Writes,
            LoggedMessage.ExpirationExpiresExceededNoExpiration);
    }

    [Fact]
    public async Task IsCachedEntryFresh_SharedMaxAgeOverridesMaxAge_ToFresh()
    {
        var utcNow = DateTimeOffset.UtcNow;
        var sink = new TestSink();
        var context = TestUtils.CreateTestContext(sink);
        context.CachedEntryAge = TimeSpan.FromSeconds(11);
        context.ResponseTime = utcNow + context.CachedEntryAge;
        context.CachedResponseHeaders = new HeaderDictionary();
        context.CachedResponseHeaders[HeaderNames.CacheControl] = new CacheControlHeaderValue()
        {
            Public = true,
            MaxAge = TimeSpan.FromSeconds(10),
            SharedMaxAge = TimeSpan.FromSeconds(15)
        }.ToString();
        context.CachedResponseHeaders[HeaderNames.Expires] = HeaderUtilities.FormatDate(utcNow);

        var options = new OutputCachingOptions { DefaultPolicy = new OutputCachePolicyBuilder().Default().Enable().Build() };
        await new OutputCachingPolicyProvider(Options.Create(options)).OnServeFromCacheAsync(context);

        Assert.True(context.IsCacheEntryFresh);
        Assert.Empty(sink.Writes);
    }
}
