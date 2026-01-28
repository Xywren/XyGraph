using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XyGraph
{
    // Lightweight adorner that displays a bitmap following the mouse during drag
    public class InputPreviewAdorner : Adorner
    {
        private readonly ImageBrush brush;
        private Point offset = new Point(0, 0);
        private double opacity = 0.85;

        public InputPreviewAdorner(UIElement adornedElement, RenderTargetBitmap bmp) : base(adornedElement)
        {
            brush = new ImageBrush(bmp) { Opacity = opacity };
            IsHitTestVisible = false;
        }

        public void SetOffset(Point p)
        {
            offset = p;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (brush == null) return;

            double w = brush.ImageSource.Width;
            double h = brush.ImageSource.Height;
            Rect dest = new Rect(offset.X - w / 2.0, offset.Y - h / 2.0, w, h);
            drawingContext.DrawRectangle(brush, null, dest);
        }
    }
}
