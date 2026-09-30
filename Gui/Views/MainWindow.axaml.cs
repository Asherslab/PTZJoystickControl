using Avalonia.Controls;

namespace PtzJoystickControl.Gui.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Title = $"PTZ Joystick Control v{Program.Version}";

            //var logWin = new LogWindow();
            //logWin.DataContext = new LogWindowViewModel();
            //logWin.Show();
        }
    }
}
