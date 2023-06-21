using FluentFTP;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;


namespace Valheim_Launcher
{
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();
            ProgressLeiste.Dispatcher.Invoke(() =>
            {
                ProgressLeiste.Visibility = Visibility.Hidden;
            });
            checkstatus();
        }

        //Check whats given to enable the right Buttons
        private async void checkstatus()
        {
            string folderPath2 = AppDomain.CurrentDomain.BaseDirectory + "valheim.exe";

            // check Valheim Exe
            if (File.Exists(folderPath2))
            {

                Start.IsEnabled = true;
                FixValheim.Visibility = Visibility.Visible;
                InstallGame.Visibility = Visibility.Hidden;
            }
            else
            {
                Start.IsEnabled = false;
                FixValheim.Visibility = Visibility.Hidden;
                InstallGame.IsEnabled = true;
            }

        }

        //Standart User Interface Methods
        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            Start.IsEnabled = false;
            InstallGame.Visibility = Visibility.Hidden;
            FixValheim.Visibility = Visibility.Hidden;
            string serverUri = "127.0.0.1";
            string ftpUserName = "yourFTPuser";
            string ftpPassword = "yourFTPPasswortd";
            string localFolderPath = Environment.CurrentDirectory + "/BepInEx";

            bool resultFTPFiles = false;
            FtpClient client = new FtpClient(serverUri, ftpUserName, ftpPassword);

            try
            {

                //Delete /BepInEx/config for clean Download - Deaktivated for testing. If Client got problems with Config files on load, aktivat it.
                //await Task.Run(() =>
                //{
                //    cleanConfig();
                //}).ConfigureAwait(true);

                Label.Content = "Überprüfe Installierte Mods!";

                resultFTPFiles = await Task.Run(() =>
                {
                    return DownloadFtpDirectory(serverUri, ftpUserName, ftpPassword);
                }).ConfigureAwait(true);

            }
            catch (Exception ex)
            {
                MessageBox.Show("Fehlercode: " + ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);

            }
            await start_process();

        }

        private async Task start_process()
        {

            var localDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BepInEx");
            var downloadresult = false;

            if (Label != null)
            {
                Label.Dispatcher.Invoke(() =>
                {
                    Label.Content = "Starte das Spiel!";
                });
            }

            string steamPath = GetSteamPath();
            if (steamPath != null)
            {
                if (!IsProcessRunning("steam"))
                {
                    Process steamProcess = Process.Start(steamPath);
                    steamProcess.WaitForInputIdle();
                }

                while (!IsProcessRunning("steam"))
                {
                    Thread.Sleep(100);
                }

                downloadresult = await Task.Run(() =>
                {
                    return downloadNotSyncedConfigs();
                }).ConfigureAwait(true);

                if (downloadresult)
                {
                    Process.Start(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "valheim.exe"));
                    Close();
                }
                else
                {
                    if (Label != null)
                    {
                        Label.Dispatcher.Invoke(() =>
                        {
                            Label.Content = "Spiel konnte nicht gestartet werden! Fehler beim Config Sync!";
                        });
                    }
                }
            }
            else
            {
                MessageBox.Show("Steam wurde nicht gefunden. Stelle sicher, dass Steam installiert ist.");
            }
        }


        //its You need an Directory for config files who not get synced. Most time Singleplayer Mods Like Auga or Mods without synced Method
        private async Task<bool> downloadNotSyncedConfigs()
        {

            string serverUri = "127.0.0.1";
            string ftpUserName = "yourFTPUser";
            string ftpPassword = "yourFTPPassword";
            var localDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BepInEx/config");

            using (var ftpClient = new FtpClient(serverUri, ftpUserName, ftpPassword))
            {
                ftpClient.EncryptionMode = FtpEncryptionMode.None;
                ftpClient.ValidateAnyCertificate = true;
                ftpClient.ReadTimeout = 30000;

                await ftpClient.ConnectAsync();

                //this are Server mod configs needet by our Server, they didnt get synced with AzuAnticheat
                var remotePath = "/config/1NotSyncedWithAntiCheat";
                await ftpClient.DownloadDirectoryAsync(localDirectoryPath, remotePath, FtpFolderSyncMode.Mirror, FtpLocalExists.Overwrite, FtpVerify.Retry);
            }

            return true;
        }

        private bool IsProcessRunning(string processName)
        {
            Process[] processes = Process.GetProcessesByName(processName);
            return processes.Length > 0;
        }

        //get Steampath, its important
        private string GetSteamPath()
        {
            string[] steamRegistryKeys = {
        @"HKEY_CURRENT_USER\Software\Valve\Steam",
        @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam"
    };

            foreach (string keyPath in steamRegistryKeys)
            {
                string steamPath = (string)Registry.GetValue(keyPath, "SteamPath", null);
                if (!string.IsNullOrEmpty(steamPath))
                {
                    return Path.Combine(steamPath, "Steam.exe");
                }
            }

            return null;
        }

        //close the window?
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        //cleans the Config Directory befor starting Valheim
        //private void cleanConfig()
        //{
        //    string configFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BepInEx", "Config");

        //    if (Directory.Exists(configFolderPath))
        //    {
        //        Directory.Delete(configFolderPath, true);
        //        if (Label != null)
        //        {
        //            Label.Dispatcher.Invoke(() =>
        //            {
        //                Label.Content = "Reinige config Ordner!";
        //            });
        //        }
        //    }
        //    else
        //    {
        //        if (Label != null)
        //        {
        //            Label.Dispatcher.Invoke(() =>
        //            {
        //                Label.Content = "Reinigung des config  Ordners nicht nötig!";
        //            });
        //        }

        //    }
        //}

        //Main Funktions to create DB and Handling Main Download. Using Rekursiv FTP searching to get all Data from FTP
        private async Task<bool> DownloadFtpDirectory(string serverUri, string ftpUserName, string ftpPassword)
        {

            ProgressLeiste.Dispatcher.Invoke(() =>
            {
                ProgressLeiste.Visibility = Visibility.Visible;
            });

            //Only if you use AzuAnticheat, so the client didnt download it. 
            var localDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BepInEx");
            var foldersToSkip = new[] { "AzuAntiCheat_Whitelist", "AzuAntiCheat_Greylist" };

            // Create Directory for Optional Mods "/plugins/optionalMods" if isnt exist
            var destinationFolder = Path.Combine(localDirectoryPath, "plugins", "1AditionalMods");
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            // Move the OptionalMod Directory to main / bevor he starts downloading to prevent delete
            var sourceFolder = Path.Combine(localDirectoryPath, "plugins", "1AditionalMods");
            var destinationPath = Path.Combine(localDirectoryPath, "1AditionalMods");
            if (Directory.Exists(sourceFolder))
            {
                if (Directory.Exists(destinationPath))
                {
                    Directory.Delete(destinationPath);

                }
                ;
                Directory.Move(sourceFolder, destinationPath);
            }
            var token = new CancellationToken();
            //initialize Download from FTP
            using (var ftpClient = new FtpClient(serverUri, ftpUserName, ftpPassword))
            {
                ftpClient.EncryptionMode = FtpEncryptionMode.None;
                ftpClient.ValidateAnyCertificate = true;
                ftpClient.ReadTimeout = 30000;

                await ftpClient.ConnectAsync();


                // define the progress tracking callback
                Progress<FtpProgress> progress = new Progress<FtpProgress>(p =>
                {


                    if (p.Progress == 1)
                    {
                        // all done!
                        ProgressLeiste.Dispatcher.Invoke(() =>
                        {
                            ProgressLeiste.Value = 100;
                        });
                    }
                    else
                    {

                        // percent done = (p.Progress * 100)
                        ProgressLeiste.Dispatcher.Invoke(() =>
                        {
                            Label.Content = "Downloade: " + Path.GetFileName(p.LocalPath);
                            ProgressLeiste.Value = p.Progress * 100;
                        });
                    }


                });

                if (Label != null)
                {
                    Label.Dispatcher.Invoke(() =>
                    {
                        Label.Content = "Überprüfe Patcher!";
                    });
                }

                var remotePath = "/patchers";
                await DownloadRecursiveAsync(ftpClient, localDirectoryPath + remotePath, remotePath, progress, token);

                if (Label != null)
                {
                    Label.Dispatcher.Invoke(() =>
                    {
                        Label.Content = "Überprüfe Mods!";
                    });
                }

                remotePath = "/plugins";
                await DownloadRecursiveAsync(ftpClient, localDirectoryPath + remotePath, remotePath, progress, token);
            }

            // Compare local files with FTP server and delete local files not found on the server
            await DeleteLocalFilesNotOnFTPServer(serverUri, ftpUserName, ftpPassword);

            if (Directory.Exists(destinationPath))
            {
                Directory.Move(destinationPath, sourceFolder);
            }

            if (Directory.Exists(destinationPath))
            {
                Directory.Delete(destinationPath);
            }

            ProgressLeiste.Dispatcher.Invoke(() =>
            {
                ProgressLeiste.Visibility = Visibility.Hidden;
            });

            return Directory.Exists(localDirectoryPath);
        }

        // IMPORTANT! This Download gets your Files for Game Files and Mods. Its checks for Filesize only because some FTP Servers didnt got all Features for Checksum
        private async Task DownloadRecursiveAsync(FtpClient ftpClient, string localDirectoryPath, string remoteDirectoryPath, IProgress<FtpProgress> progress, CancellationToken token)
        {
            if (!Directory.Exists(localDirectoryPath))
                Directory.CreateDirectory(localDirectoryPath);

            var files = await ftpClient.GetListingAsync(remoteDirectoryPath, FtpListOption.Recursive);

            foreach (var file in files)
            {
                if (file.Type == FtpFileSystemObjectType.File)
                {
                    var relativePath = file.FullName.Substring(remoteDirectoryPath.Length).TrimStart('/');
                    var localFilePath = Path.Combine(localDirectoryPath, relativePath);
                    var remoteFilePath = file.FullName;

                    // Ensure the subdirectories exist
                    var localFileDirectory = Path.GetDirectoryName(localFilePath);
                    if (!Directory.Exists(localFileDirectory))
                        Directory.CreateDirectory(localFileDirectory);


                    // Get the checksum and file size of the remote file
                    var hash = ftpClient.CompareFile(localFilePath, remoteFilePath, FtpCompareOption.Size);
                    // Compare the checksums and file sizes
                    if (hash.ToString() == "Equal")
                    {
                        // The files are the same, so no need to download them
                    }
                    else
                    {
                        // The files are different, so download the remote file
                        await ftpClient.DownloadFileAsync(localFilePath, remoteFilePath, FtpLocalExists.Overwrite, FtpVerify.None, progress, token);
                        ProgressLeiste.Dispatcher.Invoke(() =>
                        {
                            Label.Content = "Überprüfe weiter!";
                            ProgressLeiste.Value = 0;
                        });
                    }
                }
            }
        }


        //Check for Plugins or Old Files who are not on Server Anymore, Most time because you delete an Old Mod from FTP Server.
        private async Task DeleteLocalFilesNotOnFTPServer(string serverUri, string ftpUserName, string ftpPassword)
        {
            using (var ftpClient = new FtpClient(serverUri, ftpUserName, ftpPassword))
            {
                ftpClient.EncryptionMode = FtpEncryptionMode.None;
                ftpClient.ValidateAnyCertificate = true;
                ftpClient.ReadTimeout = 30000;

                await ftpClient.ConnectAsync().ConfigureAwait(true);

                // Get the list of files and directories on the FTP server
                var ftpListing = await ftpClient.GetListingAsync("/plugins");
                var localDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BepInEx/plugins");
                // Get the list of files and directories on the local system
                var localListing = new DirectoryInfo(localDirectoryPath).GetFileSystemInfos();

                // Compare the local and FTP server listings
                foreach (var localItem in localListing)
                {
                    if (!ftpListing.Any(ftpItem => ftpItem.Name == localItem.Name))
                    {
                        // Delete local item if it's not found on the FTP server
                        if (localItem is FileInfo fileInfo)
                        {
                            fileInfo.Delete();
                        }
                        else if (localItem is DirectoryInfo directoryInfo)
                        {
                            directoryInfo.Delete(true);
                        }

                    }
                }

                ftpClient.Disconnect();
            }
        }

        //Checking FTP for Missing Files! If someone missing he saved the info in DB and after this he deletes lokal files.

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        //This is for GameFiles Install Only From your FTP!

        private async void InstallGame_Click(object sender, RoutedEventArgs e)
        {
            InstallGame.Visibility = Visibility.Hidden;
            Label.Content = "Das Hauptspiel wird geladen! Die Größe beträgt 1,4GB!";

            await Task.Run(async () =>
            {
                await installer();
            });
            checkstatus();
            Label.Content = "Das Hauptspiel wurde fertig geladen";
        }
        private async void FixValheim_Click(object sender, RoutedEventArgs e)
        {
            FixValheim.Visibility = Visibility.Hidden;
            Start.IsEnabled = false;
            Label.Content = "Überprüfe vorhandene Daten auf Fehler!";

            await Task.Run(async () =>
            {
                await installer();
            });
            checkstatus();
            Label.Content = "Überprüfung beendet, bereit zum Starten!";

        }

        private async Task installer()
        {
            //Here you can add your Important Valheim Game Files, if you dont wanna use Steam Installed Version.
            //On my Server i Have Valheim and BepInEx together in an Directory

            string serverUri = "127.0.0.1";
            string ftpPassword = "yourFTPpw";
            var ftpUserName = "yourFTPusername";
            var remotePath = "/"; // Make Sure that you have Valheim Game Files + BepInEx

            var localDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);
            ProgressLeiste.Dispatcher.Invoke(() =>
            {
                ProgressLeiste.Visibility = Visibility.Visible;
            });
            var token = new CancellationToken();
            try
            {
                using (var ftpClient = new FtpClient(serverUri, ftpUserName, ftpPassword))
                {
                    ftpClient.EncryptionMode = FtpEncryptionMode.None;
                    ftpClient.ValidateAnyCertificate = true;
                    ftpClient.ReadTimeout = 30000;

                    await ftpClient.ConnectAsync();

                    // define the progress tracking callback
                    Progress<FtpProgress> progress = new Progress<FtpProgress>(p =>
                    {


                        if (p.Progress == 1)
                        {
                            // all done!
                            ProgressLeiste.Dispatcher.Invoke(() =>
                            {
                                ProgressLeiste.Value = 100;
                            });
                        }
                        else
                        {

                            // percent done = (p.Progress * 100)
                            ProgressLeiste.Dispatcher.Invoke(() =>
                            {
                                Label.Content = "Downloade: " + Path.GetFileName(p.LocalPath);
                                ProgressLeiste.Value = p.Progress * 100;

                            });
                        }


                    });

                    // Herunterladen des gesamten Verzeichnisses
                    await DownloadRecursiveAsync(ftpClient, localDirectoryPath, remotePath, progress, token);
                }

            }
            catch (Exception ex)
            {

            }
            ProgressLeiste.Dispatcher.Invoke(() =>
            {
                ProgressLeiste.Visibility = Visibility.Hidden;
            });
        }
    }
}
