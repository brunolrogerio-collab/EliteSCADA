using Step = dnp3;

namespace Scada.Drivers.Dnp3.StepFunction;

internal sealed class StepFunctionDnp3ReadHandler(
    Func<Dnp3Measurement, ValueTask> publish) : Step.IReadHandler
{
    public void BeginFragment(Step.ReadType readType, Step.ResponseHeader header) { }
    public void EndFragment(Step.ReadType readType, Step.ResponseHeader header) { }

    public void HandleBinaryInput(Step.HeaderInfo info, ICollection<Step.BinaryInput> values)
    {
        if (!StepFunctionDnp3Mapping.TryMapVariation(info.Variation, out var variation)) return;
        foreach (var value in values)
            Publish(Create(Dnp3PointKind.BinaryInput, value.Index, value.Value, variation, info, value.Flags.Value, value.Time));
    }

    public void HandleDoubleBitBinaryInput(Step.HeaderInfo info, ICollection<Step.DoubleBitBinaryInput> values)
    {
        if (!StepFunctionDnp3Mapping.TryMapVariation(info.Variation, out var variation)) return;
        foreach (var value in values)
            Publish(Create(Dnp3PointKind.DoubleBitBinaryInput, value.Index, StepFunctionDnp3Mapping.MapDoubleBit(value.Value), variation, info, value.Flags.Value, value.Time));
    }

    public void HandleBinaryOutputStatus(Step.HeaderInfo info, ICollection<Step.BinaryOutputStatus> values)
    {
        if (!StepFunctionDnp3Mapping.TryMapVariation(info.Variation, out var variation)) return;
        foreach (var value in values)
            Publish(Create(Dnp3PointKind.BinaryOutputStatus, value.Index, value.Value, variation, info, value.Flags.Value, value.Time));
    }

    public void HandleCounter(Step.HeaderInfo info, ICollection<Step.Counter> values)
    {
        if (!StepFunctionDnp3Mapping.TryMapVariation(info.Variation, out var variation)) return;
        foreach (var value in values)
            Publish(Create(Dnp3PointKind.Counter, value.Index, StepFunctionDnp3Mapping.MapCounter(value.Value, variation), variation, info, value.Flags.Value, value.Time));
    }

    public void HandleFrozenCounter(Step.HeaderInfo info, ICollection<Step.FrozenCounter> values)
    {
        if (!StepFunctionDnp3Mapping.TryMapVariation(info.Variation, out var variation)) return;
        foreach (var value in values)
            Publish(Create(Dnp3PointKind.FrozenCounter, value.Index, StepFunctionDnp3Mapping.MapCounter(value.Value, variation), variation, info, value.Flags.Value, value.Time));
    }

    public void HandleAnalogInput(Step.HeaderInfo info, ICollection<Step.AnalogInput> values)
    {
        if (!StepFunctionDnp3Mapping.TryMapVariation(info.Variation, out var variation)) return;
        foreach (var value in values)
            Publish(Create(Dnp3PointKind.AnalogInput, value.Index, StepFunctionDnp3Mapping.MapAnalog(value.Value, variation), variation, info, value.Flags.Value, value.Time));
    }

    public void HandleAnalogOutputStatus(Step.HeaderInfo info, ICollection<Step.AnalogOutputStatus> values)
    {
        if (!StepFunctionDnp3Mapping.TryMapVariation(info.Variation, out var variation)) return;
        foreach (var value in values)
            Publish(Create(Dnp3PointKind.AnalogOutputStatus, value.Index, StepFunctionDnp3Mapping.MapAnalog(value.Value, variation), variation, info, value.Flags.Value, value.Time));
    }

    public void HandleFrozenAnalogInput(Step.HeaderInfo info, ICollection<Step.FrozenAnalogInput> values) { }
    void Step.IReadHandler.HandleBinaryOutputCommandEvent(Step.HeaderInfo info, ICollection<Step.BinaryOutputCommandEvent> values) { }
    void Step.IReadHandler.HandleAnalogOutputCommandEvent(Step.HeaderInfo info, ICollection<Step.AnalogOutputCommandEvent> values) { }
    void Step.IReadHandler.HandleUnsignedInteger(Step.HeaderInfo info, ICollection<Step.UnsignedInteger> values) { }
    public void HandleOctetString(Step.HeaderInfo info, ICollection<Step.OctetString> values) { }
    void Step.IReadHandler.HandleStringAttr(Step.HeaderInfo info, Step.StringAttr attr, byte set, byte var, string value) { }
    void Step.IReadHandler.HandleUintAttr(Step.HeaderInfo info, Step.UintAttr attr, byte set, byte var, uint value) { }
    void Step.IReadHandler.HandleBoolAttr(Step.HeaderInfo info, Step.BoolAttr attr, byte set, byte var, bool value) { }
    void Step.IReadHandler.HandleIntAttr(Step.HeaderInfo info, Step.IntAttr attr, byte set, byte var, int value) { }
    void Step.IReadHandler.HandleTimeAttr(Step.HeaderInfo info, Step.TimeAttr attr, byte set, byte var, ulong value) { }
    void Step.IReadHandler.HandleFloatAttr(Step.HeaderInfo info, Step.FloatAttr attr, byte set, byte var, double value) { }
    void Step.IReadHandler.HandleVariationListAttr(Step.HeaderInfo info, Step.VariationListAttr attr, byte set, byte var, ICollection<Step.AttrItem> value) { }
    void Step.IReadHandler.HandleOctetStringAttr(Step.HeaderInfo info, Step.OctetStringAttr attr, byte set, byte var, ICollection<byte> value) { }
    void Step.IReadHandler.HandleBitStringAttr(Step.HeaderInfo info, Step.BitStringAttr attr, byte set, byte var, ICollection<byte> value) { }

    private static Dnp3Measurement Create(
        Dnp3PointKind pointKind,
        ushort index,
        object value,
        Dnp3ObjectVariation variation,
        Step.HeaderInfo info,
        byte rawFlags,
        Step.Timestamp timestamp)
    {
        var sourceTime = StepFunctionDnp3Mapping.MapTimestamp(timestamp);
        return new Dnp3Measurement(
            pointKind,
            index,
            value,
            variation,
            info.IsEvent,
            StepFunctionDnp3Mapping.MapFlags(pointKind, info.HasFlags, rawFlags),
            sourceTime.Timestamp,
            sourceTime.Synchronized);
    }

    private void Publish(Dnp3Measurement measurement)
    {
        try
        {
            publish(measurement).AsTask().GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Session shutdown may race a final stack callback. No replay is attempted.
        }
    }
}

internal sealed class StepFunctionDnp3ClientStateListener(Action<Step.ClientState> onChange)
    : Step.IClientStateListener
{
    public void OnChange(Step.ClientState state) => onChange(state);
}

internal sealed class StepFunctionDnp3AssociationHandler : Step.IAssociationHandler
{
    public Step.UtcTimestamp GetCurrentTime()
    {
        var milliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return milliseconds < 0
            ? Step.UtcTimestamp.Invalid()
            : Step.UtcTimestamp.Valid((ulong)milliseconds);
    }
}

internal sealed class StepFunctionDnp3AssociationInformation(
    Action<Step.TaskType, Step.FunctionCode, byte> onTaskStart,
    Action<Step.TaskType, Step.FunctionCode, byte> onTaskSuccess,
    Action<Step.TaskType, Step.TaskError> onTaskFail,
    Action<bool, byte> onUnsolicitedResponse)
    : Step.IAssociationInformation
{
    public void TaskStart(Step.TaskType taskType, Step.FunctionCode functionCode, byte seq) =>
        onTaskStart(taskType, functionCode, seq);

    public void TaskSuccess(Step.TaskType taskType, Step.FunctionCode functionCode, byte seq) =>
        onTaskSuccess(taskType, functionCode, seq);

    public void TaskFail(Step.TaskType taskType, Step.TaskError error) => onTaskFail(taskType, error);

    public void UnsolicitedResponse(bool isDuplicate, byte seq) => onUnsolicitedResponse(isDuplicate, seq);
}
