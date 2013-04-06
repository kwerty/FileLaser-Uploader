using System;
using System.Windows;
using System.Diagnostics;
using System.Reflection;

namespace FileLaserUploader
{

    public partial class AboutWindow : Window
    {

        MainWindow _main;

        public AboutWindow(MainWindow main)
        {
            InitializeComponent();

            //Owner = main;

            _main = main;

            LicenseBox.Text = Properties.Resources.license;

            VersionTextBlock.Text = String.Format("{0} (beta)", Assembly.GetExecutingAssembly().GetName().Version.ToString());

        }

        private void WindowClosed(object sender, EventArgs e)
        {
            _main._aboutWindow = null;
        }

        private void FatCowLinkClicked(object sender, RoutedEventArgs e)
        {
            Process.Start("http://www.fatcow.com/free-icons?ref=fluploader");
        }

        private void KwertyLinkClicked(object sender, RoutedEventArgs e)
        {
            Process.Start("http://kwerty.com?ref=fluploader");
        }

        private void RaskLinkClicked(object sender, RoutedEventArgs e)
        {
            Process.Start("http://www.jonasraskdesign.com?ref=fluploader");
        }

    }
}
