﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using Unicorn.VS.Annotations;
using Unicorn.VS.Data;
using Unicorn.VS.Helpers;
using Unicorn.VS.Types;

namespace Unicorn.VS.Models
{
    public class UnicornConnectionViewModel : BaseViewModel
    {

        private UnicornConnection _connection;
        private bool _isActive;

        public UnicornConnectionViewModel()
        {
            _connection = new UnicornConnection();
            TestConnection = new DelagateCommand(ExecuteTestConnection, () => !IsActive);
            Save = new DelagateCommand(ExecuteSave, () => !IsActive);
            Install = new DelagateCommand(ExecuteInstall, () => !IsActive);
            DownloadPackage = new DelagateCommand(ExecuteDownloadPackage, () => !IsActive);
        }

        public UnicornConnection Connection
        {
            get { return _connection; }
            set { _connection = value;
                OnPropertyChanged();
            }
        }

        public bool IsActive
        {
            get { return _isActive; }
            set
            {
                _isActive = value;
                OnPropertyChanged();
            }
        }

        public ICommand TestConnection { get; set; }

        public ICommand Save { get; set; }

        public ICommand Install { get; set; }

        public ICommand DownloadPackage { get; set; }

        public Action<bool> Close { get; set; }

        public string InstallTitle => Connection.IsUpdateRequired ? "Update" : "Install";

        public void SetData(UnicornConnection data)
        {
            Connection = data; 
            OnPropertyChanged("InstallTitle");
        }

        private async void ExecuteTestConnection()
        {
            try
            {
                IsActive = true;
                await Handshake();
            }
            catch(Exception ex)
            {
                MessageBox.Show("Ooops..." + ex.Message, "Connection test", MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
            }
            finally
            {
                IsActive = false;
            }
        }

        private void ExecuteSave()
        {
            if (string.IsNullOrEmpty(Connection.Error))
                Close(true);
        }

        private async void ExecuteInstall()
        {
            using (var folderBrowser = new FolderBrowserDialog())
            {
                try
                {
                    if (folderBrowser.ShowDialog() != DialogResult.OK)
                        return;
                    IsActive = true;
                    var binPath = Path.Combine(folderBrowser.SelectedPath, "bin");
                    var configPath = Path.Combine(folderBrowser.SelectedPath, "App_Config\\Include");
                    if (!Directory.Exists(binPath))
                        Directory.CreateDirectory(binPath);

                    var unicornPath = Path.Combine(binPath, "Unicorn.dll");
                    if (File.Exists(unicornPath))
                    {
                        var versionInfo = FileVersionInfo.GetVersionInfo(unicornPath);
                        var isUnicornRemoteIntegrated = VersionHelper.IsSupportedUnicornVersion(versionInfo.FileVersion);
                        if (isUnicornRemoteIntegrated)
                        {
                            MessageBox.Show(
                                $"Unicorn {versionInfo.FileVersion} supports Remote out of the box, no need to install external library.",
                                "Install canceled", MessageBoxButtons.OK, MessageBoxIcon.Hand);
                            return;
                        }
                    }

                    using (var wStream = File.OpenWrite(Path.Combine(binPath, "Unicorn.Remote.dll")))
                    {
                        var unicornRemote = Resources.Unicorn_Remote;
                        await wStream.WriteAsync(unicornRemote, 0, unicornRemote.Length);
                    }

                    if (!Directory.Exists(configPath))
                        Directory.CreateDirectory(configPath);

                    using (var wStream = File.OpenWrite(Path.Combine(binPath, Path.Combine(configPath, "Unicorn.Remote.config"))))
                    {
                        var unicornRemoteConfig = Encoding.UTF8.GetBytes(Resources.Unicorn_Remote_Config);
                        await wStream.WriteAsync(unicornRemoteConfig, 0, unicornRemoteConfig.Length);
                    }
                    await Handshake();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ooops... " + ex.Message, "Installing Unicorn Remote", MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                finally
                {
                    IsActive = false;
                }
            }
        }

        private async Task Handshake()
        {
            var endPoint = Connection.Get(HttpHelper.HandshakeCommand).Build();
            using (var client = Connection.CreateClient())
            {
                var response = await client.GetAsync(endPoint, CancellationToken.None);
                _connection.IsUpdateRequired = response.IsUpdateRequired();
                if (response.IsSuccessStatusCode)
                    MessageBox.Show("All good!", "Connection test", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    MessageBox.Show("Ooops..." + response.ReasonPhrase, "Connection test", MessageBoxButtons.OK,
                        MessageBoxIcon.Exclamation);

            }
        }

        private void ExecuteDownloadPackage()
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                try
                {
                    IsActive = true;
                    saveFileDialog.FileName = $"UnicornRemote-{VersionHelper.SupportedClientVersion}.zip";
                    saveFileDialog.DefaultExt = ".zip";
                    if (saveFileDialog.ShowDialog() != DialogResult.OK)
                        return;
                    var bytes = (byte[]) Resources.ResourceManager.GetObject("UnicornRemote_1_1");
                    using (var stream = saveFileDialog.OpenFile())
                    {
                        stream.Write(bytes, 0, bytes.Length);
                    }

                    Process.Start("explorer", Path.GetDirectoryName(saveFileDialog.FileName));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ooops... " + ex.Message, "Downloading Unicorn Remote package", MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                finally
                {
                    IsActive = false;
                }
            }
        }
    }
}
