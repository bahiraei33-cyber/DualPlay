using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NAudio.CoreAudioApi;
using DualPlay.Audio;

namespace DualPlay;

public partial class MainWindow : Window
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private DualPlayEngine? _engine;
    private bool _isReverse;

    public MainWindow()
    {
        InitializeComponent();
        RefreshDevices();
        UpdateTabStyles();
    }

    private void RefreshDevices()
    {
        try
        {
            var devices = _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                .Where(d => !string.IsNullOrWhiteSpace(d.FriendlyName))
                .OrderBy(d => d.FriendlyName)
                .ToList();

            FirstDevice.ItemsSource = devices;
            SecondDevice.ItemsSource = devices;

            var defaultDevice = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            FirstDevice.SelectedItem = devices.FirstOrDefault(d => d.ID == defaultDevice.ID);
            SecondDevice.SelectedItem = devices.FirstOrDefault(d => d.ID != defaultDevice.ID);

            StatusText.Text = devices.Count == 0
                ? "No active audio output devices found."
                : "Select two connected audio outputs.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Could not list audio devices: " + ex.Message;
        }
    }

    private void DualTab_Click(object sender, RoutedEventArgs e)
    {
        if (_engine?.IsRunning == true)
            StopAudio();
        _isReverse = false;
        DualPanel.Visibility = Visibility.Visible;
        ReversePanel.Visibility = Visibility.Collapsed;
        ActionButton.Content = "Enable Dual Play";
        StatusText.Text = "Select two connected audio outputs.";
        UpdateTabStyles();
    }

    private void ReverseTab_Click(object sender, RoutedEventArgs e)
    {
        if (_engine?.IsRunning == true)
            StopAudio();
        _isReverse = true;
        DualPanel.Visibility = Visibility.Collapsed;
        ReversePanel.Visibility = Visibility.Visible;
        ActionButton.Content = "Enable Reverse Play";
        StatusText.Text = "Bluetooth audio receiver support is required in Windows.";
        UpdateTabStyles();
    }

    private void UpdateTabStyles()
    {
        DualTab.Background = !_isReverse ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("PanelBrush");
        ReverseTab.Background = _isReverse ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("PanelBrush");
    }

    private void Device_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FirstDevice.SelectedItem is MMDevice first && SecondDevice.SelectedItem is MMDevice second && first.ID == second.ID)
        {
            StatusText.Text = "Choose two different audio devices.";
        }
        else if (!_isReverse)
        {
            StatusText.Text = "Ready.";
        }
    }

    private void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isReverse)
        {
            MessageBox.Show(
                "Reverse Play needs Windows to expose a Bluetooth A2DP audio-receiver endpoint for your phone. This feature is not available on every Windows 11 PC, and this prototype does not install a Bluetooth receiver driver.",
                "Reverse Play unavailable",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (_engine?.IsRunning == true)
        {
            StopAudio();
            return;
        }

        if (FirstDevice.SelectedItem is not MMDevice first || SecondDevice.SelectedItem is not MMDevice second)
        {
            StatusText.Text = "Choose two audio devices first.";
            return;
        }

        if (first.ID == second.ID)
        {
            StatusText.Text = "Choose two different audio devices.";
            return;
        }

        try
        {
            // System audio must currently be playing through the first selected device.
            var defaultDevice = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (defaultDevice.ID != first.ID)
            {
                MessageBox.Show(
                    "For this first prototype, set the first selected device as the Windows default output in Settings > System > Sound. Then start Dual Play again.",
                    "Set default output",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            _engine = new DualPlayEngine(first, second);
            _engine.Start();
            ActionButton.Content = "Disable Dual Play";
            StatusText.Text = "Dual Play is active.";
        }
        catch (Exception ex)
        {
            StopAudio();
            StatusText.Text = "Could not start audio: " + ex.Message;
        }
    }

    private void StopAudio()
    {
        try { _engine?.Dispose(); } catch { }
        _engine = null;
        ActionButton.Content = _isReverse ? "Enable Reverse Play" : "Enable Dual Play";
        StatusText.Text = "Audio stopped.";
    }

    protected override void OnClosed(EventArgs e)
    {
        StopAudio();
        _enumerator.Dispose();
        base.OnClosed(e);
    }
}
