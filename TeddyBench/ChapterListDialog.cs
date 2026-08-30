using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace TeddyBench
{
    internal class ChapterListDialog : Form
    {
        public ChapterListDialog(string albumTitle, IEnumerable<string> chapterTitles)
        {
            Text = "Chapters";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(460, 360);

            var albumLabel = new Label
            {
                AutoEllipsis = true,
                Dock = DockStyle.Top,
                Font = new Font(Font, FontStyle.Bold),
                Height = 34,
                Padding = new Padding(12, 10, 12, 4),
                Text = string.IsNullOrWhiteSpace(albumTitle) ? "Unknown album" : albumTitle
            };

            var chapters = new ListBox
            {
                Dock = DockStyle.Fill,
                HorizontalScrollbar = true,
                IntegralHeight = false,
                Margin = new Padding(12)
            };

            int chapterNumber = 1;
            if (chapterTitles != null)
            {
                foreach (string chapterTitle in chapterTitles)
                {
                    chapters.Items.Add(chapterNumber.ToString("00") + " - " + chapterTitle);
                    chapterNumber++;
                }
            }

            if (chapters.Items.Count == 0)
            {
                chapters.Items.Add("No chapter titles are available for this Tonie.");
            }

            var closeButton = new Button
            {
                DialogResult = DialogResult.OK,
                Size = new Size(75, 23),
                Text = "Close",
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
            buttonPanel.Controls.Add(closeButton);

            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 0, 12, 0)
            };
            contentPanel.Controls.Add(chapters);

            Controls.Add(contentPanel);
            Controls.Add(buttonPanel);
            Controls.Add(albumLabel);
            AcceptButton = closeButton;
            CancelButton = closeButton;
        }
    }
}
