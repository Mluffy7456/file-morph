using FileMorph.App.Conversion;
using FileMorph.Core.Models;
using FileMorph.Infrastructure.Conversion;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;

namespace FileMorph.App;

public partial class MainWindow : Window
{
    private readonly ConversionService _conversionService = new([new WpfImageFormatHandler()]);
    private readonly ConversionQueueService _queueService;
    private readonly ObservableCollection<QueueDisplayItem> _queueItems = [];
    private CancellationTokenSource? _queueRunCancellation;
    private string? _selectedFilePath;
    private bool _isConverting;

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
        if (_isConverting)
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
        if (_isConverting || string.IsNullOrWhiteSpace(_selectedFilePath))
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

    private sealed class QueueDisplayItem(ConversionQueueItem item)
    {
        public Guid Id { get; } = item.Id;
        public string DisplayText { get; } = Format(item);

        private static string Format(ConversionQueueItem item)
        {
            var state = item.Status switch
            {
                ConversionQueueStatus.Queued => "Ожидает",
                ConversionQueueStatus.Running => $"Выполняется {item.Progress:0}%",
                ConversionQueueStatus.Succeeded => "Готово",
                ConversionQueueStatus.Failed => $"Ошибка: {item.ErrorMessage}",
                ConversionQueueStatus.Cancelled => "Отменено",
                _ => item.Status.ToString()
            };
            return $"{Path.GetFileName(item.Request.SourcePath)} → {item.Request.OutputFormat.ToUpperInvariant()} · {state}";
        }
    }

    private void SetSelectedFile(string path)
    {
        _selectedFilePath = path;
        SelectedFileText.Text = path;
        SelectedFileText.ToolTip = path;
        ConversionStatusText.Text = "Готово к конвертации. Результат будет сохранён рядом с исходным файлом.";
    }
}