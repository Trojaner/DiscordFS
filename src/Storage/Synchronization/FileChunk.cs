﻿using System.Security.Cryptography;
using K4os.Compression.LZ4;
using HashAlgorithm = DiscordFS.Helpers.HashAlgorithm;

namespace DiscordFS.Storage.Synchronization;

public class FileChunk
{
    public const byte Version = 0x01;

    /*
     * Layout:
     *
     * HEADER
     * ------
     * [Version]          - 1 byte
     * [Index]            - 4 bytes
     * [IsCompressed]     - 1 byte
     * [IsEncrypted]      - 1 byte
     *                    
     * BODY               
     * ----               
     * [Body Full Size]   - 4 bytes
     * [Body Comp. Size]  - 4 bytes
     * [Body]             - [Body Size] bytes
     * [Hash Algorithm]   - 1 byte
     * [Hash]             - n bytes (md5: 16 bytes)
     */

    public byte[] Data { get; set; }

    public byte[] Hash { get; set; }

    public HashAlgorithm HashAlgorithm { get; set; }

    public bool IsCompressed { get; set; }

    public bool IsEncrypted { get; set; }

    public int Index { get; set; }

    private static readonly MD5 Md5 = MD5.Create();

    public FileChunk(bool useCompression, bool isEncrypted)
    {
        IsCompressed = useCompression;
        IsEncrypted = isEncrypted;
    }

    public FileChunk() { }

    public static FileChunk Deserialize(byte[] data, byte[] encryptionKey = null)
    {
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);

        ms.Seek(offset: 0, SeekOrigin.Begin);

        var chunk = new FileChunk();

        var version = br.ReadByte();
        if (version != Version)
        {
            throw new Exception(message: "File chunk version is not supported");
        }

        chunk.Index = br.ReadInt32();
        chunk.IsCompressed = br.ReadBoolean();
        chunk.IsEncrypted = br.ReadBoolean();

        var fullSize = br.ReadInt32();
        var bodySize = br.ReadInt32();
        var body = br.ReadBytes(bodySize);

        if (chunk.IsEncrypted)
        {
            body = Decrypt(body, encryptionKey);
        }

        if (chunk.IsCompressed)
        {
            body = Decompress(body, fullSize);
        }

        chunk.Data = body;
        chunk.HashAlgorithm = (HashAlgorithm)br.ReadByte();

        byte[] expectedHash;
        switch (chunk.HashAlgorithm)
        {
            case HashAlgorithm.Md5:
                chunk.Hash = Md5.ComputeHash(body);
                expectedHash = br.ReadBytes(count: 16);
                break;
            default:
                throw new Exception($"Unknown hash algorithm ID: {(int)chunk.HashAlgorithm}");
        }

        if (!expectedHash.SequenceEqual(chunk.Hash))
        {
            throw new Exception(message: "Chunk corrupted: invalid hash");
        }

        return chunk;
    }

    public byte[] Serialize(byte[] encryptionKey = null)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        IsEncrypted = encryptionKey != null;

        bw.Write(Version);
        bw.Write(Index);
        bw.Write(IsCompressed);
        bw.Write(IsEncrypted);

        var hash = Md5.ComputeHash(Data);
        var originalSize = Data.Length;

        var body = IsCompressed
            ? Compress(Data)
            : Data;

        if (IsEncrypted)
        {
            body = Encrypt(body, encryptionKey);
        }

        bw.Write(originalSize);
        bw.Write(body.Length);
        bw.Write(body);

        bw.Write((byte)HashAlgorithm.Md5);
        bw.Write(hash);

        bw.Flush();

        ms.Seek(offset: 0, SeekOrigin.Begin);
        return ms.ToArray();
    }

    private static byte[] Compress(byte[] data)
    {
        var target = new byte[LZ4Codec.MaximumOutputSize(data.Length)];
        var count = LZ4Codec.Encode(data, target, LZ4Level.L06_HC);
        var result = new byte[count];

        Buffer.BlockCopy(target, srcOffset: 0, result, dstOffset: 0, count);
        return result;
    }

    private static byte[] Decompress(byte[] data, int decompressedSize)
    {
        var target = new byte[decompressedSize];
        var count = LZ4Codec.Decode(data, target);
        var result = new byte[count];

        Buffer.BlockCopy(target, srcOffset: 0, result, dstOffset: 0, count);
        return result;
    }

    private static byte[] Decrypt(byte[] body, byte[] encryptionKey)
    {
        // todo: implement me
        // Recommended algorithm AES
        // Since key is known to program, IV should be random generated on every encryption
        // Since IV should be random it need to be received to decrypt
        return body;
    }

    private static byte[] Encrypt(byte[] body, byte[] encryptionKey)
    {
        // todo: implement me
        // Recommended algorithm AES
        // Since key is known to program, IV should be random generated on every encryption
        // Since IV should be random it should be send on encrypt
        return body;
    }


    public static FileChunk CreateChunk(
        int chunkIndex,
        byte[] data,
        int offset,
        int length,
        bool useCompression,
        bool isEncrypted)
    {
        var newData = new byte[length];
        Buffer.BlockCopy(data, srcOffset: 0, newData, offset, length);

        return new FileChunk(useCompression, isEncrypted)
        {
            Data = newData,
            Index = chunkIndex,
            HashAlgorithm = HashAlgorithm.Md5,
            Hash = Md5.ComputeHash(data)
        };
    }
}