using Scada.Security.Audit;

namespace Scada.Security.Tests;

public sealed class AuditSinkTests
{
    [Fact]
    public async Task SinkRecordsOperationalAuditEventsInOrder()
    {
        var sink = new InMemoryAuditSink();
        var first = AuditEvent.Create(
            "operator-1",
            "Operator 1",
            AuditActions.TagWrite,
            AuditOutcome.Succeeded,
            "tag",
            "Plant.P01.Frequency",
            new Dictionary<string, string> { ["value"] = "50" },
            "request-1");
        await Task.Delay(1);
        var second = AuditEvent.Create(
            "supervisor-1",
            "Supervisor 1",
            AuditActions.AlarmAcknowledge,
            AuditOutcome.Succeeded,
            "alarm",
            "HighPressure",
            correlationId: "request-2");

        await sink.WriteAsync(first);
        await sink.WriteAsync(second);

        var events = sink.Snapshot().ToArray();
        Assert.Equal(2, events.Length);
        Assert.Equal(AuditActions.TagWrite, events[0].Action);
        Assert.Equal("operator-1", events[0].SubjectId);
        Assert.Equal("50", events[0].Details!["value"]);
        Assert.Equal(AuditActions.AlarmAcknowledge, events[1].Action);
        Assert.Equal("request-2", events[1].CorrelationId);
    }
}
