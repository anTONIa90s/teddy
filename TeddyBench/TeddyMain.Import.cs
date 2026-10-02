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
private void btnAdd_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Multiselect = true;

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                AddFiles(dlg.FileNames);
            }
        }

        private void AddFiles(string[] fileNames)
        {
            if (fileNames.Count() == 1 && !(fileNames[0].ToLower().EndsWith(".mp3") || fileNames[0].ToLower().EndsWith(".ogg")))
            {
                AskUIDForm copyAsk = new AskUIDForm(RfidReader);
                if (copyAsk.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                string explorerTitle = AskForExplorerTitle();

                try
                {
                    TonieAudio dumpFile = TonieAudio.FromFile(fileNames[0]);

                    if (dumpFile.FileContent.Length > 0)
                    {
                        string hash = BitConverter.ToString(dumpFile.Header.Hash).Replace("-", "");
                        CopyFile(copyAsk.Uid, fileNames[0], explorerTitle, hash);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("The file you have chosen is not supported.", "Add file...");
                }

                return;
            }

            if (fileNames.Where(f => !(f.ToLower().EndsWith(".mp3") || f.ToLower().EndsWith(".ogg"))).Count() > 0)
            {
                MessageBox.Show("Please select MP3/Ogg files only.", "Add file...");
                return;
            }

            TrackSortDialog sorter = new TrackSortDialog(fileNames);

            if (sorter.ShowDialog() == DialogResult.Cancel)
            {
                return;
            }

            uint id = GetAudioID();
            AskUIDForm encodeAsk = new AskUIDForm(RfidReader);
            if (encodeAsk.ShowDialog() == DialogResult.OK)
            {
                EncodeFile(encodeAsk.Uid, sorter.SortedFiles, id, AskForExplorerTitle());
            }
        }

        private string AskForExplorerTitle()
        {
            if (MessageBox.Show(
                "Add title information for Windows Explorer?",
                "Explorer title",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return null;
            }

            using (ExplorerTitleDialog dialog = new ExplorerTitleDialog())
            {
                return dialog.ShowDialog(this) == DialogResult.OK ? dialog.ExplorerTitle : null;
            }
        }

        private void CopyFile(string uid, string fileName, string explorerTitle = null, string hash = null)
        {
            btnAdd.Enabled = false;
            btnDelete.Enabled = false;
            btnSave.Enabled = false;

            EncodeThread = new SafeThread(() =>
            {
                string newDir = Path.Combine(CurrentDirectory, ReverseUid(uid).Substring(0, 8));
                string newFile = Path.Combine(newDir, ReverseUid(uid).Substring(8, 8));

                try
                {
                    Directory.CreateDirectory(newDir);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to create directory '" + newDir + "'");
                    return;
                }

                try
                {
                    File.WriteAllBytes(newFile, File.ReadAllBytes(fileName));
                    if (!string.IsNullOrWhiteSpace(explorerTitle))
                    {
                        string explorerTitleError;
                        if (!TrySetExplorerTitle(newDir, explorerTitle, out explorerTitleError))
                        {
                            MessageBox.Show("The Tonie was created, but its Explorer title could not be saved: " + explorerTitleError);
                        }

                        if (!string.IsNullOrWhiteSpace(hash))
                        {
                            lock (TonieInfoLock)
                            {
                                GetOrCreateCustomTonieInfo(hash).Title = NormalizeExplorerTitle(explorerTitle);
                            }
                            SaveJson();
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to write file '" + newFile + "'");
                    return;
                }

                RefreshCardContent();
            }, "EncodeThread");
            EncodeThread.Start();
        }

        public class EncodeCallback : TonieAudio.EncodeCallback
        {
            private readonly TeddyMain Main;
            private int LastPct = 0;
            public List<string> ChapterTitles { get; } = new List<string>();

            public EncodeCallback(TeddyMain main)
            {
                Main = main;
            }

            public override void FileStart(int track, string sourceFile)
            {
                ParseName(track, sourceFile);
                ChapterTitles.Add(Path.GetFileNameWithoutExtension(DisplayName));
                Main.BeginInvoke(new Action(() => { Main.txtLog.Text += " File: " + ShortName; }));
            }

            public override void FileDone()
            {
                Main.BeginInvoke(new Action(() => { Main.txtLog.Text += "]" + Environment.NewLine; }));
            }

            public override void FileFailed(string message)
            {
                Main.BeginInvoke(new Action(() => { Main.txtLog.Text += "] FAILED" + Environment.NewLine; }));
            }

            public override void Progress(decimal pct)
            {
                LastPct = (int)(pct * 20);
                if (LastPct % 5 == 0)
                {
                    if (LastPct != 20)
                    {
                        Main.BeginInvoke(new Action(() => { Main.txtLog.Text += "" + (LastPct * 5) + "%"; }));
                    }
                }
                else
                {
                    Main.BeginInvoke(new Action(() => { Main.txtLog.Text += "."; }));
                }
            }

            public override void Failed(string message)
            {
                Main.BeginInvoke(new Action(() => { Main.txtLog.Text += "] FAILED" + Environment.NewLine; }));
            }

            public override void Warning(string message)
            {
                Main.BeginInvoke(new Action(() => { Main.txtLog.Text += Environment.NewLine + "Warning" + Environment.NewLine; }));
            }
        }

        private void EncodeFile(string uid, string[] filenames, uint id = uint.MaxValue, string explorerTitle = null)
        {
            btnAdd.Enabled = false;
            btnDelete.Enabled = false;
            btnSave.Enabled = false;

            grpCardContent.Visible = false;
            txtLog.Visible = true;

            EncodeThread = new SafeThread(() =>
            {
                string newDir = Path.Combine(CurrentDirectory, ReverseUid(uid).Substring(0, 8));
                string newFile = Path.Combine(newDir, ReverseUid(uid).Substring(8, 8));

                TonieAudio audio = null;
                EncodeCallback callback = new EncodeCallback(this);

                try
                {
                    if (id == uint.MaxValue)
                    {
                        id = (uint)(DateTimeOffset.Now.ToUnixTimeSeconds() - 0x50000000);
                    }
                    audio = new TonieAudio(filenames, id, cbr: callback);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to encode file. Sorry. Most likely a MP3 was corrupted." + Environment.NewLine + "(" + ex.Message + ")");
                    return;
                }

                try
                {
                    Directory.CreateDirectory(newDir);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to create directory '" + newDir + "'");
                    return;
                }
                try
                {
                    File.WriteAllBytes(newFile, audio.FileContent);
                    if (!string.IsNullOrWhiteSpace(explorerTitle))
                    {
                        string explorerTitleError;
                        if (!TrySetExplorerTitle(newDir, explorerTitle, out explorerTitleError))
                        {
                            MessageBox.Show("The Tonie was created, but its Explorer title could not be saved: " + explorerTitleError);
                        }
                    }
                    string hash = BitConverter.ToString(audio.Header.Hash).Replace("-", "");
                    lock (TonieInfoLock)
                    {
                        CustomTonieInfo customInfo = GetOrCreateCustomTonieInfo(hash);
                        customInfo.Chapters = callback.ChapterTitles.ToArray();
                        if (!string.IsNullOrWhiteSpace(explorerTitle))
                        {
                            customInfo.Title = NormalizeExplorerTitle(explorerTitle);
                        }
                    }
                    SaveJson();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to write file '" + newFile + "'");
                    return;
                }

                RefreshCardContent();
            }, "EncodeThread");
            EncodeThread.Start();
        }


        public static string ReverseUid(string uid)
        {
            List<string> groups = (from Match m in Regex.Matches(uid, @"[A-Fa-f0-9]{2}") select m.Value).ToList();
            groups.Reverse();
            string ret = string.Join("", groups.ToArray());

            return ret;
        }
    }
}
