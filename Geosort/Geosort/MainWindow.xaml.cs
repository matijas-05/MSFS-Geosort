using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

using CsvHelper;
using System.Diagnostics;

namespace Geosort
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private string m_AddonPath;
		private bool m_AddonPathValid;
		private List<Addon> m_Addons = new List<Addon>();

		private GridViewColumnHeader m_LastHeaderClicked;
		private ListSortDirection m_LastDirection = ListSortDirection.Ascending;
		private bool m_FirstSort = true;

		private Airport[] m_Airports;
		private NameCodePair[] m_Countries;
		private NameCodePair[] m_Continents;
		private AirportCorrection[] m_AirportCorrections;
		private SkipWord[] m_SkipWords;

		private const string AIRPORTS_PATH = "Database\\airports.csv";
		private const string COUNTRIES_PATH = "Database\\countries.csv";
		private const string CONTINENTS_PATH = "Database\\continents.csv";
		private const string AIRPORT_CORRECTION_PATH = "Database\\airport_correction.csv";
		private const string SKIP_WORDS_PATH = "Database\\skip_words.csv";
		private const string MANIFEST_JSON = "\\manifest.json";

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
		class Airport
		{
			public string Ident { get; set; }
			public string GPS_Code { get; set; }
			public string Name { get; set; }
			public string Continent { get; set; }
			public string Iso_Country { get; set; }
			public string Iso_Region { get; set; }
		}
		struct NameCodePair
		{
			public string Name { get; set; }
			public string Code { get; set; }
		}
		struct AirportCorrection
		{
			public string Addon_Icao { get; set; }
			public string Actual_Icao { get; set; }
		}
		struct SkipWord
		{
			public string Word { get; set; }
		}

		public MainWindow()
		{
			Log.Clear();
			InitializeComponent();
			ReadSave();

			m_Airports = ReadFile<Airport>(AIRPORTS_PATH);
			m_Countries = ReadFile<NameCodePair>(COUNTRIES_PATH);
			m_Continents = ReadFile<NameCodePair>(CONTINENTS_PATH);
			m_AirportCorrections = ReadFile<AirportCorrection>(AIRPORT_CORRECTION_PATH);
			m_SkipWords = ReadFile<SkipWord>(SKIP_WORDS_PATH);

			// TODO: Actually read from save file
			void ReadSave()
			{
				addonFolderPicker.ChangePath(@"E:\Gry\!Mody\Community");
			}
			T[] ReadFile<T>(string path)
			{
				try
				{
					using (var reader = new StreamReader(path))
					using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
					{
						csv.Configuration.PrepareHeaderForMatch = (string header, int index) => header.ToLower();
						return csv.GetRecords<T>().ToArray();
					}
				}
				catch (Exception e)
				{
					string fileName = Path.GetFileName(path);
					string fileNameNoExt = Path.GetFileNameWithoutExtension(path);

					if (e is FileNotFoundException)
					{
						MessageBox.Show($"Could not find {fileNameNoExt} database file! Make sure that there is a '{fileName}' file in the program's folder!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
						throw e;
					}
					else
					{
						MessageBox.Show($"Geosort encountered an error related to the {fileNameNoExt} database file. Try running the program as administrator.\n\n" + e, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
						throw e;
					}
				}
			}
		}

		// Load addons
		void loadBtn_Click(object sender, RoutedEventArgs e)
		{
			m_FirstSort = true;

			// Load addon list
			m_Addons.Clear();
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
			addonList.ItemsSource = m_Addons;
			addonLabel.Content = $"Addons ({m_Addons.Count}):";
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

						m_Addons.Add(new Addon(name, result, size, sizeBytes));
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
		void sortBtn_Click(object sender, RoutedEventArgs e)
		{
			bool continent = sortContinent.IsChecked.Value;
			bool country = sortContinent.IsChecked.Value;
			bool us_state = sortUS_State.IsChecked.Value;

			Stopwatch watch = new Stopwatch();
			watch.Start();

			foreach (Addon addon in m_Addons)
			{
				string result = "";

				if (continent)
				{
					// Get airport from addon name
					foreach (string sub in addon.Name.Split(new string[] { " ", "-", "_" }, StringSplitOptions.RemoveEmptyEntries))
					{
						if (SkipAddon(sub))
							break;

						if (sub.Length > 4)
							continue;

						foreach (AirportCorrection correction in m_AirportCorrections)
						{
							if (string.Equals(sub, correction.Addon_Icao, StringComparison.OrdinalIgnoreCase))
								result = Result(m_Airports.Where(arp => string.Equals(arp.Ident, correction.Actual_Icao, StringComparison.OrdinalIgnoreCase)).First(), sub);
						}

						foreach (Airport airport in m_Airports)
						{
							if (string.Equals(sub, airport.Ident, StringComparison.OrdinalIgnoreCase)
								|| string.Equals(sub, airport.GPS_Code, StringComparison.OrdinalIgnoreCase))
							{
								result = Result(airport, sub);
								break;
							}
						}

						if (result == "") result = $"Not found: {addon.Name}: {sub}";
						else break;
					}
					Log.WriteLine(result);
				}
			}

			watch.Stop();
			Log.WriteLine(watch.ElapsedMilliseconds);

			bool SkipAddon(string sub)
			{
				foreach (SkipWord skipWord in m_SkipWords)
				{
					if (string.Equals(skipWord.Word, sub, StringComparison.OrdinalIgnoreCase))
						return true;
				}
				return false;
			}
			string Result(Airport airport, string sub)
			{
				return $"{airport.Name} ({airport.Ident}{(airport.Ident != airport.GPS_Code ? "/" + airport.GPS_Code : "")}) [{sub}]: {airport.Continent}";
			}
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
		void searchFilter_TextChanged(object sender, TextChangedEventArgs e)
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
