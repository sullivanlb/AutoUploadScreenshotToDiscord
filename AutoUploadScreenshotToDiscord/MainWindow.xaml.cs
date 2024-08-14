using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using KeyDownTester.Keys;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace AutoUploadScreenshotToDiscord
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Key shortcutToScreen = Key.F10;

        private bool isWaitingForKey = false;

        GlobalHotkey saveHotKey;

        ManualResetEvent man = new ManualResetEvent(false);

        string webHookUrl; 

        public MainWindow()
        {
            InitializeComponent();
            
            // Setup hook
            HotkeysManager.SetupSystemHook();
            HotkeysManager.RequiresModifierKey = false;

            saveHotKey = new GlobalHotkey(ModifierKeys.None, shortcutToScreen, ScreenshotAndSendToDiscord, false);

            // Close hook when app close
            Closing += MainWindow_Closing;
        }

        private async void ChangeShortcutToScreen_Click(object sender, RoutedEventArgs e)
        {
            Button buttonClicked = sender as Button;

            if (buttonClicked == null) { return ; }

            isWaitingForKey = true;

            buttonClicked.Content = "Choisis ton raccourci !";

            await WaitForKeyPressed();

            isWaitingForKey = false;

            buttonClicked.Content = shortcutToScreen;
        }

        private void ScreenshotAndSendToDiscord()
        {
            string filePath;

            // Créer une image à partir de l'écran principal
            using (var bitmap = new Bitmap((int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
                }

                // Convertir l'image en format BitmapSource pour l'utiliser dans WPF
                var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(bitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

                // Enregistrer l'image
                var encoder = new PngBitmapEncoder(); // L'encodeur peut etre changer selon le format d'image souhaite
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

                filePath = this.TempPathFileTextBox.Text; // Chemin de sauvegarde de l'image
                filePath = filePath + "\\test.png";

                // TODO: tester filepath

                using (var stream = File.Create(filePath))
                {
                    encoder.Save(stream);
                }
            }

            SendDiscordMessage(filePath);
        }
    

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            Button buttonClicked = sender as Button;

            if (buttonClicked == null) { return; }

            if (buttonClicked.Content.ToString() == "Start") this.URLTextBox.IsEnabled = false;
            else this.URLTextBox.IsEnabled = true;

            buttonClicked.Content = buttonClicked.Content.ToString() == "Start" ? "Stop" : "Start";

            saveHotKey.CanExecute = !saveHotKey.CanExecute;

            webHookUrl = this.URLTextBox.Text;
        }

        private Task WaitForKeyPressed()
        {
            return Task.Factory.StartNew(() =>
            {
                man.WaitOne();

                man.Reset();
            }
            );

        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (!isWaitingForKey) return;

            shortcutToScreen = e.Key;

            // TODO: wait 1sec to not call key press event on setting the shortcut

            //Remove old hotkey
            HotkeysManager.RemoveHotkey(saveHotKey);

            // Create hotkey
            saveHotKey = new GlobalHotkey(ModifierKeys.None, shortcutToScreen, ScreenshotAndSendToDiscord, false);

            // Add hotkey
            HotkeysManager.AddHotkey(saveHotKey);

            man.Set();
        }

        private async void SendDiscordMessage(string _filepath)
        {
            HttpClient client = new HttpClient();
            MultipartFormDataContent content = new MultipartFormDataContent();

            var file = File.ReadAllBytes(_filepath);
            content.Add(new ByteArrayContent(file, 0, file.Length), Path.GetExtension(_filepath), _filepath);

            client.PostAsync(this.URLTextBox.Text, content).Wait();
            client.Dispose();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Cancel the closure
            e.Cancel = true;

            // Hide the window
            Application.Current.MainWindow.Hide();
        }

        private void MenuItemOpen_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.MainWindow.Show();
        }
        
        private void MenuItemHide_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.MainWindow.Hide();
        }

        private void MenuItemExit_Click(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }

        private void TempPathFileButton_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog diag = new FolderBrowserDialog();
            if (diag.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                webHookUrl                      = diag.SelectedPath;  // selected folder path
                this.TempPathFileTextBox.Text   = diag.SelectedPath;
            }
        }
    }
}