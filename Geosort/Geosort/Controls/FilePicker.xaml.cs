using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace Geosort.Controls
{
	/// <summary>
	/// Interaction logic for FilePathDialog.xaml
	/// </summary>
	public partial class FilePicker : UserControl
	{
		public bool IsFolderPicker { get; set; }
		public string FilePath => pathBox.Text;
		public event Action<string> OnFilePicked;

		public FilePicker()
		{
			InitializeComponent();
		}

		void Button_Click(object sender, RoutedEventArgs e)
		{
			CommonOpenFileDialog dialog = new CommonOpenFileDialog() { IsFolderPicker = this.IsFolderPicker };
			if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
			{
				pathBox.Text = dialog.FileName;
				OnFilePicked?.Invoke(dialog.FileName);
			}
		}
		void pathBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			OnFilePicked?.Invoke(pathBox.Text);
		}

		public void ChangePath(string content)
		{
			// This changes textbox text -> calls event TextChanged -> calls event OnFilePicked -> updates addon list
			pathBox.Text = content;
		}
	}
}
