using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
namespace StudioCRM.Infrastructure.Services.Branding;

// Narrow raster-only upload: non-interlaced PNG. No SVG, URLs or executable content.
public static class BrandingPng
{
    public const int MaxBytes = 5 * 1024 * 1024;
    private static readonly byte[] Signature = {137,80,78,71,13,10,26,10};
    public static async Task<byte[]> ReadAndValidateAsync(Stream input, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var block = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(block, ct)) > 0)
        {
            if (buffer.Length + count > MaxBytes) throw Invalid();
            await buffer.WriteAsync(block.AsMemory(0, count), ct);
        }
        return Validate(buffer.ToArray());
    }
    public static byte[] Validate(byte[] data)
    {
        if (data.Length < 57 || data.Length > MaxBytes || !data.AsSpan(0,8).SequenceEqual(Signature)) throw Invalid();
        using var clean = new MemoryStream();
        clean.Write(Signature);
        using var compressed = new MemoryStream();
        int offset = 8, rowBytes = 0, height = 0, color = 0;
        bool header = false, palette = false, idat = false, ended = false, idatEnded = false;
        while (offset + 12 <= data.Length)
        {
            uint rawLength = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset,4));
            if (rawLength > data.Length - offset - 12) throw Invalid();
            int length = (int)rawLength;
            var type = Encoding.ASCII.GetString(data, offset + 4, 4);
            var body = data.AsSpan(offset + 8, length);
            uint crc = 0xffffffff;
            foreach (var b in data.AsSpan(offset + 4, length + 4))
            {
                crc ^= b;
                for (int k=0;k<8;k++) crc = (crc & 1) != 0 ? 0xedb88320 ^ (crc >> 1) : crc >> 1;
            }
            if ((crc ^ 0xffffffff) != BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset + 8 + length,4))) throw Invalid();
            if (!header && type != "IHDR") throw Invalid();
            if (idat && type != "IDAT") idatEnded = true;
            bool keep = false;
            switch (type)
            {
                case "IHDR":
                    if (header || length != 13) throw Invalid();
                    int width = BinaryPrimitives.ReadInt32BigEndian(body[..4]);
                    height = BinaryPrimitives.ReadInt32BigEndian(body.Slice(4,4));
                    if (width < 1 || height < 1 || width > 4096 || height > 4096 || (long)width * height > 8000000) throw Invalid();
                    int depth = body[8]; color = body[9];
                    int channels = color switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => 0 };
                    bool validDepth = color switch
                    {
                        0 => depth is 1 or 2 or 4 or 8 or 16,
                        3 => depth is 1 or 2 or 4 or 8,
                        _ => depth is 8 or 16
                    };
                    if (channels == 0 || !validDepth || body[10] != 0 || body[11] != 0 || body[12] != 0) throw Invalid();
                    rowBytes = (width * channels * depth + 7) / 8 + 1;
                    header = keep = true;
                    break;
                case "PLTE":
                    if (palette || idat || length < 3 || length > 768 || length % 3 != 0 || color is 0 or 4) throw Invalid();
                    palette = keep = true;
                    break;
                case "tRNS":
                    if (idat || color is 4 or 6 || (color == 3 && !palette) ||
                        (color == 0 && length != 2) || (color == 2 && length != 6) ||
                        (color == 3 && (length < 1 || length > 256))) throw Invalid();
                    keep = true;
                    break;
                case "IDAT":
                    if (idatEnded || (color == 3 && !palette)) throw Invalid();
                    idat = keep = true;
                    compressed.Write(body);
                    break;
                case "IEND":
                    if (!idat || length != 0 || offset + 12 != data.Length) throw Invalid();
                    ended = keep = true;
                    break;
                default:
                    // Unknown critical chunks and animations are unsupported; strip metadata.
                    if ((data[offset + 4] & 32) == 0 || type is "acTL" or "fcTL" or "fdAT") throw Invalid();
                    break;
            }
            if (keep) clean.Write(data, offset, length + 12);
            offset += length + 12;
            if (ended) break;
        }
        if (!ended) throw Invalid();
        compressed.Position = 0;
        try
        {
            using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
            var row = new byte[rowBytes];
            for (int y = 0; y < height; y++)
            {
                zlib.ReadExactly(row);
                if (row[0] > 4) throw Invalid();
            }
            if (zlib.ReadByte() != -1) throw Invalid();
        }
        catch (IOException) { throw Invalid(); }
        return clean.ToArray();
    }
    private static InvalidOperationException Invalid() =>
        new("Upload a valid non-interlaced PNG, at most 5 MiB, 4096 px per side and 8 million pixels.");
}
