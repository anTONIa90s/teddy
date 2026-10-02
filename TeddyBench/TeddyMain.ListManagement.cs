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
private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutDialog dlg = new AboutDialog();
            dlg.ShowDialog();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            DeleteSelected();
        }



        #region Context menu for tonie files


        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (LastSelectediItem != null/* && LastSelectediItem.ImageKey == "custom"*/)
            {
                LastSelectediItem.BeginEdit();
            }
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DeleteSelected();
        }

        private void exportTooggToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveSelected();
        }

        private void exportToToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExportSelected();
        }

        private void assignNewUIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ReassignSelected();
        }

        private void setCustomImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (LastSelectediItem == null || !IsCustomTonieItem(LastSelectediItem))
            {
                return;
            }

            ListViewTag tag = LastSelectediItem.Tag as ListViewTag;
            if (tag == null || string.IsNullOrEmpty(tag.Hash))
            {
                MessageBox.Show("Please wait until this custom Tonie has finished loading.", "Set custom image");
                return;
            }

            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files (*.*)|*.*";
            dlg.Title = "Select custom image";

            if (dlg.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(CustomImageFolder);

                using (Image resized = LoadResizedImage(dlg.FileName))
                {
                    resized.Save(GetCustomImagePath(tag.Hash), ImageFormat.Png);
                    ReplaceImageListImage(GetCustomImageKey(tag.Hash), (Image)resized.Clone());
                }

                LastSelectediItem.ImageKey = GetCustomImageKey(tag.Hash);
                UpdateSorting();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to set custom image: " + ex.Message, "Set custom image failed");
            }
        }

        private void resetCustomImageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (LastSelectediItem == null || !IsCustomTonieItem(LastSelectediItem))
            {
                return;
            }

            ListViewTag tag = LastSelectediItem.Tag as ListViewTag;
            if (tag == null || string.IsNullOrEmpty(tag.Hash))
            {
                return;
            }

            try
            {
                string customImagePath = GetCustomImagePath(tag.Hash);
                if (File.Exists(customImagePath))
                {
                    File.Delete(customImagePath);
                }

                string customImageKey = GetCustomImageKey(tag.Hash);
                LastSelectediItem.ImageKey = "custom";
                if (lstTonies.LargeImageList.Images.ContainsKey(customImageKey))
                {
                    lstTonies.LargeImageList.Images.RemoveByKey(customImageKey);
                }

                UpdateSorting();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to reset custom image: " + ex.Message, "Reset custom image failed");
            }
        }

        private void changeChapterInformationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (LastSelectediItem == null || !IsCustomTonieItem(LastSelectediItem))
            {
                return;
            }

            ListViewTag tag = LastSelectediItem.Tag as ListViewTag;
            if (tag == null || string.IsNullOrEmpty(tag.Hash))
            {
                MessageBox.Show("Please wait until this custom Tonie has finished loading.", "Change chapter information");
                return;
            }

            TonieAudio audio = GetTonieAudio(tag.FileName);
            string[] currentTitles = GetCustomChapterTitles(tag.Hash);
            using (var dialog = new ChapterEditDialog(tag.Info?.Title, audio.Header.AudioChapters.Length, currentTitles))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                lock (TonieInfoLock)
                {
                    CustomTonieInfo info = GetOrCreateCustomTonieInfo(tag.Hash);
                    int titleCount = Math.Max(audio.Header.AudioChapters.Length, info.Chapters?.Length ?? 0);
                    string[] titles = new string[titleCount];
                    if (info.Chapters != null)
                    {
                        Array.Copy(info.Chapters, titles, info.Chapters.Length);
                    }

                    foreach (KeyValuePair<int, string> title in dialog.EnteredTitles)
                    {
                        titles[title.Key] = title.Value;
                    }
                    info.Chapters = titles;
                }
                SaveJson();
            }
        }

        private void showInExplorerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (LastSelectediItem != null)
            {
                ListViewTag tag = LastSelectediItem.Tag as ListViewTag;
                System.Diagnostics.Process.Start("explorer.exe", "/select, \"" + tag.FileName + "\"");
            }
        }

        private async void sendDiagnosticsReportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ReportSelected();
        }

        private void UpdateTonieContextMenu()
        {
            bool isCustom = IsCustomTonieItem(LastSelectediItem);
            setCustomImageToolStripMenuItem.Visible = isCustom;
            setCustomImageToolStripMenuItem.Enabled = isCustom;
            resetCustomImageToolStripMenuItem.Visible = isCustom;
            changeChapterInformationToolStripMenuItem.Visible = isCustom;
            changeChapterInformationToolStripMenuItem.Enabled = isCustom;

            ListViewTag tag = LastSelectediItem?.Tag as ListViewTag;
            resetCustomImageToolStripMenuItem.Enabled =
                isCustom &&
                tag != null &&
                !string.IsNullOrEmpty(tag.Hash) &&
                File.Exists(GetCustomImagePath(tag.Hash));
        }

        #endregion

        #region ListView callbacks

        private void lstTonies_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                ListViewItem clickedItem = lstTonies.GetItemAt(e.X, e.Y);
                if (clickedItem != null)
                {
                    clickedItem.Selected = true;
                    clickedItem.Focused = true;
                    LastSelectediItem = clickedItem;
                    UpdateTonieContextMenu();
                    TonieContextMenu.Show(System.Windows.Forms.Cursor.Position);
                }
            }
            else
            {
                if (lstTonies.SelectedItems.Count == 1)
                {
                    if (lstTonies.SelectedItems[0] == LastSelectediItem)
                    {
                        if (lstTonies.SelectedItems[0].ImageKey == "custom")
                        {
                            LastSelectediItem.BeginEdit();
                        }
                    }
                    LastSelectediItem = lstTonies.SelectedItems[0];
                }
            }
        }

        private void lstTonies_DoubleClick(object sender, EventArgs e)
        {
            ReassignSelected();
        }

        private void lstTonies_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyData == Keys.F2 && lstTonies.SelectedItems.Count > 0)
            {
                if (lstTonies.SelectedItems[0].ImageKey == "custom")
                {
                    lstTonies.SelectedItems[0].BeginEdit();
                }
            }
            if (e.Control && e.KeyCode == Keys.A)
            {
                foreach (ListViewItem item in lstTonies.Items)
                {
                    item.Selected = true;
                }
            }
        }

        private void lstTonies_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (!e.IsSelected)
            {
                LastSelectediItem = null;
            }
            AutoSelected = false;
        }

        private void lstTonies_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            ListViewItem item = lstTonies.Items[e.Item];
            ListViewTag tag = item.Tag as ListViewTag;

            if (e.Label == null)
            {
                return;
            }

            lock (TonieInfoLock)
            {
                GetOrCreateCustomTonieInfo(tag.Hash).Title = e.Label;
            }

            SaveJson();

            string directoryPath = tag.FileInfo.Directory.FullName;
            if (!HasTeddyBenchExplorerTitle(directoryPath))
            {
                return;
            }

            string error;
            bool updated = string.IsNullOrWhiteSpace(e.Label)
                ? TryRemoveExplorerTitle(directoryPath, out error)
                : TrySetExplorerTitle(directoryPath, e.Label, out error);
            if (!updated)
            {
                MessageBox.Show("The Tonie name was saved, but its Explorer title could not be updated: " + error, "Explorer title");
            }
        }

        #endregion


        #region Selected item commands

        private void ReassignSelected()
        {
            if (lstTonies.SelectedItems.Count == 1)
            {
                ListViewItem item = lstTonies.SelectedItems[0];

                ListViewTag tag = item.Tag as ListViewTag;
                var fi = new FileInfo(tag.FileName);
                string oldUid = ReverseUid(fi.Directory.Name + fi.Name);

                AskUIDForm dlg = new AskUIDForm(RfidReader);
                dlg.Uid = oldUid;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    string uid = dlg.Uid;
                    string newDir = Path.Combine(CurrentDirectory, ReverseUid(uid).Substring(0, 8));
                    string newFile = Path.Combine(newDir, ReverseUid(uid).Substring(8, 8));

                    if (new FileInfo(newFile).FullName == fi.FullName)
                    {
                        return;
                    }

                    if (new FileInfo(newFile).Exists)
                    {
                        MessageBox.Show("Failed to assign UID '" + uid + "' as this file already exists", "Re-assigning UID failed");
                        return;
                    }

                    try
                    {
                        Directory.CreateDirectory(newDir);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to create directory '" + newDir + "'", "Re-assigning UID failed");
                        return;
                    }

                    try
                    {
                        DirectoryInfo oldDir = fi.Directory;
                        fi.MoveTo(newFile);
                        if (oldDir.EnumerateFiles().Count() != 0 || (oldDir.EnumerateDirectories().Count() != 0))
                        {
                            MessageBox.Show("Success. Will not delete directory '" + oldDir.FullName + "' as it is not empty.", "Successfully re-assigned UID");
                        }
                        else
                        {
                            oldDir.Delete();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to write file '" + newFile + "'", "Re-assigning UID failed");
                        return;
                    }

                    RefreshCardContent();
                }
            }
        }

        private void DeleteSelected()
        {
            bool deleteAll = false;

            StopAnalyzeThread();

            if (lstTonies.SelectedItems.Count > 1)
            {
                switch (MessageBox.Show("Delete all selected files?", "Delete files?", MessageBoxButtons.YesNoCancel))
                {
                    case DialogResult.Cancel:
                        return;
                    case DialogResult.Yes:
                        deleteAll = true;
                        break;
                    case DialogResult.No:
                        break;
                }
            }

            foreach (ListViewItem item in lstTonies.SelectedItems)
            {
                ListViewTag tag = item.Tag as ListViewTag;
                string current = item.Text;

                if (!deleteAll)
                {
                    if (MessageBox.Show("Delete '" + current + "'?", "Delete files?", MessageBoxButtons.YesNo) == DialogResult.No)
                    {
                        continue;
                    }
                }
                FileInfo info = new FileInfo(tag.FileName);
                try
                {
                    info.Delete();
                    info.Directory.Delete();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to delete file/directory '" + tag.FileName + "'");
                    return;
                }
            }

            StartAnalyzeThread();
        }

        private void SaveSelected()
        {
            bool saveAll = true;
            bool createDirs = false;

            switch (MessageBox.Show("Create directories for selected file(s)?", "Save audio content", MessageBoxButtons.YesNoCancel))
            {
                case DialogResult.Cancel:
                    return;
                case DialogResult.Yes:
                    createDirs = true;
                    break;
                case DialogResult.No:
                    break;
            }

            FolderBrowserDialog dlg = new FolderBrowserDialog();

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                string outputLocation = dlg.SelectedPath;

                MessageBox.Show("You are about to export audio content of the tonie files on your SD card. " +
                    "Please *do not* share these files as they could contain information which can be used to identify your tonie ID. " +
                    "Also be aware that sharing these files is most likely illegal in your country.", "Legal information", MessageBoxButtons.OK);

                foreach (ListViewItem item in lstTonies.SelectedItems)
                {
                    ListViewTag tag = item.Tag as ListViewTag;
                    string current = item.Text;

                    if (!saveAll)
                    {
                        if (MessageBox.Show("Save '" + current + "'?", "Save audio content?", MessageBoxButtons.YesNo) == DialogResult.No)
                        {
                            continue;
                        }
                    }
                    try
                    {
                        string file = tag.FileName;

                        TonieAudio dump = TonieAudio.FromFile(file);

                        string[] titles = null;
                        List<string> tags = new List<string>();
                        tags.Add("TeddyVersion=" + GetVersion());
                        tags.Add("TeddyFile=" + file);

                        string hashString = BitConverter.ToString(dump.Header.Hash).Replace("-", "");
                        IEnumerable<TonieTools.TonieData> found = new TonieTools.TonieData[0];

                        lock (TonieInfoLock)
                        {
                            found = TonieInfo.Where(t => t.Hash.Contains(hashString));
                        }
                        TonieTools.TonieData info = null;

                        titles = GetCustomChapterTitles(hashString);

                        if (found.Count() > 0)
                        {
                            info = found.First();
                            if (titles == null)
                            {
                                titles = info.Tracks;
                            }
                            tags.Add("ALBUM=" + info.Title);
                            tags.Add("ARTIST=" + info.Series);
                            tags.Add("LANGUAGE=" + info.Language);
                        }
                        tags.Add("HASH=" + hashString);

                        string inFile = new FileInfo(file).Name;
                        string inDir = new FileInfo(file).DirectoryName;
                        string outDirectory = !string.IsNullOrEmpty(outputLocation) ? outputLocation : inDir;

                        if (createDirs)
                        {
                            if (info == null)
                            {
                                outDirectory = Path.Combine(outDirectory, dump.Header.AudioId.ToString("X8") + " - " + tag.Uid);
                            }
                            else
                            {
                                outDirectory = Path.Combine(outDirectory, info.Model + " - " + dump.Header.AudioId.ToString("X8") + " - " + RemoveInvalidChars(info.Title).Trim());
                            }
                            if (!Directory.Exists(outDirectory))
                            {
                                Directory.CreateDirectory(outDirectory);
                            }
                        }

                        if (!Directory.Exists(outDirectory))
                        {
                            Console.WriteLine("Error: Output directory '" + outDirectory + "' does not exist");
                            return;
                        }

                        try
                        {
                            dump.DumpAudioFiles(outDirectory, inFile + "-" + dump.Header.AudioId.ToString("X8"), false, tags.ToArray(), titles);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("[ERROR] Failed to write .ogg/.cue'");
                            Console.WriteLine("   Message:    " + ex.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to save file '" + tag.FileName + "'");
                        return;
                    }
                }
            }
        }

        private void ExportSelected()
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog();

            if (dlg.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            string outputLocation = dlg.SelectedPath;

            foreach (ListViewItem item in lstTonies.SelectedItems)
            {
                ListViewTag tag = item.Tag as ListViewTag;
                string current = item.Text;

                try
                {
                    string destName = Path.Combine(outputLocation, tag.Info.Model + " - " + tag.AudioId.ToString("X8") + " - " + RemoveInvalidChars(tag.Info.Title).Trim());
                    File.Copy(tag.FileName, destName, false);

                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to save file '" + tag.FileName + "': " + ex.Message);
                    return;
                }
            }
        }

        #endregion

        private void cmbSorting_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateSorting();
        }

        private void UpdateSorting()
        {
            foreach (ListViewItem item in lstTonies.Items)
            {
                if (cmbSorting.SelectedIndex == 3)
                {
                    if (item.ImageKey == "unknown")
                    {
                        item.Group = lstTonies.Groups[0];
                    }
                    else if (item.ImageKey == "custom" || item.ImageKey.StartsWith(CustomImageKeyPrefix))
                    {
                        item.Group = lstTonies.Groups[1];
                    }
                    else
                    {
                        item.Group = lstTonies.Groups[2];
                    }
                }
                else
                {
                    item.Group = lstTonies.Groups[3];
                }
            }
            (lstTonies.ListViewItemSorter as ListViewItemComparer).Characteristic = cmbSorting.SelectedIndex;
            lstTonies.Sort();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StopThreads();
            Application.Exit();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            SaveSelected();
        }

        public static string RemoveInvalidChars(string filename)
        {
            return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
        }

        private string GetVersion()
        {
            return Application.ProductVersion + (ThisAssembly.Git.IsDirty ? ",dirty" : "");
        }

        private async void reportselectedFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            await ReportSelected();
        }

        private void toggleLiveFlagToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool bUnhideAll = (lstTonies.SelectedItems.Count > 1);

            foreach (ListViewItem t in lstTonies.SelectedItems)
            {
                ListViewTag tag = t.Tag as ListViewTag;

                if(bUnhideAll || tag.FileInfo.Attributes.HasFlag(FileAttributes.Hidden))
                {
                    tag.FileInfo.Attributes &= ~FileAttributes.Hidden;
                }
                else
                {
                    tag.FileInfo.Attributes |= FileAttributes.Hidden;
                }
            }

            RefreshCardContent();
        }

        private void addExplorerTitleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ListViewTag tag = LastSelectediItem?.Tag as ListViewTag;
            if (tag == null)
            {
                return;
            }

            string title = GetExplorerTitle(tag);
            if (string.IsNullOrWhiteSpace(title))
            {
                MessageBox.Show("No story title is available for this Tonie.", "Explorer title");
                return;
            }

            string error;
            if (!TrySetExplorerTitle(tag.FileInfo.Directory.FullName, title, out error))
            {
                MessageBox.Show("Could not save the Explorer title: " + error, "Explorer title");
            }
        }

        private void removeExplorerTitleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ListViewTag tag = LastSelectediItem?.Tag as ListViewTag;
            if (tag == null)
            {
                return;
            }

            string error;
            if (!TryRemoveExplorerTitle(tag.FileInfo.Directory.FullName, out error))
            {
                MessageBox.Show("Could not remove the Explorer title: " + error, "Explorer title");
            }
        }

        private void btnSetAllLiveFlags_Click(object sender, EventArgs e)
        {
            SetAllLiveFlags(true);
        }

        private void btnRemoveAllLiveFlags_Click(object sender, EventArgs e)
        {
            SetAllLiveFlags(false);
        }

        private void btnAddAllExplorerTitles_Click(object sender, EventArgs e)
        {
            SetAllExplorerTitles(true);
        }

        private void btnRemoveAllExplorerTitles_Click(object sender, EventArgs e)
        {
            SetAllExplorerTitles(false);
        }

        private void SetAllExplorerTitles(bool addTitles)
        {
            int updated = 0;
            int skipped = 0;
            int failed = 0;
            var processedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ListViewItem item in lstTonies.Items)
            {
                ListViewTag tag = item.Tag as ListViewTag;
                string directoryPath = tag?.FileInfo?.DirectoryName;
                if (tag == null || string.IsNullOrWhiteSpace(directoryPath) || !processedDirectories.Add(directoryPath))
                {
                    continue;
                }

                string error;
                if (addTitles)
                {
                    string title = GetExplorerTitle(tag);
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        skipped++;
                        continue;
                    }

                    if (TrySetExplorerTitle(directoryPath, title, out error))
                    {
                        updated++;
                    }
                    else
                    {
                        failed++;
                    }
                }
                else if (TryRemoveExplorerTitle(directoryPath, out error))
                {
                    updated++;
                }
                else
                {
                    failed++;
                }
            }

            string action = addTitles ? "added" : "removed";
            string summary = updated + " Explorer title" + (updated == 1 ? " was" : "s were") + " " + action + ".";
            if (skipped > 0)
            {
                summary += Environment.NewLine + skipped + " Tonie" + (skipped == 1 ? " was" : "s were") + " skipped because no title is available.";
            }
            if (failed > 0)
            {
                summary += Environment.NewLine + failed + " Tonie" + (failed == 1 ? " could" : "s could") + " not be updated.";
            }

            MessageBox.Show(summary, "Explorer titles");
        }

        private void SetAllLiveFlags(bool isLive)
        {
            foreach (ListViewItem item in lstTonies.Items)
            {
                ListViewTag tag = item.Tag as ListViewTag;
                if (tag == null)
                {
                    continue;
                }

                if (isLive)
                {
                    tag.FileInfo.Attributes |= FileAttributes.Hidden;
                }
                else
                {
                    tag.FileInfo.Attributes &= ~FileAttributes.Hidden;
                }
            }

            RefreshCardContent();
        }
    }
}
