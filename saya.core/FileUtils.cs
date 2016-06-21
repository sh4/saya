using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace saya.core
{
    public static class FileUtils
    {
        private static class Shortcut
        {
            private static readonly int HeaderSize = 0x4c;
            private static readonly int LinkCLSIDSize = 0x10;

            [Flags]
            private enum LinkFlags : int
            {
                HasLinkTargetIDList = 0x1,
                HasLinkInfo = 0x2,
                HasName = 0x4,
                HasRelativePath = 0x8,
                HasWorkingDir = 0x10,
                HasArguments = 0x20,
                HasIconLocation = 0x40,
                IsUnicode = 0x80,
                ForceNoLinkInfo = 0x100,
                AllowLinkToLink = 0x800000,
            }

            [Flags]
            private enum ExtraDataBlockSignature : uint
            {
                EnvironmentVariableDataBlock = 0xA0000001,
            }

            private static readonly int IgnoreHeaderSize =
                (sizeof(long) * 3)  // CreationTime + AccessTime + WriteTime (8bytes * 3)
                + sizeof(int)       // FileSize (4bytes)
                + sizeof(int)       // IconIndex (4bytes)
                + sizeof(int)       // ShowCommand (4bytes)
                + sizeof(short)     // Hotkey (2bytes)
                + sizeof(short)     // Reserved (2bytes)
                + sizeof(int)       // Reserved2 (4bytes)
                + sizeof(int);      // Reserved3 (4bytes)

            [Flags]
            private enum LinkInfoFlags : int
            {
                VolumeIDAndLocalBasePath = 0x1,
                CommonNetworkRelativeLinkAndPathSuffix = 0x2,
            }
            private static readonly int LinkInfoHeaderSize = 0x1c;

            public static string GetPath(string lnkFilePath, BinaryReader reader)
            {
                if (reader.ReadInt32() != HeaderSize)
                {
                    throw new InvalidDataException("Shortcut header size is 0x4c as required.");
                }
                var baseStream = reader.BaseStream;
                baseStream.Seek(LinkCLSIDSize, SeekOrigin.Current);

                var flags = (LinkFlags)reader.ReadInt32();
                var fileAttributes = (FileAttributes)reader.ReadInt32();

                baseStream.Seek(IgnoreHeaderSize, SeekOrigin.Current);

                if ((flags & LinkFlags.HasLinkTargetIDList) != 0)
                {
                    var idListSize = reader.ReadInt16();
                    baseStream.Seek(idListSize, SeekOrigin.Current);
                }
                if ((flags & LinkFlags.HasLinkInfo) != 0)
                {
                    return GetLocalBasePath(reader);
                }
                if ((flags & LinkFlags.HasName) != 0)
                {
                    ReadStringData(reader, flags);
                }
                if ((flags & LinkFlags.HasRelativePath) != 0)
                {
                    var lnkFileDirectory = Path.GetDirectoryName(lnkFilePath);
                    var relativePath = ReadStringData(reader, flags);
                    return Path.GetFullPath(Path.Combine(lnkFileDirectory, relativePath));
                }
                if ((flags & LinkFlags.HasWorkingDir) != 0)
                {
                    var workingDir = ReadStringData(reader, flags);
                }
                if ((flags & LinkFlags.HasArguments) != 0)
                {
                    ReadStringData(reader, flags);
                }
                if ((flags & LinkFlags.HasIconLocation) != 0)
                {
                    ReadStringData(reader, flags);
                }
                // read the extra data section
                for (;;)
                {
                    var sectionStart = reader.BaseStream.Position;
                    var sectionSize = reader.ReadInt32();
                    if (sectionSize < 4)
                    {
                        break; // terminal block
                    }
                    var signature = (ExtraDataBlockSignature)reader.ReadUInt32();
                    switch (signature)
                    {
                        case ExtraDataBlockSignature.EnvironmentVariableDataBlock:
                            {
                                const int TargetCharLength = 260;
                                reader.BaseStream.Seek(TargetCharLength * sizeof(byte), SeekOrigin.Current);
                                var chars = reader.ReadBytes(TargetCharLength * sizeof(char));
                                var envPath = Encoding.Unicode.GetString(chars).TrimEnd('\0');
                                return Environment.ExpandEnvironmentVariables(envPath);
                            }
                        default:
                            break;
                    }
                    reader.BaseStream.Seek(sectionStart + sectionSize, SeekOrigin.Begin);
                }
                return null;
            }

            private static string ReadStringData(BinaryReader reader, LinkFlags flags)
            {
                var countChars = reader.ReadUInt16();
                var isUnicode = (flags & LinkFlags.IsUnicode) != 0;
                var encoding = isUnicode ? Encoding.Unicode : Encoding.Default;
                var charBytes = countChars * (isUnicode ? sizeof(char) : sizeof(byte));

                return encoding.GetString(reader.ReadBytes(charBytes));
            }

            private static string GetLocalBasePath(BinaryReader reader)
            {
                var baseStream = reader.BaseStream;

                var infoPosition = baseStream.Position;
                var infoSize = reader.ReadInt32();
                var headerSize = reader.ReadInt32();
                var flags = (LinkInfoFlags)reader.ReadInt32();

                var volumeIdOffset = reader.ReadInt32();
                var localBasePathOffset = reader.ReadInt32();

                if ((flags & LinkInfoFlags.VolumeIDAndLocalBasePath) == 0)
                {
                    return null;
                }

                if (headerSize > LinkInfoHeaderSize)
                {
                    var commonNetworkRelativeLinkOffset = reader.ReadInt32();
                    var commonPathSuffixOffset = reader.ReadInt32();
                    var localBasePathOffsetUnicode = reader.ReadInt32();

                    // has unicode path field
                    baseStream.Seek(infoPosition + localBasePathOffsetUnicode, SeekOrigin.Begin);
                    var bytes = new List<byte>();
                    byte b, lastb = byte.MinValue;
                    while (!((b = reader.ReadByte()) == byte.MinValue && lastb == byte.MinValue))
                    {
                        bytes.Add(b);
                        lastb = b;
                    }
                    return Encoding.Unicode.GetString(bytes.ToArray());
                }
                else
                {
                    // has multibyte path field
                    baseStream.Seek(infoPosition + localBasePathOffset, SeekOrigin.Begin);
                    var bytes = new List<byte>();
                    byte b;
                    while ((b = reader.ReadByte()) != byte.MinValue)
                    {
                        bytes.Add(b);
                    }
                    return Encoding.Default.GetString(bytes.ToArray());
                }
            }
        }

        public static string GetShortcutPath(string lnkFilePath)
        {
            using (var fs = new FileStream(lnkFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new BinaryReader(fs))
            {

                return Shortcut.GetPath(lnkFilePath, reader);
            }
        }

        public static IEnumerable<string> EnumerateFileEntries(string path)
        {
            var directories = Enumerable.Empty<string>();
            try
            {
                directories = Directory.EnumerateDirectories(path);
            }
            catch (UnauthorizedAccessException) { }

            foreach (var directoryPath in directories)
            {
                var files = Enumerable.Empty<string>();
                try
                {
                    files = Directory.EnumerateFiles(directoryPath);
                }
                catch (UnauthorizedAccessException) { }

                foreach (var filePath in files)
                {
                    yield return filePath;
                }
                foreach (var filePath in EnumerateFileEntries(directoryPath))
                {
                    yield return filePath;
                }
            }
        }

    }
}
