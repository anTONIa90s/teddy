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
            CurrentDirectoryIsCard = false;
            UpdateCompareWatcher(null);
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
                else
                {
                    /* the comparison with the backup directory may have changed */
                    RefreshCardContent();
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
            CurrentDirectoryIsCard = IsCardContentDirectory(rootDirectory);

            if (CardWatcher != null)
            {
                CardWatcher.EnableRaisingEvents = false;
                CardWatcher.Dispose();
                CardWatcher = null;
            }

            FileSystemWatcher watcher = new FileSystemWatcher(CurrentDirectory);

            watcher.Changed += OnDirectoryChanged;
            watcher.Created += OnDirectoryChanged;
            watcher.Deleted += OnDirectoryChanged;
            watcher.Renamed += OnDirectoryChanged;

            watcher.EnableRaisingEvents = true;
            CardWatcher = watcher;
            RefreshCardContent();
        }

        private void OnDirectoryChanged(object sender, FileSystemEventArgs e)
        {
            if (SyncCopyRunning)
            {
                /* the copy refreshes once when it is done */
                return;
            }

            RefreshCardContent();
        }

        private void RefreshCardContent()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => { RefreshCardContent(); }));
                return;
            }

            if (CurrentDirectory == null)
            {
                return;
            }

            StopAnalyzeThread();

            Text = TitleString + " - " + CurrentDirectory;

            /* compare with the backup directory only while a card's CONTENT folder is open */
            string compareDirectory = GetCompareDirectory();
            UpdateCompareWatcher(compareDirectory);

            lock (RegisteredItems)
            {
                btnAdd.Enabled = !SyncCopyRunning;
                btnDelete.Enabled = !SyncCopyRunning;
                btnSave.Enabled = !SyncCopyRunning;

                txtLog.Visible = false;
                grpCardContent.Visible = true;
                lblMessage.Visible = false;
                lstTonies.Items.Clear();
                RegisteredItems.Clear();

                List<ListViewTag> cardTags = ScanTonieDirectory(CurrentDirectory) ?? new List<ListViewTag>();
                List<ListViewTag> backupTags = null;

                if (compareDirectory != null)
                {
                    backupTags = ScanTonieDirectory(compareDirectory);
                    if (backupTags == null)
                    {
                        /* no partial comparison: show the card alone */
                        LogWindow.Log(LogWindow.eLogLevel.Error, "Comparison with backup directory '" + compareDirectory + "' disabled, it could not be scanned.");
                        UpdateCompareWatcher(null);
                    }
                }

                if (backupTags == null)
                {
                    foreach (ListViewTag tag in cardTags)
                    {
                        AddTonieItem(tag);
                    }
                }
                else
                {
                    var cardPaths = new HashSet<string>(cardTags.Select(t => t.RelativePath), StringComparer.OrdinalIgnoreCase);
                    var backupPaths = new HashSet<string>(backupTags.Select(t => t.RelativePath), StringComparer.OrdinalIgnoreCase);

                    foreach (ListViewTag tag in cardTags)
                    {
                        tag.Location = backupPaths.Contains(tag.RelativePath) ? eLocation.Both : eLocation.CardOnly;
                        AddTonieItem(tag);
                    }
                    foreach (ListViewTag tag in backupTags)
                    {
                        if (!cardPaths.Contains(tag.RelativePath))
                        {
                            tag.Location = eLocation.BackupOnly;
                            AddTonieItem(tag);
                        }
                    }
                }
                lstTonies.Sort();
            }

            UpdateCompareControls();
            StartAnalyzeThread();
        }

        /// <summary>
        /// Scans a CONTENT-like directory: "XXXXXXXX\YYYYYYYY" tonie files in subdirectories plus
        /// flat files named "&lt;prod&gt; - &lt;audio id&gt; - &lt;name&gt;" in the root.
        /// </summary>
        private List<ListViewTag> ScanTonieDirectory(string rootDirectory)
        {
            List<ListViewTag> tags = new List<ListViewTag>();

            try
            {
                /* scan card content */
                foreach (var dir in new DirectoryInfo(rootDirectory).GetDirectories())
                {
                    var file = dir.GetFiles("*0304E0").FirstOrDefault();

                    if (file != null)
                    {
                        ListViewTag tag = new ListViewTag(file.FullName);
                        tag.RelativePath = Path.Combine(dir.Name, file.Name);
                        tags.Add(tag);
                    }
                }

                /* scan local directory */
                foreach (var file in new DirectoryInfo(rootDirectory).GetFiles())
                {
                    if (FlatTonieFileRegex.Match(file.Name).Success)
                    {
                        ListViewTag tag = new ListViewTag(file.FullName);
                        tag.RelativePath = file.Name;
                        tags.Add(tag);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWindow.Log(LogWindow.eLogLevel.Error, "Exception while scanning directory '" + rootDirectory + "':" + ex.Message);
                return null;
            }

            return tags;
        }

        private static readonly Regex FlatTonieFileRegex = new Regex("(?<prod>[0-9]{8}|[0-9]{2}-[0-9]{4}) - [0-9A-F]{8} - (?<name>.*)");

        /* caller holds the RegisteredItems lock */
        private void AddTonieItem(ListViewTag tag)
        {
            ListViewItem item = new ListViewItem();
            var match = FlatTonieFileRegex.Match(tag.FileInfo.Name);

            item.Tag = tag;
            item.Text = (tag.RelativePath == tag.FileInfo.Name && match.Success)
                ? match.Groups["prod"].Value + " - " + match.Groups["name"].Value
                : tag.Uid;
            item.ImageKey = "unknown";
            item.Group = GetLocationGroup(tag.Location) ?? lstTonies.Groups[3];

            lstTonies.Items.Add(item);

            RegisteredItems.Add(tag, item);
        }

        #region SD card / backup comparison

        /// <summary>
        /// The backup directory to compare the open directory with, or null when comparison is off.
        /// Comparison is on when the open directory is the CONTENT folder in the root of a removable
        /// drive (detected card or the same folder opened manually) and a backup directory is set,
        /// exists and is not the open directory itself (e.g. the no-card fallback).
        /// </summary>
        private string GetCompareDirectory()
        {
            if (!CurrentDirectoryIsCard || string.IsNullOrEmpty(Settings.BackupDirectory))
            {
                return null;
            }

            try
            {
                if (!Directory.Exists(Settings.BackupDirectory) || IsSamePath(Settings.BackupDirectory, CurrentDirectory))
                {
                    return null;
                }
            }
            catch (Exception)
            {
                return null;
            }

            return Settings.BackupDirectory;
        }

        private static bool IsSamePath(string a, string b)
        {
            string fullA = Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullB = Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return string.Equals(fullA, fullB, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsCardContentDirectory(string path)
        {
            try
            {
                DirectoryInfo dir = new DirectoryInfo(path);

                if (!string.Equals(dir.Name, "CONTENT", StringComparison.OrdinalIgnoreCase) || dir.Parent == null || dir.Parent.Parent != null)
                {
                    return false;
                }

                return new DriveInfo(dir.Root.FullName).DriveType == DriveType.Removable;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /* watches the backup directory while comparing, so changes there show up in the list too */
        private void UpdateCompareWatcher(string compareDirectory)
        {
            if (CompareDirectory != null && compareDirectory != null && IsSamePath(CompareDirectory, compareDirectory) && CompareWatcher != null)
            {
                return;
            }

            if (CompareWatcher != null)
            {
                CompareWatcher.EnableRaisingEvents = false;
                CompareWatcher.Dispose();
                CompareWatcher = null;
            }

            CompareDirectory = compareDirectory;

            if (CompareDirectory == null)
            {
                return;
            }

            try
            {
                CompareWatcher = new FileSystemWatcher(CompareDirectory);
                CompareWatcher.Changed += OnDirectoryChanged;
                CompareWatcher.Created += OnDirectoryChanged;
                CompareWatcher.Deleted += OnDirectoryChanged;
                CompareWatcher.Renamed += OnDirectoryChanged;
                CompareWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                LogWindow.Log(LogWindow.eLogLevel.Error, "Failed to watch backup directory '" + CompareDirectory + "': " + ex.Message);
                CompareWatcher = null;
            }
        }

        private ListViewGroup GetLocationGroup(eLocation location)
        {
            switch (location)
            {
                case eLocation.Both:
                    return GroupOnBoth;
                case eLocation.CardOnly:
                    return GroupOnlyOnCard;
                case eLocation.BackupOnly:
                    return GroupOnlyOnBackup;
            }
            return null;
        }

        private static bool IsBackupOnlyItem(ListViewItem item)
        {
            return (item?.Tag as ListViewTag)?.Location == eLocation.BackupOnly;
        }

        private List<ListViewTag> GetTagsWithLocation(IEnumerable items, eLocation location)
        {
            return items.Cast<ListViewItem>()
                .Select(i => i.Tag as ListViewTag)
                .Where(t => t != null && t.Location == location)
                .ToList();
        }

        /* flat files in the backup root have no CONTENT layout, they are not copied onto the card */
        private static bool IsFlatRootFile(ListViewTag tag)
        {
            return string.Equals(tag.RelativePath, Path.GetFileName(tag.RelativePath), StringComparison.OrdinalIgnoreCase);
        }

        private List<ListViewTag> GetCopyToCardTags(IEnumerable items)
        {
            return GetTagsWithLocation(items, eLocation.BackupOnly).Where(t => !IsFlatRootFile(t)).ToList();
        }

        /* shows/enables the copy buttons and the group headers (with counts) for the comparison */
        private void UpdateCompareControls()
        {
            bool compare = CompareDirectory != null;
            int both = GetTagsWithLocation(lstTonies.Items, eLocation.Both).Count;
            int cardOnly = GetTagsWithLocation(lstTonies.Items, eLocation.CardOnly).Count;
            int backupOnly = GetCopyToCardTags(lstTonies.Items).Count;
            bool copying = SyncCopyRunning;

            GroupOnBoth.Header = "On SD card and backup (" + both + ")";
            GroupOnlyOnCard.Header = "Only on SD card (" + cardOnly + ")";
            GroupOnlyOnBackup.Header = "Only on backup (" + backupOnly + ")";

            btnCopyToBackup.Visible = compare;
            btnCopyToCard.Visible = compare;
            btnCopyToBackup.Enabled = compare && !copying && cardOnly > 0;
            btnCopyToCard.Enabled = compare && !copying && backupOnly > 0;
        }

        private void btnCopyToBackup_Click(object sender, EventArgs e)
        {
            CopyMissing(GetTagsWithLocation(lstTonies.Items, eLocation.CardOnly), true);
        }

        private void btnCopyToCard_Click(object sender, EventArgs e)
        {
            CopyMissing(GetCopyToCardTags(lstTonies.Items), false);
        }

        private void copyToBackupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyMissing(GetTagsWithLocation(lstTonies.SelectedItems, eLocation.CardOnly), true);
        }

        private void copyToCardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyMissing(GetCopyToCardTags(lstTonies.SelectedItems), false);
        }

        /// <summary>
        /// Copies tonie files that are missing on the other side: card to backup (toBackup) or backup to card.
        /// Keeps the relative layout, never overwrites an existing file, and carries over the Explorer title
        /// (desktop.ini) into a directory that has none yet. Runs on a background thread.
        /// </summary>
        private void CopyMissing(List<ListViewTag> tags, bool toBackup)
        {
            if (CompareDirectory == null || CurrentDirectory == null || SyncCopyRunning || tags.Count == 0)
            {
                return;
            }

            string targetRoot = toBackup ? CompareDirectory : CurrentDirectory;
            string question = "Copy " + tags.Count + " Tonie file" + (tags.Count == 1 ? "" : "s") +
                (toBackup ? " to the backup directory" : " to the SD card") + " '" + targetRoot + "'?";

            if (MessageBox.Show(question, toBackup ? "Copy to backup" : "Copy to SD card", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            var jobs = tags.Select(t => new Tuple<string, string>(t.FileName, Path.Combine(targetRoot, t.RelativePath))).ToList();

            SyncCopyRunning = true;
            btnAdd.Enabled = false;
            btnDelete.Enabled = false;
            btnSave.Enabled = false;
            UpdateCompareControls();

            SyncCopyThread = new SafeThread(() =>
            {
                int copied = 0;
                List<string> errors = new List<string>();

                foreach (var job in jobs)
                {
                    string source = job.Item1;
                    string target = job.Item2;

                    try
                    {
                        if (File.Exists(target))
                        {
                            errors.Add("'" + target + "' already exists, skipped.");
                            continue;
                        }

                        string targetDir = Path.GetDirectoryName(target);
                        Directory.CreateDirectory(targetDir);
                        File.Copy(source, target, false);

                        /* the hidden attribute is the "live" flag, make sure it is kept */
                        FileAttributes sourceAttributes = File.GetAttributes(source);
                        if (sourceAttributes.HasFlag(FileAttributes.Hidden) && !File.GetAttributes(target).HasFlag(FileAttributes.Hidden))
                        {
                            File.SetAttributes(target, File.GetAttributes(target) | FileAttributes.Hidden);
                        }
                        copied++;

                        /* tonies in subdirectories may carry an Explorer title in desktop.ini */
                        string sourceDir = Path.GetDirectoryName(source);
                        string sourceIni = Path.Combine(sourceDir, DesktopIniFileName);
                        if (!IsSamePath(targetDir, targetRoot) && File.Exists(sourceIni) && !File.Exists(Path.Combine(targetDir, DesktopIniFileName)))
                        {
                            try
                            {
                                WriteDesktopIni(targetDir, ReadDesktopIni(sourceIni));
                            }
                            catch (Exception ex)
                            {
                                errors.Add("'" + target + "' was copied, but its Explorer title could not be copied: " + ex.Message);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add("Failed to copy '" + source + "' to '" + target + "': " + ex.Message);
                    }
                }

                this.BeginInvoke(new Action(() =>
                {
                    SyncCopyRunning = false;

                    string summary = copied + " of " + jobs.Count + " Tonie file" + (jobs.Count == 1 ? " was" : "s were") + " copied.";
                    if (errors.Count > 0)
                    {
                        summary += Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, errors);
                    }
                    MessageBox.Show(summary, toBackup ? "Copy to backup" : "Copy to SD card", MessageBoxButtons.OK, errors.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

                    RefreshCardContent();
                }));
            }, "CopyThread");
            SyncCopyThread.Start();
        }

        #endregion

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
