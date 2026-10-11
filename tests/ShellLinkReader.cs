using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

// Read the Unicode Windows Shell interface. WScript.Shell may expose an ANSI target.
public static class ShellLinkReader
{
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkObject { }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        // First method after IUnknown. The remaining methods are deliberately unused.
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path,
                     int capacity, IntPtr findData, uint flags);
    }

    public static string ReadTarget(string shortcutPath)
    {
        object link = new ShellLinkObject();
        try
        {
            ((IPersistFile)link).Load(Path.GetFullPath(shortcutPath), 0);
            var target = new StringBuilder(32768);
            ((IShellLinkW)link).GetPath(target, target.Capacity, IntPtr.Zero, 0);
            return target.ToString();
        }
        finally { Marshal.FinalReleaseComObject(link); }
    }
}
