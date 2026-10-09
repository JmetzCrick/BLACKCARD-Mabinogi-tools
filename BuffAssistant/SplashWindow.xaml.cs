using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace BuffAssistant;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            StartStarlight();
            await FadeToAsync(1, 350);
            await Task.Delay(2800);
            await FadeToAsync(0, 900);

            var main = new MainWindow();
            Application.Current.MainWindow = main;
            main.Show();
            Close();
        };
        Closed += (_, _) =>
        {
            foreach (System.Windows.UIElement star in StarlightCanvas.Children)
            {
                star.BeginAnimation(OpacityProperty, null);
                if (star.RenderTransform is System.Windows.Media.TranslateTransform transform)
                {
                    transform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
                    transform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
                }
            }
            StarlightCanvas.Children.Clear();
        };
    }

    private void StartStarlight()
    {
        if (StarlightCanvas.Children.Count > 0) return;
        StarlightCanvas.Clip = new System.Windows.Media.RectangleGeometry(new Rect(0, 0, Width, Height), 16, 16);
        var random = new Random();
        for (var i = 0; i < 28; i++)
        {
            var length = 22 + random.NextDouble() * 34;
            var star = new System.Windows.Controls.Canvas
            {
                Width = length * 0.65 + 3, Height = length + 3, Opacity = 0,
                RenderTransform = new System.Windows.Media.TranslateTransform()
            };
            var trail = new System.Windows.Shapes.Line
            {
                X1 = length * 0.65, Y1 = 0, X2 = 1, Y2 = length,
                StrokeThickness = 0.8 + random.NextDouble() * 0.5,
                StrokeEndLineCap = System.Windows.Media.PenLineCap.Round,
                Stroke = new System.Windows.Media.LinearGradientBrush
                {
                    StartPoint = new Point(1, 0), EndPoint = new Point(0, 1),
                    GradientStops = new System.Windows.Media.GradientStopCollection
                    {
                        new(System.Windows.Media.Colors.Transparent, 0),
                        new(System.Windows.Media.Color.FromArgb(65, 155, 193, 255), 0.55),
                        new(System.Windows.Media.Color.FromArgb(220, 234, 245, 255), 1)
                    }
                }
            };
            var head = new System.Windows.Shapes.Ellipse { Width = 1.8, Height = 1.8, Fill = System.Windows.Media.Brushes.AliceBlue };
            System.Windows.Controls.Canvas.SetLeft(head, 0.1);
            System.Windows.Controls.Canvas.SetTop(head, length - 0.9);
            star.Children.Add(trail);
            star.Children.Add(head);
            StarlightCanvas.Children.Add(star);
            var duration = TimeSpan.FromSeconds(1.3 + random.NextDouble() * 1.1);
            var delay = TimeSpan.FromSeconds(random.NextDouble() * 1.5);
            var startX = 60 + random.NextDouble() * 580;
            var startY = -120 + random.NextDouble() * 260;
            var fall = new DoubleAnimation(startY, startY + 720, duration) { BeginTime = delay, RepeatBehavior = RepeatBehavior.Forever };
            var drift = new DoubleAnimation(startX, startX - 468, duration) { BeginTime = delay, RepeatBehavior = RepeatBehavior.Forever };
            var shimmer = new DoubleAnimationUsingKeyFrames { Duration = duration, BeginTime = delay, RepeatBehavior = RepeatBehavior.Forever };
            shimmer.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
            shimmer.KeyFrames.Add(new LinearDoubleKeyFrame(0.75, KeyTime.FromPercent(0.08)));
            shimmer.KeyFrames.Add(new LinearDoubleKeyFrame(0.65, KeyTime.FromPercent(0.8)));
            shimmer.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
            var transform = (System.Windows.Media.TranslateTransform)star.RenderTransform;
            transform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, fall);
            transform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, drift);
            star.BeginAnimation(OpacityProperty, shimmer);
        }
    }

    private Task FadeToAsync(double opacity, int milliseconds)
    {
        var tcs = new TaskCompletionSource();
        var animation = new DoubleAnimation(opacity, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };
        animation.Completed += (_, _) => tcs.TrySetResult();
        BeginAnimation(OpacityProperty, animation);
        return tcs.Task;
    }
}

