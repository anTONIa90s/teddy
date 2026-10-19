using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TeddyBench.Properties;
using TonieFile;
using Application = System.Windows.Forms.Application;

namespace TeddyBench
{
    public partial class TeddyMain
    {
private void StartThreads()
        {
            ScanCardThreadStop = false;
            ScanCardThread = new SafeThread(ScanCardMain, "ScanCardThread");
            ScanCardThread.Start();


            new SafeThread(() =>
            {
                LoadJson();
            }, "JSON Downloader").Start();
        }

        private void ScanCardMain()
        {
            while (!ScanCardThreadStop)
            {
                try
                {
                    var drives = DriveInfo.GetDrives();
                    bool cardFound = false;

                    foreach (var drive in drives)
                    {
                        if (drive.DriveType == DriveType.Removable)
                        {
                            try
                            {
                                if (drive.IsReady && drive.RootDirectory.GetDirectories().Where(d => d.Name == "CONTENT").Count() == 1)
                                {
                                    cardFound = true;
                                    NotifyDrive(drive);
                                }
                            }
                            catch (Exception ex)
                            {
                            }
                        }
                    }

                    if (!cardFound && CurrentDirectory == null)
                    {
                        NotifyNoDrive();
                    }

                    if (CurrentDirectory != null)
                    {
                        bool failedRead = false;

                        try
                        {
                            if (!new DirectoryInfo(CurrentDirectory).Exists)
                            {
                                failedRead = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            failedRead = true;
                        }

                        if (failedRead)
                        {
                            NotifyDriveLost();
                        }
                    }
                }
                catch (Exception ex)
                {
                }

                Thread.Sleep(500);
            }
        }

        private void NotifyDriveLost()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => { NotifyDriveLost(); }));
                return;
            }
            grpCardContent.Visible = false;
            lblMessage.Visible = true;
            CurrentDirectory = null;
            AutoOpenDrive = true;
            Text = TitleString;
            StopAnalyzeThread();
        }

        private void NotifyDrive(DriveInfo drive)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => { NotifyDrive(drive); }));
                return;
            }

            if (AutoOpenDrive)
            {
                try
                {
                    OpenPath(Path.Combine(drive.RootDirectory.FullName, "CONTENT"));
                    AutoOpenDrive = false;
                }
                catch (Exception ex)
                {
                    LogWindow.Log(LogWindow.eLogLevel.Error, "Wanted to open CONTENT on " + drive.RootDirectory + " but that caused a: " + ex.Message);
                    Thread.Sleep(1000);
                }
            }
        }

        private void NotifyNoDrive()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => { NotifyNoDrive(); }));
                return;
            }

            /* no SD card found: fall back to the backup directory, a card inserted later still takes over (AutoOpenDrive stays true) */
            if (CurrentDirectory == null && AutoOpenDrive && !string.IsNullOrEmpty(Settings.BackupDirectory) && Directory.Exists(Settings.BackupDirectory))
            {
                OpenPath(Settings.BackupDirectory);
            }
        }

        private void openDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog();

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                OpenPath(dlg.SelectedPath);
            }
        }

        private void pickBackupDirectoryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            dlg.Description = "Directory to open when no SD card with a CONTENT folder is found";

            if (!string.IsNullOrEmpty(Settings.BackupDirectory) && Directory.Exists(Settings.BackupDirectory))
            {
                dlg.SelectedPath = Settings.BackupDirectory;
            }

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                Settings.BackupDirectory = dlg.SelectedPath;
                SaveSettings();

                if (CurrentDirectory == null)
                {
                    OpenPath(Settings.BackupDirectory);
                }
            }
        }

        private void OpenPath(string rootDirectory)
        {
            if (!Directory.Exists(rootDirectory))
            {
                CurrentDirectory = null;
                return;
            }

            CurrentDirectory = rootDirectory;

            FileSystemWatcher watcher = new FileSystemWatcher(CurrentDirectory);

            watcher.Changed += OnDirectoryChanged;
            watcher.Created += OnDirectoryChanged;
            watcher.Deleted += OnDirectoryChanged;
            watcher.Renamed += OnDirectoryChanged;

            watcher.EnableRaisingEvents = true;
            RefreshCardContent();
        }

        private void OnDirectoryChanged(object sender, FileSystemEventArgs e)
        {
            RefreshCardContent();
        }

        private void RefreshCardContent()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => { RefreshCardContent(); }));
                return;
            }

            StopAnalyzeThread();

            Text = TitleString + " - " + CurrentDirectory;

            lock (RegisteredItems)
            {
                btnAdd.Enabled = true;
                btnDelete.Enabled = true;
                btnSave.Enabled = true;

                txtLog.Visible = false;
                grpCardContent.Visible = true;
                lblMessage.Visible = false;
                lstTonies.Items.Clear();
                RegisteredItems.Clear();

                try
                {
                    /* scan card content */
                    foreach (var dir in new DirectoryInfo(CurrentDirectory).GetDirectories())
                    {
                        var file = dir.GetFiles("*0304E0").FirstOrDefault();

                        if (file != null)
                        {
                            ListViewItem item = new ListViewItem();
                            ListViewTag tag = new ListViewTag(file.FullName);

                            item.Tag = tag;
                            item.Text = tag.Uid;
                            item.ImageKey = "unknown";
                            item.Group = lstTonies.Groups[3];

                            lstTonies.Items.Add(item);

                            RegisteredItems.Add(item.Tag as ListViewTag, item);
                        }
                    }

                    /* scan local directory */
                    foreach(var file in new DirectoryInfo(CurrentDirectory).GetFiles())
                    {
                        Regex reg = new Regex("(?<prod>[0-9]{8}|[0-9]{2}-[0-9]{4}) - [0-9A-F]{8} - (?<name>.*)");
                        var match = reg.Match(file.Name);
                        if (match.Success)
                        {
                            ListViewItem item = new ListViewItem();
                            ListViewTag tag = new ListViewTag(file.FullName);

                            item.Tag = tag;
                            item.Text = match.Groups["prod"].Value + " - " + match.Groups["name"].Value;
                            item.ImageKey = "unknown";
                            item.Group = lstTonies.Groups[3];

                            lstTonies.Items.Add(item);

                            RegisteredItems.Add(item.Tag as ListViewTag, item);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogWindow.Log(LogWindow.eLogLevel.Error, "Exception while scanning directory '" + CurrentDirectory + "':" + ex.Message);
                }
                lstTonies.Sort();
            }

            StartAnalyzeThread();
        }

        private void StartAnalyzeThread()
        {
            AnalyzeThreadStop = false;
            AnalyzeThread = new SafeThread(AnalyzeMain, "AnalyzeThread");
            AnalyzeThread.Start();
        }

        private void StopAnalyzeThread()
        {
            if (AnalyzeThread != null)
            {
                AnalyzeThreadStop = true;
                if(!AnalyzeThread.Join(2000))
                {
                    LogWindow.Log(LogWindow.eLogLevel.Error, "Failed to stop Analyze Thread");
                }
                AnalyzeThread.Abort();
                AnalyzeThread = null;
            }
        }

        private void AnalyzeMain()
        {
            ByteQueue ByteBuffer = new ByteQueue();
            while (!AnalyzeThreadStop)
            {
                Thread.Sleep(100);

                lock (RegisteredItems)
                {
                    LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "Rescanning");
                    foreach (var entry in RegisteredItems)
                    {
                        ListViewTag tag = entry.Key;
                        LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "  Filename: " + entry.Key.FileName);
                        LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "     Image: " + entry.Value.ImageKey);

                        if (entry.Value.ImageKey == "unknown")
                        {
                            try
                            {
                                LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "GetTonieAudio...");
                                TonieAudio dumpFile = GetTonieAudio(tag.FileName);
                                LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "GetTonieAudio... done");
                                string image = "";
                                string hash = BitConverter.ToString(dumpFile.Header.Hash).Replace("-", "");
                                IEnumerable<TonieTools.TonieData> found = new TonieTools.TonieData[0];

                                lock (TonieInfoLock)
                                {
                                    found = TonieInfo.Where(t => t.Hash.Where(h => h == hash).Count() > 0);
                                }
                                string tonieName = "[" + tag.Uid + "]" + Environment.NewLine + "(unknown: " + dumpFile.Header.AudioId.ToString("X8") + ")";

                                bool update = false;

                                if (found.Count() > 0)
                                {
                                    LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "     Found");
                                    var info = found.First();
                                    tag.Info = info;
                                    tonieName = info.Title;

                                    if (!string.IsNullOrEmpty(info.Model))
                                    {
                                        tonieName = info.Model + " - " + tonieName;
                                    }
                                    lock (TonieInfoLock)
                                    {
                                        CustomTonieInfo customInfo;
                                        if (TonieInfoCustom.TryGetValue(hash, out customInfo) && !string.IsNullOrWhiteSpace(customInfo.Title))
                                        {
                                            LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "     known tonie, overriding name");
                                            tonieName = customInfo.Title;
                                        }
                                    }
                                    if (!string.IsNullOrEmpty(info.Pic) && !lstTonies.LargeImageList.Images.ContainsKey(hash))
                                    {
                                        Image img = GetImage(info.Pic, hash);
                                        if (img != null)
                                        {
                                            this.BeginInvoke(new Action(() =>
                                            {
                                                lstTonies.LargeImageList.Images.Add(hash, img);
                                            }));
                                        }
                                    }
                                    update = true;
                                    image = hash;
                                }
                                else
                                {
                                    tag.Info = new TonieTools.TonieData();
                                    image = "unknown";

                                    lock (TonieInfoLock)
                                    {
                                        CustomTonieInfo customInfo;
                                        if (TonieInfoCustom.TryGetValue(hash, out customInfo))
                                        {
                                            LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "     known custom tonie");
                                            if (!string.IsNullOrWhiteSpace(customInfo.Title))
                                            {
                                                tonieName = customInfo.Title;
                                                tag.Info.Title = tonieName;
                                            }
                                            else
                                            {
                                                tonieName = "Unnamed Teddy - " + tonieName;
                                                tag.Info.Title = "Unnamed Teddy";
                                            }
                                            image = "custom";
                                            update = true;
                                        }
                                        else if (dumpFile.Header.AudioId < 0x50000000)
                                        {
                                            LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "     unknown custom tonie");
                                            tonieName = "Unnamed Teddy - " + tonieName;
                                            tag.Info.Title = "Unnamed Teddy";
                                            image = "custom";
                                            update = true;
                                        }
                                        else
                                        {
                                            LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "     Not found -> unknown");
                                        }
                                    }
                                }

                                tag.Hash = hash;
                                tag.AudioId = dumpFile.Header.AudioId;
                                LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "     Hash: " + tag.Hash);
                                LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "     AudioId: " + tag.AudioId);

                                bool live = tag.FileInfo.Attributes.HasFlag(FileAttributes.Hidden);
                                string newText = (live ? "[live] " : "") +  tonieName;
                                string newImakeKey = image;
                                if (newImakeKey == "custom")
                                {
                                    try
                                    {
                                        string customPath = GetCustomImagePath(hash);
                                        if (File.Exists(customPath))
                                        {
                                            string customImageKey = GetCustomImageKey(hash);
                                            if (!lstTonies.LargeImageList.Images.ContainsKey(customImageKey))
                                            {
                                                Image cimg = LoadResizedImage(customPath);
                                                this.BeginInvoke(new Action(() =>
                                                {
                                                    try
                                                    {
                                                        ReplaceImageListImage(customImageKey, cimg);
                                                    }
                                                    catch
                                                    {
                                                        cimg.Dispose();
                                                    }
                                                }));
                                            }

                                            newImakeKey = customImageKey;
                                        }
                                    }
                                    catch { }
                                }
                                string newToolTipText =
                                    "File:     " + tag.FileName + Environment.NewLine +
                                    "Name:     " + tag.Info.Title + Environment.NewLine +
                                    "UID:      " + tag.Uid + Environment.NewLine +
                                    "Date:     " + tag.FileInfo.CreationTime + Environment.NewLine +
                                    "AudioID:  0x" + tag.AudioId.ToString("X8") + Environment.NewLine +
                                    "Chapters: " + dumpFile.Header.AudioChapters.Length + Environment.NewLine;

                                update |= entry.Value.Text != newText;
                                update |= entry.Value.ImageKey != newImakeKey;
                                update |= entry.Value.ToolTipText != newToolTipText;

                                if (update)
                                {
                                    this.BeginInvoke(new Action(() =>
                                    {
                                        LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "     Update list");
                                        entry.Value.Text = newText;
                                        entry.Value.ImageKey = newImakeKey;
                                        entry.Value.ToolTipText = newToolTipText;
                                        UpdateSorting();
                                    }));
                                }
                            }
                            catch (Exception ex)
                            {
                            }
                        }
                    }
                    LogWindow.Log(LogWindow.eLogLevel.DebugVerbose, "Done");
                }
            }
        }
    }
}
