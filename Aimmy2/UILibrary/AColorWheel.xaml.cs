using Aimmy2.Theme;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Aimmy2.UILibrary
{
    public partial class AColorWheel : UserControl
    {
        public event Action<Color> ColorChanged;

        private Color _initialColor;
        public bool SuppressThemeApply { get; set; } = false;
        private bool _isMouseDown = false;
        private WriteableBitmap _colorWheelBitmap;
        private Color _selectedColor = Color.FromRgb(114, 46, 209);
        private Color _previewColor = Color.FromRgb(114, 46, 209);
        private double _brightness = 1.0;
        private double _currentHue = 0;
        private double _currentSaturation = 0;
        private bool _isUpdatingFromCode = false;

        public AColorWheel()
        {
            InitializeComponent();
            Loaded += AColorWheel_Loaded;
        }

        private void AColorWheel_Loaded(object sender, RoutedEventArgs e)
        {
            CreateColorWheel();
            if (SuppressThemeApply)
            {
                _selectedColor = _initialColor;
            }
            else
            {
                _selectedColor = ThemeManager.ThemeColor;
            }

            _previewColor = _selectedColor;
            UpdateColorPreview(_previewColor);
            PositionSelectorForColor(_selectedColor);
            UpdateBrightnessGradient();
            var thumb = (BrightnessSlider.Template.FindName("PART_Track", BrightnessSlider) as Track)?.Thumb;
            if (thumb != null)
            {
                thumb.DragDelta += BrightnessSlider_DragDelta;
            }

        }


        private void CreateColorWheel()
        {
            int size = 200;
            _colorWheelBitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Bgra32, null);

            byte[] pixels = new byte[size * size * 4];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    double dx = x - size / 2.0;
                    double dy = y - size / 2.0;
                    double distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance <= size / 2.0)
                    {
                        double angle = Math.Atan2(dy, dx);
                        double hue = (angle + Math.PI) / (2 * Math.PI) * 360;
                        double saturation = distance / (size / 2.0);
                        Color color = HsvToRgb(hue, saturation, 1.0);

                        int pixelOffset = (y * size + x) * 4;
                        pixels[pixelOffset] = color.B;
                        pixels[pixelOffset + 1] = color.G;
                        pixels[pixelOffset + 2] = color.R;
                        pixels[pixelOffset + 3] = 255;
                    }
                    else
                    {
                        int pixelOffset = (y * size + x) * 4;
                        pixels[pixelOffset + 3] = 0;
                    }
                }
            }

            _colorWheelBitmap.WritePixels(new Int32Rect(0, 0, size, size), pixels, size * 4, 0);
            ColorWheelEllipse.Fill = new ImageBrush(_colorWheelBitmap);
        }

        private void ColorWheel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isMouseDown = true;
            UpdateColorFromPosition(e.GetPosition(ColorWheelCanvas));
            ColorWheelCanvas.CaptureMouse();
        }

        private void ColorWheel_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isMouseDown)
            {
                UpdateColorFromPosition(e.GetPosition(ColorWheelCanvas));
            }
        }

        private void ColorWheel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isMouseDown = false;
            ColorWheelCanvas.ReleaseMouseCapture();
            if (!SuppressThemeApply)
            {
                SaveThemeColor(_previewColor);
            }

        }
        private void UpdateColorFromPosition(Point position)
        {
            double centerX = ColorWheelCanvas.Width / 2;
            double centerY = ColorWheelCanvas.Height / 2;

            double dx = position.X - centerX;
            double dy = position.Y - centerY;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance > centerX)
            {
                double angle = Math.Atan2(dy, dx);
                dx = Math.Cos(angle) * centerX;
                dy = Math.Sin(angle) * centerX;
                distance = centerX;
            }
            Canvas.SetLeft(ColorSelector, centerX + dx - 10);
            Canvas.SetTop(ColorSelector, centerY + dy - 10);
            _currentHue = (Math.Atan2(dy, dx) + Math.PI) / (2 * Math.PI) * 360;
            _currentSaturation = distance / centerX;
            _previewColor = HsvToRgb(_currentHue, _currentSaturation, _brightness);
            UpdateColorPreview(_previewColor);
            UpdateBrightnessGradient();
            if (ColorDot != null)
            {
                ColorDot.Fill = new SolidColorBrush(_previewColor);
            }

            _previewColor = HsvToRgb(_currentHue, _currentSaturation, _brightness);
            UpdateColorPreview(_previewColor);
            UpdateBrightnessGradient();
            if (ColorDot != null)
            {
                ColorDot.Fill = new SolidColorBrush(_previewColor);
            }

            if (!SuppressThemeApply)
            {
                SaveThemeColor(_previewColor);
            }

        }

        private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BrightnessSlider != null && !_isUpdatingFromCode)
            {
                _brightness = BrightnessSlider.Value;
                _previewColor = HsvToRgb(_currentHue, _currentSaturation, _brightness);
                UpdateColorPreview(_previewColor);

                if (ColorDot != null)
                {
                    ColorDot.Fill = new SolidColorBrush(_previewColor);
                }
                ColorChanged?.Invoke(_previewColor);
                if (!SuppressThemeApply)
                {
                    SaveThemeColor(_previewColor);
                }
            }
        }

        private void BrightnessSlider_DragDelta(object sender, DragDeltaEventArgs e)
        {
            _brightness = BrightnessSlider.Value;
            _previewColor = HsvToRgb(_currentHue, _currentSaturation, _brightness);
            UpdateColorPreview(_previewColor);

            if (ColorDot != null)
            {
                ColorDot.Fill = new SolidColorBrush(_previewColor);
            }

            ColorChanged?.Invoke(_previewColor);

            if (!SuppressThemeApply)
            {
                SaveThemeColor(_previewColor);
            }
        }

        private void UpdateColorPreview(Color color)
        {
            if (ColorPreview != null)
            {
                ColorPreview.Fill = new SolidColorBrush(color);
            }
            if (HexValue != null)
            {
                HexValue.Content = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
        }

        private void UpdateBrightnessGradient()
        {
            if (BrightnessGradientStart != null && BrightnessGradientEnd != null)
            {
                BrightnessGradientStart.Color = Color.FromRgb(0, 0, 0);
                BrightnessGradientEnd.Color = HsvToRgb(_currentHue, _currentSaturation, 1.0);
            }
        }
        private void SaveThemeColor(Color color)
        {
            _selectedColor = color;
            ThemeManager.SetThemeColor(color);
            string hexColor = ThemeManager.GetThemeColorHex();
        }
        private void PositionSelectorForColor(Color color)
        {
            _isUpdatingFromCode = true;
            double h, s, v;
            RgbToHsv(color, out h, out s, out v);
            _currentHue = h;
            _currentSaturation = s;
            _brightness = v;
            double angle = h * Math.PI / 180.0 - Math.PI;
            double radius = s * (ColorWheelCanvas.Width / 2);
            double x = ColorWheelCanvas.Width / 2 + Math.Cos(angle) * radius;
            double y = ColorWheelCanvas.Height / 2 + Math.Sin(angle) * radius;
            Canvas.SetLeft(ColorSelector, x - 10);
            Canvas.SetTop(ColorSelector, y - 10);
            BrightnessSlider.Value = v;
            if (ColorDot != null)
            {
                ColorDot.Fill = new SolidColorBrush(color);
            }
            UpdateBrightnessGradient();

            _isUpdatingFromCode = false;
        }

        public void LoadSavedThemeColor(string hexColor)
        {
            try
            {
                Color color = (Color)ColorConverter.ConvertFromString(hexColor);
                _selectedColor = color;
                _previewColor = color;
                UpdateColorPreview(color);
                PositionSelectorForColor(color);
                ThemeManager.SetThemeColor(color);
            }
            catch
            {
            }
        }

        #region Color Conversion Methods
        private Color HsvToRgb(double hue, double saturation, double value)
        {
            int hi = (int)(hue / 60) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            byte v = (byte)value;
            byte p = (byte)(value * (1 - saturation));
            byte q = (byte)(value * (1 - f * saturation));
            byte t = (byte)(value * (1 - (1 - f) * saturation));

            switch (hi)
            {
                case 0: return Color.FromRgb(v, t, p);
                case 1: return Color.FromRgb(q, v, p);
                case 2: return Color.FromRgb(p, v, t);
                case 3: return Color.FromRgb(p, q, v);
                case 4: return Color.FromRgb(t, p, v);
                default: return Color.FromRgb(v, p, q);
            }
        }

        private void RgbToHsv(Color color, out double hue, out double saturation, out double value)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;
            value = max;
            if (max == 0)
                saturation = 0;
            else
                saturation = delta / max;
            if (delta == 0)
            {
                hue = 0;
            }
            else if (max == r)
            {
                hue = 60 * (((g - b) / delta) % 6);
            }
            else if (max == g)
            {
                hue = 60 * (((b - r) / delta) + 2);
            }
            else
            {
                hue = 60 * (((r - g) / delta) + 4);
            }

            if (hue < 0)
                hue += 360;
        }

        #endregion
        public Color GetCurrentPreviewColor()
        {
            if (ColorPreview.Fill is SolidColorBrush brush)
            {
                return brush.Color;
            }
            return Colors.Transparent;
        }
        public void SetInitialColor(Color color)
        {
            _initialColor = color;
        }



    }
}