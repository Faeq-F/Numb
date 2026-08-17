using System.Windows;

namespace Numb
{
  public partial class TouchShieldOverlay : Window
  {
    public TouchShieldOverlay(Screen screen)
    {
      InitializeComponent();

      // Cover the full bounds of the designated screen (handles multi-monitor setups)
      Rectangle bounds = screen.Bounds;
      Left = bounds.Left;
      Top = bounds.Top;
      Width = bounds.Width;
      Height = bounds.Height;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
      base.OnSourceInitialized(e);
      Topmost = true;
    }
  }
}
