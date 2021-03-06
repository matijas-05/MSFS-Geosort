﻿using System;
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

using SharpCompress.Common;
using SharpCompress.Archives;
using SharpCompress.Writers;
using SharpCompress.Archives.Zip;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;

using JDStuart.DirectoryUtils;
using Directory = System.IO.Directory;

namespace Geosort
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private string m_AddonPath;
		private bool m_AddonPathValid;
		private string m_OutputPath;
		private bool m_OutputPathValid;
		private string m_UpdatePath;
		private bool m_UpdatePathValid;

		private List<Addon> m_AddonsLoaded = new List<Addon>();
		private List<Addon> m_AddonsAirports = new List<Addon>();
		private List<Addon> m_NotFound = new List<Addon>();
		private List<Addon> m_NotIdAsAddonInLNM = new List<Addon>();
		private List<Addon> m_BadLocation = new List<Addon>();
		private List<Addon> m_Zips = new List<Addon>();
		private string[] m_Archives;
		private List<string> m_AddonsToUpdate = new List<string>();

		private GridViewColumnHeader m_LastHeaderClicked;
		private ListSortDirection m_LastDirection = ListSortDirection.Ascending;
		private bool m_FirstSort = true;

		private Airport[] m_Airports;
		private CountryContinentPair[] m_CountryContinent;
		private NameCodePair[] m_Continents;
		private NameCodePair[] m_US_States;
		private NameCodePair[] m_CanadaProvinces;
		private AirportCorrection[] m_AirportCorrections;
		private SkipWord[] m_SkipWords;

		private const string ADDONS_PATH = @"E:\Gry\!Mody\test";
		private const string OUTPUT_PATH = @"E:\Gry\!Mody\test";
		private const string UPDATE_PATH = @"D:\Pobrane\Gry\Flight Sim\MFS\addons";
		private const string TEMP_PATH = @"D:\Pobrane\Gry\Flight Sim\MFS\tmp";
		private const string AIRPORTS_PATH = "Database\\airports_lnm.csv";
		private const string COUNTRIES_CONTINENTS_PATH = "Database\\countries_continents.csv";
		private const string CONTINENTS_PATH = "Database\\continents.csv";
		private const string US_STATES_PATH = "Database\\us_states.csv";
		private const string CANADA_PROVINCES_PATH = "Database\\canada_provinces.csv";
		private const string AIRPORT_CORRECTION_PATH = "Database\\airport_correction.csv";
		private const string SKIP_WORDS_PATH = "Database\\skip_words.csv";
		private const string LOCATION_CACHE_PATH = "Database\\location_cache.csv";
		private const string MANIFEST_JSON = "\\manifest.json";
		private const string BAT_FILE = "Database\\load_lnm_airports.bat";
		private const string BING_KEY = "0IdWzWxUd4A3QfCpiORd~DdYhDa6fAlG8ffUUyusDOw~AgZDz-ygdJ1z5h9M-PxBqv_HF-MWhNk9sbD5Jpxnlk-4haHwxXYE6huVTPROe6H3";

		class Addon
		{
			public string Name { get; set; }
			public string Path { get; set; }
			public string RelativePath => Path.Remove(0, Window.m_AddonPath.Length);
			public string Size { get; set; }
			public long SizeBytes { get; set; }
			public Airport Airport { get; set; }
			public MainWindow Window { get; }

			public Addon(string name, string path, string size, long sizeBytes, MainWindow window)
			{
				Name = name;
				Path = path;
				Size = size;
				SizeBytes = sizeBytes;
				Window = window;
			}
			public Addon(Addon addon)
			{
				Name = addon.Name;
				Path = addon.Path;
				Size = addon.Size;
				SizeBytes = addon.SizeBytes;
				Airport = addon.Airport;
				Window = addon.Window;
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
		struct SkipWord
		{
			public string Word { get; set; }
			public bool Compare_Full_Name { get; set; }
		}
		struct CountryContinentPair
		{
			public string Continent_Name { get; set; }
			public string Continent_Code { get; set; }
			public string Country_Name { get; set; }
			public string Two_Letter_Country_Code { get; set; }
			public string Three_Letter_Country_Code { get; set; }
			public string Country_Number { get; set; }

			public string GetSimpleCountryName() => Country_Name.Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries)[0];
		}

		public MainWindow()
		{
			Log.Clear();
			InitializeComponent();
			ReadSave();
			//UpdateLNM_Airports();
			LoadDatabases();

			// TODO: Actually read from save file
			void ReadSave()
			{
				addonFolderPicker.ChangePath(ADDONS_PATH);
				outputFolderPicker.ChangePath(OUTPUT_PATH);
				updateFolderPicker.ChangePath(UPDATE_PATH);
			}
			void UpdateLNM_Airports()
			{
				using (var reader = new StreamReader(BAT_FILE))
				{
					string command = reader.ReadToEnd();
					Process cmd = new Process();
					cmd.StartInfo.FileName = "cmd.exe";
					cmd.StartInfo.RedirectStandardInput = true;
					cmd.StartInfo.RedirectStandardOutput = true;
					cmd.StartInfo.CreateNoWindow = false;
					cmd.StartInfo.UseShellExecute = false;
					cmd.Start();

					cmd.StandardInput.WriteLine(command);
					cmd.StandardInput.Flush();
					cmd.StandardInput.Close();
					cmd.WaitForExit();
					Debug.WriteLine(cmd.StandardOutput.ReadToEnd());
				}
			}
		}
		// TODO: Load database only when starting and database changed (check last time of modifiaction?)
		void LoadDatabases()
		{
			m_Airports = ReadFile<Airport>(AIRPORTS_PATH);
			m_CountryContinent = ReadFile<CountryContinentPair>(COUNTRIES_CONTINENTS_PATH);
			m_Continents = ReadFile<NameCodePair>(CONTINENTS_PATH);
			m_US_States = ReadFile<NameCodePair>(US_STATES_PATH);
			m_CanadaProvinces = ReadFile<NameCodePair>(CANADA_PROVINCES_PATH);
			m_AirportCorrections = ReadFile<AirportCorrection>(AIRPORT_CORRECTION_PATH);
			m_SkipWords = ReadFile<SkipWord>(SKIP_WORDS_PATH);

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
			m_AddonsLoaded.Clear();
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
			addonList.ItemsSource = m_AddonsLoaded;
			addonLabel.Content = $"Addons ({m_AddonsLoaded.Count}):";
			SortDataView(nameof(Addon.Path), ListSortDirection.Ascending);

			updateBtn.IsEnabled = m_AddonsLoaded.Count > 0;
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

					m_AddonsLoaded.Add(new Addon(name, result, size, sizeBytes, this));
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

		// Identifies addons based on the icao and lnm
		async void identBtn_Click(object sender, RoutedEventArgs e)
		{
			bool continent = sortContinent.IsChecked.Value;
			bool country = sortContinent.IsChecked.Value;
			bool us_state = sortUS_CA.IsChecked.Value;

			LoadDatabases();

			IdentifyAddons();
			await AssignLocation();
			LogBadAddons();

			void IdentifyAddons()
			{
				Log.WriteHeader("IDENTIFYING ADDONS");
				m_NotFound.Clear();
				m_NotIdAsAddonInLNM.Clear();

				// Identify addons
				foreach (Addon addon in m_AddonsLoaded)
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
									m_NotIdAsAddonInLNM.Add(addon);
								}
							}
						}
					}

					// Identify airport based on lnm local_path var
					if (result == "")
					{
						foreach (Airport airport in m_Airports)
						{
							if (airport.Scenery_Local_Path == "fs-base")
								continue;

							foreach (string sub in airport.Scenery_Local_Path.Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries))
							{
								if (sub != "fs-base" && string.Equals(sub, addon.Name, StringComparison.OrdinalIgnoreCase))
								{
									result = Result(addon, airport, addon.Name);
								}
							}
						}
					}

					// If not found, identify airport from addon name
					if (result == "")
					{
						foreach (string sub in addon.Name.Split(new string[] { " ", ".", "-", "_", "(", ")" }, StringSplitOptions.RemoveEmptyEntries))
						{
							stoppedAt = sub;

							if (SkipAddon(sub, addon.Name))
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
				MessageBox.Show("Completed addon identification.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

				bool SkipAddon(string sub, string addonName)
				{
					foreach (SkipWord word in m_SkipWords)
					{
						if (!word.Compare_Full_Name)
						{
							if (string.Equals(sub, word.Word, StringComparison.OrdinalIgnoreCase) || sub.IndexOf(word.Word, StringComparison.OrdinalIgnoreCase) >= 0)
								return true;
						}
						else
						{
							if (string.Equals(addonName, word.Word, StringComparison.OrdinalIgnoreCase))
								return true;
						}
					}
					return false;
				}
				string Result(Addon addon, Airport airport, string name)
				{
					Addon newAddon = new Addon(addon);
					newAddon.Airport = airport;
					m_AddonsAirports.Add(newAddon);

					return $"{airport.Name} ({airport.Ident}) [{name}]: {airport.Country}, {airport.State}";
				}
			}
			async Task AssignLocation()
			{
				// Read location cache and load it into airports
				List<Airport> locationCache = ReadLocationCache();
				foreach (var addon in m_AddonsAirports)
				{
					foreach (var arp in locationCache)
					{
						if (addon.Airport != null && addon.Airport.Ident == arp.Ident)
						{
							addon.Airport = new Airport(arp);
						}
					}
				}
				List<Airport> newCache = new List<Airport>(locationCache);

				// Assign country and usa state to airports without one
				await AssignMissingLocation();
				AssignContinents();

				async Task AssignMissingLocation()
				{
					int airportsWithoutLocation = m_AddonsAirports.Where(addon => addon.Airport != null && addon.Airport.Country == "").Count();

					Log.WriteHeader($"ASSIGNING LOCATION");
					foreach (Addon addon in m_AddonsAirports)
					{
						if (addon.Airport == null || locationCache.Any(arp => arp.Ident == addon.Airport.Ident && arp.Scenery_Local_Path == addon.Airport.Scenery_Local_Path))
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

						bool responseValid = response != null
											&& response.ResourceSets != null
											&& response.ResourceSets.Length > 0
											&& response.ResourceSets[0].Resources != null
											&& response.ResourceSets[0].Resources.Length > 0
											&&
												(((Location)response.ResourceSets[0].Resources[0]).Address.CountryRegion != null ||
												((Location)response.ResourceSets[0].Resources[0]).Address.CountryRegionIso2 != null ||
												((Location)response.ResourceSets[0].Resources[0]).Address.Locality != null);

						if (responseValid)
						{
							var address = ((Location)response.ResourceSets[0].Resources[0]).Address;
							addon.Airport.Country = address.CountryRegionIso2 ?? address.CountryRegion ?? address.Locality;

							if (addon.Airport.Country == "US" || addon.Airport.Country == "CA") addon.Airport.State = address.AdminDistrict;

							Log.WriteLine($"{addon.Airport.Name} ({addon.Airport.Ident}): {addon.Airport.Country}");
						}
						else
						{
							if (addon.Airport.Country == "") m_BadLocation.Add(addon);
							else
							{
								Log.WriteLine($"{addon.Airport.Name} ({addon.Airport.Ident}): {addon.Airport.Country}");
							}
						}
					}
				}
				void AssignContinents()
				{
					// Assign continent
					foreach (Addon addon in m_AddonsAirports)
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
						}

						// If still can't assign continent mark as bad location
						if (addon.Airport.Continent == "")
						{
							m_BadLocation.Add(addon);
						}

						// If airport folder name changed, remove old cache entry and add new one
						if (newCache.Any(arp => arp.Ident == addon.Airport.Ident && arp.Laty == addon.Airport.Laty && arp.Lonx == addon.Airport.Lonx && arp.Scenery_Local_Path != addon.Airport.Scenery_Local_Path))
						{
							newCache.Remove(newCache.Single(entry => entry.Ident == addon.Airport.Ident));
							newCache.Add(addon.Airport);
						}
						// If airport not in cache, add a new entry
						else if (!newCache.Any(arp => arp.Ident == addon.Airport.Ident && arp.Scenery_Local_Path == addon.Airport.Scenery_Local_Path)) newCache.Add(addon.Airport);
					}

					WriteLocationCache(newCache);
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

				Log.WriteHeader($"AIRPORTS NOT IDENTIFIED AS ADDONS IN LNM ({m_NotIdAsAddonInLNM.Count})");
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
		}

		// Sorts addons
		void sortBtn_Click(object sender, RoutedEventArgs e)
		{
			//foreach (Addon addon in m_AddonsAirports)
			//{
			//	string path = m_OutputPath + addon.Path.Remove(0, ADDONS_PATH.Length);
			//	Directory.CreateDirectory(path);
			//	File.Create(path + "\\manifest.txt").Close();
			//}

			// Sort
			foreach (Addon addon in m_AddonsAirports)
			{
				if (!Directory.Exists(addon.Path))
					continue;

				int index = addon.RelativePath.LastIndexOf(addon.Name);
				string addonParentFolders = addon.RelativePath.Remove(index, addon.Name.Length) != "\\" ? addon.RelativePath.Remove(index, addon.Name.Length) : "";
				string addonFolderRoot = addonParentFolders.Split(new string[] { "\\" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
				if (addonFolderRoot != null) addonFolderRoot = "\\" + addonFolderRoot;
				string dirTree = "";

				// Sort by continent
				if (sortContinent.IsChecked.Value)
				{
					bool continentFound = false;
					foreach (var continent in m_Continents)
					{
						if (continent.Code == addon.Airport.Continent || continent.Name == addon.Airport.Continent)
						{
							dirTree += "\\" + continent.Name;
							continentFound = true;
							break;
						}
					}

					if (!continentFound)
					{
						dirTree += "\\zzz_Unassgined";
					}
				}

				// Sort by country
				if (sortCountry.IsChecked.Value)
				{
					bool countryFound = false;
					foreach (var country_continent in m_CountryContinent)
					{
						if (country_continent.GetSimpleCountryName() == addon.Airport.Country || country_continent.Two_Letter_Country_Code == addon.Airport.Country)
						{
							dirTree += "\\" + country_continent.GetSimpleCountryName();
							countryFound = true;
							break;
						}
					}

					if (!countryFound)
					{
						dirTree += "\\zzz_Unassgined";
					}
				}

				// Sort by US state
				if (sortUS_CA.IsChecked.Value)
				{
					if (addon.Airport.Country == "US")
					{
						bool stateFound = false;
						foreach (var state in m_US_States)
						{
							if (addon.Airport.State == state.Name || addon.Airport.State == state.Code)
							{
								dirTree += "\\" + state.Name;
								stateFound = true;
								break;
							}
						}

						if (!stateFound)
						{
							dirTree += "\\zzz_Unassgined";
						}
					}
					else if (addon.Airport.Country == "CA")
					{
						bool stateFound = false;
						foreach (var province in m_CanadaProvinces)
						{
							if (addon.Airport.State == province.Name || addon.Airport.State == province.Code)
							{
								dirTree += "\\" + province.Name;
								stateFound = true;
								break;
							}
						}

						if (!stateFound)
						{
							dirTree += "\\zzz_Unassgined";
						}
					}
				}

				// Add parent directories to output path and move airport folder
				Directory.CreateDirectory($"{m_OutputPath}\\{dirTree}{addonParentFolders}");
				if (!(!sortContinent.IsChecked.Value && !sortCountry.IsChecked.Value && sortUS_CA.IsChecked.Value))
					Directory.Move(addon.Path, m_OutputPath + "\\" + dirTree + addon.RelativePath);

				// Delete root folder if there is an additional folder containing airports eg. Malaysian Airports/xxxx, Malaysian Airports/yyyy
				if (addonFolderRoot != null)
				{
					DirectoryInfo addonFolderRootDir = new DirectoryInfo(m_AddonPath + addonFolderRoot);
					if (addonFolderRootDir.GetFiles("*", SearchOption.AllDirectories).Length == 0)
					{
						Directory.Delete(addonFolderRootDir.FullName, true);
					}
				}
			}
			MessageBox.Show("Finished sorting.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		// Update addons
		void loadArchivesBtn_Click(object sender, RoutedEventArgs e)
		{
			// Load addon list
			m_Archives = Directory.GetFiles(m_UpdatePath, "*.*")
				.Where(file => file.ToLower().EndsWith(".zip") || file.ToLower().EndsWith(".rar") || file.ToLower().EndsWith(".7z"))
				.ToArray();


			// Abort if dir is empty
			if (m_Archives.Length == 0)
			{
				MessageBox.Show($"Directory '{m_UpdatePath}' is empty.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			// Add addons to UI
			addonList.ItemsSource = m_Zips;
			addonLabel.Content = $"Addons ({m_AddonsLoaded.Count}):";
			SortDataView(nameof(Addon.Path), ListSortDirection.Ascending);
		}
		void extractBtn_Click(object sender, RoutedEventArgs e)
		{
			m_AddonsToUpdate.Clear();

			foreach (string path in m_Archives)
			{
				if (Path.GetExtension(path) == ".zip")
				{
					using (var zip = ZipArchive.Open(path))
					{
						Extract(zip);
					}
				}
				else if (Path.GetExtension(path) == ".rar")
				{
					using (var rar = RarArchive.Open(path))
					{
						Extract(rar);
					}
				}
				//else if (Path.GetExtension(path) == ".7z")
				//{
				//	using (var sevenZip = SevenZipArchive.Open(path))
				//	{
				//		Extract(sevenZip);
				//	}
				//}
				else MessageBox.Show($"File {path} is not a supported archive type.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);

				// Delete contents of tmp
				Directory.Delete(TEMP_PATH, true);
				Directory.CreateDirectory(TEMP_PATH);

				void Extract(IArchive archive)
				{
					// Check for a folder inside archive
					foreach (var entry in archive.Entries)
					{
						// Extract to tmp folder
						entry.WriteToDirectory(TEMP_PATH, new ExtractionOptions()
						{
							ExtractFullPath = true,
							Overwrite = true
						});
					}

					// Search for folders containing manifest.json and mark them as root folders
					List<string> rootFolderNames = new List<string>();
					foreach (var dir in Directory.GetDirectories(TEMP_PATH))
					{
						if (File.Exists(dir + MANIFEST_JSON))
							rootFolderNames.Add(dir);
					}
					foreach (var rootFolder in rootFolderNames)
					{
						string rootFolderName = rootFolder.Split('\\').Last();

						JDStuart.DirectoryUtils.Directory.Move(Path.Combine(TEMP_PATH, rootFolderName), Path.Combine(m_UpdatePath, rootFolderName));
						m_AddonsToUpdate.Add(rootFolderName);
					}
				}
			}
			MessageBox.Show($"Extracted archives.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
		}
		void updateBtn_Click(object sender, RoutedEventArgs e)
		{
			foreach (var addon in m_AddonsLoaded)
			{
				foreach (var updateAddon in m_AddonsToUpdate)
				{
					if (addon.Name == updateAddon)
					{
						MessageBoxResult result = MessageBox.Show($"Replace original '{addon.RelativePath}' with '{updateAddon}'?", "Update?", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
						if (result == MessageBoxResult.Yes)
						{
							JDStuart.DirectoryUtils.Directory.Move(addon.Path, TEMP_PATH + "\\" + addon.Name);
							JDStuart.DirectoryUtils.Directory.Move(Path.Combine(m_UpdatePath, updateAddon), addon.Path);
						}
						else if (result == MessageBoxResult.Cancel)
						{
							return;
						}
					}
				}
			}
			MessageBox.Show($"Updated addons.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		void addonFolderPicker_OnFilePicked(string path)
		{
			m_AddonPath = path;
			m_AddonPathValid = Directory.Exists(m_AddonPath);
			loadBtn.IsEnabled = sortBtn.IsEnabled = m_AddonPathValid;
		}
		void outputFolderPicker_OnFilePicked(string path)
		{
			m_OutputPath = path;
			m_OutputPathValid = Directory.Exists(m_OutputPath);
			loadBtn.IsEnabled = sortBtn.IsEnabled = m_AddonPathValid;
		}
		void updateFolderPicker_OnFilePicked(string path)
		{
			m_UpdatePath = path;
			m_UpdatePathValid = Directory.Exists(m_UpdatePath);
			updateBtn.IsEnabled = m_UpdatePathValid;
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
