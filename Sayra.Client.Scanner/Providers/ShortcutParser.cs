using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Sayra.Client.Scanner.Providers
{
    public static class ShortcutParser
    {
        public static string? ResolveShortcut(string shortcutPath)
        {
            if (string.IsNullOrWhiteSpace(shortcutPath) || !File.Exists(shortcutPath))
                return null;

            string ext = Path.GetExtension(shortcutPath);
            if (ext.Equals(".url", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveUrlShortcut(shortcutPath);
            }
            if (ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveLnkShortcut(shortcutPath);
            }

            return null;
        }

        private static string? ResolveUrlShortcut(string path)
        {
            try
            {
                foreach (var line in File.ReadLines(path))
                {
                    if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                    {
                        string target = line.Substring(4).Trim();
                        // Sometimes URL can point to a local file e.g., file:///C:/Games/game.exe
                        if (target.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                        {
                            target = target.Substring(8);
                            if (Path.DirectorySeparatorChar == '/')
                            {
                                if (!target.StartsWith("/"))
                                {
                                    target = "/" + target;
                                }
                            }
                            else
                            {
                                target = target.Replace('/', '\\');
                            }
                        }
                        return target;
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }
            return null;
        }

        private static string? ResolveLnkShortcut(string path)
        {
            try
            {
                using var fileStream = File.OpenRead(path);
                using var reader = new BinaryReader(fileStream);

                if (fileStream.Length < 0x4C) return null;

                // 1. Verify header size
                int headerSize = reader.ReadInt32();
                if (headerSize != 0x4C) return null;

                // 2. Verify CLSID (optional check but good for safety)
                byte[] clsid = reader.ReadBytes(16);

                // 3. LinkFlags
                uint flags = reader.ReadUInt32();

                // Skip to the end of header (header is 76 bytes total, we've read 4 + 16 + 4 = 24 bytes)
                reader.ReadBytes(76 - 24);

                // Check HasLinkTargetIDList (bit 0)
                if ((flags & 0x01) != 0)
                {
                    ushort idListSize = reader.ReadUInt16();
                    reader.ReadBytes(idListSize);
                }

                // Check HasLinkInfo (bit 1)
                if ((flags & 0x02) != 0)
                {
                    long linkInfoPos = fileStream.Position;
                    int linkInfoSize = reader.ReadInt32();
                    int linkInfoHeaderSize = reader.ReadInt32();
                    int linkInfoFlags = reader.ReadInt32();
                    int volumeIDOffset = reader.ReadInt32();
                    int localBasePathOffset = reader.ReadInt32();

                    if (localBasePathOffset > 0 && localBasePathOffset < linkInfoSize)
                    {
                        fileStream.Position = linkInfoPos + localBasePathOffset;
                        var sb = new StringBuilder();
                        char c;
                        while ((c = (char)reader.ReadByte()) != '\0')
                        {
                            sb.Append(c);
                        }
                        string targetPath = sb.ToString();
                        if (!string.IsNullOrWhiteSpace(targetPath) && targetPath.Contains(@":\"))
                        {
                            return targetPath;
                        }
                    }
                }

                // Fallback: Scan the binary file for any absolute windows path strings
                fileStream.Position = 0;
                byte[] bytes = reader.ReadBytes((int)fileStream.Length);
                string rawText = Encoding.ASCII.GetString(bytes);
                var match = Regex.Match(rawText, @"[a-zA-Z]:\\[^"":\n\r\t<>|?*]+(\.exe)");
                if (match.Success)
                {
                    return match.Value;
                }
            }
            catch
            {
                // Ignore link parsing errors, fallback to null
            }
            return null;
        }
    }
}
