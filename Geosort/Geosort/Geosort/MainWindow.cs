using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace Geosort
{
	public partial class MainWindow : Form
	{
		public MainWindow()
		{
			InitializeComponent();
		}
		void MainWindow_Load(object sender, EventArgs e)
		{

		}

		void BrowseBtn_Click(object sender, EventArgs e)
		{
			CommonOpenFileDialog dialog = new CommonOpenFileDialog() { IsFolderPicker = true };
			if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
				addonDirPath.Text = dialog.FileName;
		}
	}
}
