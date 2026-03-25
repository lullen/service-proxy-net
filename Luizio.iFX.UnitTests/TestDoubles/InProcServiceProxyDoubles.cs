using Luizio.iFX.Models;
using Luizio.iFX.Server;

namespace Luizio.iFX.UnitTests.TestDoubles;

public class TestRequest { }

public class TestResponse
{
    public string Value { get; set; } = string.Empty;
}

public class TestService : IService
{
    public Task<Response<TestResponse>> Handle(TestRequest request)
        => Task.FromResult<Response<TestResponse>>(new TestResponse { Value = "ok" });

    public Task<Response<TestResponse>> ThrowingMethod(TestRequest request)
        => throw new InvalidOperationException("method failed");
}

/// <summary>
/// Captures the scoped <see cref="CurrentUser"/> state when the service method is invoked.
/// Registered as a singleton so the test can inspect it after the scope is disposed.
/// </summary>
public class CurrentUserCapture
{
    public string? CapturedToken { get; set; }
    public List<KeyValuePair<string, string>> CapturedMetadata { get; set; } = [];
}

public class UserCapturingTestService(CurrentUser currentUser, CurrentUserCapture capture) : IService
{
    public Task<Response<TestResponse>> Handle(TestRequest request)
    {
        capture.CapturedToken = currentUser.Token;
        capture.CapturedMetadata = [.. currentUser.Metadata];
        return Task.FromResult<Response<TestResponse>>(new TestResponse { Value = "captured" });
    }
}
