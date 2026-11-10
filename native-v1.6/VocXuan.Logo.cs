using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShiCi {
public static class RomanLogo {
    public static DrawingImage Image() {
        var group = new DrawingGroup();
        var ink = (SolidColorBrush)new BrushConverter().ConvertFromString("#245C4B");
        var pen = new Pen(ink, 2.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        using (var d = group.Open()) {
            d.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 128, 128));
            d.DrawGeometry(null, pen, Geometry.Parse("M 56,114 C 21,101 12,58 35,20 M 72,114 C 107,101 116,58 93,20"));
            double[][] leaves = {
                new[]{35.0,30.0,-36.0},new[]{26.0,46.0,-58.0},new[]{23.0,64.0,-73.0},
                new[]{27.0,82.0,-99.0},new[]{35.0,98.0,-115.0},new[]{48.0,109.0,-130.0}
            };
            var leaf = Geometry.Parse("M 0,0 C -6,-6 -5,-15 0,-20 C 5,-14 6,-6 0,0 Z");
            foreach (var item in leaves) {
                d.PushTransform(new TranslateTransform(item[0], item[1])); d.PushTransform(new RotateTransform(item[2])); d.DrawGeometry(ink,null,leaf); d.Pop();d.Pop();
                d.PushTransform(new TranslateTransform(128-item[0], item[1])); d.PushTransform(new RotateTransform(-item[2])); d.DrawGeometry(ink,null,leaf); d.Pop();d.Pop();
            }
            d.DrawGeometry(null,pen,Geometry.Parse("M 45,34 L 83,34 M 49,47 L 79,47 M 53,48 L 53,91 M 75,48 L 75,91 M 60,52 L 60,87 M 68,52 L 68,87 M 48,93 L 80,93 L 80,98 L 48,98 Z M 43,99 L 85,99 L 85,104 L 43,104 Z"));
            d.DrawEllipse(null,pen,new Point(47,41),6,6); d.DrawEllipse(null,pen,new Point(81,41),6,6);
            d.DrawGeometry(null,pen,Geometry.Parse("M 47,41 L 52,41 M 76,41 L 81,41 M 52,38 L 76,38"));
        }
        var image = new DrawingImage(group); image.Freeze(); return image;
    }
    public static void Export(string path) {
        var visual = new DrawingVisual();
        using (var d = visual.RenderOpen()) d.DrawImage(Image(),new Rect(0,0,256,256));
        var target = new RenderTargetBitmap(256,256,96,96,PixelFormats.Pbgra32); target.Render(visual);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(target));
        using (var f = File.Create(path)) png.Save(f);
    }
}
}
