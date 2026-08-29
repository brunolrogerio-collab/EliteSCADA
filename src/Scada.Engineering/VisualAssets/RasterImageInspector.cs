using System.Buffers.Binary;

namespace Scada.Engineering.VisualAssets;

public sealed record RasterImageInspection(
    string MediaType,
    int PixelWidth,
    int PixelHeight);

public static class RasterImageInspector
{
    private static ReadOnlySpan<byte> PngSignature => [137, 80, 78, 71, 13, 10, 26, 10];

    public static RasterImageInspection Inspect(ReadOnlySpan<byte> content)
    {
        if (content.Length == 0)
            throw new InvalidDataException("Image payload is empty.");
        if (content.Length > VisualAssetEngineeringValidator.MaximumPayloadBytes)
            throw new InvalidDataException($"Image payload exceeds {VisualAssetEngineeringValidator.MaximumPayloadBytes} bytes.");

        RasterImageInspection inspection;
        if (content.StartsWith(PngSignature))
            inspection = InspectPng(content);
        else if (content.Length >= 2 && content[0] == 0xFF && content[1] == 0xD8)
            inspection = InspectJpeg(content);
        else if (content.Length >= 2 && content[0] == (byte)'B' && content[1] == (byte)'M')
            inspection = InspectBmp(content);
        else
            throw new InvalidDataException("Unsupported raster image signature. Expected PNG, JPEG or BMP.");

        if (inspection.PixelWidth is <= 0 or > VisualAssetEngineeringValidator.MaximumPixelDimension ||
            inspection.PixelHeight is <= 0 or > VisualAssetEngineeringValidator.MaximumPixelDimension)
            throw new InvalidDataException(
                $"Image dimensions must be between 1 and {VisualAssetEngineeringValidator.MaximumPixelDimension} pixels.");

        return inspection;
    }

    private static RasterImageInspection InspectPng(ReadOnlySpan<byte> content)
    {
        if (content.Length < 45)
            throw new InvalidDataException("PNG payload is truncated.");

        var offset = PngSignature.Length;
        var chunkIndex = 0;
        var sawIdat = false;
        var sawIend = false;
        uint width = 0;
        uint height = 0;

        while (offset < content.Length)
        {
            if (content.Length - offset < 12)
                throw new InvalidDataException("PNG chunk header is truncated.");

            var chunkLength = BinaryPrimitives.ReadUInt32BigEndian(content.Slice(offset, 4));
            if (chunkLength > int.MaxValue)
                throw new InvalidDataException("PNG chunk exceeds the supported size range.");

            var dataLength = (int)chunkLength;
            var chunkTotalLength = checked(12 + dataLength);
            if (chunkTotalLength > content.Length - offset)
                throw new InvalidDataException("PNG chunk payload is truncated.");

            var type = content.Slice(offset + 4, 4);
            var data = content.Slice(offset + 8, dataLength);

            if (chunkIndex == 0)
            {
                if (!type.SequenceEqual("IHDR"u8) || dataLength != 13)
                    throw new InvalidDataException("PNG first chunk must be a canonical IHDR chunk.");

                width = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(0, 4));
                height = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4, 4));
                if (width > int.MaxValue || height > int.MaxValue)
                    throw new InvalidDataException("PNG dimensions exceed supported integer range.");
            }
            else if (type.SequenceEqual("IHDR"u8))
            {
                throw new InvalidDataException("PNG contains more than one IHDR chunk.");
            }

            if (type.SequenceEqual("IDAT"u8))
                sawIdat = true;

            if (type.SequenceEqual("IEND"u8))
            {
                if (dataLength != 0)
                    throw new InvalidDataException("PNG IEND chunk must be empty.");
                sawIend = true;
                offset += chunkTotalLength;
                if (offset != content.Length)
                    throw new InvalidDataException("PNG contains trailing data after IEND.");
                break;
            }

            offset += chunkTotalLength;
            chunkIndex++;
        }

        if (!sawIdat)
            throw new InvalidDataException("PNG payload does not contain image data.");
        if (!sawIend)
            throw new InvalidDataException("PNG payload does not contain a complete IEND chunk.");

        return new RasterImageInspection("image/png", (int)width, (int)height);
    }

    private static RasterImageInspection InspectJpeg(ReadOnlySpan<byte> content)
    {
        if (content.Length < 6 || content[^2] != 0xFF || content[^1] != 0xD9)
            throw new InvalidDataException("JPEG payload is truncated or missing its End Of Image marker.");

        var offset = 2;
        int? width = null;
        int? height = null;
        var sawScan = false;

        while (offset < content.Length - 2)
        {
            if (content[offset] != 0xFF)
                throw new InvalidDataException("JPEG marker stream is malformed before image data.");

            while (offset < content.Length && content[offset] == 0xFF)
                offset++;
            if (offset >= content.Length)
                break;

            var marker = content[offset++];
            if (marker == 0xD9)
                break;
            if (marker == 0xD8 || marker is >= 0xD0 and <= 0xD7 || marker == 0x01)
                continue;

            if (offset + 2 > content.Length)
                throw new InvalidDataException("JPEG segment length is truncated.");
            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(content.Slice(offset, 2));
            if (segmentLength < 2 || offset + segmentLength > content.Length)
                throw new InvalidDataException("JPEG segment length is invalid.");

            if (IsStartOfFrame(marker))
            {
                if (segmentLength < 7)
                    throw new InvalidDataException("JPEG Start Of Frame segment is truncated.");
                height = BinaryPrimitives.ReadUInt16BigEndian(content.Slice(offset + 3, 2));
                width = BinaryPrimitives.ReadUInt16BigEndian(content.Slice(offset + 5, 2));
            }

            if (marker == 0xDA)
            {
                sawScan = true;
                break;
            }

            offset += segmentLength;
        }

        if (!width.HasValue || !height.HasValue)
            throw new InvalidDataException("JPEG payload does not contain a supported Start Of Frame segment.");
        if (!sawScan)
            throw new InvalidDataException("JPEG payload does not contain a Start Of Scan segment.");

        return new RasterImageInspection("image/jpeg", width.Value, height.Value);
    }

    private static bool IsStartOfFrame(byte marker) => marker is
        0xC0 or 0xC1 or 0xC2 or 0xC3 or
        0xC5 or 0xC6 or 0xC7 or
        0xC9 or 0xCA or 0xCB or
        0xCD or 0xCE or 0xCF;

    private static RasterImageInspection InspectBmp(ReadOnlySpan<byte> content)
    {
        if (content.Length < 26)
            throw new InvalidDataException("BMP payload is truncated.");

        var declaredFileSize = BinaryPrimitives.ReadUInt32LittleEndian(content.Slice(2, 4));
        if (declaredFileSize != content.Length)
            throw new InvalidDataException("BMP declared file size does not match the payload length.");

        var pixelOffset = BinaryPrimitives.ReadUInt32LittleEndian(content.Slice(10, 4));
        var dibHeaderSize = BinaryPrimitives.ReadUInt32LittleEndian(content.Slice(14, 4));
        if (dibHeaderSize > int.MaxValue)
            throw new InvalidDataException("BMP DIB header exceeds the supported range.");

        int width;
        int height;
        ushort planes;

        if (dibHeaderSize == 12)
        {
            if (pixelOffset < 26 || pixelOffset >= content.Length)
                throw new InvalidDataException("BMP pixel-data offset is invalid.");
            width = BinaryPrimitives.ReadUInt16LittleEndian(content.Slice(18, 2));
            height = BinaryPrimitives.ReadUInt16LittleEndian(content.Slice(20, 2));
            planes = BinaryPrimitives.ReadUInt16LittleEndian(content.Slice(22, 2));
        }
        else if (dibHeaderSize >= 40)
        {
            var minimumPixelOffset = checked(14 + (int)dibHeaderSize);
            if (content.Length < minimumPixelOffset)
                throw new InvalidDataException("BMP DIB header is truncated.");
            if (pixelOffset < minimumPixelOffset || pixelOffset >= content.Length)
                throw new InvalidDataException("BMP pixel-data offset is invalid.");

            width = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(18, 4));
            var signedHeight = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(22, 4));
            if (signedHeight == int.MinValue || signedHeight == 0)
                throw new InvalidDataException("BMP height is invalid.");
            height = Math.Abs(signedHeight);
            planes = BinaryPrimitives.ReadUInt16LittleEndian(content.Slice(26, 2));
        }
        else
        {
            throw new InvalidDataException($"Unsupported BMP DIB header size {dibHeaderSize}.");
        }

        if (planes != 1)
            throw new InvalidDataException("BMP must declare exactly one color plane.");

        return new RasterImageInspection("image/bmp", width, height);
    }
}
