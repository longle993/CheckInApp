using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using CheckInApp.Application;

namespace CheckInApp.Infrastructure;

public static class PngValidator
{
    public static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static void Validate(byte[] data)
    {
        try
        {
            using var compressed = new MemoryStream();
            var offset = 8;
            var width = 0;
            var height = 0;
            var channels = 0;
            var ended = false;
            while (offset + 12 <= data.Length)
            {
                var length = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset, 4));
                if (length < 0 || length > data.Length - offset - 12)
                    throw new InvalidDataException();
                var type = Encoding.ASCII.GetString(data, offset + 4, 4);
                uint crc = 0xffffffff;
                for (var i = offset + 4; i < offset + 8 + length; i++)
                {
                    crc ^= data[i];
                    for (var bit = 0; bit < 8; bit++)
                        crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1;
                }
                if ((crc ^ 0xffffffff) != BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 8 + length, 4)))
                    throw new InvalidDataException();
                if (offset == 8 && type != "IHDR")
                    throw new InvalidDataException();
                if (type == "IHDR")
                {
                    if (offset != 8 || length != 13)
                        throw new InvalidDataException();
                    width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset + 8, 4));
                    height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(offset + 12, 4));
                    if (data[offset + 16] != 8 || data[offset + 18] != 0 || data[offset + 19] != 0 || data[offset + 20] != 0)
                        throw new InvalidDataException();
                    channels = data[offset + 17] switch
                    {
                        2 => 3,
                        6 => 4,
                        _ => throw new InvalidDataException()
                    };
                }
                if (type == "IDAT")
                    compressed.Write(data, offset + 8, length);
                offset += 12 + length;
                if (type == "IEND")
                {
                    if (length != 0 || offset != data.Length)
                        throw new InvalidDataException();
                    ended = true;
                    break;
                }
            }
            if (!ended || width < 1 || height < 1 || width > 2048 || height > 2048 || channels == 0)
                throw new InvalidDataException();
            compressed.Position = 0;
            using var inflated = new ZLibStream(compressed, CompressionMode.Decompress);
            var row = new byte[checked(width * channels + 1)];
            for (var i = 0; i < height; i++)
            {
                inflated.ReadExactly(row);
                if (row[0] > 4)
                    throw new InvalidDataException();
            }
            if (inflated.ReadByte() != -1)
                throw new InvalidDataException();
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException or OverflowException)
        {
            throw new AppException(400, "LOGO_INVALID", "Ảnh PNG bị lỗi. Hãy chọn lại logo trong cửa sổ cấu hình.");
        }
    }
}
