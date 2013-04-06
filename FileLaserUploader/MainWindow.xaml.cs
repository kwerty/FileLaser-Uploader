using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using UploaderServices;

namespace FileLaserUploader
{


    public partial class MainWindow : Window
    {

        //http://www.xroxy.com/proxylist.htm
        //202.171.34.234:3124

        public UploaderViewModel Uploader { get; set; }

        public AboutWindow _aboutWindow;
        public SettingsWindow _settingsWindow;

        public MainWindow(UploaderViewModel vm)
        {
            InitializeComponent();

            Uploader = vm;
            Uploader.LogUpdate += new EventHandler<LogEventArgs>(LogUpdate);
            Uploader.StatusChanged += new EventHandler<UploaderStatusEventArgs>(StatusChanged);

            ActiveList.ItemsSource = Uploader.Active;
            CompleteList.ItemsSource = Uploader.Complete;
        }


        void StatusChanged(object sender, UploaderStatusEventArgs e)
        {

            AuthRequiredStatus.Visibility = Visibility.Collapsed;
            AuthenticatingStatus.Visibility = Visibility.Collapsed;
            AuthenticatedStatus.Visibility = Visibility.Collapsed;
            AuthFailedPanel.Visibility = Visibility.Collapsed;

            if (e.Status == UploaderStatus.NotAuthenticated)
                AuthRequiredStatus.Visibility = Visibility.Visible;

            else if (e.Status == UploaderStatus.Authenticated)
            {
                StatusUsername.Text = Uploader.Username;
                AuthenticatedStatus.Visibility = Visibility.Visible;
                ShowUserGrid();
            }

            else if (e.Status == UploaderStatus.Authenticating)
                AuthenticatingStatus.Visibility = Visibility.Visible;

            else if (e.Status == UploaderStatus.FailedAuthentication)
            {
                AuthFailedError.Text = Uploader.LastError;
                AuthFailedPanel.Visibility = Visibility.Visible;
            }

            //if not authenticated or failed auth
            if (e.Status <= UploaderStatus.FailedAuthentication)
                ShowAuthenticateButton();

        }

        void ShowUserGrid()
        {

            AliasLabel.Content = Uploader.Alias;
            PointsLabel.Content = Uploader.Points;
            ReferrerPointsLabel.Content = Uploader.RefererPoints;
            SpaceUsedLabel.Content = Uploader.SpaceUsed;
            AccountTypeLabel.Content = Uploader.AccountType;
            PremiumExpiresLabel.Content = Uploader.PremiumExpiry.ToShortDateString();
            ftpPasswordBox.Content = Uploader.FTPpassword;

            PremiumLinkLabel.Visibility = Uploader.AccountType == "Free account" ? Visibility.Visible : Visibility.Collapsed;

            AuthenticatePanel.Visibility = System.Windows.Visibility.Collapsed;
            UserGrid.Visibility = System.Windows.Visibility.Visible;

        }

        void ShowAuthenticateButton()
        {
            UserGrid.Visibility = System.Windows.Visibility.Collapsed;
            AuthenticatePanel.Visibility = System.Windows.Visibility.Visible;
        }

        void LogUpdate(object sender, LogEventArgs e)
        {
            LogBox.AppendText(String.Format("{0} {1}\r\n", e.Timestamp, e.Message));
            LogBox.ScrollToEnd();
        }

        void ActiveListContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            CancelContextItem.IsEnabled = ActiveList.SelectedItems.Count > 0;
        }

        void CompleteListContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            RestartContextItem.IsEnabled = CompleteList.SelectedItems.Count > 0;
            CopyURLContextItem.IsEnabled = CompleteList.SelectedItems.Count > 0;
            ClearContextItem.IsEnabled = CompleteList.SelectedItems.Count > 0;
            ClearAllContextItem.IsEnabled = Uploader.Complete.Count > 0;
            ClearUnsuccessfulContextItem.IsEnabled = Uploader.Complete.Where(u => u.Status > UploaderServices.UploadStatus.Success).Count() > 0;
        }

        private void ViewMenuOpened(object sender, RoutedEventArgs e)
        {
            ClearCompletedMenu.IsEnabled = Uploader.Complete.Count > 0;
            ClearUnsucessfulMenu.IsEnabled  = Uploader.Complete.Where(u => u.Status > UploaderServices.UploadStatus.Success).Count() > 0;
        }

        void ActiveListDrop(object sender, DragEventArgs e)
        {
            DataObject data = e.Data as DataObject;

            if (data == null || !data.ContainsFileDropList())
                return;

            foreach (string file in data.GetFileDropList())
                Uploader.CreateUpload(file);

        }

        void WindowClosing(object sender, CancelEventArgs e)
        {

            foreach (UploadViewModel ul in Uploader.Active.ToList())
                ul.Cancel();

            if (_aboutWindow != null)
                _aboutWindow.Close();

            if (_settingsWindow != null)
                _settingsWindow.Close();

        }

        void AddFileClicked(object sender, RoutedEventArgs e)
        {

            System.Windows.Forms.OpenFileDialog openFile = new System.Windows.Forms.OpenFileDialog();
            openFile.CheckFileExists = true;
            openFile.Multiselect = true;

            System.Windows.Forms.DialogResult result = openFile.ShowDialog();

            if (result != System.Windows.Forms.DialogResult.OK)
                return;

            foreach (string fn in openFile.FileNames)
                Uploader.CreateUpload(fn);
        }


        void CopyURLsClicked(object sender, RoutedEventArgs e)
        {

            StringBuilder sb = new StringBuilder();

            foreach (UploadViewModel ul in CompleteList.SelectedItems)
                sb.AppendLine(ul.DownloadURL);

            SetClipboardText(sb.ToString());

        }

        void RestartClicked(object sender, RoutedEventArgs e)
        {
            foreach (UploadViewModel ul in CompleteList.SelectedItems.Cast<UploadViewModel>().ToList())
                ul.Restart();
        }

        void CancelClicked(object sender, RoutedEventArgs e)
        {
            foreach (UploadViewModel ul in ActiveList.SelectedItems.Cast<UploadViewModel>().ToList())
                ul.Cancel();
        }

        void ClearClicked(object sender, RoutedEventArgs e)
        {
            foreach (UploadViewModel ul in CompleteList.SelectedItems.Cast<UploadViewModel>().ToList())
                ul.Close();
        }

        void ClearAllClicked(object sender, RoutedEventArgs e)
        {
            foreach (UploadViewModel ul in Uploader.Complete.ToList())
                ul.Close();
        }

        void ClearUnsuccessfulClicked(object sender, RoutedEventArgs e)
        {
            foreach (UploadViewModel ul in Uploader.Complete.Where(v => v.Status > UploadStatus.Success).ToList())
                ul.Close();
        }

        void SettingsClicked(object sender, RoutedEventArgs e)
        {
            ShowSettings();
        }

        void ShowSettings()
        {
            if (_settingsWindow == null)
                _settingsWindow = new SettingsWindow(this, Uploader);

            _settingsWindow.Show();
            _settingsWindow.Focus();
        }

        void AboutClicked(object sender, RoutedEventArgs e)
        {
            if (_aboutWindow == null)
                _aboutWindow = new AboutWindow(this);

            _aboutWindow.Show();
            _aboutWindow.Focus();


        }

        void ExitClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        void CheckUpdatesClicked(object sender, RoutedEventArgs e)
        {
            Process.Start("http://kwerty.com/FileLaser-Uploader/?curr_version=" + Assembly.GetExecutingAssembly().GetName().Version.ToString());
        }

        void VisitWebsiteClicked(object sender, RoutedEventArgs e)
        {
            Process.Start("http://kwerty.com?ref=fluploader");
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {

            if (Uploader.FirstTime)
                ShowSettings();

        }

        private void TryAgainClicked(object sender, RoutedEventArgs e)
        {
            Uploader.Authenticate();
        }

        private void ActiveListMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ActiveList.Focus();
        }

        private void CompleteListMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            CompleteList.Focus();
        }

        private void FtpButtonClick(object sender, RoutedEventArgs e)
        {
            SetClipboardText((string)ftpPasswordBox.Content);
        }

        private void AuthenticateButtonClick(object sender, RoutedEventArgs e)
        {
            ShowSettings();
        }

        private void FileLaserLinkClicked(object sender, RoutedEventArgs e)
        {
            Process.Start("http://filelaser.com/users/register/?ref=51");
        }

        private void FileLaserAccountClicked(object sender, RoutedEventArgs e)
        {
            Process.Start("http://filelaser.com/users/account/?ref=51");
        }

        private void FileLaserFilesClicked(object sender, RoutedEventArgs e)
        {
            Process.Start("http://filelaser.com/users/files/?ref=51");
        }

        private void PremiumLinkClicked(object sender, RoutedEventArgs e)
        {
            Process.Start("http://www.filelaser.com/premium/?ref=51");
        }

        public static void SetClipboardText(string text)
        {

            try
            {
                System.Windows.Forms.Clipboard.SetDataObject(text, true, 50, 200);
            }
            catch (ExternalException)
            {
                MessageBox.Show("Could not copy to clipboard", "Clipboard error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        private void ActiveListKeyUp(object sender, KeyEventArgs e)
        {

            if (e.Key == Key.Delete)
                CancelClicked(null, null);

        }


    }


}
