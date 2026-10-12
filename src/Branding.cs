using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SILocalAI
{
    public static class Branding
    {
        [DllImport("gdi32.dll")] static extern IntPtr AddFontMemResourceEx(IntPtr memory, uint size, IntPtr reserved, ref uint count);
        static readonly PrivateFontCollection fonts = new PrivateFontCollection();
        // Keep font memory alive for GDI+; Windows releases the private GDI registrations at process exit.
        static readonly List<IntPtr> memory = new List<IntPtr>();
        public static readonly FontFamily Family;
        static Branding()
        {
            foreach (string weight in new[] { "Regular", "Bold" })
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SILocalAI.Font." + weight))
            {
                if (stream == null) throw new InvalidDataException("Embedded font missing.");
                using (var bytes = new MemoryStream())
                {
                    stream.CopyTo(bytes); byte[] data = bytes.ToArray();
                    IntPtr pointer = Marshal.AllocCoTaskMem(data.Length); memory.Add(pointer); Marshal.Copy(data, 0, pointer, data.Length);
                    uint count = 0;
                    if (AddFontMemResourceEx(pointer, (uint)data.Length, IntPtr.Zero, ref count) == IntPtr.Zero || count == 0)
                        throw new InvalidDataException("Embedded font could not be loaded.");
                    fonts.AddMemoryFont(pointer, data.Length);
                }
            }
            Family = fonts.Families[0];
        }
        public static Icon LoadIcon()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SILocalAI.AppIcon"))
            using (var icon = new Icon(stream)) return (Icon)icon.Clone();
        }
    }
}
