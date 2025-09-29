#nullable disable
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Win32;

namespace DSTSModTool;

public class AppSettings
{
    public string GamePath { get; set; } = "";
}

public class Mod : INotifyPropertyChanged
{
    private bool _isSelected;
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    private bool _isGamePathSelected;
    private string _gamePath = string.Empty;

    public bool IsGamePathSelected
    {
        get => _isGamePathSelected;
        set
        {
            _isGamePathSelected = value;
            OnPropertyChanged(nameof(IsGamePathSelected));
        }
    }

    public ObservableCollection<Mod> ModList { get; set; } = new ObservableCollection<Mod>();

    public bool HasSelectedMods => ModList.Any(m => m.IsSelected);

    private readonly string settingsFile = "settings.json";

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        ModListBox.ItemsSource = ModList;
        LoadSettings();
        LoadMods();

        // Listen to mod selection changes
        ModList.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (Mod mod in e.NewItems)
                {
                    mod.PropertyChanged += Mod_PropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (Mod mod in e.OldItems)
                {
                    mod.PropertyChanged -= Mod_PropertyChanged;
                }
            }
        };

        // Load banner image
        string bannerPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "banner.png");
        if (System.IO.File.Exists(bannerPath))
        {
            try
            {
                var bitmap = new BitmapImage(new Uri(bannerPath));
                BannerImage.Source = bitmap;
            }
            catch
            {
                // Ignore if image cannot be loaded
            }
        }
    }

    private void Mod_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Mod.IsSelected))
        {
            OnPropertyChanged(nameof(HasSelectedMods));
        }
    }

    private void LoadMods()
    {
        ModList.Clear();
        string modsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods");
        if (Directory.Exists(modsDir))
        {
            foreach (var dir in Directory.GetDirectories(modsDir))
            {
                string modName = System.IO.Path.GetFileName(dir);
                string modType = "Unknown";
                if (Directory.Exists(System.IO.Path.Combine(dir, "data")))
                {
                    modType = "Data Mod";
                }
                else if (Directory.Exists(System.IO.Path.Combine(dir, "message")))
                {
                    modType = "Language Mod";
                }
                var mod = new Mod { Name = modName, Type = modType };
                mod.PropertyChanged += Mod_PropertyChanged;
                ModList.Add(mod);
            }
        }
    }

    private void LoadSettings()
    {
        if (File.Exists(settingsFile))
        {
            try
            {
                string json = File.ReadAllText(settingsFile);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null && !string.IsNullOrEmpty(settings.GamePath))
                {
                    _gamePath = settings.GamePath;
                    GamePathTextBox.Text = _gamePath;
                    IsGamePathSelected = true;
                }
            }
            catch
            {
                // Ignore errors
            }
        }
    }

    private void SaveSettings()
    {
        try
        {
            var settings = new AppSettings { GamePath = _gamePath };
            string json = JsonSerializer.Serialize(settings);
            File.WriteAllText(settingsFile, json);
        }
        catch
        {
            // Ignore errors
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        dialog.Title = "Select Digimon Story: Time Stranger game folder";
        if (dialog.ShowDialog() == true)
        {
            _gamePath = dialog.FolderName;
            GamePathTextBox.Text = _gamePath;
            IsGamePathSelected = true;
            SaveSettings();
        }
    }

    private void ModListBox_Drop(object sender, System.Windows.DragEventArgs e)
    {
        // Drag drop disabled for now
    }

    private void ModListBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Delete)
        {
            var selectedMods = ModList.Where(m => m.IsSelected).ToList();
            if (selectedMods.Count > 0)
            {
                var result = System.Windows.MessageBox.Show("Are you sure you want to delete the selected mods?", "Confirm", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    foreach (var mod in selectedMods)
                    {
                        string modDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods", mod.Name);
                        if (Directory.Exists(modDir))
                        {
                            Directory.Delete(modDir, true);
                        }
                        ModList.Remove(mod);
                    }
                }
            }
        }
    }

    private void CreateModButton_Click(object sender, RoutedEventArgs e)
    {
        // Show create mod dialog
        var createWindow = new Window
        {
            Title = "Create New Mod",
            Width = 400,
            Height = 250,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };

        var stackPanel = new StackPanel { Margin = new Thickness(20) };

        var nameLabel = new TextBlock { Text = "Mod Name:", Margin = new Thickness(0, 0, 0, 5) };
        var nameTextBox = new TextBox { Margin = new Thickness(0, 0, 0, 10) };

        var typeLabel = new TextBlock { Text = "Mod Type:", Margin = new Thickness(0, 0, 0, 5) };
        var typeComboBox = new ComboBox { ItemsSource = new[] { "Data Mod", "Language Mod" }, SelectedIndex = 0, Margin = new Thickness(0, 0, 0, 20) };

        var createButton = new Button
        {
            Content = "Create Mod",
            Style = (Style)FindResource("ModernButton"),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        createButton.Click += (s, args) =>
        {
            string modName = nameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(modName))
            {
                System.Windows.MessageBox.Show("Please enter a mod name.");
                return;
            }

            string modType = typeComboBox.SelectedItem.ToString();
            string modsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods");
            Directory.CreateDirectory(modsDir);
            string modDir = System.IO.Path.Combine(modsDir, modName);

            if (Directory.Exists(modDir))
            {
                System.Windows.MessageBox.Show("Mod with this name already exists.");
                return;
            }

            Directory.CreateDirectory(modDir);

            if (modType == "Data Mod")
            {
                Directory.CreateDirectory(System.IO.Path.Combine(modDir, "data"));
                Directory.CreateDirectory(System.IO.Path.Combine(modDir, "font"));
                Directory.CreateDirectory(System.IO.Path.Combine(modDir, "images"));
                Directory.CreateDirectory(System.IO.Path.Combine(modDir, "lua"));
                Directory.CreateDirectory(System.IO.Path.Combine(modDir, "shaders"));
            }
            else if (modType == "Language Mod")
            {
                Directory.CreateDirectory(System.IO.Path.Combine(modDir, "font"));
                Directory.CreateDirectory(System.IO.Path.Combine(modDir, "images"));
                Directory.CreateDirectory(System.IO.Path.Combine(modDir, "message"));
                Directory.CreateDirectory(System.IO.Path.Combine(modDir, "text"));
            }

            System.Windows.MessageBox.Show($"Mod '{modName}' created successfully.");
            LoadMods();
            createWindow.Close();
        };

        stackPanel.Children.Add(nameLabel);
        stackPanel.Children.Add(nameTextBox);
        stackPanel.Children.Add(typeLabel);
        stackPanel.Children.Add(typeComboBox);
        stackPanel.Children.Add(createButton);
        createWindow.Content = stackPanel;
        createWindow.ShowDialog();
    }

    private void AddModButton_Click(object sender, RoutedEventArgs e)
    {
        var openDialog = new Microsoft.Win32.OpenFileDialog();
        openDialog.Filter = "ZIP Files (*.zip)|*.zip";
        openDialog.Title = "Select mod ZIP file";
        if (openDialog.ShowDialog() == true)
        {
            string zipFile = openDialog.FileName;
            string modName = System.IO.Path.GetFileNameWithoutExtension(zipFile);
            string modsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods");
            Directory.CreateDirectory(modsDir);
            string modDir = System.IO.Path.Combine(modsDir, modName);

            if (Directory.Exists(modDir))
            {
                System.Windows.MessageBox.Show("Mod with this name already exists.");
                return;
            }

            try
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(zipFile, modDir);
                LoadMods(); // Refresh list
                System.Windows.MessageBox.Show($"Mod '{modName}' added successfully.");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error adding mod: {ex.Message}");
            }
        }
    }

    private void InstallModButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedMods = ModList.Where(m => m.IsSelected).ToList();
        if (selectedMods.Count == 0)
        {
            System.Windows.MessageBox.Show("Please select a mod to install.");
            return;
        }

        if (selectedMods.Count > 1)
        {
            System.Windows.MessageBox.Show("Please select only one mod to install.");
            return;
        }

        var selectedMod = selectedMods[0];
        string modDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods", selectedMod.Name);

        // Get gamedata directory
        string gamedataDir = System.IO.Path.Combine(_gamePath, "gamedata");
        if (!Directory.Exists(gamedataDir))
        {
            System.Windows.MessageBox.Show("Gamedata directory not found in game path.");
            return;
        }

        // Determine target file based on mod type
        string targetFile;
        if (selectedMod.Type == "Data Mod")
        {
            targetFile = System.IO.Path.Combine(gamedataDir, "patch_0.dx11.mvgl");
        }
        else if (selectedMod.Type == "Language Mod")
        {
            // Assume English (01) for now - TODO: detect or select language
            string languageCode = "01"; // English
            targetFile = System.IO.Path.Combine(gamedataDir, $"patch_text{languageCode}.dx11.mvgl");
        }
        else
        {
            System.Windows.MessageBox.Show("Unknown mod type.");
            return;
        }

        // Run DSCSToolsCLI --pack
        string cliPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "DSCSTools", "DSCSToolsCLI.exe");
        if (!System.IO.File.Exists(cliPath))
        {
            System.Windows.MessageBox.Show("DSCSToolsCLI.exe not found.");
            return;
        }

        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = cliPath,
                Arguments = $"--pack \"{modDir}\" \"{targetFile}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode == 0)
        {
            System.Windows.MessageBox.Show($"Mod installed successfully. Patched file: {targetFile}");
        }
        else
        {
            System.Windows.MessageBox.Show($"Error installing mod: {error}");
        }
    }

    private void UninstallModButton_Click(object sender, RoutedEventArgs e)
    {
        // Get gamedata directory
        string gamedataDir = System.IO.Path.Combine(_gamePath, "gamedata");
        if (!Directory.Exists(gamedataDir))
        {
            System.Windows.MessageBox.Show("Gamedata directory not found in game path.");
            return;
        }

        var patchFiles = Directory.GetFiles(gamedataDir, "patch_*.mvgl", SearchOption.AllDirectories);
        if (patchFiles.Length == 0)
        {
            System.Windows.MessageBox.Show("No patch files found in gamedata.");
            return;
        }

        // Show file selection dialog
        var selectWindow = new Window
        {
            Title = "Select Patch File to Uninstall",
            Width = 400,
            Height = 350,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };

        var listBox = new ListBox { Margin = new Thickness(10), Height = 200 };
        foreach (var file in patchFiles)
        {
            listBox.Items.Add(System.IO.Path.GetFileName(file));
        }

        var selectButton = new Button
        {
            Content = "Uninstall Mod",
            Style = (Style)FindResource("ModernButton"),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        selectButton.Click += (s, args) =>
        {
            if (listBox.SelectedItem != null)
            {
                string selectedFileName = listBox.SelectedItem.ToString();
                string filePath = patchFiles[listBox.SelectedIndex];

                var result = System.Windows.MessageBox.Show($"Are you sure you want to delete {selectedFileName}?", "Confirm Uninstall", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        System.IO.File.Delete(filePath);
                        System.Windows.MessageBox.Show("Mod uninstalled successfully.");
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Error uninstalling mod: {ex.Message}");
                    }
                }
            }
            selectWindow.Close();
        };

        var stackPanel = new StackPanel();
        stackPanel.Children.Add(new TextBlock { Text = "Select patch file to uninstall:", Margin = new Thickness(10, 10, 10, 5) });
        stackPanel.Children.Add(listBox);
        stackPanel.Children.Add(selectButton);
        selectWindow.Content = stackPanel;
        selectWindow.ShowDialog();
    }

    // Tool buttons
    private void MVGLExtactButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_gamePath))
        {
            System.Windows.MessageBox.Show("Please select game path first.");
            return;
        }

        string gamedataPath = System.IO.Path.Combine(_gamePath, "gamedata");
        if (!Directory.Exists(gamedataPath))
        {
            System.Windows.MessageBox.Show("Gamedata folder not found.");
            return;
        }

        var mvglFiles = Directory.GetFiles(gamedataPath, "*.mvgl", SearchOption.AllDirectories);
        if (mvglFiles.Length == 0)
        {
            System.Windows.MessageBox.Show("No .mvgl files found.");
            return;
        }

        // Show file selection dialog
        var selectWindow = new Window
        {
            Title = "Select MVGL File",
            Width = 400,
            Height = 350,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };

        var listBox = new ListBox { Margin = new Thickness(10), Height = 200 };
        foreach (var file in mvglFiles)
        {
            listBox.Items.Add(System.IO.Path.GetFileName(file));
        }

        var progressBar = new ProgressBar
        {
            Margin = new Thickness(10),
            IsIndeterminate = false,
            Visibility = Visibility.Collapsed
        };

        var selectButton = new Button
        {
            Content = "Extract",
            Style = (Style)FindResource("ModernButton"),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        selectButton.Click += async (s, args) =>
        {
            if (listBox.SelectedItem != null)
            {
                selectButton.IsEnabled = false;
                progressBar.Visibility = Visibility.Visible;

                string selectedFile = mvglFiles[listBox.SelectedIndex];
                string fileName = System.IO.Path.GetFileNameWithoutExtension(selectedFile);
                string extractedDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extracted", fileName);
                Directory.CreateDirectory(extractedDir);

                // Use indeterminate progress for all files
                progressBar.IsIndeterminate = true;
                await RunDSCSToolsCLIAsync($"--extract \"{selectedFile}\" \"{extractedDir}\"");

                progressBar.Visibility = Visibility.Collapsed;
                selectButton.IsEnabled = true;
            }
            selectWindow.Close();
        };

        var stackPanel = new StackPanel();
        stackPanel.Children.Add(listBox);
        stackPanel.Children.Add(progressBar);
        stackPanel.Children.Add(selectButton);
        selectWindow.Content = stackPanel;
        selectWindow.ShowDialog();
    }
    private void MVGLHelpButton_Click(object sender, RoutedEventArgs e)
    {
        string helpText = "MVGL Tool Help:\n\n" +
            "This tool extracts MVGL model files from the Digimon Story: Time Stranger game data.\n\n" +
            "Instructions:\n" +
            "1. Ensure the game path is set correctly.\n" +
            "2. Click 'Extract' to view available .mvgl files in the gamedata folder.\n" +
            "3. Select a file from the list.\n" +
            "4. Click 'Extract' in the dialog to unpack the file.\n" +
            "5. Files will be extracted to the 'Extracted' folder in the application directory.\n\n" +
            "Note: Large files like app_0.dx11.mvgl may take several minutes to extract.";

        System.Windows.MessageBox.Show(helpText, "MVGL Tool Help", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void CPKExtractButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_gamePath))
        {
            System.Windows.MessageBox.Show("Please select game path first.");
            return;
        }

        string gamedataPath = System.IO.Path.Combine(_gamePath, "gamedata");
        if (!Directory.Exists(gamedataPath))
        {
            System.Windows.MessageBox.Show("Gamedata folder not found.");
            return;
        }

        var cpkFiles = Directory.GetFiles(gamedataPath, "*.cpk", SearchOption.AllDirectories);
        if (cpkFiles.Length == 0)
        {
            System.Windows.MessageBox.Show("No .cpk files found.");
            return;
        }

        // Show file selection dialog
        var selectWindow = new Window
        {
            Title = "Select CPK File",
            Width = 400,
            Height = 350,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };

        var listBox = new ListBox { Margin = new Thickness(10), Height = 200 };
        foreach (var file in cpkFiles)
        {
            listBox.Items.Add(System.IO.Path.GetFileName(file));
        }

        var progressBar = new ProgressBar
        {
            Margin = new Thickness(10),
            IsIndeterminate = true,
            Visibility = Visibility.Collapsed
        };

        var selectButton = new Button
        {
            Content = "Extract",
            Style = (Style)FindResource("ModernButton"),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        selectButton.Click += async (s, args) =>
        {
            if (listBox.SelectedItem != null)
            {
                selectButton.IsEnabled = false;
                progressBar.Visibility = Visibility.Visible;

                string selectedFile = cpkFiles[listBox.SelectedIndex];
                string fileName = System.IO.Path.GetFileNameWithoutExtension(selectedFile);
                string extractedDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CPKExtracted", fileName);
                Directory.CreateDirectory(extractedDir);

                await RunYACpkToolAsync($"\"{selectedFile}\" \"{extractedDir}\"");

                progressBar.Visibility = Visibility.Collapsed;
                selectButton.IsEnabled = true;
            }
            selectWindow.Close();
        };

        var stackPanel = new StackPanel();
        stackPanel.Children.Add(listBox);
        stackPanel.Children.Add(progressBar);
        stackPanel.Children.Add(selectButton);
        selectWindow.Content = stackPanel;
        selectWindow.ShowDialog();
    }
    private void CPKRepackButton_Click(object sender, RoutedEventArgs e)
    {
        string extractedDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CPKExtracted");
        if (!Directory.Exists(extractedDir))
        {
            System.Windows.MessageBox.Show("CPKExtracted folder not found. Extract some CPK files first.");
            return;
        }

        var subDirs = Directory.GetDirectories(extractedDir);
        if (subDirs.Length == 0)
        {
            System.Windows.MessageBox.Show("No extracted folders found.");
            return;
        }

        // Show folder selection dialog
        var selectWindow = new Window
        {
            Title = "Select Folder to Repack",
            Width = 400,
            Height = 350,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };

        var listBox = new ListBox { Margin = new Thickness(10), Height = 200 };
        foreach (var dir in subDirs)
        {
            listBox.Items.Add(System.IO.Path.GetFileName(dir));
        }

        var progressBar = new ProgressBar
        {
            Margin = new Thickness(10),
            IsIndeterminate = true,
            Visibility = Visibility.Collapsed
        };

        var selectButton = new Button
        {
            Content = "Repack",
            Style = (Style)FindResource("ModernButton"),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        selectButton.Click += async (s, args) =>
        {
            if (listBox.SelectedItem != null)
            {
                selectButton.IsEnabled = false;
                progressBar.Visibility = Visibility.Visible;

                string selectedDir = subDirs[listBox.SelectedIndex];
                string dirName = System.IO.Path.GetFileName(selectedDir);
                string packedDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CPKPacked");
                Directory.CreateDirectory(packedDir);
                string outputCpk = System.IO.Path.Combine(packedDir, dirName + ".cpk");

                await RunYACpkToolAsync($"\"{selectedDir}\" \"{outputCpk}\"");

                progressBar.Visibility = Visibility.Collapsed;
                selectButton.IsEnabled = true;
            }
            selectWindow.Close();
        };

        var stackPanel = new StackPanel();
        stackPanel.Children.Add(listBox);
        stackPanel.Children.Add(progressBar);
        stackPanel.Children.Add(selectButton);
        selectWindow.Content = stackPanel;
        selectWindow.ShowDialog();
    }
    private void CPKHelpButton_Click(object sender, RoutedEventArgs e)
    {
        string helpText = "CPK Tool Help:\n\n" +
            "This tool extracts and repacks CPK archive files from the Digimon Story: Time Stranger game data.\n\n" +
            "Extract Instructions:\n" +
            "1. Ensure the game path is set correctly.\n" +
            "2. Click 'Extract' to view available .cpk files in the gamedata folder.\n" +
            "3. Select a file from the list.\n" +
            "4. Click 'Extract' in the dialog to unpack the file.\n" +
            "5. Files will be extracted to the 'Extracted' folder in the application directory.\n\n" +
            "Repack Instructions:\n" +
            "1. Click 'Repack' to view extracted folders in the 'Extracted' directory.\n" +
            "2. Select a folder from the list.\n" +
            "3. Click 'Repack' in the dialog to pack the folder into a .cpk file.\n" +
            "4. The packed file will be saved to the 'CPKPacked' folder in the application directory.";

        System.Windows.MessageBox.Show(helpText, "CPK Tool Help", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void MBEExtractButton_Click(object sender, RoutedEventArgs e)
    {
        string extractedDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extracted");
        if (!Directory.Exists(extractedDir))
        {
            System.Windows.MessageBox.Show("Extracted folder not found. Extract some files first.");
            return;
        }

        var subDirs = Directory.GetDirectories(extractedDir);
        if (subDirs.Length == 0)
        {
            System.Windows.MessageBox.Show("No extracted folders found.");
            return;
        }

        // Show folder selection dialog
        var selectWindow = new Window
        {
            Title = "Select Extracted Folder",
            Width = 400,
            Height = 350,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };

        var listBox = new ListBox { Margin = new Thickness(10), Height = 150 };
        foreach (var dir in subDirs)
        {
            listBox.Items.Add(System.IO.Path.GetFileName(dir));
        }

        var typeComboBox = new ComboBox { Margin = new Thickness(10), ItemsSource = new[] { "message", "text" } };
        typeComboBox.SelectedIndex = 0;

        var progressBar = new ProgressBar
        {
            Margin = new Thickness(10),
            IsIndeterminate = true,
            Visibility = Visibility.Collapsed
        };

        var selectButton = new Button
        {
            Content = "Extract MBE",
            Style = (Style)FindResource("ModernButton"),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        selectButton.Click += async (s, args) =>
        {
            if (listBox.SelectedItem != null && typeComboBox.SelectedItem != null)
            {
                selectButton.IsEnabled = false;
                progressBar.Visibility = Visibility.Visible;

                string selectedDir = subDirs[listBox.SelectedIndex];
                string type = typeComboBox.SelectedItem.ToString();
                string typeDir = System.IO.Path.Combine(selectedDir, type);
                if (!Directory.Exists(typeDir))
                {
                    System.Windows.MessageBox.Show($"{type} folder not found in {System.IO.Path.GetFileName(selectedDir)}.");
                    selectButton.IsEnabled = true;
                    progressBar.Visibility = Visibility.Collapsed;
                    return;
                }

                var mbeFiles = Directory.GetFiles(typeDir, "*.mbe");
                if (mbeFiles.Length == 0)
                {
                    System.Windows.MessageBox.Show($"No .mbe files found in {type} folder.");
                    selectButton.IsEnabled = true;
                    progressBar.Visibility = Visibility.Collapsed;
                    return;
                }

                string outputDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MBEExtracted", type);
                Directory.CreateDirectory(outputDir);

                foreach (var mbeFile in mbeFiles)
                {
                    await RunPythonScriptAsync("MBE_Parser.py", $"\"{mbeFile}\"", outputDir);
                }

                if (mbeFiles.Length > 0)
                {
                    System.Windows.MessageBox.Show($"Extracted {mbeFiles.Length} .mbe files to {outputDir}.");
                }

                progressBar.Visibility = Visibility.Collapsed;
                selectButton.IsEnabled = true;
            }
            selectWindow.Close();
        };

        var stackPanel = new StackPanel();
        stackPanel.Children.Add(new TextBlock { Text = "Select extracted folder:", Margin = new Thickness(10, 10, 10, 5) });
        stackPanel.Children.Add(listBox);
        stackPanel.Children.Add(new TextBlock { Text = "Select type:", Margin = new Thickness(10, 10, 10, 5) });
        stackPanel.Children.Add(typeComboBox);
        stackPanel.Children.Add(progressBar);
        stackPanel.Children.Add(selectButton);
        selectWindow.Content = stackPanel;
        selectWindow.ShowDialog();
    }
    private void MBERepackButton_Click(object sender, RoutedEventArgs e)
    {
        string mbeExtractDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MBEExtracted");
        if (!Directory.Exists(mbeExtractDir))
        {
            System.Windows.MessageBox.Show("MBEExtracted folder not found. Extract some MBE files first.");
            return;
        }

        var subDirs = Directory.GetDirectories(mbeExtractDir);
        if (subDirs.Length == 0)
        {
            System.Windows.MessageBox.Show("No MBE extracted folders found.");
            return;
        }

        // Show folder selection dialog
        var selectWindow = new Window
        {
            Title = "Select MBE Folder to Repack",
            Width = 400,
            Height = 350,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };

        var listBox = new ListBox { Margin = new Thickness(10), Height = 200 };
        foreach (var dir in subDirs)
        {
            listBox.Items.Add(System.IO.Path.GetFileName(dir));
        }

        var progressBar = new ProgressBar
        {
            Margin = new Thickness(10),
            IsIndeterminate = true,
            Visibility = Visibility.Collapsed
        };

        var selectButton = new Button
        {
            Content = "Repack MBE",
            Style = (Style)FindResource("ModernButton"),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        selectButton.Click += async (s, args) =>
        {
            if (listBox.SelectedItem != null)
            {
                selectButton.IsEnabled = false;
                progressBar.Visibility = Visibility.Visible;

                string selectedDir = subDirs[listBox.SelectedIndex];
                string selectedName = System.IO.Path.GetFileName(selectedDir);
                string packedDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MBEPacked", selectedName);
                Directory.CreateDirectory(packedDir);

                // Get subfolders in selectedDir (e.g., message\analyse)
                var subFolders = Directory.GetDirectories(selectedDir);
                foreach (var subFolder in subFolders)
                {
                    await RunPythonScriptAsync("MBE_Repacker.py", $"\"{subFolder}\"");

                    // Script creates file in the same directory as subFolder
                    string subName = System.IO.Path.GetFileName(subFolder);
                    string subDir = System.IO.Path.GetDirectoryName(subFolder);
                    string mbeFile = System.IO.Path.Combine(subDir, subName + ".mbe");
                    string targetFile = System.IO.Path.Combine(packedDir, subName + ".mbe");

                    if (File.Exists(mbeFile))
                    {
                        File.Move(mbeFile, targetFile, true);
                    }
                }

                progressBar.Visibility = Visibility.Collapsed;
                selectButton.IsEnabled = true;

                System.Windows.MessageBox.Show($"Repacked {subFolders.Length} MBE folders to {packedDir}.");
            }
            selectWindow.Close();
        };

        var stackPanel = new StackPanel();
        stackPanel.Children.Add(listBox);
        stackPanel.Children.Add(progressBar);
        stackPanel.Children.Add(selectButton);
        selectWindow.Content = stackPanel;
        selectWindow.ShowDialog();
    }
    private void MBEHelpButton_Click(object sender, RoutedEventArgs e)
    {
        string helpText = "MBE Tool Help:\n\n" +
            "This tool extracts and repacks MBE message/text files from the Digimon Story: Time Stranger game data.\n\n" +
            "Extract Instructions:\n" +
            "1. Ensure files are extracted to the 'Extracted' folder.\n" +
            "2. Click 'Extract' to select an extracted folder.\n" +
            "3. Choose type: 'message' or 'text'.\n" +
            "4. The tool will process all .mbe files in the selected type folder.\n" +
            "5. Extracted files will be saved to the 'MBEExtracted' folder.\n\n" +
            "Repack Instructions:\n" +
            "1. Click 'Repack' to select 'message' or 'text' folder from 'MBEExtracted'.\n" +
            "2. The tool will repack all subfolders in the selected folder to .mbe files.\n" +
            "3. Packed files will be saved to 'MBEPacked\\selected_folder\\'.\n\n" +
            "Note: Requires Python to be installed on the system.";

        System.Windows.MessageBox.Show(helpText, "MBE Tool Help", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void IMGExtractButton_Click(object sender, RoutedEventArgs e)
    {
        string extractedDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extracted");
        if (!Directory.Exists(extractedDir))
        {
            System.Windows.MessageBox.Show("Extracted folder not found. Extract some files first.");
            return;
        }

        var subDirs = Directory.GetDirectories(extractedDir);
        if (subDirs.Length == 0)
        {
            System.Windows.MessageBox.Show("No extracted folders found.");
            return;
        }

        // Show folder selection dialog
        var selectWindow = new Window
        {
            Title = "Select Extracted Folder",
            Width = 400,
            Height = 350,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };

        var listBox = new ListBox { Margin = new Thickness(10), Height = 200 };
        foreach (var dir in subDirs)
        {
            listBox.Items.Add(System.IO.Path.GetFileName(dir));
        }

        var progressBar = new ProgressBar
        {
            Margin = new Thickness(10),
            IsIndeterminate = true,
            Visibility = Visibility.Collapsed
        };

        var selectButton = new Button
        {
            Content = "Extract IMG",
            Style = (Style)FindResource("ModernButton"),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        selectButton.Click += async (s, args) =>
        {
            if (listBox.SelectedItem != null)
            {
                selectButton.IsEnabled = false;
                progressBar.Visibility = Visibility.Visible;

                string selectedDir = subDirs[listBox.SelectedIndex];
                string imagesDir = System.IO.Path.Combine(selectedDir, "images");
                if (!Directory.Exists(imagesDir))
                {
                    System.Windows.MessageBox.Show($"Images folder not found in {System.IO.Path.GetFileName(selectedDir)}.");
                    selectButton.IsEnabled = true;
                    progressBar.Visibility = Visibility.Collapsed;
                    return;
                }

                var imgFiles = Directory.GetFiles(imagesDir, "*.img");
                if (imgFiles.Length == 0)
                {
                    var allFiles = Directory.GetFiles(imagesDir);
                    string fileList = string.Join("\n", allFiles.Select(f => System.IO.Path.GetFileName(f)));
                    System.Windows.MessageBox.Show($"No .img files found in images folder.\nFiles found: {fileList}");
                    selectButton.IsEnabled = true;
                    progressBar.Visibility = Visibility.Collapsed;
                    return;
                }

                string outputDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IMGExtracted", "images");
                Directory.CreateDirectory(outputDir);

                foreach (var imgFile in imgFiles)
                {
                    string ddsFile = imgFile.Replace(".img", ".dds");
                    File.Move(imgFile, ddsFile);
                    string pngFile = System.IO.Path.Combine(outputDir, System.IO.Path.GetFileNameWithoutExtension(ddsFile) + ".png");
                    await RunCLIAsync("compressonatorcli", $"-ff PNG \"{ddsFile}\" \"{pngFile}\"");
                    File.Move(ddsFile, imgFile); // Restore original
                }

                if (imgFiles.Length > 0)
                {
                    System.Windows.MessageBox.Show($"Extracted {imgFiles.Length} .img files to {outputDir}.");
                }

                progressBar.Visibility = Visibility.Collapsed;
                selectButton.IsEnabled = true;
            }
            selectWindow.Close();
        };

        var stackPanel = new StackPanel();
        stackPanel.Children.Add(new TextBlock { Text = "Select extracted folder:", Margin = new Thickness(10, 10, 10, 5) });
        stackPanel.Children.Add(listBox);
        stackPanel.Children.Add(progressBar);
        stackPanel.Children.Add(selectButton);
        selectWindow.Content = stackPanel;
        selectWindow.ShowDialog();
    }

    private void IMGRepackButton_Click(object sender, RoutedEventArgs e)
    {
        string imgExtractDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IMGExtracted");
        if (!Directory.Exists(imgExtractDir))
        {
            System.Windows.MessageBox.Show("IMGExtracted folder not found. Extract some IMG files first.");
            return;
        }

        var subDirs = Directory.GetDirectories(imgExtractDir);
        if (subDirs.Length == 0)
        {
            System.Windows.MessageBox.Show("No IMG extracted folders found.");
            return;
        }

        // Show folder selection dialog
        var selectWindow = new Window
        {
            Title = "Select IMG Folder to Repack",
            Width = 400,
            Height = 350,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };

        var listBox = new ListBox { Margin = new Thickness(10), Height = 200 };
        foreach (var dir in subDirs)
        {
            listBox.Items.Add(System.IO.Path.GetFileName(dir));
        }

        var progressBar = new ProgressBar
        {
            Margin = new Thickness(10),
            IsIndeterminate = true,
            Visibility = Visibility.Collapsed
        };

        var selectButton = new Button
        {
            Content = "Repack IMG",
            Style = (Style)FindResource("ModernButton"),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        selectButton.Click += async (s, args) =>
        {
            if (listBox.SelectedItem != null)
            {
                selectButton.IsEnabled = false;
                progressBar.Visibility = Visibility.Visible;

                string selectedDir = subDirs[listBox.SelectedIndex];
                string packedDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IMGPacked", System.IO.Path.GetFileName(selectedDir));
                Directory.CreateDirectory(packedDir);

                var pngFiles = Directory.GetFiles(selectedDir, "*.png");
                foreach (var pngFile in pngFiles)
                {
                    string ddsFile = System.IO.Path.Combine(packedDir, System.IO.Path.GetFileNameWithoutExtension(pngFile) + ".dds");
                    await RunCLIAsync("compressonatorcli", $"-fd BC7 \"{pngFile}\" \"{ddsFile}\"");
                    string imgFile = ddsFile.Replace(".dds", ".img");
                    File.Move(ddsFile, imgFile);
                }

                if (pngFiles.Length > 0)
                {
                    System.Windows.MessageBox.Show($"Repacked {pngFiles.Length} .png files to {packedDir}.");
                }

                progressBar.Visibility = Visibility.Collapsed;
                selectButton.IsEnabled = true;
            }
            selectWindow.Close();
        };

        var stackPanel = new StackPanel();
        stackPanel.Children.Add(listBox);
        stackPanel.Children.Add(progressBar);
        stackPanel.Children.Add(selectButton);
        selectWindow.Content = stackPanel;
        selectWindow.ShowDialog();
    }

    private void IMGHelpButton_Click(object sender, RoutedEventArgs e)
    {
        string helpText = "IMG Tool Help:\n\n" +
            "This tool extracts and repacks IMG image files from the Digimon Story: Time Stranger game data.\n\n" +
            "Extract Instructions:\n" +
            "1. Ensure files are extracted to the 'Extracted' folder.\n" +
            "2. Click 'Extract' to select an extracted folder.\n" +
            "3. The tool will process all .img files in the images subfolder.\n" +
            "4. Extracted files will be saved as .png to the 'IMGExtracted' folder.\n\n" +
            "Repack Instructions:\n" +
            "1. Click 'Repack' to select a folder from 'IMGExtracted'.\n" +
            "2. The tool will convert .png files back to .img format.\n" +
            "3. Packed files will be saved to 'IMGPacked\\selected_folder\\'.\n\n" +
            "Note: Requires compressonatorcli.exe to be installed.";

        System.Windows.MessageBox.Show(helpText, "IMG Tool Help", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void TEXTExtractButton_Click(object sender, RoutedEventArgs e)
    {
        // Merge CSV to TSV from MBEExtracted
        string mbeExtractedDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MBEExtracted");
        if (!Directory.Exists(mbeExtractedDir))
        {
            System.Windows.MessageBox.Show("MBEExtracted folder not found. Extract some MBE files first.");
            return;
        }

        var subDirs = Directory.GetDirectories(mbeExtractedDir);
        if (subDirs.Length == 0)
        {
            System.Windows.MessageBox.Show("No MBE extracted folders found.");
            return;
        }

        // Show folder selection dialog
        var selectWindow = new Window
        {
            Title = "Select MBE Extracted Folder",
            Width = 400,
            Height = 350,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };

        var listBox = new ListBox { Margin = new Thickness(10), Height = 200 };
        foreach (var dir in subDirs)
        {
            listBox.Items.Add(System.IO.Path.GetFileName(dir));
        }

        var progressBar = new ProgressBar
        {
            Margin = new Thickness(10),
            IsIndeterminate = true,
            Visibility = Visibility.Collapsed
        };

        var selectButton = new Button
        {
            Content = "Merge to TSV",
            Style = (Style)FindResource("ModernButton"),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        selectButton.Click += (s, args) =>
        {
            if (listBox.SelectedItem != null)
            {
                selectButton.IsEnabled = false;
                progressBar.Visibility = Visibility.Visible;

                string selectedDir = subDirs[listBox.SelectedIndex];
                string dirName = System.IO.Path.GetFileName(selectedDir);
                string textExtractedDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TEXTExtracted");
                Directory.CreateDirectory(textExtractedDir);
                string targetFile = System.IO.Path.Combine(textExtractedDir, dirName + ".tsv");

                MergeCsvToTsv(selectedDir, targetFile);

                progressBar.Visibility = Visibility.Collapsed;
                selectButton.IsEnabled = true;
            }
            selectWindow.Close();
        };

        var stackPanel = new StackPanel();
        stackPanel.Children.Add(new TextBlock { Text = "Select MBE extracted folder:", Margin = new Thickness(10, 10, 10, 5) });
        stackPanel.Children.Add(listBox);
        stackPanel.Children.Add(progressBar);
        stackPanel.Children.Add(selectButton);
        selectWindow.Content = stackPanel;
        selectWindow.ShowDialog();
    }

    private void TEXTRepackButton_Click(object sender, RoutedEventArgs e)
    {
        // Split TSV to CSV from TEXTExtracted
        string textExtractedDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TEXTExtracted");
        if (!Directory.Exists(textExtractedDir))
        {
            System.Windows.MessageBox.Show("TEXTExtracted folder not found. Extract some TEXT files first.");
            return;
        }

        var tsvFiles = Directory.GetFiles(textExtractedDir, "*.tsv");
        if (tsvFiles.Length == 0)
        {
            System.Windows.MessageBox.Show("No .tsv files found in TEXTExtracted.");
            return;
        }

        // Show file selection dialog
        var selectWindow = new Window
        {
            Title = "Select TSV File to Split",
            Width = 400,
            Height = 350,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this
        };

        var listBox = new ListBox { Margin = new Thickness(10), Height = 200 };
        foreach (var file in tsvFiles)
        {
            listBox.Items.Add(System.IO.Path.GetFileName(file));
        }

        var progressBar = new ProgressBar
        {
            Margin = new Thickness(10),
            IsIndeterminate = true,
            Visibility = Visibility.Collapsed
        };

        var selectButton = new Button
        {
            Content = "Split TSV",
            Style = (Style)FindResource("ModernButton"),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        selectButton.Click += async (s, args) =>
        {
            if (listBox.SelectedItem != null)
            {
                selectButton.IsEnabled = false;
                progressBar.Visibility = Visibility.Visible;

                string selectedFile = tsvFiles[listBox.SelectedIndex];
                string fileName = System.IO.Path.GetFileNameWithoutExtension(selectedFile);
                string textPackedDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TEXTPacked", fileName);
                Directory.CreateDirectory(textPackedDir);

                await SplitTsvToCsvAsync(selectedFile, textPackedDir);

                progressBar.Visibility = Visibility.Collapsed;
                selectButton.IsEnabled = true;
            }
            selectWindow.Close();
        };

        var stackPanel = new StackPanel();
        stackPanel.Children.Add(new TextBlock { Text = "Select TSV file to split:", Margin = new Thickness(10, 10, 10, 5) });
        stackPanel.Children.Add(listBox);
        stackPanel.Children.Add(progressBar);
        stackPanel.Children.Add(selectButton);
        selectWindow.Content = stackPanel;
        selectWindow.ShowDialog();
    }

    private void TEXTHelpButton_Click(object sender, RoutedEventArgs e)
    {
        string helpText = "TEXT Tool Help:\n\n" +
            "This tool processes CSV/TSV files for text data in Digimon Story: Time Stranger.\n\n" +
            "Extract (Merge CSV to TSV):\n" +
            "1. Select directory containing subdirectories with CSV files.\n" +
            "2. Choose output TSV file.\n" +
            "3. Tool merges all CSV files into one TSV with metadata.\n\n" +
            "Repack (Split TSV to CSV):\n" +
            "1. Select TSV file to split.\n" +
            "2. Choose destination directory.\n" +
            "3. Tool splits TSV into CSV files organized by subdirectories.\n\n" +
            "Note: Handles line breaks and metadata for data integrity.";

        System.Windows.MessageBox.Show(helpText, "TEXT Tool Help", MessageBoxButton.OK, MessageBoxImage.Information);
    }


    private async Task RunYACpkToolAsync(string arguments)
    {
        try
        {
            // Assume YACpkTool.exe is in bin\Debug\Tools\YACpkTool\YACpkTool.exe
            string cliPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "YACpkTool", "YACpkTool.exe");
            cliPath = System.IO.Path.GetFullPath(cliPath); // Resolve relative path

            if (!File.Exists(cliPath))
            {
                System.Windows.MessageBox.Show($"YACpkTool.exe not found at {cliPath}. Please ensure YACpkTool is installed in the Tools\\YACpkTool directory.");
                return;
            }

            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = cliPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                System.Windows.MessageBox.Show($"Operation failed with exit code {process.ExitCode}.");
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error running CLI: {ex.Message}");
        }
    }

    private async Task RunPythonScriptAsync(string scriptName, string arguments, string workingDir = null)
    {
        try
        {
            // Assume python scripts are in bin\Debug\Tools\THL-MBE-Parser\
            string scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "THL-MBE-Parser", scriptName);
            scriptPath = System.IO.Path.GetFullPath(scriptPath); // Resolve relative path

            if (!File.Exists(scriptPath))
            {
                System.Windows.MessageBox.Show($"{scriptName} not found at {scriptPath}. Please ensure THL-MBE-Parser is installed in the Tools\\THL-MBE-Parser directory.");
                return;
            }

            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"\"{scriptPath}\" {arguments}",
                    WorkingDirectory = workingDir ?? System.IO.Path.GetDirectoryName(scriptPath),
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                System.Windows.MessageBox.Show($"Operation failed with exit code {process.ExitCode}.");
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error running Python script: {ex.Message}");
        }
    }

    private async Task RunCLIAsync(string cliName, string arguments, string workingDir = null)
    {
        try
        {
            string cliPath;
            if (cliName == "compressonatorcli")
            {
                cliPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "compressonator", "compressonatorcli.exe");
            }
            else
            {
                cliPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", cliName, cliName + ".exe");
            }
            cliPath = System.IO.Path.GetFullPath(cliPath); // Resolve relative path

            if (!File.Exists(cliPath))
            {
                string dirName = cliName == "compressonatorcli" ? "compressonator" : cliName;
                System.Windows.MessageBox.Show($"{cliName}.exe not found at {cliPath}. Please ensure {cliName} is installed in the Tools\\{dirName} directory.");
                return;
            }

            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = cliPath,
                    Arguments = arguments,
                    WorkingDirectory = workingDir ?? System.IO.Path.GetDirectoryName(cliPath),
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                System.Windows.MessageBox.Show($"Operation failed with exit code {process.ExitCode}.");
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error running CLI: {ex.Message}");
        }
    }

    private async Task RunDSCSToolsCLIAsync(string arguments)
    {
        try
        {
            // Assume DSCSToolsCLI.exe is in bin\Debug\Tools\DSCSTools\DSCSToolsCLI.exe
            string cliPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "DSCSTools", "DSCSToolsCLI.exe");
            cliPath = System.IO.Path.GetFullPath(cliPath); // Resolve relative path

            if (!File.Exists(cliPath))
            {
                System.Windows.MessageBox.Show($"DSCSToolsCLI.exe not found at {cliPath}. Please ensure DSCSTools is installed in the Tools\\DSCSTools directory.");
                return;
            }

            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = cliPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();

            // Read output asynchronously to avoid blocking
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await Task.WhenAll(outputTask, process.WaitForExitAsync());

            string output = await outputTask;
            string error = await errorTask;

            if (process.ExitCode == 0)
            {
                System.Windows.MessageBox.Show("Extract completed successfully.");
            }
            else
            {
                System.Windows.MessageBox.Show($"Extract failed: {error}");
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error running CLI: {ex.Message}");
        }
    }

    private void MergeCsvToTsv(string sourceDir, string targetFile)
    {
        try
        {
            // Scan for CSV directories
            var csvDirs = ScanCsvDirectories(sourceDir);
            if (csvDirs.Count == 0)
            {
                System.Windows.MessageBox.Show("No directories containing CSV files found.");
                return;
            }

            // Merge files
            var allRows = new List<List<string>>();
            List<string> headers = null;
            int totalFiles = csvDirs.Sum(d => d.CsvFiles.Count);
            int processedFiles = 0;

            foreach (var dirInfo in csvDirs)
            {
                foreach (var csvFile in dirInfo.CsvFiles)
                {
                    string csvPath = System.IO.Path.Combine(dirInfo.DirPath, csvFile);
                    var rows = ReadCsvFile(csvPath);
                    if (rows.Count > 0)
                    {
                        if (headers == null)
                        {
                            headers = new List<string>(rows[0]) { "metadata" };
                            allRows.Add(headers);
                        }
                        string metadata = $"{dirInfo.DirName}/{csvFile}";
                        for (int i = 1; i < rows.Count; i++) // Skip header
                        {
                            var row = new List<string>(rows[i]) { metadata };
                            allRows.Add(row);
                        }
                    }
                    processedFiles++;
                }
            }

            // Write TSV
            if (allRows.Count > 0)
            {
                var escapedRows = EscapeLineBreaks(allRows);
                WriteTsvFile(targetFile, escapedRows);
                System.Windows.MessageBox.Show("Merge completed successfully.");
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error during merge: {ex.Message}");
        }
    }

    private Task SplitTsvToCsvAsync(string tsvPath, string targetDir)
    {
        try
        {
            var rows = ReadTsvFile(tsvPath);
            if (rows.Count == 0)
            {
                System.Windows.MessageBox.Show("TSV file is empty.");
            }

            var headers = rows[0];
            if (!headers.Contains("metadata"))
            {
                System.Windows.MessageBox.Show("TSV file does not have metadata column.");
            }

            int metadataIdx = headers.IndexOf("metadata");
            var headersNoMeta = headers.Take(metadataIdx).ToList();

            var groupedData = GroupRowsByMetadata(rows.Skip(1).ToList(), metadataIdx);

            foreach (var kvp in groupedData)
            {
                string metadata = kvp.Key;
                var dataRows = kvp.Value;

                var parts = metadata.Split('/');
                if (parts.Length >= 2)
                {
                    string dirName = parts[0];
                    string fileName = string.Join("/", parts.Skip(1));

                    string outputDir = System.IO.Path.Combine(targetDir, dirName);
                    Directory.CreateDirectory(outputDir);

                    string csvPath = System.IO.Path.Combine(outputDir, fileName);
                    WriteCsvFile(csvPath, headersNoMeta, dataRows);
                }
            }

            System.Windows.MessageBox.Show("Split completed successfully.");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error during split: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    private List<CsvDirInfo> ScanCsvDirectories(string sourceDir)
    {
        var csvDirs = new List<CsvDirInfo>();
        foreach (var item in Directory.GetDirectories(sourceDir))
        {
            string dirName = System.IO.Path.GetFileName(item);
            var csvFiles = Directory.GetFiles(item, "*.csv").Select(f => System.IO.Path.GetFileName(f)).ToList();
            if (csvFiles.Count > 0)
            {
                csvDirs.Add(new CsvDirInfo { DirName = dirName, DirPath = item, CsvFiles = csvFiles });
            }
        }
        return csvDirs;
    }

    private List<List<string>> ReadCsvFile(string path)
    {
        var rows = new List<List<string>>();
        using (var reader = new StreamReader(path, Encoding.UTF8))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            while (csv.Read())
            {
                var row = new List<string>();
                for (int i = 0; csv.TryGetField(i, out string field); i++)
                {
                    row.Add(field);
                }
                rows.Add(row);
            }
        }
        return rows;
    }

    private List<List<string>> ReadTsvFile(string path)
    {
        var rows = new List<List<string>>();
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = "\t",
            HasHeaderRecord = false
        };
        using (var reader = new StreamReader(path, Encoding.UTF8))
        using (var csv = new CsvReader(reader, config))
        {
            while (csv.Read())
            {
                var row = new List<string>();
                for (int i = 0; csv.TryGetField(i, out string field); i++)
                {
                    row.Add(field);
                }
                rows.Add(row);
            }
        }
        return rows;
    }

    private void WriteTsvFile(string path, List<List<string>> rows)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = "\t",
            HasHeaderRecord = false
        };
        using (var writer = new StreamWriter(path, false, Encoding.UTF8))
        using (var csv = new CsvWriter(writer, config))
        {
            foreach (var row in rows)
            {
                foreach (var field in row)
                {
                    csv.WriteField(field);
                }
                csv.NextRecord();
            }
        }
    }

    private void WriteCsvFile(string path, List<string> headers, List<List<string>> rows)
    {
        using (var writer = new StreamWriter(path, false, Encoding.UTF8))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            // Write headers
            foreach (var header in headers)
            {
                csv.WriteField(header);
            }
            csv.NextRecord();

            // Write rows
            foreach (var row in rows)
            {
                foreach (var field in row)
                {
                    csv.WriteField(field);
                }
                csv.NextRecord();
            }
        }
    }


    private Dictionary<string, List<List<string>>> GroupRowsByMetadata(List<List<string>> rows, int metadataIdx)
    {
        var groupedData = new Dictionary<string, List<List<string>>>();
        foreach (var row in rows)
        {
            string metadata = row[metadataIdx];
            var rowNoMeta = row.Take(metadataIdx).ToList();
            // Unescape
            var unescapedRow = new List<string>();
            foreach (var cell in rowNoMeta)
            {
                string unescapedCell = cell.Replace("\\n", "\n");
                unescapedRow.Add(unescapedCell);
            }

            if (!groupedData.ContainsKey(metadata))
            {
                groupedData[metadata] = new List<List<string>>();
            }
            groupedData[metadata].Add(unescapedRow);
        }
        return groupedData;
    }

    private List<List<string>> EscapeLineBreaks(List<List<string>> rows)
    {
        var escapedRows = new List<List<string>>();
        foreach (var row in rows)
        {
            var escapedRow = new List<string>();
            foreach (var cell in row)
            {
                string escapedCell = cell.Replace("\n", "\\n").Replace("\r", "");
                escapedRow.Add(escapedCell);
            }
            escapedRows.Add(escapedRow);
        }
        return escapedRows;
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class CsvDirInfo
{
    public string DirName { get; set; } = "";
    public string DirPath { get; set; } = "";
    public List<string> CsvFiles { get; set; } = new List<string>();
}