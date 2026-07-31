using System.Net;
using System.Text;
using System.Text.Json;
using Esotera.Application.Exceptions;
using Esotera.Application.Interfaces;
using Esotera.Application.Options;
using Esotera.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Esotera.Tests;

public class MercadoPagoHttpClientErrorTests
{
    [Fact]
    public async Task CreateOrder_Http400_ReadsCodeMessage_AndDoesNotLogQrSecrets()
    {
        var handler = new StubHandler(_ =>
        {
            var payload = JsonSerializer.Serialize(new
            {
                code = "invalid_email_for_sandbox",
                message = "Invalid email for sandbox. Use @testuser.com",
                status = 400,
                errors = new[]
                {
                    new { code = "invalid_email_for_sandbox", message = "payer.email invalid" }
                },
                transactions = new
                {
                    payments = new[]
                    {
                        new
                        {
                            payment_method = new
                            {
                                qr_code = "SECRET_QR_CODE_SHOULD_NOT_APPEAR",
                                qr_code_base64 = "SECRET_BASE64_SHOULD_NOT_APPEAR"
                            }
                        }
                    }
                }
            });
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
                Headers = { { "x-request-id", "req-test-400" } }
            };
        });

        var logger = new CapturingLogger<MercadoPagoHttpClient>();
        var client = CreateClient(handler, logger, MercadoPagoEnvironmentKind.Test);

        var act = () => client.CreatePaymentAsync(
            new MercadoPagoCreatePaymentCommand(
                89.90m,
                "Pedido",
                Guid.NewGuid().ToString("D"),
                "cliente@gmail.com",
                null,
                null,
                "pix",
                null,
                1,
                null,
                null,
                false),
            Guid.NewGuid().ToString("N")[..32]);

        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Message.Should().Contain("ambiente de teste");

        logger.Messages.Should().NotBeEmpty();
        var joined = string.Join('\n', logger.Messages);
        joined.Should().Contain("invalid_email_for_sandbox");
        joined.Should().Contain("HttpStatus=400");
        joined.Should().Contain("[REDACTED]");
        joined.Should().NotContain("SECRET_QR_CODE_SHOULD_NOT_APPEAR");
        joined.Should().NotContain("SECRET_BASE64_SHOULD_NOT_APPEAR");
        joined.Should().NotContain("TEST_ACCESS_TOKEN_VALUE");
    }

    [Fact]
    public async Task CreateOrder_SandboxOfficial_SendsFiftyAndAproPayer()
    {
        string? capturedBody = null;
        string? capturedIdempotency = null;

        var handler = new StubHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            if (req.Headers.TryGetValues("X-Idempotency-Key", out var keys))
                capturedIdempotency = keys.FirstOrDefault();

            var ok = """
                     {
                       "id": "ORDTESTOK000000000001",
                       "status": "action_required",
                       "status_detail": "waiting_transfer",
                       "external_reference": "teste_esotera_pix_50_abc",
                       "total_amount": "50.00",
                       "currency_id": "BRL",
                       "transactions": {
                         "payments": [
                           {
                             "id": "PAYTESTOK000000000001",
                             "amount": "50.00",
                             "status": "action_required",
                             "status_detail": "waiting_transfer",
                             "payment_method": {
                               "id": "pix",
                               "type": "bank_transfer",
                               "qr_code": "00020126pix",
                               "qr_code_base64": "abc123"
                             }
                           }
                         ]
                       }
                     }
                     """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ok, Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler, new CapturingLogger<MercadoPagoHttpClient>(), MercadoPagoEnvironmentKind.Test);
        var key = Guid.NewGuid().ToString("N")[..32];
        var snap = await client.CreatePaymentAsync(
            new MercadoPagoCreatePaymentCommand(
                50.00m,
                null,
                "teste_esotera_pix_50_abc",
                MercadoPagoOptions.SandboxPayerEmail,
                MercadoPagoOptions.SandboxPayerFirstName,
                null,
                "pix",
                null,
                1,
                null,
                null,
                true),
            key);

        snap.TransactionAmount.Should().Be(50.00m);
        capturedIdempotency.Should().Be(key);

        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;
        root.GetProperty("total_amount").GetString().Should().Be("50.00");
        root.GetProperty("payer").GetProperty("email").GetString()
            .Should().Be(MercadoPagoOptions.SandboxPayerEmail);
        root.GetProperty("payer").GetProperty("first_name").GetString()
            .Should().Be(MercadoPagoOptions.SandboxPayerFirstName);
        root.GetProperty("transactions").GetProperty("payments")[0]
            .GetProperty("payment_method").GetProperty("id").GetString().Should().Be("pix");
        root.GetProperty("transactions").GetProperty("payments")[0]
            .GetProperty("payment_method").GetProperty("type").GetString().Should().Be("bank_transfer");
        root.TryGetProperty("description", out _).Should().BeFalse();
    }

    private static MercadoPagoHttpClient CreateClient(
        HttpMessageHandler handler,
        CapturingLogger<MercadoPagoHttpClient> logger,
        MercadoPagoEnvironmentKind kind)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.mercadopago.com/")
        };
        var options = Options.Create(new MercadoPagoOptions
        {
            AccessToken = "TEST_ACCESS_TOKEN_VALUE",
            Environment = kind.ToString(),
            EnvironmentKind = kind,
            SandboxPixEnabled = kind == MercadoPagoEnvironmentKind.Test,
            SandboxPixAmount = 50.00m
        });
        return new MercadoPagoHttpClient(http, options, logger);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
            _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }

    private sealed class CapturingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public List<string> Messages { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull =>
            NullScope.Instance;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
