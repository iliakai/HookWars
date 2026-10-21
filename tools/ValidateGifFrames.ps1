$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
public static class GifFrameValidation {
    static byte[] Pixels(Bitmap bitmap) {
        var data = bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var result = new byte[data.Stride * bitmap.Height];
        Marshal.Copy(data.Scan0, result, 0, result.Length);
        bitmap.UnlockBits(data); return result;
    }
    public static string Validate(string source, string folder) {
        using (var gif = Image.FromFile(source)) {
            var dimension = new FrameDimension(gif.FrameDimensionsList[0]);
            int count = gif.GetFrameCount(dimension);
            var delays = gif.GetPropertyItem(0x5100).Value;
            for (int n=0; n<count; n++) {
                gif.SelectActiveFrame(dimension,n);
                using (var native = new Bitmap(gif.Width,gif.Height,PixelFormat.Format32bppArgb))
                using (var png = new Bitmap(System.IO.Path.Combine(folder,"frame_"+n.ToString("000")+".png"))) {
                    using(var draw=Graphics.FromImage(native)) {
                        draw.Clear(Color.Transparent);
                        draw.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                        draw.DrawImageUnscaled(gif,0,0);
                    }
                    if(png.Size!=native.Size) throw new Exception("Canvas mismatch");
                    byte[] a=Pixels(native), b=Pixels(png);
                    int opaque=0, transparent=0;
                    for(int i=0;i<a.Length;i+=4) {
                        if(a[i+3]!=b[i+3] || (a[i+3]!=0 && (a[i]!=b[i] || a[i+1]!=b[i+1] || a[i+2]!=b[i+2])))
                            throw new Exception(source+" frame "+n+" pixel "+(i/4)+" differs from Windows GIF decoder");
                        if(b[i+3]==0) transparent++; else opaque++;
                    }
                    if(transparent==0 || opaque==0) throw new Exception("Missing alpha or animal pixels");
                    if(BitConverter.ToUInt32(delays,n*4)!=16) throw new Exception("Unexpected frame duration");
                }
            }
            return System.IO.Path.GetFileName(source)+": "+count+" frames match native Windows decoder pixel-for-pixel, alpha and 160 ms delays verified";
        }
    }
}
'@
Get-ChildItem -LiteralPath Assets/Textures/Animals -Filter '*_walk.gif' | ForEach-Object {
    $frameFolder = Join-Path $_.DirectoryName ('Frames/' + $_.BaseName)
    [GifFrameValidation]::Validate($_.FullName, $frameFolder)
}
