using System;
using System.Linq;
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
		private bool m_AddonPathValid;

		private GridViewColumnHeader m_LastHeaderClicked;
		private ListSortDirection m_LastDirection = ListSortDirection.Ascending;
		private bool m_FirstSort = true;

		private const string AIRPORT_FILE_PATH = "airports.csv";
		private const string MANIFEST_JSON = "\\manifest.json";

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
				addonFolderPicker.ChangePath(@"E:\Gry\!Mody\Community");
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

		// Load addons
		void LoadBtn_Click(object sender, RoutedEventArgs e)
		{
			m_FirstSort = true;

			// Load addon list
			List<Addon> addons = new List<Addon>();
			List<string> addonDirs = Directory.GetDirectories(m_AddonPath).ToList();

			// Abort if dir is empty
			if (addonDirs.Count == 0)
			{
				MessageBox.Show($"Directory '{m_AddonPath}' is empty.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			for (int i = 0; i < addonDirs.Count; i++)
			{
				LoadAddon(i, addonDirs[i]);
			}

			// Add addons to UI
			addonList.ItemsSource = addons;
			addonLabel.Content = $"Addons ({addons.Count}):";
			SortDataView(nameof(Addon.Path), ListSortDirection.Ascending);

			long DirSize(DirectoryInfo d)
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
			string HumanFileSize(long size)
			{
				string[] sizes = { "B", "kB", "MB", "GB", "TB" };
				var i = size == 0 ? 0 : Math.Floor(Math.Log(size) / Math.Log(1024));
				return Math.Round(size / Math.Pow(1024, i), 2) + " " + sizes[(int)i];
			}

			void LoadAddon(int i, string path)
			{
				// Load all folders of path
				string result = "";
				List<string> dirsAndRoot = new List<string>();
				dirsAndRoot.Add(path);

				try { dirsAndRoot.AddRange(Directory.GetDirectories(path, "*", SearchOption.AllDirectories)); }
				catch (DirectoryNotFoundException ex)
				{
					MessageBox.Show("Could not open a directory. Try running the program as administrator and make sure to delete any invalid links in MFS Addon Linker.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					throw ex;
				}
				catch (Exception ex)
				{
					MessageBox.Show("Could not open a directory. Try running the program as administrator and make sure to delete any invalid links in MFS Addon Linker.\n\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					throw ex;
				}

				// TODO: display this at the end of loading
				// Folder has no subfolders
				if (dirsAndRoot.Count == 1)
				{
					MessageBox.Show($"Directory '{dirsAndRoot[dirsAndRoot.Count - 1]}' isn't a valid addon directory and is going to be skipped.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
					return;
				}

				// Add folders to addon list
				foreach (string dir in dirsAndRoot)
				{
					// Optimization code - don't search for manifest.json if it has already been found in one of parent folders.
					if (result != "")
					{
						if (IsSubfolder(result, dir))
							continue;
					}

					// If manifest.json exists in folder, add it to addon list
					if (File.Exists(dir + MANIFEST_JSON))
					{
						result = dir;
						DirectoryInfo resultDir = new DirectoryInfo(result);
						string name = resultDir.Name;
						long sizeBytes = DirSize(resultDir);
						string size = HumanFileSize(sizeBytes);

						addons.Add(new Addon(name, result, size, sizeBytes));
					}
				}

				// TODO: display this at the end of loading
				// Didn't find manifest.json in a folder
				if (result == "")
				{
					MessageBox.Show($"Directory '{dirsAndRoot[dirsAndRoot.Count - 1]}' isn't a valid addon directory and is going to be skipped.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
				}

				bool IsSubfolder(string parentPath, string childPath)
				{
					var parentUri = new Uri(parentPath);
					var childUri = new DirectoryInfo(childPath).Parent;
					while (childUri != null)
					{
						if (new Uri(childUri.FullName) == parentUri)
						{
							return true;
						}
						childUri = childUri.Parent;
					}
					return false;
				}
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
					if (headerClicked != m_LastHeaderClicked && m_FirstSort && headerClicked.Column.Header.ToString() == nameof(Addon.Path))
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

		// Search for addons
		void searchBtn_Click(object sender, RoutedEventArgs e)
		{
			ICollectionView dataView = CollectionViewSource.GetDefaultView(addonList.ItemsSource);

			if (dataView != null)
			{
				dataView.Filter = Filter;
				addonLabel.Content = $"Addons ({addonList.Items.Count}):"; 
			}

			bool Filter(object item)
			{
				if (string.IsNullOrEmpty(searchFilter.Text))
					return true;
				else
					return ((Addon)item).Name.IndexOf(searchFilter.Text, StringComparison.OrdinalIgnoreCase) >= 0 
						|| ((Addon)item).Path.IndexOf(searchFilter.Text, StringComparison.OrdinalIgnoreCase) >= 0;
			}
		}
		void searchFilter_TextChanged(object sender, TextChangedEventArgs e)
		{
			searchBtn_Click(sender, e);
		}
		void searchFilter_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == System.Windows.Input.Key.Enter)
				searchBtn_Click(sender, e);
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
