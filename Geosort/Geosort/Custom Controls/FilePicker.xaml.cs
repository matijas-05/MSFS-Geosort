using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace Geosort.Custom_Controls
{
	/// <summary>
	/// Interaction logic for FilePathDialog.xaml
	/// </summary>
	public partial class FilePicker : UserControl
	{
		public bool IsFolderPicker { get; set; }

		public FilePicker()
		{
			InitializeComponent();
		}

		void Button_Click(object sender, RoutedEventArgs e)
		{
			CommonOpenFileDialog dialog = new CommonOpenFileDialog() { IsFolderPicker = this.IsFolderPicker };
			if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
				MainWindow.AddonPath = pathBox.Text = dialog.FileName;
		}
	}
}
