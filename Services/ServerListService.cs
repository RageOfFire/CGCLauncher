using System.IO;
using System.Text;

namespace CGCLauncher.Services;

public record DefaultServerEntry(string Name, string Ip);

/// <summary>
/// Writes the vanilla client's servers.dat (binary NBT) so a fresh install already has
/// your server in the multiplayer list. Only acts when the file doesn't exist yet, so a
/// returning player's own edits (added/removed servers) are never overwritten.
/// </summary>
public class ServerListService
{
    private enum NbtTag : byte { End = 0, String = 8, List = 9, Compound = 10 }

    public void EnsureDefaultServers(string installDir, IReadOnlyList<DefaultServerEntry> defaults)
    {
        if (defaults.Count == 0) return;

        var path = Path.Combine(installDir, "servers.dat");
        if (File.Exists(path)) return;

        Directory.CreateDirectory(installDir);

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream);

        WriteTagHeader(writer, NbtTag.Compound, string.Empty); // root compound, unnamed

        WriteTagHeader(writer, NbtTag.List, "servers");
        writer.Write((byte)NbtTag.Compound);
        WriteBigEndianInt(writer, defaults.Count);

        foreach (var server in defaults)
        {
            WriteStringTag(writer, "name", server.Name);
            WriteStringTag(writer, "ip", server.Ip);
            writer.Write((byte)NbtTag.End); // closes this server's compound
        }

        writer.Write((byte)NbtTag.End); // closes the root compound
    }

    private void WriteTagHeader(BinaryWriter writer, NbtTag type, string name)
    {
        writer.Write((byte)type);
        WriteModifiedUtf8(writer, name);
    }

    private void WriteStringTag(BinaryWriter writer, string name, string value)
    {
        WriteTagHeader(writer, NbtTag.String, name);
        WriteModifiedUtf8(writer, value);
    }

    private void WriteModifiedUtf8(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteBigEndianShort(writer, (short)bytes.Length);
        writer.Write(bytes);
    }

    private void WriteBigEndianShort(BinaryWriter writer, short value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        writer.Write(bytes);
    }

    private void WriteBigEndianInt(BinaryWriter writer, int value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        writer.Write(bytes);
    }
}
