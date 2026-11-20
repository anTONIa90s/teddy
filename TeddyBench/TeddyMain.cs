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
    public partial class TeddyMain : Form
    {
        private SafeThread ScanCardThread = null;
        private bool ScanCardThreadStop = false;
        private SafeThread AnalyzeThread = null;
        private bool AnalyzeThreadStop = false;
        private SafeThread EncodeThread = null;
        private SafeThread SyncCopyThread = null;
        private SafeThread LogThread;
        private bool LogThreadStop;

        private string CurrentDirectory = null;
        private bool CurrentDirectoryIsCard = false;
        private string CompareDirectory = null;
        private FileSystemWatcher CompareWatcher = null;
        private FileSystemWatcher CardWatcher = null;
        private ListViewGroup GroupOnBoth = null;
        private ListViewGroup GroupOnlyOnCard = null;
        private ListViewGroup GroupOnlyOnBackup = null;
        private bool SyncCopyRunning = false;
        private bool AutoOpenDrive = true;
        private Dictionary<ListViewTag, ListViewItem> RegisteredItems = new Dictionary<ListViewTag, ListViewItem>();

        private static object TonieInfoLock = new object();
        private static TonieTools.TonieData[] TonieInfo = new TonieTools.TonieData[0];
        private static Dictionary<string, CustomTonieInfo> TonieInfoCustom = new Dictionary<string, CustomTonieInfo>();
        private static string TonieInfoString = "";
        /* incremented whenever TonieInfo is (re)loaded, so unknown tonies get looked up again */
        private static int TonieInfoGeneration = 0;
        private System.Windows.Forms.Timer RefreshDebounceTimer = null;

        private Dictionary<string, Tuple<TonieAudio, DateTime>> CachedAudios = new Dictionary<string, Tuple<TonieAudio, DateTime>>();
        private ListViewItem LastSelectediItem = null;
        private RfidReaderBase RfidReader;
        private bool AutoSelected;
        private Settings Settings = null;
        internal LogWindow Log;
        private string LastFoundUid = null;
        private SafeThread AsyncTagActionThread = null;
        private System.Windows.Forms.Timer StatusBarTimer = null;

        private string TitleString => "TeddyBench - " + GetVersion();

        private SafeThread PlayThread = null;
        private bool PlayThreadStop = true;
        private bool TrackMouseDown = false;
        private int TrackNewPosition = -1;
        private const string CustomImageFolder = "customImages";
        private const string CustomImageKeyPrefix = "custom:";
        private const string DesktopIniFileName = "desktop.ini";
        private const string ShellClassInfoSection = ".ShellClassInfo";
        private const string DocumentPropertiesSection = "{F29F85E0-4FF9-1068-AB91-08002B27B3D9}";
        private const string TeddyBenchSection = "TeddyBench";

        /// <summary>
        /// Where a tonie file exists when the SD card is compared with the backup directory.
        /// None: no comparison active (plain directory view).
        /// </summary>
        public enum eLocation
        {
            None,
            Both,
            CardOnly,
            BackupOnly
        }

        public class ListViewTag
        {
            public string FileName;
            public FileInfo FileInfo;
            public string Hash;
            public TonieTools.TonieData Info;
            public string Uid;
            public uint AudioId;
            /* path relative to the scanned root ("XXXXXXXX\YYYYYYYY" or the flat file name), identifies the same tonie on card and backup */
            public string RelativePath;
            public eLocation Location = eLocation.None;

            public ListViewTag(string filename)
            {
                FileName = filename;

                FileInfo = new FileInfo(FileName);
                Uid = ReverseUid(FileInfo.Directory.Name + FileInfo.Name);
            }
        }

        public class ListViewItemComparer : IComparer
        {
            public int Characteristic = 0;
            public bool Order = true;

            public ListViewItemComparer(int ch)
            {
                Characteristic = ch;
            }

            public int Compare(object a, object b)
            {
                ListViewItem item1 = a as ListViewItem;
                ListViewItem item2 = b as ListViewItem;
                ListViewTag t1 = item1.Tag as ListViewTag;
                ListViewTag t2 = item2.Tag as ListViewTag;

                string s1 = "";
                string s2 = "";

                switch (Characteristic)
                {
                    case 0:
                        s1 = t1.Uid;
                        s2 = t2.Uid;
                        break;
                    case 1:
                        s1 = t1.Info?.Model;
                        s2 = t2.Info?.Model;
                        break;
                    case 2:
                        s1 = t1.FileInfo.CreationTime.ToString();
                        s2 = t2.FileInfo.CreationTime.ToString();
                        break;
                    case 3:
                        s1 = t1.Info?.SortString;
                        s2 = t2.Info?.SortString;
                        break;
                    case 4:
                        s1 = t1.Info?.Title;
                        s2 = t2.Info?.Title;
                        break;
                }

                if (s1 == null)
                {
                    return -1;
                }
                if (s2 == null)
                {
                    return 1;
                }

                int returnVal = String.Compare(s1, s2);

                return (Order ? 1 : -1) * returnVal;
            }
        }



        public TeddyMain()
        {
            InitializeComponent();
            Log = new LogWindow();

            Settings = Settings.FromFile(Settings.DefaultFile);

            lstTonies.LargeImageList = new ImageList();
            lstTonies.LargeImageList.ImageSize = new Size(128, 128);
            lstTonies.LargeImageList.ColorDepth = ColorDepth.Depth32Bit;
            lstTonies.LargeImageList.Images.Add("unknown", ResizeImage(Resources.unknown, 128, 128));
            lstTonies.LargeImageList.Images.Add("custom", ResizeImage(Resources.custom, 128, 128));
            lstTonies.ListViewItemSorter = new ListViewItemComparer(2);
            lstTonies.DoubleBuffered(true);
            GroupOnBoth = new ListViewGroup("OnBoth", "On SD card and backup");
            GroupOnlyOnCard = new ListViewGroup("OnlyOnCard", "Only on SD card");
            GroupOnlyOnBackup = new ListViewGroup("OnlyOnBackup", "Only on backup");
            lstTonies.Groups.AddRange(new ListViewGroup[] { GroupOnBoth, GroupOnlyOnCard, GroupOnlyOnBackup });
            cmbSorting.SelectedIndex = 2;
            Text = TitleString;

            autodetectionEnabledToolStripMenuItem.Checked = Settings.NfcEnabled;
            ReportForm.DefaultUsername = Settings.Username;
            UpdateNfcReader();

            AllowDrop = true;

            if (Settings.DebugWindow)
            {
                LogWindow.LogLevel = LogWindow.eLogLevel.DebugVerbose;
                enableDebugModeToolStripMenuItem.Checked = true;
            }
            if (Settings.DownloadJson)
            {
                downloadToniesjsonOnStartupToolStripMenuItem.Checked = true;
            }

            StatusBarTimer = new System.Windows.Forms.Timer();
            StatusBarTimer.Tick += (object sender, EventArgs e) => { UpdateStatusBar(); };
            StatusBarTimer.Interval = 250;
            StatusBarTimer.Start();
        }

        void SaveSettings()
        {
            Settings.Save(Settings.DefaultFile);
        }




        protected override void OnDragEnter(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        protected override void OnDragDrop(DragEventArgs drgevent)
        {
            string[] drop = drgevent.Data.GetData(DataFormats.FileDrop) as string[];

            if (drop != null)
            {
                AddFiles(drop);
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            /* start only once the window handle exists: before that InvokeRequired is false and the
             * scan thread would fill lstTonies itself while the UI thread creates its handle */
            StartThreads();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopThreads();
            SaveSettings();
            base.OnFormClosing(e);
        }

        private void StopThreads()
        {
            if (ScanCardThread != null)
            {
                ScanCardThreadStop = true;
                ScanCardThread.Join(100);
                ScanCardThread.Abort();
                ScanCardThread = null;
            }
            if (PlayThread != null)
            {
                PlayThreadStop = true;
                PlayThread.Join(1000);
                PlayThread.Abort();
                PlayThread = null;
            }
            StopAnalyzeThread();
            if (EncodeThread != null)
            {
                EncodeThread.Join(100);
                EncodeThread.Abort();
                EncodeThread = null;
            }
            if (SyncCopyThread != null)
            {
                SyncCopyThread.Join(100);
                SyncCopyThread.Abort();
                SyncCopyThread = null;
            }
            if (RefreshDebounceTimer != null)
            {
                RefreshDebounceTimer.Stop();
                RefreshDebounceTimer.Dispose();
                RefreshDebounceTimer = null;
            }
            if (CardWatcher != null)
            {
                CardWatcher.EnableRaisingEvents = false;
                CardWatcher.Dispose();
                CardWatcher = null;
            }
            if (CompareWatcher != null)
            {
                CompareWatcher.EnableRaisingEvents = false;
                CompareWatcher.Dispose();
                CompareWatcher = null;
            }
            if (LogThread != null)
            {
                LogThreadStop = true;
                LogThread.Join(100);
                LogThread.Abort();
                LogThread = null;
            }
            if (RfidReader != null)
            {
                RfidReader.Stop();
                RfidReader = null;
            }
        }











        internal void ReportException(string name, Exception e)
        {
            if (MessageBox.Show("Sorry, there was an exception in '" + name + "'. Do you want to report that crash?", "Some Exception happened", MessageBoxButtons.YesNo) == DialogResult.No)
            {
                return;
            }

            StringBuilder str = new StringBuilder();

            str.AppendLine(" Reporting an unhandled eception");
            str.AppendLine("-----------------------------------");

            str.AppendLine("Thread: '" + name + "'");
            str.AppendLine("");
            str.AppendLine("-----------------------------------");
            str.AppendLine("");
            str.AppendLine("Message:");
            str.AppendLine(e.Message);
            str.AppendLine("");
            str.AppendLine("-----------------------------------");
            str.AppendLine("");
            str.AppendLine("StackTrace:");
            str.AppendLine(e.StackTrace);
            str.AppendLine("");
            str.AppendLine("-----------------------------------");
            str.AppendLine("");


            ReportForm form = new ReportForm(str.ToString());

            if (form.ShowDialog() == DialogResult.OK)
            {
                Settings.Username = form.Username;
                SaveSettings();

                DiagStatus ret = DiagnosticsSendInfo(str.ToString(), form.Username, form.Message, "Exception.txt").Result;
                if (ret.Success)
                {
                    MessageBox.Show("Report Successfully sent." + Environment.NewLine + "Server response:" + Environment.NewLine + ret.Message + "", "Report sent", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("Error sending the report." + Environment.NewLine + "Server response:" + Environment.NewLine + ret.Message + "", "Report sent", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

            }
        }

        private void downloadToniesjsonOnStartupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.DownloadJson = !downloadToniesjsonOnStartupToolStripMenuItem.Checked;
            SaveSettings();
        }

        private void downloadToniesjsonNowToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new SafeThread(() =>
            {
                LoadJson(true);
            }, "JSON Downloader").Start();
        }
    }
}
