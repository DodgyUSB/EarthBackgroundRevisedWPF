using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.Drawing;
using MenuItem = System.Windows.Forms.MenuItem;
using ContextMenu = System.Windows.Forms.ContextMenu;
using Application = System.Windows.Application;
using System.Threading;
using Timer = System.Timers.Timer;
using System.IO;
using Path = System.IO.Path;
using Image = System.Drawing.Image;
using System.Runtime.InteropServices;
using Binding = System.Windows.Data.Binding;

namespace EarthBackgroundRevisedWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        EarthBackgroundCore EarthBackground;
        Task<bool> updateTask;
        Timer timer;
        NotifyIcon trayIcon = new NotifyIcon();
        string filePath;
        bool startOnBoot;
        int[] resOptions = new int[] { 1, 2, 4, 8, 16 };
        int res;
        const int timeIntervalMins = 5;
        const string AppKeyName = "EarthBackround";
        public string timerString { get; set; }
        int currentTick = 0;
        int finalTick = 300;

        public MainWindow()
        {
            if (System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)).Count() > 1) System.Diagnostics.Process.GetCurrentProcess().Kill();
            InitializeComponent();
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            this.Closing += MainWindow_Closing;
            setParameters();
            ResSelectionComboBox.ItemsSource = resOptions;
            ResSelectionComboBox.SelectedIndex = (int)Math.Log(res, 2);
            SavePathInputTextBox.Text = filePath;
            StartOnBootCheckBox.IsChecked = startOnBoot;
            setStartOnBootReg();
            trayIcon.Visible = true;
            trayIcon.Icon = new Icon(Application.GetContentStream(new Uri("TrayIcon.ico", UriKind.Relative)).Stream);
            trayIcon.Text = "Earth Background settings";
            trayIcon.Click += TrayIcon_Click;
            ContextMenu trayMenu = new ContextMenu(new MenuItem[] { new MenuItem("Exit", new EventHandler(ExitApplication)), new MenuItem("Manual Update", new EventHandler(manualUpdate)) });
            trayIcon.ContextMenu = trayMenu;
            EarthBackground = new EarthBackgroundCore(res, filePath);
            timer = new Timer(1000);
            timer.Elapsed += Timer_Elapsed;
            timer.AutoReset = true;
            timer.Start();
            timerString = "test";
        }


        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            currentTick++;
            int secsLeftInMin = 0;
            int minsLeft = Math.DivRem(finalTick - currentTick, 60, out secsLeftInMin);
            Dispatcher.Invoke(() =>
            {
                StatusBarUpdateTime.Text = string.Format("{0}:{1}", minsLeft, secsLeftInMin);
            });
        }

        private void Update()
        {
            if (resOptions.Contains(res) && Directory.Exists(filePath) && EarthBackground != null)
            {
                EarthBackgroundCore.UpdateComplete += EarthBackgroundCore_UpdateComplete;
                updateTask = EarthBackground.update(EarthBackgroundCore.siteOption.HimawariBanded);
            }
        }

        private void EarthBackgroundCore_UpdateComplete(object sender, EarthBackgroundCore.UpdateCompleteEventArgs e)
        {
            EarthBackgroundCore.UpdateComplete -= EarthBackgroundCore_UpdateComplete;
            Console.WriteLine("update complete exit code: {0}", updateTask.Result);
            if (updateTask.Result)
            {
                Wallpaper.Set(new Uri(EarthBackground.getLatestImagePath()), Wallpaper.Style.Fit);
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            toggleVisibility();
        }

        private void toggleVisibility()
        {
            if (this.Visibility == Visibility.Visible)
            {
                this.Visibility = Visibility.Hidden;
                this.ShowInTaskbar = false;
            }
            else
            {
                this.Visibility = Visibility.Visible;
                this.ShowInTaskbar = true;
            }
        }

        private void TrayIcon_Click(object sender, EventArgs e)
        {
            toggleVisibility();
        }

        private void ExitApplication(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void manualUpdate(object sender, EventArgs e)
        {
            Update();
        }

        private void setStartOnBootReg()
        {
            Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (startOnBoot)
            {
                key.SetValue(AppKeyName, Assembly.GetExecutingAssembly().Location);
            }
            else
            {
                key.SetValue(AppKeyName, false);
            }
        }

        private void setParameters()
        {
            filePath = Properties.Settings.Default.imagePath;
            SavePathInputTextBox.Text = filePath;
            startOnBoot = Properties.Settings.Default.startOnBoot;
            res = Properties.Settings.Default.res;
        }

        private void saveSettings()
        {
            Properties.Settings.Default.imagePath = filePath;
            Properties.Settings.Default.startOnBoot = startOnBoot;
            Properties.Settings.Default.res = res;
            Properties.Settings.Default.Save();
        }

        private void ResSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            res = resOptions[ResSelectionComboBox.SelectedIndex];
        }

        private void StartOnBootCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            startOnBoot = true;
            setStartOnBootReg();
        }

        private void StartOnBootCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            startOnBoot = false;
            setStartOnBootReg();
        }

        private void BrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderBrowser = new FolderBrowserDialog();
            folderBrowser.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            folderBrowser.ShowDialog();
            filePath = folderBrowser.SelectedPath;
            SavePathInputTextBox.Text = filePath;
            EarthBackground = new EarthBackgroundCore(res, filePath);
            saveSettings();
        }

        public sealed class Wallpaper
        {
            Wallpaper() { }

            const int SPI_SETDESKWALLPAPER = 20;
            const int SPIF_UPDATEINIFILE = 0x01;
            const int SPIF_SENDWININICHANGE = 0x02;

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

            public enum Style : int
            {
                Tiled,
                Centered,
                Stretched,
                Fit
            }

            public static void Set(Uri uri, Style style)
            {
                Stream s = new System.Net.WebClient().OpenRead(uri.ToString());

                Image img = Image.FromStream(s);
                string tempPath = Path.Combine(Path.GetTempPath(), "wallpaper.bmp");
                img.Save(tempPath, System.Drawing.Imaging.ImageFormat.Bmp);
                img.Dispose();

                Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
                if (style == Style.Stretched)
                {
                    key.SetValue(@"WallpaperStyle", 2.ToString());
                    key.SetValue(@"TileWallpaper", 0.ToString());
                }

                if (style == Style.Centered)
                {
                    key.SetValue(@"WallpaperStyle", 1.ToString());
                    key.SetValue(@"TileWallpaper", 0.ToString());
                }

                if (style == Style.Tiled)
                {
                    key.SetValue(@"WallpaperStyle", 1.ToString());
                    key.SetValue(@"TileWallpaper", 1.ToString());
                }

                if (style == Style.Fit)
                {
                    key.SetValue(@"WallpaperStyle", 6.ToString());
                    key.SetValue(@"TileWallpaper", 0.ToString());
                }

                SystemParametersInfo(SPI_SETDESKWALLPAPER,
                    0,
                    tempPath,
                    SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
            }
        }
    }
}
