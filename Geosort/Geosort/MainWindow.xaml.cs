using System;
using System.IO;
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

namespace Geosort
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private string m_AddonPath;
		private string[] m_AddonFolders;

		private const string AIRPORT_FILE_PATH = "airports.txt";
		private string m_Airports;

		public MainWindow()
		{
			InitializeComponent();
			m_Airports = File.ReadAllText(AIRPORT_FILE_PATH);
		}

		void addonFolderPicker_OnFilePicked(string path)
		{
			m_AddonPath = path;
		}
		void SortBtn_Click(object sender, RoutedEventArgs e)
		{
			if (!Directory.Exists(m_AddonPath))
			{
				MessageBox.Show("Invalid folder path!", "", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}
			m_AddonFolders = Directory.GetDirectories(m_AddonPath);
		}
	}
}
