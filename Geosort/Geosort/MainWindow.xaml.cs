using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

using CsvHelper;

namespace Geosort
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private string m_AddonPath;
		private string[] m_AddonFolders;
		private bool m_AddonPathValid;

		private GridViewColumnHeader m_LastHeaderClicked;
		private ListSortDirection m_LastDirection = ListSortDirection.Ascending;
		private bool m_FirstSort = true;

		private const string AIRPORT_FILE_PATH = "airports.csv";

		class Airport
		{
			public string Ident { get; set; }
			public string Name { get; set; }
			public string Iso_Country { get; set; }
			public string Iso_Region { get; set; }
		}
		class Addon
		{
			public string Name { get; set; }
			public string Path { get; set; }
			public string Size { get; set; }
			public long SizeBytes { get; set; }

			public Addon(string name, string path, string size, long sizeBytes)
			{
				Name = name;
				Path = path;
				Size = size;
				SizeBytes = sizeBytes;
			}
		}

		public MainWindow()
		{
			InitializeComponent();
			ReadSave();
			ReadAirportFile();

			// TODO: Actually read from save file
			void ReadSave()
			{
				addonFolderPicker.ChangePath("E:\\Gry\\Microsoft Flight Simulator\\Community");
			}
			void ReadAirportFile()
			{
				try
				{
					using (var reader = new StreamReader(AIRPORT_FILE_PATH))
					using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
					{
						csv.Configuration.PrepareHeaderForMatch = (string header, int index) => header.ToLower();
						var airports = csv.GetRecords<Airport>();
					}
				}
				catch (Exception e)
				{
					if (e is FileNotFoundException)
					{
						MessageBox.Show("Could not find airport database file! Make sure that there is a 'airports.txt' file in the program's folder!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
						throw e;
					}
					else
					{
						MessageBox.Show("Geosort encountered an error related to the airport database file. Try running the program as administrator.\n\n" + e, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
						throw e;
					}
				}
			}
		}

		void LoadBtn_Click(object sender, RoutedEventArgs e)
		{
			if (!m_AddonPathValid)
				return;

			// Load addon list
			m_AddonFolders = Directory.GetDirectories(m_AddonPath);
			List<Addon> addons = new List<Addon>();

			for (int i = 0; i < m_AddonFolders.Length; i++)
			{
				string name = Path.GetFileName(m_AddonFolders[i]);
				string path = Path.GetDirectoryName(m_AddonFolders[i]);
				long sizeBytes = DirSize(new DirectoryInfo(m_AddonFolders[i]));
				string size = HumanFileSize(sizeBytes);

				addons.Add(new Addon(name, path, size, sizeBytes));
			}

			addonList.ItemsSource = addons;
			addonLabel.Content = $"Addons ({addons.Count}):";
			SortDataView(nameof(Addon.Name), ListSortDirection.Ascending);

			long DirSize(DirectoryInfo d)
			{
				try
				{
					long size = 0;
					// Add file sizes.
					FileInfo[] fis = d.GetFiles();
					foreach (FileInfo fi in fis)
					{
						size += fi.Length;
					}
					// Add subdirectory sizes.
					DirectoryInfo[] dis = d.GetDirectories();
					foreach (DirectoryInfo di in dis)
					{
						size += DirSize(di);
					}
					return size;
				}
				catch (DirectoryNotFoundException ex)
				{
					MessageBox.Show($"Could not open a directory. Try running the program as administrator and make sure to delete any invalid links in MFS Addon Linker.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					throw ex;
				}
			}
			string HumanFileSize(long size)
			{
				string[] sizes = { "B", "kB", "MB", "GB", "TB" };
				var i = size == 0 ? 0 : Math.Floor(Math.Log(size) / Math.Log(1024));
				return Math.Round(size / Math.Pow(1024, i), 2)  + " " + sizes[(int)i];
			}
		}

		// TODO
		void SortBtn_Click(object sender, RoutedEventArgs e)
		{

		}
		void addonFolderPicker_OnFilePicked(string path)
		{
			m_AddonPath = addonFolderPicker.FilePath;
			m_AddonPathValid = Directory.Exists(m_AddonPath);
			loadBtn.IsEnabled = sortBtn.IsEnabled = m_AddonPathValid;
		}

		// Sort addons in listview
		void addonList_Click(object sender, RoutedEventArgs e)
		{
			GridViewColumnHeader headerClicked = (GridViewColumnHeader)e.OriginalSource;
			ListSortDirection direction;

			if (headerClicked != null)
			{
				if (headerClicked.Role != GridViewColumnHeaderRole.Padding)
				{
					if (headerClicked != m_LastHeaderClicked && m_FirstSort && headerClicked.Column.Header.ToString() == nameof(Addon.Name))
					{
						direction = ListSortDirection.Descending;
						m_FirstSort = false;
					}
					else if (headerClicked != m_LastHeaderClicked)
					{
						direction = ListSortDirection.Ascending;
					}
					else
					{
						if (m_LastDirection == ListSortDirection.Ascending)
						{
							direction = ListSortDirection.Descending;
						}
						else
						{
							direction = ListSortDirection.Ascending;
						}
					}

					string header = (string)headerClicked.Column.Header;
					SortDataView(header == nameof(Addon.Size) ? nameof(Addon.SizeBytes) : header, direction);

					m_LastHeaderClicked = headerClicked;
					m_LastDirection = direction;
				}
			}
		}
		void SortDataView(string sortBy, ListSortDirection dir)
		{
			ICollectionView dataView = CollectionViewSource.GetDefaultView(addonList.ItemsSource);

			dataView.SortDescriptions.Clear();
			SortDescription sd = new SortDescription(sortBy, dir);
			dataView.SortDescriptions.Add(sd);
			dataView.Refresh();
		}
	}
}
