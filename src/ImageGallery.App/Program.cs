using ImageGallery.App.Forms;
using System.Windows.Forms;

namespace ImageGallery.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
