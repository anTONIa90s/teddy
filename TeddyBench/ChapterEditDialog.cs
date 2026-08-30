using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TeddyBench
{
    internal class ChapterEditDialog : Form
    {
        private readonly List<TextBox> chapterInputs = new List<TextBox>();

        public IEnumerable<KeyValuePair<int, string>> EnteredTitles
        {
            get
            {
                return chapterInputs
                    .Select((input, index) => new KeyValuePair<int, string>(index, input.Text.Trim()))
                    .Where(title => !string.IsNullOrWhiteSpace(title.Value));
            }
        }

        public ChapterEditDialog(string albumTitle, int chapterCount, string[] currentTitles)
        {
            Text = "Change chapter information";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(520, 400);

            var albumLabel = new Label
            {
                AutoEllipsis = true,
                Dock = DockStyle.Top,
                Font = new Font(Font, FontStyle.Bold),
                Height = 34,
                Padding = new Padding(12, 10, 12, 4),
                Text = string.IsNullOrWhiteSpace(albumTitle) ? "Unnamed Teddy" : albumTitle
            };

            var chapterTable = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 0, 0, 8)
            };
            chapterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
            chapterTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            for (int chapter = 0; chapter < chapterCount; chapter++)
            {
                chapterTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                var number = new Label
                {
                    AutoSize = true,
                    Margin = new Padding(0, 6, 8, 3),
                    Text = "Chapter " + (chapter + 1).ToString("00")
                };
                chapterTable.Controls.Add(number, 0, chapter);

                var input = new TextBox
                {
                    Anchor = AnchorStyles.Left | AnchorStyles.Right,
                    Margin = new Padding(0, 3, 0, 3),
                    Text = currentTitles != null && chapter < currentTitles.Length ? currentTitles[chapter] ?? "" : ""
                };
                chapterInputs.Add(input);
                chapterTable.Controls.Add(input, 1, chapter);
            }

            var chapterPanel = new Panel
            {
                AutoScroll = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 0, 12, 0)
            };
            chapterPanel.Controls.Add(chapterTable);

            var saveButton = new Button
            {
                DialogResult = DialogResult.OK,
                Size = new Size(75, 23),
                Text = "Save",
                UseVisualStyleBackColor = true
            };
            var cancelButton = new Button
            {
                DialogResult = DialogResult.Cancel,
                Size = new Size(75, 23),
                Text = "Cancel",
                UseVisualStyleBackColor = true
            };
            var buttonPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 8, 12, 8),
                WrapContents = false
            };
            buttonPanel.Controls.Add(saveButton);
            buttonPanel.Controls.Add(cancelButton);

            Controls.Add(chapterPanel);
            Controls.Add(buttonPanel);
            Controls.Add(albumLabel);
            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }
    }
}
