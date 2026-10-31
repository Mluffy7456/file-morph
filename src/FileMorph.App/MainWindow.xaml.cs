using FileMorph.App.Conversion;
using FileMorph.Core.Models;
using FileMorph.Infrastructure.Conversion;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace FileMorph.App;

public partial class MainWindow : Window
{
    private readonly ConversionService _conversionService = new([new WpfImageFormatHandler()]);
    private readonly ConversionQueueService _queueService;
    private readonly ObservableCollection<QueueDisplayItem> _queueItems = [];
    private CancellationTokenSource? _queueRunCancellation;
    private string? _selectedFilePath;
    private bool _isConverting;
    private bool _isQueueRunning;

    public MainWindow()
    {
        InitializeComponent();
        _queueService = new ConversionQueueService(_conversionService);
        _queueService.ItemChanged += QueueService_OnItemChanged;
        QueueListBox.ItemsSource = _queueItems;
    }

    private void SelectFile_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите изображение",
            CheckFileExists = true,
            Multiselect = false,
            Filter = "Поддерживаемые изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|Все файлы|*.*"
        };

        if (dialog.ShowDialog(this) == true)
            SetSelectedFile(dialog.FileName);
    }

    private void DropZone_OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void DropZone_OnDrop(object sender, DragEventArgs e)
    {
        if (_isConverting || _isQueueRunning)
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            var firstFile = files.FirstOrDefault(File.Exists);
            if (firstFile is not null)
                SetSelectedFile(firstFile);
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
        var request = new ConversionRequest(sourcePath, destinationPath, ConversionOperation.Convert, outputFormat);
        var progress = new Progress<double>(value =>
        {
            ConversionStatusText.Text = $"Выполняется конвертация… {value:P0}";
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
                SelectedFileText.ToolTip = sourcePath;
            }
            else
            {
                ConversionStatusText.Text = $"Не удалось выполнить конвертацию: {result.ErrorMessage ?? result.ErrorCode ?? "неизвестная ошибка"}";
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

        var dialog = new OpenFileDialog
        {
            Title = "Добавить изображения в очередь",
            CheckFileExists = true,
            Multiselect = true,
            Filter = "Поддерживаемые изображения|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|Все файлы|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            SetSelectedFile(dialog.FileNames[0]);
            EnqueueFiles(dialog.FileNames, GetSelectedOutputFormat());
        }
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
                    sourcePath, destinationPath, ConversionOperation.Convert, outputFormat));
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
