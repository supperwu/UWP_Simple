using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

//“空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 上有介绍

namespace ScanQRCode
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        const int borderThickness = 5;

        public MainPage()
        {
            this.InitializeComponent();
        }

        private void InitFocusRec()
        {
            leftTopBorder.BorderThickness = new Thickness(borderThickness, borderThickness, 0, 0);
            rightTopBorder.BorderThickness = new Thickness(0, borderThickness, borderThickness, 0);
            leftBottomBorder.BorderThickness = new Thickness(borderThickness, 0, 0, borderThickness);
            rightBottomBorder.BorderThickness = new Thickness(0, 0, borderThickness, borderThickness);

            var borderLength = 20;
            leftTopBorder.Width = leftTopBorder.Height = borderLength;
            rightTopBorder.Width = rightTopBorder.Height = borderLength;
            leftBottomBorder.Width = leftBottomBorder.Height = borderLength;
            rightBottomBorder.Width = rightBottomBorder.Height = borderLength;

            var focusRecLength = Math.Min(ActualWidth / 2, ActualHeight / 2);
            scanGrid.Width = scanGrid.Height = focusRecLength;
            scanCavas.Width = scanCavas.Height = focusRecLength;

            scanStoryboard.Stop();
            scanLine.X2 = scanCavas.Width - 20;
            scanAnimation.To = scanCavas.Height;

            scanStoryboard.Begin();
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            InitFocusRec();
        }
    }
}
