using System.Collections.Immutable;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasaDomotica.Notifications.Application;
using CasaDomotica.Notifications.Domain;
using CasaDomotica.Notifications.Functions;
using CasaDomotica.Notifications.Infrastructure;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

public sealed class NotificationTests
{
    private sealed class FakeEmailProvider : IEmailProvider
    {
        public int SendCalls;
        public Func<DeliveryWorkItem, CancellationToken, Task<EmailDispatchResult>>? Handler;
        public Func<string, CancellationToken, Task<EmailDispatchResult?>>? StatusHandler;

        public Task<EmailDispatchResult> SendAsync(DeliveryWorkItem delivery, CancellationToken ct)
        {
            SendCalls++;
            if (Handler is not null) return Handler(delivery, ct);
            return Task.FromResult(new EmailDispatchResult(DeliveryStatuses.Accepted, $"op-{SendCalls}"));
        }

        public Task<EmailDispatchResult?> CheckOperationAsync(string operationId, CancellationToken ct) =>
            StatusHandler?.Invoke(operationId, ct) ?? Task.FromResult<EmailDispatchResult?>(null);

        public Task VerifyConfigurationAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeAuthorizer(Guid allowedHouse, Guid resident, Guid admin, bool revoked = false) : INotificationsAuthorizer
    {
        public Task<bool> CheckAsync(Guid userId, Guid sessionId, Guid houseId, string action, Guid correlationId, CancellationToken ct)
        {
            if (revoked) throw new NotificationsAuthorizationException("sessionRevoked");
            return Task.FromResult(houseId == allowedHouse && (userId == admin || userId == resident && action == "notifications:read"));
        }
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }
    }

    private sealed class FakeFunctionContext : FunctionContext
    {
        public override string InvocationId { get; } = Guid.NewGuid().ToString("N");
        public override string FunctionId { get; } = "fn-test";
        public override TraceContext TraceContext { get; } = null!;
        public override BindingContext BindingContext { get; } = null!;
        public override RetryContext RetryContext { get; } = null!;
        public override IServiceProvider InstanceServices { get; set; } = null!;
        public override FunctionDefinition FunctionDefinition { get; } = null!;
        public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();
        public override IInvocationFeatures Features { get; } = null!;
    }

    private sealed class FakeHttpResponseData(FunctionContext context, HttpStatusCode statusCode) : HttpResponseData(context)
    {
        public override HttpStatusCode StatusCode { get; set; } = statusCode;
        public override HttpHeadersCollection Headers { get; set; } = new();
        public override Stream Body { get; set; } = new MemoryStream();
        public override HttpCookies Cookies { get; } = null!;

        public string ReadBodyText()
        {
            Body.Position = 0;
            using var reader = new StreamReader(Body, Encoding.UTF8, leaveOpen: true);
            return reader.ReadToEnd();
        }
    }

    private sealed class FakeHttpRequestData : HttpRequestData
    {
        private readonly Stream _body;
        public FakeHttpRequestData(FunctionContext context, string method, string url, string? body = null) : base(context)
        {
            Method = method;
            Url = new Uri(url);
            Headers = new HttpHeadersCollection();
            _body = new MemoryStream(Encoding.UTF8.GetBytes(body ?? string.Empty));
        }

        public override Stream Body => _body;
        public override HttpHeadersCollection Headers { get; }
        public override IReadOnlyCollection<IHttpCookie> Cookies => ImmutableArray<IHttpCookie>.Empty;
        public override Uri Url { get; }
        public override IEnumerable<ClaimsIdentity> Identities => Enumerable.Empty<ClaimsIdentity>();
        public override string Method { get; }
        public override HttpResponseData CreateResponse() => new FakeHttpResponseData(FunctionContext, HttpStatusCode.OK);
    }

    private static async Task<(SqliteConnection Connection, NotificationsDb Db)> CreateDbAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<NotificationsDb>().UseSqlite(connection).Options;
        var db = new NotificationsDb(options);
        await db.Database.EnsureCreatedAsync();
        return (connection, db);
    }

    [Fact]
    public void DomainRejectsInvalidEventsAndBlocksRetryOnAmbiguousOrAcceptedStates()
    {
        Assert.True(DeliveryStatuses.CanAttemptSend(DeliveryStatuses.Requested));
        Assert.False(DeliveryStatuses.CanAttemptSend(DeliveryStatuses.Unknown));
        Assert.False(DeliveryStatuses.CanAttemptSend(DeliveryStatuses.Accepted));
        Assert.False(DeliveryStatuses.CanAttemptSend(DeliveryStatuses.Delivered));
        Assert.False(DeliveryStatuses.CanAttemptSend(DeliveryStatuses.Failed));

        var house = Guid.NewGuid();
        var evtId = Guid.NewGuid();
        var corrId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var incidentJson = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            type = "SecurityIncidentCreated",
            eventId = evtId,
            correlationId = corrId,
            houseId = house,
            occurredAtUtc = now,
            receivedAtUtc = now,
            payload = new { incidentId = Guid.NewGuid(), roomId = "sala", capturesEnabled = true }
        });
        var parsed = NotificationProjection.Parse(incidentJson);
        Assert.Equal("SecurityIncidentCreated", parsed.Type);
        Assert.Contains("sala", parsed.Subject);

        var accessJson = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            type = "AccessRecorded",
            eventId = Guid.NewGuid(),
            correlationId = corrId,
            houseId = house,
            occurredAtUtc = now,
            receivedAtUtc = now,
            payload = new { activityId = Guid.NewGuid(), result = "executed", roomId = (string?)null }
        });
        var access = NotificationProjection.Parse(accessJson);
        Assert.Contains("puerta principal", access.BodyText);

        var future = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            type = "SecurityIncidentCreated",
            eventId = evtId,
            correlationId = corrId,
            houseId = house,
            occurredAtUtc = now.AddMinutes(5),
            receivedAtUtc = now,
            payload = new { incidentId = Guid.NewGuid(), roomId = "sala" }
        });
        Assert.Throws<InvalidDataException>(() => NotificationProjection.Parse(future));
        Assert.Throws<InvalidDataException>(() => NotificationProjection.Parse("{\"schemaVersion\":2}"));
    }

    [Fact]
    public async Task RecipientVerificationAndPreferencesBlockUnverifiedEmailEnabling()
    {
        var (connection, db) = await CreateDbAsync();
        await using var cScope = connection;
        await using var dScope = db;

        var house = Guid.NewGuid();
        var provider = new FakeEmailProvider();
        var ops = new NotificationOperations(new SqlNotificationsStore(db), provider, TimeProvider.System);

        var unverifiedEx = await Assert.ThrowsAsync<NotificationException>(() => ops.SavePreferencesAsync(house, true, default));
        Assert.Equal("recipientNotVerified", unverifiedEx.Code);
        Assert.Equal(422, unverifiedEx.StatusCode);

        var testEx = await Assert.ThrowsAsync<NotificationException>(() => ops.RequestTestEmailAsync(house, null, Guid.NewGuid(), true, default));
        Assert.Equal("recipientNotVerified", testEx.Code);

        // Request verification challenge in dryRun mode (no external send invoked)
        var challenge = await ops.RequestRecipientVerificationAsync(house, "User@Example.COM ", Guid.NewGuid(), dryRun: true, default);
        Assert.Equal("user@example.com", challenge.PendingRecipientEmail);
        Assert.Equal("dryRun", challenge.Status);
        Assert.Equal(0, provider.SendCalls);

        // Wrong code is rejected
        var invalidEx = await Assert.ThrowsAsync<NotificationException>(() => ops.ConfirmRecipientVerificationAsync(house, "000000", default));
        Assert.Equal("invalidVerificationCode", invalidEx.Code);

        // Replace hash cleanly in test DB to confirm fixed-time verification & enabling email
        var row = await db.Preferences.SingleAsync(x => x.HouseId == house);
        Assert.NotEqual("654321", row.VerificationCodeHash);
        row.VerificationCodeHash = NotificationOperations.HashSecret("654321");
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var confirmed = await ops.ConfirmRecipientVerificationAsync(house, "654321", default);
        Assert.Equal("user@example.com", confirmed.VerifiedEmail);
        Assert.NotNull(confirmed.VerifiedAtUtc);

        var saved = await ops.SavePreferencesAsync(house, true, default);
        Assert.True(saved.Email);

        var dryDelivery = await ops.RequestTestEmailAsync(house, "dry-key-1", Guid.NewGuid(), dryRun: true, default);
        Assert.Equal(DeliveryStatuses.Requested, dryDelivery.Status);
        Assert.Equal("dryRun", dryDelivery.ErrorCode);
        Assert.Equal(0, provider.SendCalls);
    }

    [Fact]
    public async Task AmbiguousSendBecomesUnknownAndNeverDuplicatesOnIdempotentRetry()
    {
        var (connection, db) = await CreateDbAsync();
        await using var cScope = connection;
        await using var dScope = db;

        var house = Guid.NewGuid();
        db.Preferences.Add(new NotificationPreferenceRow
        {
            HouseId = house,
            EmailEnabled = true,
            VerifiedEmail = "admin@example.com",
            VerifiedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var provider = new FakeEmailProvider
        {
            Handler = (_, _) => throw new TimeoutException("Provider timed out after HTTP transmission")
        };
        var ops = new NotificationOperations(new SqlNotificationsStore(db), provider, TimeProvider.System);

        var first = await ops.RequestTestEmailAsync(house, "retry-key-1", Guid.NewGuid(), dryRun: false, default);
        Assert.Equal(DeliveryStatuses.Unknown, first.Status);
        Assert.Equal("providerAmbiguous", first.ErrorCode);
        Assert.Equal(1, provider.SendCalls);

        // Retrying the same key when status is unknown must NOT resend
        provider.Handler = (_, _) => Task.FromResult(new EmailDispatchResult(DeliveryStatuses.Accepted, "op-should-not-run"));
        var second = await ops.RequestTestEmailAsync(house, "retry-key-1", Guid.NewGuid(), dryRun: false, default);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(DeliveryStatuses.Unknown, second.Status);
        Assert.Equal(1, provider.SendCalls);
    }

    [Fact]
    public async Task AcceptedIsDistinctFromDeliveredAndServiceBusDeduplicates()
    {
        var (connection, db) = await CreateDbAsync();
        await using var cScope = connection;
        await using var dScope = db;

        var house = Guid.NewGuid();
        var provider = new FakeEmailProvider
        {
            StatusHandler = (opId, _) => Task.FromResult<EmailDispatchResult?>(new EmailDispatchResult(DeliveryStatuses.Accepted, opId))
        };
        var ops = new NotificationOperations(new SqlNotificationsStore(db), provider, TimeProvider.System);

        // Event received while disabled -> recorded in Inbox as skippedDisabled, 0 emails sent
        var evtIdDisabled = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        string MakeEvent(Guid id, string room) => JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            type = "SecurityIncidentCreated",
            eventId = id,
            correlationId = Guid.NewGuid(),
            houseId = house,
            occurredAtUtc = now,
            receivedAtUtc = now,
            payload = new { incidentId = Guid.NewGuid(), roomId = room }
        });

        await ops.IngestEventAsync(evtIdDisabled.ToString(), MakeEvent(evtIdDisabled, "entrada"), default);
        Assert.Equal(0, provider.SendCalls);
        Assert.Equal("skippedDisabled", (await db.Inbox.SingleAsync(x => x.EventId == evtIdDisabled)).Outcome);

        // Configure verified email + enable
        db.Preferences.Add(new NotificationPreferenceRow
        {
            HouseId = house,
            EmailEnabled = true,
            VerifiedEmail = "owner@example.com",
            VerifiedAtUtc = now,
            UpdatedAtUtc = now
        });
        await db.SaveChangesAsync();

        var evtId = Guid.NewGuid();
        var body = MakeEvent(evtId, "cochera");
        await ops.IngestEventAsync(evtId.ToString(), body, default);
        await ops.IngestEventAsync(evtId.ToString(), body, default); // duplicate delivery
        Assert.Equal(1, provider.SendCalls);
        await Assert.ThrowsAsync<InvalidDataException>(() => ops.IngestEventAsync(evtId.ToString(), body.Replace("cochera", "patio"), default));

        var delivery = await db.Deliveries.AsNoTracking().SingleAsync();
        Assert.Equal(DeliveryStatuses.Accepted, delivery.Status);

        var fetched = await ops.GetDeliveryAsync(house, delivery.DeliveryId, default);
        Assert.NotNull(fetched);
        Assert.Equal(DeliveryStatuses.Accepted, fetched!.Status);

        await ops.ApplyDeliveryReportAsync(house, delivery.DeliveryId, DeliveryStatuses.Delivered, null, default);
        var delivered = await ops.GetDeliveryAsync(house, delivery.DeliveryId, default);
        Assert.Equal(DeliveryStatuses.Delivered, delivered!.Status);
    }

    [Fact]
    public async Task AcsEmailProviderClassifies202vs4xxvs5xxAndSignsRequests()
    {
        var accessKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:ConnectionString"] = $"endpoint=https://acs-test.communication.azure.com/;accesskey={accessKey}",
            ["Email:SenderAddress"] = "DoNotReply@example.azurecomm.net"
        }).Build();

        var work = new DeliveryWorkItem(Guid.NewGuid(), Guid.NewGuid(), "test", null, "k1", "to@example.com", "Asunto", "Cuerpo",
            DeliveryStatuses.Requested, null, null, Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        // 202 Accepted
        var handler202 = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new StringContent("{\"id\":\"acs-op-123\",\"status\":\"Running\"}", Encoding.UTF8, "application/json")
        });
        var provider202 = new AcsEmailProvider(new HttpClient(handler202), config);
        var res202 = await provider202.SendAsync(work, default);
        Assert.Equal(DeliveryStatuses.Accepted, res202.Status);
        Assert.Equal("acs-op-123", res202.OperationId);
        Assert.NotNull(handler202.LastRequest?.Headers.Authorization);
        Assert.Equal("HMAC-SHA256", handler202.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Contains("DoNotReply@example.azurecomm.net", handler202.LastBody);

        // 400 BadRequest -> Failed
        var handler400 = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var res400 = await new AcsEmailProvider(new HttpClient(handler400), config).SendAsync(work, default);
        Assert.Equal(DeliveryStatuses.Failed, res400.Status);
        Assert.Equal("provider_400", res400.ErrorCode);

        // 503 ServiceUnavailable -> Unknown
        var handler503 = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var res503 = await new AcsEmailProvider(new HttpClient(handler503), config).SendAsync(work, default);
        Assert.Equal(DeliveryStatuses.Unknown, res503.Status);
    }

    [Fact]
    public async Task FunctionsHttpEndpointsEnforceJwtAudienceAndHousePermissions()
    {
        var (connection, db) = await CreateDbAsync();
        await using var cScope = connection;
        await using var dScope = db;

        var house = Guid.NewGuid();
        var resident = Guid.NewGuid();
        var admin = Guid.NewGuid();
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:PublicKeyPem"] = ecdsa.ExportSubjectPublicKeyInfoPem(),
            ["Jwt:KeyId"] = "identity-v1",
            ["Email:Endpoint"] = "https://acs-test.communication.azure.com/",
            ["Email:SenderAddress"] = "DoNotReply@example.azurecomm.net"
        }).Build();

        using var identityKey = new IdentityKey(config);
        var provider = new FakeEmailProvider();
        var ops = new NotificationOperations(new SqlNotificationsStore(db), provider, TimeProvider.System);
        var functions = new NotificationFunctions(
            db, ops, new FakeAuthorizer(house, resident, admin), provider, identityKey, config, NullLogger<NotificationFunctions>.Instance);

        var ctx = new FakeFunctionContext();

        string Token(Guid sub, string audience = "Notifications.Public")
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var payload = new JwtPayload
            {
                ["iss"] = "Identity",
                ["aud"] = audience,
                ["sub"] = sub.ToString(),
                ["sid"] = Guid.NewGuid().ToString(),
                ["iat"] = now,
                ["exp"] = now + 300
            };
            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                new JwtHeader(new SigningCredentials(new ECDsaSecurityKey(ecdsa) { KeyId = "identity-v1" }, SecurityAlgorithms.EcdsaSha256)),
                payload));
        }

        // Health + Ready
        var ready = (FakeHttpResponseData)await functions.Ready(new FakeHttpRequestData(ctx, "GET", "https://localhost/ready"), default);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);

        // Missing token -> 401
        var unauth = (FakeHttpResponseData)await functions.GetPreferences(
            new FakeHttpRequestData(ctx, "GET", $"https://localhost/api/v1/notifications/houses/{house}/preferences"), house, default);
        Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode);

        // Wrong audience -> 401
        var wrongAudReq = new FakeHttpRequestData(ctx, "GET", $"https://localhost/api/v1/notifications/houses/{house}/preferences");
        wrongAudReq.Headers.Add("Authorization", "Bearer " + Token(admin, "Home.Public"));
        var wrongAud = (FakeHttpResponseData)await functions.GetPreferences(wrongAudReq, house, default);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongAud.StatusCode);

        // Resident can GET preferences, cannot PUT preferences (403)
        var residentGet = new FakeHttpRequestData(ctx, "GET", $"https://localhost/api/v1/notifications/houses/{house}/preferences");
        residentGet.Headers.Add("Authorization", "Bearer " + Token(resident));
        Assert.Equal(HttpStatusCode.OK, ((FakeHttpResponseData)await functions.GetPreferences(residentGet, house, default)).StatusCode);

        var residentPut = new FakeHttpRequestData(ctx, "PUT", $"https://localhost/api/v1/notifications/houses/{house}/preferences", "{\"email\":true}");
        residentPut.Headers.Add("Authorization", "Bearer " + Token(resident));
        Assert.Equal(HttpStatusCode.Forbidden, ((FakeHttpResponseData)await functions.SavePreferences(residentPut, house, default)).StatusCode);

        // Admin cannot enable email before recipient verified (422)
        var adminPutEarly = new FakeHttpRequestData(ctx, "PUT", $"https://localhost/api/v1/notifications/houses/{house}/preferences", "{\"email\":true}");
        adminPutEarly.Headers.Add("Authorization", "Bearer " + Token(admin));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, ((FakeHttpResponseData)await functions.SavePreferences(adminPutEarly, house, default)).StatusCode);

        // Admin verifies recipient in dryRun mode and calls dryRun test-email (0 external emails sent)
        var verifyReq = new FakeHttpRequestData(ctx, "POST", $"https://localhost/api/v1/notifications/houses/{house}/recipient/verify?dryRun=true", "{\"email\":\"owner@example.com\"}");
        verifyReq.Headers.Add("Authorization", "Bearer " + Token(admin));
        Assert.Equal(HttpStatusCode.Accepted, ((FakeHttpResponseData)await functions.VerifyRecipient(verifyReq, house, default)).StatusCode);

        var row = await db.Preferences.SingleAsync(x => x.HouseId == house);
        row.VerificationCodeHash = NotificationOperations.HashSecret("123456");
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var confirmReq = new FakeHttpRequestData(ctx, "POST", $"https://localhost/api/v1/notifications/houses/{house}/recipient/confirm", "{\"code\":\"123456\"}");
        confirmReq.Headers.Add("Authorization", "Bearer " + Token(admin));
        Assert.Equal(HttpStatusCode.OK, ((FakeHttpResponseData)await functions.ConfirmRecipient(confirmReq, house, default)).StatusCode);

        var adminPut = new FakeHttpRequestData(ctx, "PUT", $"https://localhost/api/v1/notifications/houses/{house}/preferences", "{\"email\":true}");
        adminPut.Headers.Add("Authorization", "Bearer " + Token(admin));
        Assert.Equal(HttpStatusCode.OK, ((FakeHttpResponseData)await functions.SavePreferences(adminPut, house, default)).StatusCode);

        var dryTestReq = new FakeHttpRequestData(ctx, "POST", $"https://localhost/api/v1/notifications/houses/{house}/test-email?dryRun=true", "{}");
        dryTestReq.Headers.Add("Authorization", "Bearer " + Token(admin));
        var dryTestRes = (FakeHttpResponseData)await functions.TestEmail(dryTestReq, house, default);
        Assert.Equal(HttpStatusCode.Accepted, dryTestRes.StatusCode);
        Assert.Equal(0, provider.SendCalls);

        // Missing delivery returns 404
        var missingDeliveryReq = new FakeHttpRequestData(ctx, "GET", $"https://localhost/api/v1/notifications/houses/{house}/deliveries/{Guid.NewGuid()}");
        missingDeliveryReq.Headers.Add("Authorization", "Bearer " + Token(admin));
        Assert.Equal(HttpStatusCode.NotFound, ((FakeHttpResponseData)await functions.GetDelivery(missingDeliveryReq, house, Guid.NewGuid(), default)).StatusCode);
    }
}
