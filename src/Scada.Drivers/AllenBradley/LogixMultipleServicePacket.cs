using System.Buffers.Binary;

namespace Scada.Drivers.AllenBradley;

public static class LogixMultipleServicePacket
{
    private const int MessageRouterPathLength = 4;
    private const int RequestHeaderLength = 2 + MessageRouterPathLength;

    public static byte[] BuildRequest(IReadOnlyList<byte[]> serviceRequests)
    {
        ArgumentNullException.ThrowIfNull(serviceRequests);
        if (serviceRequests.Count is < 1 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(serviceRequests), "A Multiple Service Packet requires between 1 and 65535 embedded service requests.");
        if (serviceRequests.Any(static request => request is null || request.Length < 2))
            throw new ArgumentException("Each embedded CIP service request must contain at least service and path-size bytes.", nameof(serviceRequests));

        var offsetTableLength = checked(2 + (serviceRequests.Count * 2));
        var requestDataLength = offsetTableLength;
        foreach (var serviceRequest in serviceRequests)
            requestDataLength = checked(requestDataLength + serviceRequest.Length);
        if (requestDataLength > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(serviceRequests), "Multiple Service Packet request data exceeds the 16-bit offset range.");

        var request = new byte[checked(RequestHeaderLength + requestDataLength)];
        request[0] = LogixCipCodec.MultipleServicePacketService;
        request[1] = 0x02;
        request[2] = 0x20;
        request[3] = 0x02;
        request[4] = 0x24;
        request[5] = 0x01;

        var data = request.AsSpan(RequestHeaderLength);
        BinaryPrimitives.WriteUInt16LittleEndian(data, checked((ushort)serviceRequests.Count));
        var serviceOffset = offsetTableLength;
        for (var index = 0; index < serviceRequests.Count; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.Slice(2 + (index * 2), 2), checked((ushort)serviceOffset));
            var serviceRequest = serviceRequests[index];
            serviceRequest.CopyTo(data.Slice(serviceOffset, serviceRequest.Length));
            serviceOffset += serviceRequest.Length;
        }
        return request;
    }

    public static IReadOnlyList<LogixCipResponse> ParseResponse(LogixCipResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.Service != (byte)(LogixCipCodec.MultipleServicePacketService | 0x80))
            throw new InvalidDataException($"CIP response service 0x{response.Service:X2} is not a Multiple Service Packet reply.");
        LogixCipCodec.ThrowIfFailed(response, "Multiple Service Packet");

        var data = response.Data.AsSpan();
        if (data.Length < 2) throw new InvalidDataException("Multiple Service Packet reply is missing the service-reply count.");
        var count = BinaryPrimitives.ReadUInt16LittleEndian(data);
        if (count == 0) return Array.Empty<LogixCipResponse>();

        var tableLength = checked(2 + (count * 2));
        if (data.Length < tableLength)
            throw new InvalidDataException("Multiple Service Packet reply offset table is truncated.");

        var offsets = new int[count];
        for (var index = 0; index < count; index++)
        {
            var offset = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(2 + (index * 2), 2));
            if (offset < tableLength || offset >= data.Length)
                throw new InvalidDataException($"Multiple Service Packet reply offset {offset} is outside the service payload region.");
            if (index > 0 && offset <= offsets[index - 1])
                throw new InvalidDataException("Multiple Service Packet reply offsets must be strictly increasing.");
            offsets[index] = offset;
        }

        var replies = new LogixCipResponse[count];
        for (var index = 0; index < count; index++)
        {
            var start = offsets[index];
            var end = index + 1 < count ? offsets[index + 1] : data.Length;
            if (end - start < 4)
                throw new InvalidDataException("Multiple Service Packet contains a truncated embedded CIP response.");
            replies[index] = LogixCipCodec.ParseResponse(data.Slice(start, end - start));
        }
        return replies;
    }

    public static byte[] BuildReadRequest(IReadOnlyList<LogixSymbolReference> references)
    {
        ArgumentNullException.ThrowIfNull(references);
        if (references.Count == 0) throw new ArgumentException("At least one Logix reference is required.", nameof(references));
        return BuildRequest(references.Select(static reference => LogixCipCodec.BuildReadTagRequest(reference)).ToArray());
    }
}
