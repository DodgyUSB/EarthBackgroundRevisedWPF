using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
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
//using CContextMenu = System.Windows.Controls.ContextMenu;

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
        const string AppKeyName = "EarthBackround";
        public string timerString { get; set; }
        int currentTick = 0;
        int finalTick = 300;
        public string[] avaliableSites = EarthBackgroundCore.siteOptionNames;
        private EarthBackgroundCore.siteOption selectedSite;
        private static event Microsoft.Win32.PowerModeChangedEventHandler powerChanged;
        private static event Microsoft.Win32.SessionEndedEventHandler sessionEnded;
        private bool autoSetBackground;
        private DateTime LastImageCaptureTime;
        private DateTime LastImageDownloadTime;
        private string currentImagePath;
        private bool validExit = false;
        List<int> timerOptions = new List<int>(new int[] { 300 });

        public MainWindow()
        {
            if (System.Diagnostics.Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location)).Count() > 1) System.Diagnostics.Process.GetCurrentProcess().Kill();
            InitializeComponent();
            powerChanged += MainWindow_powerChanged;
            sessionEnded += MainWindow_sessionEnded;
            Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            this.Closing += MainWindow_Closing;
            setParameters();
            ResSelectionComboBox.ItemsSource = resOptions;
            ResSelectionComboBox.SelectedIndex = (int)Math.Log(res, 2);
            SavePathInputTextBox.Text = filePath;
            StartOnBootCheckBox.IsChecked = startOnBoot;
            setStartOnBootReg();
            siteSelectionComboBox.ItemsSource = avaliableSites;
            trayIcon.Visible = true;
            trayIcon.Icon = new Icon(Application.GetContentStream(new Uri("TrayIcon.ico", UriKind.Relative)).Stream);
            trayIcon.Text = "Earth Background settings";
            trayIcon.DoubleClick += TrayIcon_Click;
            ContextMenu trayMenu = new ContextMenu(new MenuItem[] { new MenuItem("Manual Update", new EventHandler(manualUpdate)), new MenuItem("Force Update", new EventHandler(forceUpdate)), new MenuItem("Exit", new EventHandler(ExitApplication)) });
            trayIcon.ContextMenu = trayMenu;
            changeTimerOptions(timerOptions);
            EarthBackground = new EarthBackgroundCore(res, filePath);
            if (File.Exists(currentImagePath))
            {
                setImage(currentImagePath);
            }
            else if (File.Exists(EarthBackground.getLatestImagePath()))
            {
                setImage(EarthBackground.getLatestImagePath());
            }
                timer = new Timer(1000);
            timer.Elapsed += Timer_Elapsed;
            timer.AutoReset = true;
            timer.Start();
            timerString = "test";
            //Update();
        }

        private void MainWindow_sessionEnded(object sender, Microsoft.Win32.SessionEndedEventArgs e)
        {
            ApplicationClose();
        }

        private void MainWindow_powerChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
        {
            if(e.Mode == Microsoft.Win32.PowerModes.Resume)
            {
                EarthBackground = new EarthBackgroundCore(res, filePath);
            }
            else if(updateTask != null)
            {
                if(updateTask.Status == TaskStatus.Running)
                {
                    updateTask.Wait();
                }
            }
        }

        private void changeTimerOptions()
        {
            if (StatusBarUpdateTime.ContextMenu != null)
            {
                StatusBarUpdateTime.ClearValue(TextBlock.ContextMenuProperty);
            }
            StatusBarUpdateTime.ContextMenu = new System.Windows.Controls.ContextMenu();
            System.Windows.Controls.MenuItem item = new System.Windows.Controls.MenuItem();
            item.Header = "Custom Time";
            item.Click += CustomTime_Clicked;
            StatusBarUpdateTime.ContextMenu.Items.Add(item);
        }

        private void changeTimerOptions(List<int> timerTickOptions)
        {
            Dispatcher.Invoke(() =>
            {
                if (StatusBarUpdateTime.ContextMenu != null)
                {
                    StatusBarUpdateTime.ClearValue(TextBlock.ContextMenuProperty);
                }
                StatusBarUpdateTime.ContextMenu = new System.Windows.Controls.ContextMenu();
                if (timerTickOptions.Count() > 0)
                {
                    foreach (int tick in timerTickOptions)
                    {
                        StatusBarUpdateTime.ContextMenu.Items.Add(menuItemBuilder(buildTimerString(tick), tick));
                    }
                }
                System.Windows.Controls.MenuItem item = new System.Windows.Controls.MenuItem();
                item.Header = "Custom Time";
                item.Click += CustomTime_Clicked;
                StatusBarUpdateTime.ContextMenu.Items.Add(item);
            });
        }

        private void CustomTime_Clicked(object sender, RoutedEventArgs e)
        {
            CustomTimeWindow customTimeWindow = new CustomTimeWindow();
            if (customTimeWindow.ShowDialog() == true)
            {
                timerOptions.Add(customTimeWindow.Ticks);
                changeTimerOptions(timerOptions);
                setTimerLength(customTimeWindow.Ticks);
            }
        }

        private System.Windows.Controls.MenuItem menuItemBuilder(string header, int tag)
        {
            System.Windows.Controls.MenuItem item = new System.Windows.Controls.MenuItem();
            item.Header = header;
            item.Tag = tag;
            item.Click += Item_Click;
            item.PreviewMouseRightButtonDown += Item_PreviewMouseRightButtonDown;

            //define the context menu and menu item for deleting the time from the list cos why not
            item.ContextMenu = new System.Windows.Controls.ContextMenu();
            System.Windows.Controls.MenuItem itemItem = new System.Windows.Controls.MenuItem();
            itemItem.Header = "Delete";
            itemItem.Click += ItemDelete_Click;
            itemItem.Tag = tag;
            item.ContextMenu.Items.Add(itemItem);

            return item;
        }

        private void Item_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ((System.Windows.Controls.MenuItem)e.Source).ContextMenu.IsOpen = true;
            e.Handled = true;
        }

        private void ItemDelete_Click(object sender, RoutedEventArgs e)
        {
            int tickValue = (int)((System.Windows.Controls.MenuItem)e.Source).Tag;
            timerOptions.Remove(tickValue);
            changeTimerOptions(timerOptions);
            if(tickValue == finalTick)
            {
                setTimerLength(300);
            }
        }

        private void Item_Click(object sender, RoutedEventArgs e)
        {
            int tick = (int)((System.Windows.Controls.MenuItem)(e.Source)).Tag;
            setTimerLength(tick);
        }

        private void setImage(string path)
        {

            //LastImage.BeginInit();
            //LastImage.Source = new BitmapImage(new Uri(@"C:\Users\650084\Pictures\Patched Earth.png"));
            LastImage.Source = new BitmapImage(new Uri(path));
            //LastImage.EndInit();
        }

        private void setTimerLength(int hours, int mins, int secs)
        {
            finalTick = (hours * 1200) + (mins * 60) + secs;
        }

        private void setTimerLength(int tick)
        {
            finalTick = tick;
            currentTick = 0;
        }

        private void clearImage()
        {
            Dispatcher.Invoke(() =>
            {
                LastImage.BeginInit();
                LastImage.ClearValue(System.Windows.Controls.Image.SourceProperty);
                LastImage.EndInit();
            });
        }

        private void Update_Progressed(object sender, EarthBackgroundCore.DownloadStatusChangedEventArgs e)
        {
            Console.WriteLine("update event recieved message: {0} percentage: {1}%", e.Status, e.percentageComplete);
            Dispatcher.Invoke(() =>
            {
                statusBarStatusTextBlock.Text = e.Status;
                statusBarUpdateProgressBar.Value = e.percentageComplete;
            });
        }


        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            currentTick++;
            int remainingTick = finalTick - currentTick;
            setTimerText(remainingTick);
            if (remainingTick <= 0)
            {
                Update();
            }
        }

        private void setTimerText(int SecondTicks)
        {
            Dispatcher.Invoke(() =>
            {
                StatusBarUpdateTime.Text = buildTimerString(SecondTicks);
            });
        }

        private string buildTimerString(int SecondsTick)
        {
            int secs = 0;
            int mins = Math.DivRem(SecondsTick, 60, out secs);
            int hours = Math.DivRem(mins, 60, out mins);
            if (finalTick >= 1200) 
            {
                return string.Format("{0}:{1}:{2}", addZeros(hours, 2), addZeros(mins, 2), addZeros(secs, 2));
            }
            else
            {
                return string.Format("{0}:{1}", addZeros(mins, 2), addZeros(secs, 2));
            }
        }

        private void Update()
        {
            timer.Stop();
            currentTick = 0;
            if (resOptions.Contains(res) && Directory.Exists(filePath) && EarthBackground != null)
            {
                EarthBackgroundCore.UpdateComplete += EarthBackgroundCore_UpdateComplete;
                EarthBackgroundCore.DownloadStatusChanged += Update_Progressed;
                clearImage();
                if (updateTask != null)
                {
                    if (updateTask.IsCompleted)
                    {
                        updateTask.Dispose();
                    }
                    else
                    {
                        updateTask.Wait();
                        updateTask.Dispose();
                    }
                }
                updateTask = EarthBackground.update(selectedSite);
            }
            else
            {
                timer.Start();
            }
        }

        private bool running()
        {
            if (updateTask != null)
            {
                if (!updateTask.IsCompleted)
                {
                    Console.WriteLine("Update is already running");
                    return true;
                }
            }
            return false;
        }

        private void Update(bool forceUpdate)
        {
            timer.Stop();
            currentTick = 0;
            if (resOptions.Contains(res) && Directory.Exists(filePath) && EarthBackground != null && !running())
            {
                EarthBackgroundCore.UpdateComplete += EarthBackgroundCore_UpdateComplete;
                EarthBackgroundCore.DownloadStatusChanged += Update_Progressed;
                clearImage();
                if (updateTask != null)
                {
                    if (updateTask.IsCompleted)
                    {
                        updateTask.Dispose();
                    }
                    else
                    {
                        updateTask.Wait();
                        updateTask.Dispose();
                    }
                }
                updateTask = EarthBackground.update(selectedSite, forceUpdate);
            }
            else
            {
                timer.Start();
            }
        }

        private void EarthBackgroundCore_UpdateComplete(object sender, EarthBackgroundCore.UpdateCompleteEventArgs e)
        {
            timer.Start();
            DateTime updateTime = DateTime.Now;
            Dispatcher.Invoke(() =>
            {
                DownloadAttemptTextBlock.Text = updateTime.ToShortTimeString();
            });
            EarthBackgroundCore.UpdateComplete -= EarthBackgroundCore_UpdateComplete;
            EarthBackgroundCore.DownloadStatusChanged -= Update_Progressed;
            Console.WriteLine("update complete exit code: {0}", updateTask.Result);
            if (updateTask.Result)
            {
                if (autoSetBackground)
                {
                    Wallpaper.Set(new Uri(EarthBackground.getLatestImagePath()), Wallpaper.Style.Fit);
                }
                Console.WriteLine("Latest Image time: {0}", EarthBackground.latestImageTimeUTC.ToString());
                LastImageCaptureTime = EarthBackground.latestImageTimeUTC.ToLocalTime();
                Console.WriteLine("Latest Image timer local: {0}", LastImageCaptureTime.ToString());
                LastImageDownloadTime = updateTime;
                currentImagePath = EarthBackground.getLatestImagePath();
                Dispatcher.Invoke(() =>
                {
                    setImage(currentImagePath);
                    ImageTakenTextBlock.Text = LastImageCaptureTime.ToString();
                    ImageDownloadedTextBlock.Text = LastImageDownloadTime.ToShortTimeString();
                });
                Thread.Sleep(2000);
                Dispatcher.Invoke(() =>
                {
                    statusBarStatusTextBlock.Text = string.Format("last updated: {0}:{1}", addZeros(updateTime.Hour, 2) , addZeros(updateTime.Minute, 2));
                    statusBarUpdateProgressBar.Value = 0;
                });
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    string temp = statusBarStatusTextBlock.Text;
                    statusBarStatusTextBlock.Text = "No new Image";
                    setImage(EarthBackground.getLatestImagePath());
                    Thread.Sleep(2000);

                    statusBarStatusTextBlock.Text = temp;
                    statusBarUpdateProgressBar.Value = 0;
                });
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!validExit)
            {
                e.Cancel = true;
                toggleVisibility();
            }
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
            ApplicationClose();
        }

        private void ApplicationClose()
        {
            validExit = true;
            saveSettings();
            if(updateTask != null)
            {
                if (updateTask.IsCompleted)
                {
                    updateTask.Dispose();
                }
                else
                {
                    updateTask.Wait();
                    updateTask.Dispose();
                }
            }
            powerChanged -= MainWindow_powerChanged;
            sessionEnded -= MainWindow_sessionEnded;
            Application.Current.Shutdown();
        }

        private void manualUpdate(object sender, EventArgs e)
        {
            Update();
        }

        private void forceUpdate(object sender, EventArgs e)
        {
            Update(true);
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
            selectedSite = (EarthBackgroundCore.siteOption)Properties.Settings.Default.siteOption;
            siteSelectionComboBox.SelectedIndex = (int)selectedSite;
            autoSetBackground = Properties.Settings.Default.autoSetBackground;
            AutoSetBackgroundCheckBox.IsChecked = autoSetBackground;
            LastImageCaptureTime = Properties.Settings.Default.lastImageCaptureTime;
            LastImageDownloadTime = Properties.Settings.Default.lastImageDownloadTime;
            currentImagePath = Properties.Settings.Default.currentImagePath;
            ImageTakenTextBlock.Text = LastImageCaptureTime.ToString();
            ImageDownloadedTextBlock.Text = LastImageDownloadTime.ToShortTimeString();
            if (Properties.Settings.Default.timerTickOptions != null)
            {
                timerOptions = new List<int>(Properties.Settings.Default.timerTickOptions);
            }
        }

        private void saveSettings()
        {
            Properties.Settings.Default.imagePath = filePath;
            Properties.Settings.Default.startOnBoot = startOnBoot;
            Properties.Settings.Default.res = res;
            Properties.Settings.Default.siteOption = (int)selectedSite;
            Properties.Settings.Default.autoSetBackground = autoSetBackground;
            Properties.Settings.Default.lastImageCaptureTime = LastImageCaptureTime;
            Properties.Settings.Default.lastImageDownloadTime = LastImageDownloadTime;
            Properties.Settings.Default.currentImagePath = currentImagePath;
            Properties.Settings.Default.timerTickOptions = timerOptions.ToArray();
            Properties.Settings.Default.Save();
        }

        private void ResSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            res = resOptions[ResSelectionComboBox.SelectedIndex];
            if (updateTask != null)
            {
                if (updateTask.Status == TaskStatus.Running)
                {
                    updateTask.Wait();
                }
            }
            EarthBackground = new EarthBackgroundCore(res, filePath);
            saveSettings();
        }

        private void StartOnBootCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            startOnBoot = true;
            setStartOnBootReg();
            saveSettings();
        }

        private void StartOnBootCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            startOnBoot = false;
            setStartOnBootReg();
            saveSettings();
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

        private void siteSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedSite = (EarthBackgroundCore.siteOption)siteSelectionComboBox.SelectedIndex;
            saveSettings();
        }

        private static string addZeros(int value, int size)
        {
            string val = value.ToString();
            if (val.Length < size)
            {
                for (int x = val.Length; x < size; x++)
                {
                    val = "0" + val;
                }
            }
            return val;
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
                s.Close();
                s.Dispose();

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

        private void AutoSetBackgroundCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            autoSetBackground = true;
        }

        private void AutoSetBackgroundCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            autoSetBackground = false;
        }

        private void ManualUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            Update();
        }

        private void ForceUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            Update(true);
        }
    }

}
