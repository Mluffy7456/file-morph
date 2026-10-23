using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace FileMorph.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void SelectFile_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Выберите файл",
            CheckFileExists = true,
            Multiselect = false
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
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            var firstFile = files.FirstOrDefault(File.Exists);
            if (firstFile is not null)
                SetSelectedFile(firstFile);
        }
    }

    private void SetSelectedFile(string path)
    {
        SelectedFileText.Text = path;
        SelectedFileText.ToolTip = path;
    }
}
