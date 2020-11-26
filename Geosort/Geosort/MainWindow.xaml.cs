using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

using System.Diagnostics;
using CsvHelper;
using BingMapsRESTToolkit;
using System.Threading.Tasks;

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
		private List<Addon> m_NotFound = new List<Addon>();
		private List<Addon> m_NotIdAsAddonInLNM = new List<Addon>();
		private List<Addon> m_BadLocation = new List<Addon>();

		private GridViewColumnHeader m_LastHeaderClicked;
		private ListSortDirection m_LastDirection = ListSortDirection.Ascending;
		private bool m_FirstSort = true;

		private Airport[] m_Airports;
		private CountryContinentPair[] m_CountryContinent;
		private NameCodePair[] m_US_States;
		private AirportCorrection[] m_AirportCorrections;
		private SingleWord[] m_SkipWords;

		private const string AIRPORTS_PATH = "Database\\airports_lnm.csv";
		private const string COUNTRIES_CONTINENTS_PATH = "Database\\countries_continents.csv";
		private const string US_STATES_PATH = "Database\\us_states.csv";
		private const string AIRPORT_CORRECTION_PATH = "Database\\airport_correction.csv";
		private const string SKIP_WORDS_PATH = "Database\\skip_words.csv";
		private const string LOCATION_CACHE_PATH = "Database\\location_cache.csv";
		private const string MANIFEST_JSON = "\\manifest.json";
		public const string BING_KEY = "0IdWzWxUd4A3QfCpiORd~DdYhDa6fAlG8ffUUyusDOw~AgZDz-ygdJ1z5h9M-PxBqv_HF-MWhNk9sbD5Jpxnlk-4haHwxXYE6huVTPROe6H3";

		class Addon
		{
			public string Name { get; set; }
			public string Path { get; set; }
			public string Size { get; set; }
			public long SizeBytes { get; set; }
			public Airport Airport { get; set; }

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
			public string Name { get; set; }
			public string Scenery_Local_Path { get; set; }
			public string Continent { get; set; }
			public string Country { get; set; }
			public string State { get; set; }
			public float Laty { get; set; }
			public float Lonx { get; set; }

			public Airport(string ident, string name, string scenery_Local_Path, string continent, string country, string state, string laty, string lonx)
			{
				Ident = ident;
				Name = name;
				Scenery_Local_Path = scenery_Local_Path;
				Continent = continent;
				Country = country;
				State = state;
				Laty = float.Parse(laty.Replace('.', ','));
				Lonx = float.Parse(lonx.Replace('.', ','));
			}
			public Airport(string ident, string name, string scenery_Local_Path, string continent, string country, string state)
			{
				Ident = ident;
				Name = name;
				Scenery_Local_Path = scenery_Local_Path;
				Continent = continent;
				Country = country;
				State = state;
			}
			public Airport(Airport airport)
			{
				Ident = airport.Ident;
				Name = airport.Name;
				Scenery_Local_Path = airport.Scenery_Local_Path;
				Continent = airport.Continent;
				Country = airport.Country;
				State = airport.State;
				Laty = airport.Laty;
				Lonx = airport.Lonx;
			}
		}
		struct NameCodePair
		{
			public string Name { get; set; }
			public string Code { get; set; }
		}
		struct AirportCorrection
		{
			public string Actual_Icao { get; set; }
			public string Addon_Name { get; set; }
			public string Country { get; set; }
		}
		struct SingleWord
		{
			public string Word { get; set; }
		}
		struct CountryContinentPair
		{
			public string Continent_Name { get; set; }
			public string Continent_Code { get; set; }
			public string Country_Name { get; set; }
			public string Two_Letter_Country_Code { get; set; }
			public string Three_Letter_Country_Code { get; set; }
			public string Country_Number { get; set; }
		}

		public MainWindow()
		{
			Log.Clear();
			InitializeComponent();
			ReadSave();
			LoadDatabases();

			// TODO: Actually read from save file
			void ReadSave()
			{
				addonFolderPicker.ChangePath(@"E:\Gry\!Mody\Community");
			}
		}

		// TODO: Load database only when starting and database changed (check last time of modifiaction?)
		void LoadDatabases()
		{
			m_Airports = ReadFile<Airport>(AIRPORTS_PATH);
			m_CountryContinent = ReadFile<CountryContinentPair>(COUNTRIES_CONTINENTS_PATH);
			m_AirportCorrections = ReadFile<AirportCorrection>(AIRPORT_CORRECTION_PATH);
			m_SkipWords = ReadFile<SingleWord>(SKIP_WORDS_PATH);

			T[] ReadFile<T>(string path)
			{
				try
				{
					using (var reader = new StreamReader(path))
					using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
					{
						csv.Configuration.PrepareHeaderForMatch = (string header, int index) => header.ToLower();
						csv.Configuration.HeaderValidated = (bool valid, string[] headers, int headerIndex, ReadingContext context) =>
						{
							if (valid || (!valid && headers.Length == 1 && string.Equals(headers[0], nameof(Airport.Continent), StringComparison.OrdinalIgnoreCase)))
								return;
							else if (!valid)
								throw new HeaderValidationException(context, headers, headerIndex);
						};
						csv.Configuration.MissingFieldFound = (string[] headers, int index, ReadingContext context) =>
						{
							if (headers == null)
								return;

							if (headers.Length == 1 && string.Equals(headers[0], nameof(Airport.Continent), StringComparison.OrdinalIgnoreCase))
								return;
							else
								throw new CsvHelper.MissingFieldException(context);
						};

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

		// Identifies addons based on the icao and lnm
		async void identBtn_Click(object sender, RoutedEventArgs e)
		{
			bool continent = sortContinent.IsChecked.Value;
			bool country = sortContinent.IsChecked.Value;
			bool us_state = sortUS_State.IsChecked.Value;

			LoadDatabases();

			InvokeAndCountElapsedTime(IdentifyAddons);
			await AssignLocation();
			LogBadAddons();

			void IdentifyAddons()
			{
				Log.WriteHeader("IDENTIFYING ADDONS");
				m_NotFound.Clear();
				m_NotIdAsAddonInLNM.Clear();

				// Identify addons
				foreach (Addon addon in m_Addons)
				{
					string result = "";
					string stoppedAt = "";

					foreach (AirportCorrection correction in m_AirportCorrections)
					{
						// Airport is in the database, but name of addon doesn't have icao, identified by name
						if (correction.Actual_Icao != "" && correction.Addon_Name != ""
							&& addon.Name == correction.Addon_Name)
						{
							foreach (var arp in m_Airports)
							{
								if (string.Equals(arp.Ident, correction.Actual_Icao, StringComparison.OrdinalIgnoreCase))
								{
									if (correction.Country != "") arp.Country = correction.Country;
									result = Result(addon, arp, "");
								}
							}

						}
					}

					// Identify airport based on lnm local_path var
					foreach (Airport airport in m_Airports)
					{
						if (airport.Scenery_Local_Path == "fs-base")
							continue;

						foreach (string sub in airport.Scenery_Local_Path.Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries))
						{
							if (sub != "fs-base" && string.Equals(sub, addon.Name, StringComparison.OrdinalIgnoreCase))
								result = Result(addon, airport, addon.Name);
						}
					}

					// If not found, identify airport from addon name
					if (result == "")
					{
						foreach (string sub in addon.Name.Split(new string[] { " ", ".", "-", "_", "(", ")" }, StringSplitOptions.RemoveEmptyEntries))
						{
							stoppedAt = sub;

							if (SkipAddon(sub))
							{
								result = $"SKIPPED {addon.Name}";
								break;
							}

							if ((sub.Length < 3 || sub.Length > 4) && !sub.StartsWith("lf", StringComparison.OrdinalIgnoreCase))
								continue;

							// If airport still not found, iterate through all in airports_lnm.csv
							if (result == "")
							{
								foreach (Airport airport in m_Airports)
								{
									if (string.Equals(sub, airport.Ident, StringComparison.OrdinalIgnoreCase))
									{
										result = Result(addon, airport, addon.Name);
										m_NotIdAsAddonInLNM.Add(addon);
										break;
									}
								}
							}
						}
					}

					// If still not found, mark it as not found
					if (result == "")
					{
						result = $"NOT FOUND: {addon.Name}. Stopped at: {stoppedAt}";
						m_NotFound.Add(addon);
					}

					Log.WriteLine(result);
				}

				MessageBox.Show("Completed addon identification.", "Sucess", MessageBoxButton.OK, MessageBoxImage.Information);
			}
			async Task AssignLocation()
			{
				// Read location cache and load it into airports
				List<Airport> locationCache = ReadLocationCache();
				foreach (var addon in m_Addons)
				{
					foreach (var arp in locationCache)
					{
						if (addon.Airport != null && addon.Airport.Ident == arp.Ident)
						{
							addon.Airport = new Airport(arp);
						}
					}
				}

				await AssignMissingLocation();
				//AssignContinents();

				async Task AssignMissingLocation()
				{
					// Assign country and usa state to airports without one
					int airportsWithoutLocation = m_Addons.Where(addon => addon.Airport != null && addon.Airport.Country == "").Count();
					List<Airport> newCache = locationCache;

					Log.WriteHeader($"ASSIGNING LOCATION TO AIRPORTS WITHOUT ONE ({airportsWithoutLocation})");
					foreach (Addon addon in m_Addons)
					{
						if (addon.Airport == null || addon.Airport.Country != "" || locationCache.Contains(addon.Airport))
							continue;

						var point = new Coordinate(addon.Airport.Laty, addon.Airport.Lonx);
						var request = new ReverseGeocodeRequest()
						{
							Point = point,
							Culture = "en-us",
							IncludeIso2 = true,
							BingMapsKey = BING_KEY
						};

						var response = await request.Execute();
						var address = ((Location)response.ResourceSets[0].Resources[0]).Address;

						bool responseValid = response != null
											&& response.ResourceSets != null
											&& response.ResourceSets.Length > 0
											&& response.ResourceSets[0].Resources != null
											&& response.ResourceSets[0].Resources.Length > 0
											&&
												(address.CountryRegion != null ||
												address.CountryRegionIso2 != null ||
												address.Locality != null);

						if (responseValid)
						{
							addon.Airport.Country = address.CountryRegionIso2 != null ? address.CountryRegionIso2 : address.Locality;

							if (addon.Airport.State == "" && addon.Airport.Country == "United States")
								addon.Airport.State = address.AdminDistrict;

							newCache.Add(addon.Airport);

							Log.WriteLine($"{addon.Airport.Name} ({addon.Airport.Ident}): {addon.Airport.Country}");
						}
						else
						{
							m_BadLocation.Add(addon);
						}
					}

					// Write airports without location to cache
					WriteLocationCache(newCache);
				}
				void AssignContinents()
				{
					// Assign continent
					foreach (Addon addon in m_Addons)
					{
						if (addon.Airport == null)
							continue;

						foreach (var cc in m_CountryContinent)
						{
							string countryName = cc.Country_Name.Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries)[0];

							if (string.Equals(addon.Airport.Country, countryName, StringComparison.OrdinalIgnoreCase)
								|| string.Equals(addon.Airport.Country, cc.Two_Letter_Country_Code, StringComparison.OrdinalIgnoreCase)
								|| string.Equals(addon.Airport.Country, cc.Three_Letter_Country_Code, StringComparison.OrdinalIgnoreCase))
							{
								addon.Airport.Continent = cc.Continent_Code;
							}
							else
							{
								Debug.WriteLine("no kurwa no japierdole: " + addon.Airport.Country);
							}
						}
					}

					MessageBox.Show("Completed location assignment.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
				}

				List<Airport> ReadLocationCache()
				{
					if (!File.Exists(LOCATION_CACHE_PATH))
						return new List<Airport>();

					using (var reader = new StreamReader(LOCATION_CACHE_PATH))
					using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
					{
						csv.Configuration.PrepareHeaderForMatch = (string header, int index) => header.ToLower();
						return csv.GetRecords<Airport>().ToList();
					}
				}
				void WriteLocationCache(List<Airport> records)
				{
					using (var writer = new StreamWriter(LOCATION_CACHE_PATH))
					using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
					{
						csv.WriteRecords(records);
					}
				}
			}
			void LogBadAddons()
			{
				Log.WriteHeader($"ADDONS NOT IDENTIFIED ({m_NotFound.Count})");
				if (m_NotFound.Count != 0)
				{
					MessageBox.Show($"{m_NotFound.Count} addons couldn't be identified.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);

					foreach (Addon addon in m_NotFound)
					{
						Log.WriteLine($"{addon.Name}\n{addon.Path}\n");
					}
				}

				Log.WriteHeader($"AIRPORTS NOT IDENTIFIED AS ADDONS IN LNM ({m_NotFound.Count})");
				if (m_NotIdAsAddonInLNM.Count != 0)
				{
					foreach (Addon addon in m_NotIdAsAddonInLNM)
					{
						Log.WriteLine(addon.Name);
					}
				}

				Log.WriteHeader($"AIRPORTS WITH INVALID LOCATION ({m_BadLocation.Count})");
				if (m_BadLocation.Count != 0)
				{
					foreach (Addon addon in m_BadLocation)
					{
						Log.WriteLine(addon.Name);
					}
				}
			}

			bool SkipAddon(string sub)
			{
				foreach (SingleWord skipWord in m_SkipWords)
				{
					if (string.Equals(sub, skipWord.Word, StringComparison.OrdinalIgnoreCase)
						|| sub.IndexOf(skipWord.Word, StringComparison.OrdinalIgnoreCase) >= 0)
						return true;
				}
				return false;
			}
			string Result(Addon addon, Airport airport, string name)
			{
				addon.Airport = airport;
				return $"{airport.Name} ({airport.Ident}) [{name}]: {airport.Country}, {airport.State}";
			}
			void InvokeAndCountElapsedTime(Action method)
			{
				Stopwatch watch = new Stopwatch();
				watch.Start();
				method.Invoke();
				watch.Stop();
				Log.WriteLine("TIME ELAPSED: " + watch.ElapsedMilliseconds);
			}
		}

		// TODO
		void sortBtn_Click(object sender, RoutedEventArgs e)
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
