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
        if (content.Length < 33)
            throw new InvalidDataException("PNG payload is truncated.");

        var ihdrLength = BinaryPrimitives.ReadUInt32BigEndian(content.Slice(8, 4));
        if (ihdrLength != 13 || !content.Slice(12, 4).SequenceEqual("IHDR"u8))
            throw new InvalidDataException("PNG first chunk must be a canonical IHDR chunk.");

        var width = BinaryPrimitives.ReadUInt32BigEndian(content.Slice(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(content.Slice(20, 4));
        if (width > int.MaxValue || height > int.MaxValue)
            throw new InvalidDataException("PNG dimensions exceed supported integer range.");

        return new RasterImageInspection("image/png", (int)width, (int)height);
    }

    private static RasterImageInspection InspectJpeg(ReadOnlySpan<byte> content)
    {
        var offset = 2;
        while (offset < content.Length)
        {
            while (offset < content.Length && content[offset] == 0xFF)
                offset++;
            if (offset >= content.Length)
                break;

            var marker = content[offset++];
            if (marker is 0xD8 or 0xD9)
                continue;
            if (marker == 0xDA)
                break;
            if (marker is >= 0xD0 and <= 0xD7 || marker == 0x01)
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
                var height = BinaryPrimitives.ReadUInt16BigEndian(content.Slice(offset + 3, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(content.Slice(offset + 5, 2));
                return new RasterImageInspection("image/jpeg", width, height);
            }

            offset += segmentLength;
        }

        throw new InvalidDataException("JPEG payload does not contain a supported Start Of Frame segment.");
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

        var dibHeaderSize = BinaryPrimitives.ReadUInt32LittleEndian(content.Slice(14, 4));
        int width;
        int height;

        if (dibHeaderSize == 12)
        {
            width = BinaryPrimitives.ReadUInt16LittleEndian(content.Slice(18, 2));
            height = BinaryPrimitives.ReadUInt16LittleEndian(content.Slice(20, 2));
        }
        else if (dibHeaderSize >= 40)
        {
            if (content.Length < 14 + dibHeaderSize)
                throw new InvalidDataException("BMP DIB header is truncated.");
            width = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(18, 4));
            var signedHeight = BinaryPrimitives.ReadInt32LittleEndian(content.Slice(22, 4));
            if (signedHeight == int.MinValue)
                throw new InvalidDataException("BMP height is invalid.");
            height = Math.Abs(signedHeight);
        }
        else
        {
            throw new InvalidDataException($"Unsupported BMP DIB header size {dibHeaderSize}.");
        }

        return new RasterImageInspection("image/bmp", width, height);
    }
}
