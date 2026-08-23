using System;
using System.Windows;
using System.Windows.Threading;

namespace Numb
{
  public partial class CountdownWindow : Window
  {
    private readonly DispatcherTimer _timer;
    private int _secondsRemaining;

    public CountdownWindow(int seconds)
    {
      InitializeComponent();
      _secondsRemaining = seconds;
      CountdownText.Text = $"Locking in {_secondsRemaining}s...";

      _timer = new DispatcherTimer
      {
        Interval = TimeSpan.FromSeconds(1)
      };
      _timer.Tick += Timer_Tick;
      _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
      _secondsRemaining--;
      if (_secondsRemaining <= 0)
      {
        _timer.Stop();
        DialogResult = true;
        Close();
      }
      else
      {
        CountdownText.Text = $"Locking in {_secondsRemaining}s...";
      }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
      _timer.Stop();
      DialogResult = false;
      Close();
    }
  }
}
