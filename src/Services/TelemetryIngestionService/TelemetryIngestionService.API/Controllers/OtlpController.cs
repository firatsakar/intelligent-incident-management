using BuildingBlocks.SharedKernel;
using Google.Protobuf;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TelemetryIngestionService.Application.Abstractions;
using TelemetryIngestionService.Application.Commands.IngestPushedLogs;
using TelemetryIngestionService.Domain.Enums;
using TelemetryIngestionService.Domain.Services;
using TelemetryIngestionService.Infrastructure.Otlp;

namespace TelemetryIngestionService.API.Controllers;

/// <summary>
/// OTLP/HTTP log receiver: where a customer's collector, or an OpenTelemetry SDK, sends logs.
/// </summary>
/// <remarks>
/// <para>
/// The path is the OTLP convention — an exporter configured with this service's base address
/// appends <c>/v1/logs</c> itself — under <c>/otlp</c>, which the gateway routes here.
/// </para>
/// <para>
/// No user signs in to push logs, so the platform's token is not what authenticates this: the
/// source's ingest key is. The key is also the only thing that says whose the logs are, which is
/// why this endpoint, and nothing after it, sets the organisation — the same rule as the
/// middleware, the bus and the poller. A header of its own rather than <c>Authorization</c>, so it
/// can never be mistaken for a platform token by the bearer handler or the gateway.
/// </para>
/// </remarks>
[ApiController]
[AllowAnonymous]
[Route("otlp/v1")]
public sealed class OtlpController : ControllerBase
{
    public const string IngestKeyHeader = "X-IIM-Ingest-Key";

    // Collectors batch; a batch past this is a sender configured to send everything at once.
    private const long MaxBodyBytes = 5 * 1024 * 1024;

    private readonly ITelemetrySourceRepository _sources;
    private readonly IOrganizationContext _organization;
    private readonly ISender _sender;
    private readonly ILogger<OtlpController> _logger;

    public OtlpController(
        ITelemetrySourceRepository sources,
        IOrganizationContext organization,
        ISender sender,
        ILogger<OtlpController> logger
    )
    {
        _sources = sources;
        _organization = organization;
        _sender = sender;
        _logger = logger;
    }

    [HttpPost("logs")]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> Logs(CancellationToken cancellationToken)
    {
        // 401 and 403 are the codes OTLP senders treat as final: a wrong key is not retried into
        // a storm of its own.
        var key = Request.Headers[IngestKeyHeader].ToString();

        if (string.IsNullOrWhiteSpace(key))
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: $"Missing {IngestKeyHeader} header.");

        var source = await _sources.FindByIngestKeyHashForIngestAsync(IngestKey.Hash(key.Trim()), cancellationToken);

        if (source is null || !source.Kind.IsPushed())
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unknown ingest key.");

        if (!source.IsEnabled)
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "This source is paused.");

        // Everything from here on belongs to the key's organisation, and to nobody else's.
        _organization.Set(source.OrganizationId);

        if (OtlpLogsCodec.EncodingOf(Request.ContentType) is not { } encoding)
            return Problem(
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: $"Send {OtlpLogsCodec.ProtobufContentType} or {OtlpLogsCodec.JsonContentType}."
            );

        OtlpDecodedBatch decoded;

        try
        {
            // Decompressed by the middleware when the sender gzips, which a collector does by
            // default.
            using var buffer = new MemoryStream();
            await Request.Body.CopyToAsync(buffer, cancellationToken);

            decoded = OtlpLogsCodec.Map(OtlpLogsCodec.Parse(buffer.ToArray(), encoding), DateTime.UtcNow);
        }
        catch (Exception ex) when (ex is InvalidProtocolBufferException or InvalidJsonException)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Not an OTLP logs export request.", detail: ex.Message);
        }

        try
        {
            var result = await _sender.Send(new IngestPushedLogsCommand(source.Id, decoded.Events), cancellationToken);

            _logger.LogInformation(
                "OTLP push to {SourceName}: {Received} record(s), {Rejected} rejected, {BelowMinimum} below the minimum severity, {Stored} stored, {Folded} folded, {Duplicates} already held.",
                source.Name,
                result.Received + decoded.Rejected,
                decoded.Rejected,
                result.BelowMinimum,
                result.Ingested.Stored,
                result.Ingested.Folded,
                result.Ingested.Duplicates
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Our failure, not the sender's: 503 is one of the codes an OTLP exporter retries, so
            // the batch waits in the collector instead of being dropped.
            _logger.LogError(ex, "OTLP push to {SourceName} could not be stored.", source.Name);

            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Logs could not be stored; retry.");
        }

        var contentType = encoding == OtlpEncoding.Protobuf
            ? OtlpLogsCodec.ProtobufContentType
            : OtlpLogsCodec.JsonContentType;

        return File(OtlpLogsCodec.Response(encoding, decoded.Rejected), contentType);
    }
}
