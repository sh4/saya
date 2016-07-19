using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace saya.core
{
    static public partial class FileUtils
    {
        public class Shortcut
        {
            public string LinkPath { get; private set; }
            public string TargetPath { get; private set; }
            public string WorkingDirectory { get; private set; }
            public string Name { get; private set; }
            public string IconLocation { get; private set; }
            public string Arguments { get; private set; }

            [DllImport("shell32.dll")]
            private static extern bool SHGetPathFromIDListEx(byte[] pidl, [MarshalAs(UnmanagedType.LPWStr)]StringBuilder pszPath, int cchPath, uint options);

            [DllImport("msi.dll", CharSet = CharSet.Unicode)]
            private static extern uint MsiGetShortcutTarget(
                string shortcutTarget, 
                [MarshalAs(UnmanagedType.LPWStr)]StringBuilder productCode,
                [MarshalAs(UnmanagedType.LPWStr)]StringBuilder featureId,
                [MarshalAs(UnmanagedType.LPWStr)]StringBuilder componentCode);

            [DllImport("msi.dll", CharSet = CharSet.Unicode)]
            private static extern int MsiGetComponentPath(
                string product,
                string component,
                [MarshalAs(UnmanagedType.LPWStr)]StringBuilder path,
                ref int cchPath);

            private const int _MAX_PATH = 260;

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
                DarwinDataBlock = 0xa0000006,
                VistaAndAboveIDListDataBlock = 0xA000000C,
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

            private static readonly int HeaderSize = 0x4c;
            private static readonly int LinkCLSIDSize = 0x10;
            private static readonly int LinkInfoHeaderSize = 0x1c;
            private static readonly int GuidCharLength = 39;

            public Shortcut(string lnkFilePath)
            {
                using (var fs = new FileStream(lnkFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new BinaryReader(fs, Encoding.Unicode))
                {
                    Parse(lnkFilePath, reader);
                }
            }

            public Shortcut(string lnkFilePath, Stream stream)
            {
                using (var reader = new BinaryReader(stream, Encoding.Unicode, true))
                {
                    Parse(lnkFilePath, reader);
                }
            }

            private void Parse(string lnkFilePath, BinaryReader reader)
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
                    var idListSize = reader.ReadUInt16();
                    var idList = reader.ReadBytes(idListSize);
                    if ((flags & LinkFlags.HasLinkInfo) == 0)
                    {
                        // advertise shortcut っぽいので MSI 経由で情報を得る
                        TargetPath = GetMsiShortcutTargetPath(lnkFilePath);
                        // MSI 経由で情報を得られない場合は IDLIST とみなしてパス取得を試みる
                        if (string.IsNullOrEmpty(TargetPath))
                        {
                            TargetPath = GetPathFromIdList(idList);
                        }
                    }
                }
                if ((flags & LinkFlags.HasLinkInfo) != 0)
                {
                    TargetPath = GetLocalBasePath(reader);
                }
                if ((flags & LinkFlags.HasName) != 0)
                {
                    Name = ReadStringData(reader, flags);
                }
                if ((flags & LinkFlags.HasRelativePath) != 0)
                {
                    var lnkFileDirectory = Path.GetDirectoryName(lnkFilePath);
                    var relativePath = ReadStringData(reader, flags);

                    // 既に TargetPath が設定されていた時はそちらを優先する
                    if (TargetPath == null)
                    {
                        TargetPath = Path.GetFullPath(Path.Combine(lnkFileDirectory, relativePath));
                    }
                }
                if ((flags & LinkFlags.HasWorkingDir) != 0)
                {
                    WorkingDirectory = ReadStringData(reader, flags);
                }
                if ((flags & LinkFlags.HasArguments) != 0)
                {
                    Arguments = ReadStringData(reader, flags);
                }
                if ((flags & LinkFlags.HasIconLocation) != 0)
                {
                    IconLocation = ReadStringData(reader, flags);
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
                    var signature = reader.ReadUInt32();
                    switch ((ExtraDataBlockSignature)signature)
                    {
                        case ExtraDataBlockSignature.EnvironmentVariableDataBlock:
                            {
                                const int TargetCharLength = 260;

                                reader.BaseStream.Seek(TargetCharLength * sizeof(byte), SeekOrigin.Current);

                                var chars = reader.ReadBytes(TargetCharLength * sizeof(char));
                                var envPath = Encoding.Unicode.GetString(chars).TrimEnd('\0');

                                TargetPath = Environment.ExpandEnvironmentVariables(envPath);
                            }
                            break;
                        case ExtraDataBlockSignature.DarwinDataBlock:
                            // Advertise Shortcut の場合、パスではなくバイナリが格納されているので自力でパースできない
                            // たぶん Windows Installer に格納されている DB 側の何らかのキーっぽい
                            break;
                        case ExtraDataBlockSignature.VistaAndAboveIDListDataBlock:
                            {
                                var idList = reader.ReadBytes(sectionSize - sizeof(int));
                                TargetPath = GetPathFromIdList(idList);
                            }
                            break;
                        default:
                            break;
                    }
                    reader.BaseStream.Seek(sectionStart + sectionSize, SeekOrigin.Begin);
                }

                LinkPath = lnkFilePath;
            }

            private string GetPathFromIdList(byte[] idList)
            {
                var linkTarget = new StringBuilder(_MAX_PATH);
                if (SHGetPathFromIDListEx(idList, linkTarget, _MAX_PATH, 0))
                {
                    return linkTarget.ToString();
                }
                else
                {
                    return null;
                }
            }

            private static string ReadStringData(BinaryReader reader, LinkFlags flags)
            {
                var countChars = reader.ReadUInt16();
                var isUnicode = (flags & LinkFlags.IsUnicode) != 0;
                var encoding = isUnicode ? Encoding.Unicode : Encoding.Default;
                var charBytes = countChars * (isUnicode ? sizeof(char) : sizeof(byte));

                return encoding.GetString(reader.ReadBytes(charBytes));
            }

            private string GetLocalBasePath(BinaryReader reader)
            {
                var baseStream = reader.BaseStream;
                var infoPosition = baseStream.Position;

                var linkInfoSize = reader.ReadUInt32();
                var linkInfoHeaderSize = reader.ReadUInt32();
                var linkInfoFlags = (LinkInfoFlags)reader.ReadInt32();

                if ((linkInfoFlags & LinkInfoFlags.VolumeIDAndLocalBasePath) == 0)
                {
                    return null;
                }

                var lolumeIDOffset = reader.ReadUInt32();
                var localBasePathOffset = reader.ReadUInt32();

                var lommonNetworkRelativeLinkOffset = reader.ReadUInt32();
                var lommonPathSuffixOffset = reader.ReadUInt32();

                uint localBasePathOffsetUnicode = 0;
                if (linkInfoHeaderSize > LinkInfoHeaderSize)
                {
                    localBasePathOffsetUnicode = reader.ReadUInt32();
                }

                string localBasePath;
                
                if (linkInfoHeaderSize > LinkInfoHeaderSize)
                {
                    // has unicode path field
                    baseStream.Seek(infoPosition + localBasePathOffsetUnicode, SeekOrigin.Begin);
                    var sb = new StringBuilder();
                    char ch;
                    while ((ch = reader.ReadChar()) != char.MinValue)
                    {
                        sb.Append(ch);
                    }
                    localBasePath = sb.ToString();
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
                    localBasePath = Encoding.Default.GetString(bytes.ToArray());
                }

                baseStream.Seek(infoPosition + linkInfoSize, SeekOrigin.Begin);

                return localBasePath;
            }

            private string GetMsiShortcutTargetPath(string lnkFilePath)
            {
                var productCode = new StringBuilder(GuidCharLength);
                var componentCode = new StringBuilder(GuidCharLength);
                if (MsiGetShortcutTarget(lnkFilePath, productCode, null, componentCode) != 0)
                {
                    return null;
                }
                var path = new StringBuilder(_MAX_PATH);
                var cchPath = _MAX_PATH;
                if (MsiGetComponentPath(productCode.ToString(), componentCode.ToString(), path, ref cchPath) > 0)
                {
                    return path.ToString();
                }
                return null;
            }
        }

        public class ShortcutEqualityComparer : IEqualityComparer<Shortcut>
        {
            public bool Equals(Shortcut x, Shortcut y)
            {
                return PathUtils.EqualsPath(x.TargetPath, y.TargetPath)
                    && PathUtils.EqualsPath(x.WorkingDirectory, y.WorkingDirectory)
                    && x.Arguments == y.Arguments;
            }

            public int GetHashCode(Shortcut obj)
            {
                if (obj == null)
                {
                    return 0;
                }
                if (obj.TargetPath == null)
                {
                    return PathUtils.NormalizePath(obj.LinkPath).GetHashCode();
                }

                var r = PathUtils.NormalizePath(obj.TargetPath).GetHashCode();
                if (obj.WorkingDirectory != null)
                {
                    r ^= PathUtils.NormalizePath(obj.WorkingDirectory).GetHashCode();
                }
                if (obj.Arguments != null)
                {
                    r ^= obj.Arguments.GetHashCode();
                }
                return r;
            }
        }
    }
}
