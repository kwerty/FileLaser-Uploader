using System;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;

namespace FileLaserUploader
{

    public partial class SettingsWindow : Window
    {

        MainWindow _main;
        UploaderViewModel _viewModel;
        bool _authRequired;

        public SettingsWindow(MainWindow main, UploaderViewModel viewModel)
        {
            
            InitializeComponent();

            //Owner = main;

            _main = main;
            _viewModel = viewModel;

            if (_viewModel.Username != null)
            {
                UsernameBox.Text = _viewModel.Username;
                PasswordBox.Password = _viewModel.Password;

                UsernameBox.IsEnabled = false;
                PasswordBox.IsEnabled = false;
                RemoveButton.Visibility = Visibility.Visible;
            }

            if (_viewModel.Proxy == null)
                UseSystemRadio.IsChecked = true;

            else if (_viewModel.Proxy == UploaderViewModel.ProxyDisabled)
                DisableProxyRadio.IsChecked = true;

            else
            {
                ProxyAddressBox.Text = _viewModel.Proxy;
                UserDefinedRadio.IsChecked = true;
                
            }

            UsernameBox.Focus();

        }

        private void WindowClosed(object sender, EventArgs e)
        {
            _main._settingsWindow = null;
        }

        private void SaveSettingsButtonClicked(object sender, RoutedEventArgs e)
        {

            if ((UsernameBox.Text == String.Empty) != (PasswordBox.Password == String.Empty))
            {
                MessageBox.Show("Please enter both a username and password", "Incomplete login details", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (UserDefinedRadio.IsChecked == true && ProxyAddressBox.Text == String.Empty)
            {
                MessageBox.Show("Please enter a proxy server address", "Invalid proxy server", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string proxy = null;
            string username = UsernameBox.Text != String.Empty ? UsernameBox.Text : null;
            string password = PasswordBox.Password != String.Empty ? PasswordBox.Password : null;

            if (UseSystemRadio.IsChecked == true)
                proxy = null;

            else if (DisableProxyRadio.IsChecked == true)
                proxy = UploaderViewModel.ProxyDisabled;

            else if (UserDefinedRadio.IsChecked == true)
                proxy = ProxyAddressBox.Text;

            if (_viewModel.Username != username || _viewModel.Password != password || proxy != _viewModel.Proxy)
                _authRequired = true;

            if (!_authRequired)
            {
                Close();
                return;
            }

            try
            {
                _viewModel.SetProxy(proxy);
            }
            catch (ArgumentException)
            {
                MessageBox.Show("Proxy invalid (use format 'server:port')", "Invalid proxy server", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _viewModel.SetUser(username, password);

            _viewModel.SaveSettings();
            _viewModel.Authenticate();
            Close();

        }

        private void RemoveButtonClicked(object sender, RoutedEventArgs e)
        {

            _authRequired = true;

            UsernameBox.Text = null;
            PasswordBox.Password = null;
            UsernameBox.IsEnabled = true;
            PasswordBox.IsEnabled = true;

            RemoveButton.Visibility = Visibility.Collapsed;

        }

        private void CancelButtonClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ProxySettingsLinkClicked(object sender, RoutedEventArgs e)
        {

            Process myProcess = new Process();
            ProcessStartInfo myProcessStartInfo = new ProcessStartInfo("rundll32.exe");
            myProcessStartInfo.UseShellExecute = false;
            myProcessStartInfo.RedirectStandardOutput = true;
            myProcessStartInfo.Arguments = "shell32.dll,Control_RunDLL inetcpl.cpl,,4";
            myProcess.StartInfo = myProcessStartInfo;
            myProcess.Start();

        }

        private void UserDefinedRadioUnchecked(object sender, RoutedEventArgs e)
        {
            ProxyAddressBox.Visibility = Visibility.Collapsed;
        }

        private void UserDefinedRadioChecked(object sender, RoutedEventArgs e)
        {
            ProxyAddressBox.Visibility = Visibility.Visible;
            ProxyAddressBox.Focus();
        }

        private void PasswordBoxKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                SaveSettingsButtonClicked(null, null);
        }


    }
}
