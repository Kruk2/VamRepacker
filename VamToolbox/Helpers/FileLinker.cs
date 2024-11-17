using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace VamToolbox.Helpers;

public interface ISoftLinker
{
    bool SoftLink(string destination, string source, bool dryRun);
    bool IsSoftLink(string file);
    string? GetSoftLink(string file);
}

public sealed class SoftLinker : ISoftLinker
{
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        [MarshalAs(UnmanagedType.LPWStr)] string filename,
        int dwDesiredAccess,
        [MarshalAs(UnmanagedType.U4)] FileShare share,
        uint lpSecurityAttributes,
        [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
        int dwFlagsAndAttributes,
        IntPtr templateFile);

    [DllImport("Kernel32.dll", SetLastError = true)]
    private static extern bool SetFileInformationByHandle(SafeHandle hFile, int FileInformationClass, ref FILE_BASIC_INFO FileInformation, uint dwBufferSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct FILE_BASIC_INFO
    {
        internal long CreationTime;
        internal long LastAccessTime;
        internal long LastWriteTime;
        internal long ChangeTime;
        internal uint FileAttributes;
    }

    public bool IsSoftLink(string file) => GetSoftLink(file) != null;
    public string? GetSoftLink(string file) => File.ResolveLinkTarget(file, true)?.FullName;

    public bool SoftLink(string destination, string source, bool dryRun)
    {
        if (File.Exists(destination))
            return true;
        if (!File.Exists(source))
            throw new VamToolboxException($"Copying failed. {source} doesn't exist;");

        if (dryRun)
            return true;

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        try {
            File.CreateSymbolicLink(destination, source);
            return SetSymbolicLinkTimes(destination, source);

        } catch (UnauthorizedAccessException) {
            return false;
        }
    }

    private static bool SetSymbolicLinkTimes(string destination, string source)
    {
        const int fileBasicInfo = 0;
        var sourceFileInfo = new FileInfo(source);

        using var handle = CreateFile(
            destination,
            0x40000000 /* Write */,
            FileShare.ReadWrite | FileShare.Delete,
            lpSecurityAttributes: 0,
            FileMode.Open,
            0x00200000,
            IntPtr.Zero);

        if (handle.IsInvalid) {
            return false;
        }

        var basicInfo = new FILE_BASIC_INFO {
            CreationTime = sourceFileInfo.CreationTimeUtc.ToFileTime(),
            LastAccessTime = -1,
            LastWriteTime = sourceFileInfo.LastWriteTimeUtc.ToFileTime(),
            ChangeTime = -1,
            FileAttributes = 0
        };

        return SetFileInformationByHandle(handle, fileBasicInfo, ref basicInfo, (uint)Marshal.SizeOf(basicInfo));
    }
}