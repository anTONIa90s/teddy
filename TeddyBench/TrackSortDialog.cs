using Id3;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TeddyBench
{
    /// <summary>
    /// The single dialog behind "Add...": pick and sort the tracks, enter the tag UID,
    /// an optional Audio-ID and an optional Windows Explorer title.
    /// A single non-audio file (e.g. a Tonie dump) is copied as-is instead of encoded.
    /// </summary>
    public partial class TrackSortDialog : Form
    {
        private const string UidPrefix = "E00403";

        private readonly RfidReaderBase RfidReader;
        private string[] FileNames;
        private List<Tuple<string, Id3Tag>> FileList = new List<Tuple<string, Id3Tag>>();
        private bool RawMode;

        private TextBox txtUid;
        private CheckBox chkAudioId;
        private TextBox txtAudioId;
        private TextBox txtTitle;

        public TrackSortDialog()
        {
            InitializeComponent();
            BuildSettingsPanel();
        }

        public TrackSortDialog(RfidReaderBase rfidReader, string[] fileNames = null) : this()
        {
            this.RfidReader = rfidReader;
            this.FileNames = fileNames;

            if (RfidReader != null)
            {
                RfidReader.UidFound += RfidReader_UidFound;
            }
        }

        public string[] SortedFiles
        {
            get
            {
                return FileList.Select(i => i.Item1).ToArray();
            }
        }

        /// <summary>True when the only entry is a non-audio file that gets copied, not encoded.</summary>
        public bool IsRawFile => RawMode;

        public string Uid => txtUid.Text.ToUpper();

        /// <summary>The chosen Audio-ID, or uint.MaxValue to let the encoder pick one.</summary>
        public uint AudioId
        {
            get
            {
                if (RawMode || !chkAudioId.Checked)
                {
                    return uint.MaxValue;
                }
                return uint.Parse(txtAudioId.Text, System.Globalization.NumberStyles.HexNumber);
            }
        }

        public string ExplorerTitle
        {
            get
            {
                string title = txtTitle.Text.Trim();
                return title.Length > 0 ? title : null;
            }
        }

        private void BuildSettingsPanel()
        {
            TableLayoutPanel table = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                Padding = new Padding(6)
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            txtUid = new TextBox { Dock = DockStyle.Fill, MaxLength = 16, CharacterCasing = CharacterCasing.Upper, Text = UidPrefix };
            txtUid.TextChanged += txtUid_TextChanged;
            AddRow(table, 0, "Tag UID:", txtUid, CreateHint("Place the tag on the reader to fill it in."));

            txtAudioId = new TextBox
            {
                Dock = DockStyle.Fill,
                MaxLength = 8,
                CharacterCasing = CharacterCasing.Upper,
                Enabled = false,
                Text = (DateTimeOffset.Now.ToUnixTimeSeconds() - 0x50000000).ToString("X8")
            };
            txtAudioId.TextChanged += (s, e) => UpdateOkState();
            chkAudioId = new CheckBox { AutoSize = true, Anchor = AnchorStyles.Left, Text = "Set a specific Audio-ID (if unsure, leave unchecked)" };
            chkAudioId.CheckedChanged += (s, e) => UpdateModeState();
            AddRow(table, 1, "Audio-ID:", txtAudioId, chkAudioId);

            txtTitle = new TextBox { Dock = DockStyle.Fill };
            AddRow(table, 2, "Explorer title:", txtTitle, null);
            table.SetColumnSpan(txtTitle, 2);

            /* Added last, so it docks before the track list fills the remaining space. */
            splitContainer1.Panel1.Controls.Add(table);
        }

        private static Label CreateHint(string text)
        {
            return new Label { AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = SystemColors.GrayText, Text = text };
        }

        private static void AddRow(TableLayoutPanel table, int row, string caption, Control input, Control extra)
        {
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(new Label { AutoSize = true, Anchor = AnchorStyles.Left, Text = caption }, 0, row);
            table.Controls.Add(input, 1, row);
            if (extra != null)
            {
                table.Controls.Add(extra, 2, row);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (FileNames != null)
            {
                AddTracks(FileNames);
            }

            UpdateView();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            txtUid.Select();
            txtUid.SelectionStart = txtUid.Text.Length;

            if (FileList.Count == 0)
            {
                BeginInvoke(new Action(() => btnAddTracks.PerformClick()));
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (RfidReader != null)
            {
                RfidReader.UidFound -= RfidReader_UidFound;
            }

            base.OnClosing(e);
        }

        private void RfidReader_UidFound(object sender, string e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => RfidReader_UidFound(sender, e)));
                return;
            }

            if (e != null && txtUid.Text != e)
            {
                txtUid.Text = e;
                SystemSounds.Beep.Play();
            }
        }

        private void txtUid_TextChanged(object sender, EventArgs e)
        {
            if (!txtUid.Text.StartsWith(UidPrefix))
            {
                txtUid.Text = UidPrefix;
                txtUid.SelectionStart = txtUid.Text.Length;
            }

            UpdateOkState();
        }

        private static bool IsHex(string text, int length)
        {
            return text.Length == length && text.All("0123456789abcdefABCDEF".Contains);
        }

        private static bool IsAudioFile(string fileName)
        {
            string lower = fileName.ToLower();
            return lower.EndsWith(".mp3") || lower.EndsWith(".ogg");
        }

        private void UpdateModeState()
        {
            btnUp.Enabled = !RawMode;
            btnDown.Enabled = !RawMode;
            chkAudioId.Enabled = !RawMode;
            txtAudioId.Enabled = !RawMode && chkAudioId.Checked;
            btnEncode.Text = RawMode ? "Copy" : "Encode";

            UpdateOkState();
        }

        private void UpdateOkState()
        {
            bool uidValid = IsHex(txtUid.Text, 16);
            bool audioIdValid = !txtAudioId.Enabled || IsHex(txtAudioId.Text, 8);

            txtUid.BackColor = uidValid ? Color.LightGreen : Color.PaleVioletRed;
            txtAudioId.BackColor = txtAudioId.Enabled ? (audioIdValid ? Color.LightGreen : Color.PaleVioletRed) : SystemColors.Control;

            btnEncode.Enabled = FileList.Count > 0 && uidValid && audioIdValid;
        }

        private void AddTracks(string[] fileNames)
        {
            if (fileNames.Any(f => !IsAudioFile(f)))
            {
                if (fileNames.Length == 1 && FileList.Count == 0)
                {
                    FileList.Add(new Tuple<string, Id3Tag>(fileNames[0], null));
                    RawMode = true;
                    return;
                }

                MessageBox.Show("Please select MP3/Ogg files, or a single Tonie file.", "Add tracks");
                return;
            }

            if (RawMode)
            {
                FileList.Clear();
                RawMode = false;
            }

            var fileTuples = fileNames.Select(f => new Tuple<string, Id3Tag>(f, GetTag(f)));

            /* A fresh list is ordered by ID3 track number; later additions are appended. */
            if (FileList.Count == 0)
            {
                fileTuples = fileTuples.OrderBy(i => (i.Item2 == null) ? int.MaxValue : i.Item2.Track.Value);
            }

            FileList.AddRange(fileTuples);
        }

        private Id3Tag GetTag(string f)
        {
            try
            {
                Mp3 mp3 = new Mp3(f, Mp3Permissions.Read);

                Id3Tag ret = mp3.GetAllTags().Where(t => t.Track.IsAssigned).FirstOrDefault();

                return ret;
            }
            catch(Exception ex)
            {
                return null;
            }
        }

        private void UpdateView()
        {
            lstTracks.Items.Clear();
            int track = 1;
            foreach (Tuple<string, Id3Tag> item in FileList)
            {
                ListViewItem lvi = new ListViewItem();

                lvi.Tag = item;
                lvi.Text = track.ToString();

                string id3 = "";

                if (item.Item2 != null)
                {
                    id3 = item.Item2.Artists + " - " + item.Item2.Title;
                }
                lvi.SubItems.Add(item.Item1);
                lvi.SubItems.Add(id3);

                lstTracks.Items.Add(lvi);

                track++;
            }

            /* Resize each column to fit its contents. */
            for (int i = 0; i < lstTracks.Columns.Count; i++)
            {
                lstTracks.AutoResizeColumn(i, ColumnHeaderAutoResizeStyle.ColumnContent);
            }

            lstTracks.Columns[lstTracks.Columns.Count - 1].Width = -2;

            UpdateModeState();
        }

        private void btnAddTracks_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.RestoreDirectory = true;
            dlg.Multiselect = true;
            dlg.Filter = "Audio files (*.mp3;*.ogg)|*.mp3;*.ogg|All files (*.*)|*.*";

            if (dlg.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            AddTracks(dlg.FileNames);
            UpdateView();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (lstTracks.SelectedItems.Count == 0)
            {
                return;
            }

            foreach (ListViewItem item in lstTracks.SelectedItems.Cast<ListViewItem>().ToArray())
            {
                lstTracks.Items.Remove(item);
            }

            RebuildFileList();
            lstTracks.Select();
        }

        private void btnUp_Click(object sender, EventArgs e)
        {
            if (lstTracks.SelectedIndices.Count == 0)
            {
                return;
            }
            if (lstTracks.SelectedIndices[0] == 0)
            {
                return;
            }

            int numberOfItems = lstTracks.Items.Count;
            List<object> selectedItems = new List<object>();

            foreach (var item in lstTracks.SelectedItems)
            {
                selectedItems.Add(item);
            }

            lstTracks.BeginUpdate();
            for (int i = 0; i < lstTracks.Items.Count; i++)
            {
                if (lstTracks.SelectedIndices.Contains(i))
                {
                    if (i > 0)
                    {
                        /* Check to avoid moving the first item further up */
                        ListViewItem item = lstTracks.Items[i];
                        lstTracks.Items.RemoveAt(i);
                        lstTracks.Items.Insert(i - 1, item);
                    }
                }
            }

            lstTracks.SelectedItems.Clear();
            lstTracks.EndUpdate();

            foreach (ListViewItem item in selectedItems)
            {
                item.Selected = true;
            }

            RebuildFileList();
            lstTracks.Select();
        }

        private void btnDown_Click(object sender, EventArgs e)
        {
            if (lstTracks.SelectedIndices.Count == 0)
            {
                return;
            }

            if (lstTracks.SelectedIndices[lstTracks.SelectedIndices.Count-1] == lstTracks.Items.Count - 1)
            {
                return;
            }
            int numberOfItems = lstTracks.Items.Count;
            List<object> selectedItems = new List<object>();

            foreach (var item in lstTracks.SelectedItems)
            {
                selectedItems.Add(item);
            }

            lstTracks.BeginUpdate();
            for (int i = numberOfItems - 2; i >= 0; i--)
            {
                if (lstTracks.SelectedIndices.Contains(i))
                {
                    ListViewItem item = lstTracks.Items[i];
                    lstTracks.Items.RemoveAt(i);
                    lstTracks.Items.Insert(i + 1, item);
                }
            }

            lstTracks.SelectedItems.Clear();
            lstTracks.EndUpdate();

            foreach (ListViewItem item in selectedItems)
            {
                item.Selected = true;
            }

            RebuildFileList();
            lstTracks.Select();
        }

        private void RebuildFileList()
        {
            FileList = new List<Tuple<string, Id3Tag>>();
            int track = 1;
            foreach (ListViewItem item in lstTracks.Items)
            {
                item.Text = track.ToString();
                track++;
                FileList.Add((Tuple<string, Id3Tag>)item.Tag);
            }

            if (FileList.Count == 0)
            {
                RawMode = false;
            }

            UpdateModeState();
        }
    }
}
