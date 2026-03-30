using System;
using System.Collections.ObjectModel;
using System.Windows;
using DiscogsSniper.Models;
using DiscogsSniper.Services;
using Microsoft.Toolkit.Uwp.Notifications;

namespace DiscogsSniper
{
    public partial class MainWindow : Window
    {
        private readonly DatabaseService _dbService;
        private readonly SniperEngine _engine;
        private ObservableCollection<Label> _labels;
        private ObservableCollection<Offer> _offers;

        public MainWindow()
        {
            InitializeComponent();
            _dbService = new DatabaseService();
            _engine = new SniperEngine();

            _labels = new ObservableCollection<Label>(_dbService.GetLabels());
            gridLabels.ItemsSource = _labels;

            _offers = new ObservableCollection<Offer>();
            gridOffers.ItemsSource = _offers;

            _engine.OnDealFound += Engine_OnDealFound;
            _engine.OnLogMessage += Engine_OnLogMessage;

            Log("System gotowy. Dodaj wytwórnię i kliknij START.");
        }

        private void BtnAddLabel_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtLabelId.Text, out int id) && !string.IsNullOrWhiteSpace(txtLabelName.Text))
            {
                var label = new Label { Id = id, Name = txtLabelName.Text, IsActive = true };
                _dbService.AddLabel(label);
                _labels.Add(label);

                txtLabelId.Clear();
                txtLabelName.Clear();
                Log($"Dodano nową wytwórnię: {label.Name} (ID: {label.Id})");
            }
            else
            {
                MessageBox.Show("Wprowadź poprawne ID (liczba) i Nazwę wytwórni.");
            }
        }

        private void BtnDeleteLabel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is Label labelToDelete)
            {
                _dbService.DeleteLabel(labelToDelete.Id);
                _labels.Remove(labelToDelete);
                Log($"Usunięto wytwórnię: {labelToDelete.Name} (ID: {labelToDelete.Id})");
            }
        }

        private void CheckBoxActive_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox cb && cb.DataContext is Label clickedLabel)
            {
                bool isChecked = cb.IsChecked ?? false;
                _dbService.UpdateLabelActiveState(clickedLabel.Id, isChecked);
                Log($"Zmieniono status skanowania dla: {clickedLabel.Name} na {(isChecked ? "ON" : "OFF")}");
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(txtMaxPrice.Text, out decimal maxPrice)) maxPrice = 0;
            if (!decimal.TryParse(txtMinProfit.Text, out decimal minProfit)) minProfit = 0;

            // NOWOŚĆ: Pobieramy maksymalną ilość dni
            if (!int.TryParse(txtMaxAge.Text, out int maxAgeDays)) maxAgeDays = 0;

            int minCondition = 0;
            if (cmbMinCondition.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
            {
                minCondition = Convert.ToInt32(selectedItem.Tag);
            }

            // Przekazujemy maxAgeDays do Silnika!
            _engine.StartScanning(minCondition, maxPrice, minProfit, maxAgeDays);

            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;
            cmbMinCondition.IsEnabled = false;
            txtMaxPrice.IsEnabled = false;
            txtMinProfit.IsEnabled = false;
            txtMaxAge.IsEnabled = false; // Blokujemy zmianę dni podczas działania
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            _engine.StopScanning();
            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;
            cmbMinCondition.IsEnabled = true;
            txtMaxPrice.IsEnabled = true;
            txtMinProfit.IsEnabled = true;
            txtMaxAge.IsEnabled = true; // Odblokowujemy zmianę dni
        }

private void Engine_OnDealFound(object? sender, Offer offer)
{
    Dispatcher.Invoke(() =>
    {
        _offers.Insert(0, offer);
        
        try 
        {
            // Nowoczesny sposób wywołania powiadomienia
            var toast = new ToastContentBuilder()
                .AddText("🔥 ZNALEZIONO OKAZJĘ!")
                .AddText($"{offer.Title}")
                .AddText($"Cena: {offer.TotalPrice:F2} zł");

            // Jeśli .Show() nadal nie działa, użyjemy tej metody:
            toast.Show(); 
        }
        catch (Exception ex)
        {
            // Logujemy błąd, żeby nie wywaliło programu, jeśli Windows ma wyłączone powiadomienia
            System.Diagnostics.Debug.WriteLine("Błąd Toast: " + ex.Message);
        }

        System.Media.SystemSounds.Exclamation.Play();
    });
}

        private void Engine_OnLogMessage(object? sender, string message)
        {
            Log(message);
        }

        private void Log(string message)
        {
            Dispatcher.Invoke(() =>
            {
                string time = DateTime.Now.ToString("HH:mm:ss");
                txtLogs.Text += $"[{time}] {message}\n";
                scrollLogs.ScrollToEnd();
            });
        }

        private void GridOffers_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (gridOffers.SelectedItem is Offer selectedOffer)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = selectedOffer.Url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Nie udało się otworzyć linku: {ex.Message}");
                }
            }
        }
        private void BtnClearMemory_Click(object sender, RoutedEventArgs e)
        {
            _dbService.ClearSeenOffers();
            _offers.Clear();
            Log("Pamięć bota została wyczyszczona. Następne skanowanie pobierze wszystko od nowa!");
        }
    }
}