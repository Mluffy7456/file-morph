using FileMorph.App.Conversion;
using FileMorph.Core.Models;
using FileMorph.Infrastructure.Conversion;
using FileMorph.App.History;
using FileMorph.App.Presets;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Drawing;
using Forms = System.Windows.Forms;

namespace FileMorph.App;

public partial class MainWindow : Window
{
    private readonly ConversionService _conversionService = new([new WpfImageFormatHandler()]);
    private readonly ConversionQueueService _queueService;
    private readonly ConversionHistoryService _historyService = new();
    private readonly ConversionPresetService _presetService = new();
    private readonly HashSet<Guid> _recordedHistoryItems = [];
    private readonly ObservableCollection<QueueDisplayItem> _queueItems = [];
    private CancellationTokenSource? _queueRunCancellation;
    private string? _selectedFilePath;
    private bool _isConverting;
    private bool _isQueueRunning;

    public MainWindow()
    {
        InitializeComponent();
        _isLightTheme = App.LoadThemePreference();
        App.ApplyTheme(_isLightTheme);
        UpdateThemeToggleText();
        QualityValueText.Text = $"{QualitySlider.Value:0}%";
        _queueService = new ConversionQueueService(_conversionService);
        _queueService.ItemChanged += QueueService_OnItemChanged;
        QueueListBox.ItemsSource = _queueItems;
        HistoryListBox.ItemsSource = _historyService.GetEntries();
        RefreshPresets();
        ClearHistoryButton.IsEnabled = _historyService.GetEntries().Count > 0;
        _trayIcon = new Forms.NotifyIcon();
        _trayIcon.Icon = SystemIcons.Application;
        _trayIcon.Text = "FileMorph";
        _trayIcon.Visible = true;
        _trayIcon.ContextMenuStrip = new Forms.ContextMenuStrip();
        _trayIcon.ContextMenuStrip.Items.Add("Открыть", null, (_, _) => Dispatcher.Invoke(() => { Show(); WindowState = WindowState.Normal; Activate(); }));
        _trayIcon.ContextMenuStrip.Items.Add("Сменить тему", null, (_, _) => Dispatcher.Invoke(ToggleTheme));
        _trayIcon.ContextMenuStrip.Items.Add("Выход", null, (_, _) => Dispatcher.Invoke(() => { _allowClose = true; Close(); System.Windows.Application.Current.Shutdown(); }));
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(() => { Show(); WindowState = WindowState.Normal; Activate(); });
    }

    private bool _isLightTheme;

    private void ThemeToggle_OnClick(object sender, RoutedEventArgs e) => ToggleTheme();

    private void QualitySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (QualityValueText is not null)
            QualityValueText.Text = $"{e.NewValue:0}%";
    }

    private void ToggleTheme()
    {
        _isLightTheme = !_isLightTheme;
        App.ApplyTheme(_isLightTheme);
        App.SaveThemePreference(_isLightTheme);
        UpdateThemeToggleText();
    }

    private void UpdateThemeToggleText() =>
        ThemeToggleButton.Content = _isLightTheme ? "Тёмная тема" : "Светлая тема";

    private void CompactMode_OnClick(object sender, RoutedEventArgs e) { FullLayout.Visibility = Visibility.Collapsed; CompactLayout.Visibility = Visibility.Visible; Width = 440; Height = 235; MinWidth = 390; MinHeight = 210; MaxWidth = 560; MaxHeight = 320; WindowState = WindowState.Normal; }
    private void FullMode_OnClick(object sender, RoutedEventArgs e) { CompactLayout.Visibility = Visibility.Collapsed; FullLayout.Visibility = Visibility.Visible; MinWidth = 560; MinHeight = 520; MaxWidth = double.PositiveInfinity; MaxHeight = double.PositiveInfinity; Width = 1040; Height = 780; }
    private void MiniSelectFile_OnClick(object sender, RoutedEventArgs e) => SelectFile_OnClick(sender, e);
    private void MiniConvert_OnClick(object sender, RoutedEventArgs e) { OutputFormatComboBox.SelectedIndex = MiniOutputFormatComboBox.SelectedIndex; Convert_OnClick(sender, e); }
    private void Window_StateChanged(object? sender, EventArgs e) { if (WindowState == WindowState.Minimized) Hide(); }
    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e) { if (!_allowClose) { e.Cancel = true; Hide(); } else { _trayIcon.Visible = false; _trayIcon.Dispose(); } }
    private bool _allowClose;
    private readonly Forms.NotifyIcon _trayIcon;
    private void SelectFile_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Выберите изображение",
            CheckFileExists = true,
            Multiselect = false,
            Filter = "Поддерживаемые изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp|Все файлы|*.*"
        };

        if (dialog.ShowDialog(this) == true)
            SetSelectedFile(dialog.FileName);
    }

    private void DropZone_OnDragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void DropZone_OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (_isConverting || _isQueueRunning)
            return;

        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            var validFiles = files
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (validFiles.Length == 0)
                return;

            SetSelectedFile(validFiles[0]);
            if (validFiles.Length > 1)
                EnqueueFiles(validFiles, GetSelectedOutputFormat());
        }
    }

    private async void Convert_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isConverting || _isQueueRunning || string.IsNullOrWhiteSpace(_selectedFilePath))
            return;

        if (OutputFormatComboBox.SelectedItem is not ComboBoxItem { Tag: string outputFormat })
            return;

        var sourcePath = _selectedFilePath;
        var destinationPath = GetAvailableDestinationPath(sourcePath, outputFormat);
        var request = new ConversionRequest(sourcePath, destinationPath, ConversionOperation.Convert, outputFormat, (int)QualitySlider.Value,
            GetTargetDimension(TargetWidthTextBox), GetTargetDimension(TargetHeightTextBox));
        var progress = new Progress<double>(value =>
        {
            ConversionStatusText.Text = $"Выполняется конвертация… {value / 100d:P0}";
        });

        _isConverting = true;
        SetControlsEnabled(false);
        ConversionStatusText.Text = "Подготовка изображения…";

        try
        {
            var result = await _conversionService.ConvertAsync(request, progress);
            if (result.Succeeded && result.OutputPath is not null)
            {
                ConversionStatusText.Text = $"Готово: {result.OutputPath}";
                _historyService.Add(new(DateTimeOffset.Now, sourcePath, result.OutputPath, true, null));
                RefreshHistory();
                SelectedFileText.ToolTip = sourcePath;
            }
            else
            {
                ConversionStatusText.Text = $"Не удалось выполнить конвертацию: {result.ErrorMessage ?? result.ErrorCode ?? "неизвестная ошибка"}";
                _historyService.Add(new(DateTimeOffset.Now, sourcePath, destinationPath, false, result.ErrorMessage ?? result.ErrorCode));
                RefreshHistory();
            }
        }
        catch (Exception exception)
        {
            ConversionStatusText.Text = $"Ошибка: {exception.Message}";
        }
        finally
        {
            _isConverting = false;
            SetControlsEnabled(true);
        }
    }

    private void AddToQueue_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isConverting || _isQueueRunning || string.IsNullOrWhiteSpace(_selectedFilePath))
            return;

        EnqueueFiles([_selectedFilePath], GetSelectedOutputFormat());
    }

    private void AddFilesToQueue_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isConverting || _isQueueRunning)
            return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Добавить изображения в очередь",
            CheckFileExists = true,
            Multiselect = true,
            Filter = "Поддерживаемые изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp|Все файлы|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            SetSelectedFile(dialog.FileNames[0]);
            EnqueueFiles(dialog.FileNames, GetSelectedOutputFormat());
        }
    }

    private static int? GetTargetDimension(TextBox textBox)
    {
        if (string.IsNullOrWhiteSpace(textBox.Text))
            return null;
        if (!int.TryParse(textBox.Text.Trim(), out var value) || value is < 1 or > 20000)
            throw new ArgumentException("Размер изображения должен быть целым числом от 1 до 20000 пикселей.");
        return value;
    }

    private string GetSelectedOutputFormat()
    {
        return OutputFormatComboBox.SelectedItem is ComboBoxItem { Tag: string format }
            ? format
            : throw new InvalidOperationException("Выберите формат результата.");
    }

    private void EnqueueFiles(IEnumerable<string> sourcePaths, string outputFormat)
    {
        try
        {
            var reservedPaths = _queueService.GetItems()
                .Where(item => item.Status is ConversionQueueStatus.Queued or ConversionQueueStatus.Running)
                .Select(item => item.Request.DestinationPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var addedCount = 0;

            foreach (var sourcePath in sourcePaths
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var destinationPath = GetAvailableDestinationPath(sourcePath, outputFormat, reservedPaths);
                _queueService.Enqueue(new ConversionRequest(
                    sourcePath, destinationPath, ConversionOperation.Convert, outputFormat, (int)QualitySlider.Value,
                    GetTargetDimension(TargetWidthTextBox), GetTargetDimension(TargetHeightTextBox)));
                reservedPaths.Add(destinationPath);
                addedCount++;
            }

            RunQueueButton.IsEnabled = _queueService.GetItems()
                .Any(item => item.Status == ConversionQueueStatus.Queued);
            ConversionStatusText.Text = addedCount == 1
                ? "Файл добавлен в очередь."
                : $"Добавлено файлов в очередь: {addedCount}.";
        }
        catch (Exception exception)
        {
            ConversionStatusText.Text = $"Не удалось добавить файлы: {exception.Message}";
        }
    }

    private async void RunQueue_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isQueueRunning || _isConverting ||
            !_queueService.GetItems().Any(item => item.Status == ConversionQueueStatus.Queued))
            return;

        _isQueueRunning = true;
        _queueRunCancellation = new CancellationTokenSource();
        RunQueueButton.IsEnabled = false;
        AddToQueueButton.IsEnabled = false;
        AddFilesToQueueButton.IsEnabled = false;
        CancelQueueButton.IsEnabled = true;
        SetControlsEnabled(false);
        ConversionStatusText.Text = "Выполняется очередь конвертации…";

        try
        {
            await _queueService.RunAsync(_queueRunCancellation.Token);
            ConversionStatusText.Text = "Обработка очереди завершена.";
        }
        catch (OperationCanceledException)
        {
            ConversionStatusText.Text = "Очередь остановлена. Необработанные задания сохранены в списке.";
        }
        catch (Exception exception)
        {
            ConversionStatusText.Text = $"Ошибка очереди: {exception.Message}";
        }
        finally
        {
            _queueRunCancellation.Dispose();
            _queueRunCancellation = null;
            _isQueueRunning = false;
            CancelQueueButton.IsEnabled = false;
            AddToQueueButton.IsEnabled = true;
            AddFilesToQueueButton.IsEnabled = true;
            SetControlsEnabled(true);
            RunQueueButton.IsEnabled = _queueService.GetItems()
                .Any(item => item.Status == ConversionQueueStatus.Queued);
        }
    }

    private void QueueListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateRemoveQueueItemButton();
        UpdateQueueSelectionDetails();
    }

    private void UpdateQueueSelectionDetails()
    {
        if (QueueListBox.SelectedItem is not QueueDisplayItem selected)
        {
            QueueErrorDetailsText.Text = "Выберите задание, чтобы увидеть подробности.";
            return;
        }

        var item = _queueService.GetItems().FirstOrDefault(entry => entry.Id == selected.Id);
        QueueErrorDetailsText.Text = item is null ? "Задание удалено." : item.Status switch
        {
            ConversionQueueStatus.Failed => $"Код: {item.ErrorCode ?? "не указан"} · {item.ErrorMessage ?? "Подробности отсутствуют."}",
            ConversionQueueStatus.Cancelled => "Операция была отменена.",
            ConversionQueueStatus.Succeeded => $"Результат: {item.OutputPath ?? "путь не указан"}",
            _ => $"Источник: {item.Request.SourcePath}\nРезультат: {item.Request.DestinationPath}"
        };
    }

    private void RetryQueueItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isQueueRunning || QueueListBox.SelectedItem is not QueueDisplayItem selected)
            return;
        if (_queueService.Retry(selected.Id))
            ConversionStatusText.Text = "Задание добавлено в очередь повторно.";
    }

    private void ClearCompleted_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isQueueRunning)
            return;
        var count = _queueService.ClearCompleted();
        ConversionStatusText.Text = $"Удалено завершённых заданий: {count}.";
    }

    private void UpdateRemoveQueueItemButton()
    {
        var selected = QueueListBox.SelectedItem as QueueDisplayItem;
        var item = selected is null ? null : _queueService.GetItems().FirstOrDefault(entry => entry.Id == selected.Id);
        RemoveQueueItemButton.IsEnabled = !_isQueueRunning && item?.Status == ConversionQueueStatus.Queued;
        RetryQueueItemButton.IsEnabled = !_isQueueRunning && item?.Status is ConversionQueueStatus.Failed or ConversionQueueStatus.Cancelled;
        ClearCompletedButton.IsEnabled = !_isQueueRunning && _queueService.GetItems().Any(entry => entry.IsTerminal);
    }

    private void RemoveQueueItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (_isQueueRunning || QueueListBox.SelectedItem is not QueueDisplayItem selected)
            return;

        if (_queueService.Remove(selected.Id))
        {
            var item = _queueItems.FirstOrDefault(entry => entry.Id == selected.Id);
            if (item is not null)
                _queueItems.Remove(item);

            ConversionStatusText.Text = "Задание удалено из очереди.";
        }

        RemoveQueueItemButton.IsEnabled = false;
    }

    private void CancelQueue_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_isQueueRunning)
            return;

        _queueService.CancelCurrent();
        _queueRunCancellation?.Cancel();
        CancelQueueButton.IsEnabled = false;
        ConversionStatusText.Text = "Останавливаем текущую операцию…";
    }

    private void QueueService_OnItemChanged(ConversionQueueItem item)
    {
        Dispatcher.Invoke(() =>
        {
            if (item.IsTerminal && _recordedHistoryItems.Add(item.Id))
            {
                _historyService.Add(new(DateTimeOffset.Now, item.Request.SourcePath, item.OutputPath ?? item.Request.DestinationPath,
                    item.Status == ConversionQueueStatus.Succeeded, item.ErrorMessage ?? item.ErrorCode));
                RefreshHistory();
            }
            var existing = _queueItems.FirstOrDefault(entry => entry.Id == item.Id);
            var stillInQueue = _queueService.GetItems().Any(entry => entry.Id == item.Id);

            // Remove() emits a Cancelled notification after the item has left the service.
            if (!stillInQueue)
            {
                if (existing is not null)
                    _queueItems.Remove(existing);
            }
            else
            {
                var updated = new QueueDisplayItem(item);
                if (existing is null)
                    _queueItems.Add(updated);
                else
                {
                    var index = _queueItems.IndexOf(existing);
                    _queueItems[index] = updated;
                }
            }

            if (!_isQueueRunning)
                RunQueueButton.IsEnabled = _queueService.GetItems()
                    .Any(entry => entry.Status == ConversionQueueStatus.Queued);

            UpdateQueueProgress();
            UpdateRemoveQueueItemButton();
            UpdateQueueSelectionDetails();
        });
    }

    private void RefreshHistory()
    {
        HistoryListBox.ItemsSource = _historyService.GetEntries();
        ClearHistoryButton.IsEnabled = _historyService.GetEntries().Count > 0;
    }


    private void SavePreset_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(PresetNameTextBox.Text))
                throw new ArgumentException("Введите название пресета.");
            _presetService.Save(PresetNameTextBox.Text, GetSelectedOutputFormat(), (int)QualitySlider.Value);
            RefreshPresets();
            PresetComboBox.SelectedItem = _presetService.GetAll()
                .FirstOrDefault(p => string.Equals(p.Name, PresetNameTextBox.Text.Trim(), StringComparison.OrdinalIgnoreCase));
            ConversionStatusText.Text = "Пресет сохранён на этом компьютере.";
        }
        catch (Exception exception) when (exception is ArgumentException)
        {
            ConversionStatusText.Text = $"Не удалось сохранить пресет: {exception.Message}";
        }
    }

    private void ApplyPreset_OnClick(object sender, RoutedEventArgs e)
    {
        if (PresetComboBox.SelectedItem is not ConversionPreset preset)
        {
            ConversionStatusText.Text = "Сначала выберите пресет.";
            return;
        }

        var formatItem = OutputFormatComboBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), preset.OutputFormat, StringComparison.OrdinalIgnoreCase));
        if (formatItem is null)
        {
            ConversionStatusText.Text = "Формат пресета больше не поддерживается.";
            return;
        }

        OutputFormatComboBox.SelectedItem = formatItem;
        MiniOutputFormatComboBox.SelectedIndex = OutputFormatComboBox.SelectedIndex;
        QualitySlider.Value = preset.Quality;
        ConversionStatusText.Text = $"Применён пресет «{preset.Name}»: {preset.OutputFormat.ToUpperInvariant()}, {preset.Quality}%.";
    }

    private void DeletePreset_OnClick(object sender, RoutedEventArgs e)
    {
        if (PresetComboBox.SelectedItem is not ConversionPreset preset)
        {
            ConversionStatusText.Text = "Выберите пресет для удаления.";
            return;
        }

        if (_presetService.Delete(preset.Name))
        {
            RefreshPresets();
            ConversionStatusText.Text = $"Пресет «{preset.Name}» удалён.";
        }
    }

    private void RefreshPresets()
    {
        PresetComboBox.ItemsSource = _presetService.GetAll();
        PresetComboBox.SelectedItem = null;
    }

    private void ClearHistory_OnClick(object sender, RoutedEventArgs e)
    {
        _historyService.Clear();
        RefreshHistory();
    }

    private void UpdateQueueProgress()
    {
        var items = _queueService.GetItems();
        var total = items.Count;
        var completed = items.Count(item => item.IsTerminal);
        var percent = total == 0 ? 0 : items.Average(item => item.Status == ConversionQueueStatus.Succeeded ? 100 : item.Progress);
        QueueProgressBar.Value = percent;
        QueueProgressText.Text = $"{completed} из {total} · {percent:0}%";
        ClearCompletedButton.IsEnabled = !_isQueueRunning && completed > 0;
    }

    private static string GetAvailableDestinationPath(
        string sourcePath,
        string outputFormat,
        IEnumerable<string>? reservedPaths = null)
    {
        var directory = Path.GetDirectoryName(sourcePath)
            ?? throw new InvalidOperationException("Не удалось определить папку исходного файла.");
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var candidate = Path.Combine(directory, $"{baseName}-converted.{outputFormat}");
        var reserved = reservedPaths?.ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var suffix = 2;

        while (File.Exists(candidate) || reserved.Contains(candidate))
        {
            candidate = Path.Combine(directory, $"{baseName}-converted-{suffix}.{outputFormat}");
            suffix++;
        }

        return candidate;
    }

    private void SetControlsEnabled(bool enabled)
    {
        OutputFormatComboBox.IsEnabled = enabled;
        SelectFileButton.IsEnabled = enabled;
        ConvertButton.IsEnabled = enabled;
        DropZoneBorder.AllowDrop = enabled;
    }

    private void SetSelectedFile(string path)
    {
        _selectedFilePath = path;
        SelectedFileText.Text = path;
        MiniSelectedFileText.Text = Path.GetFileName(path);
        SelectedFileText.ToolTip = path;
        ConversionStatusText.Text = "Готово к конвертации. Результат будет сохранён рядом с исходным файлом.";
    }

    private sealed class QueueDisplayItem(ConversionQueueItem item)
    {
        public Guid Id { get; } = item.Id;
        public string DisplayText { get; } = Format(item);
        public double Progress { get; } = item.Status == ConversionQueueStatus.Succeeded ? 100 : item.Progress;
        public string DetailText { get; } = item.Status switch
        {
            ConversionQueueStatus.Running => $"Выполняется · {item.Progress:0}%",
            ConversionQueueStatus.Failed => $"Ошибка · {item.ErrorCode ?? "unknown"}",
            ConversionQueueStatus.Cancelled => "Отменено",
            ConversionQueueStatus.Succeeded => "Успешно завершено",
            _ => "Ожидает запуска"
        };

        private static string Format(ConversionQueueItem item)
        {
            var state = item.Status switch
            {
                ConversionQueueStatus.Queued => "Ожидает",
                ConversionQueueStatus.Running => $"Выполняется {item.Progress:0}%",
                ConversionQueueStatus.Succeeded => "Готово",
                ConversionQueueStatus.Failed => "Ошибка",
                ConversionQueueStatus.Cancelled => "Отменено",
                _ => item.Status.ToString()
            };
            return $"{Path.GetFileName(item.Request.SourcePath)} → {item.Request.OutputFormat.ToUpperInvariant()} · {state}";
        }
    }
}
