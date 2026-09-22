using System;
using System.Drawing;
using System.Windows.Forms;

namespace TeddyBench
{
    internal sealed class ExplorerTitleDialog : Form
    {
        private readonly TextBox titleTextBox;

        public string ExplorerTitle => titleTextBox.Text.Trim();

        public ExplorerTitleDialog()
        {
            Text = "Explorer title";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(420, 105);

            var label = new Label
            {
                AutoSize = true,
                Location = new Point(12, 12),
                Text = "Story title shown in Windows Explorer:"
            };

            titleTextBox = new TextBox
            {
                Location = new Point(15, 35),
                Size = new Size(390, 20)
            };

            var okButton = new Button
            {
                DialogResult = DialogResult.OK,
                Location = new Point(249, 70),
                Size = new Size(75, 23),
                Text = "OK"
            };

            var cancelButton = new Button
            {
                DialogResult = DialogResult.Cancel,
                Location = new Point(330, 70),
                Size = new Size(75, 23),
                Text = "Cancel"
            };

            Controls.Add(label);
            Controls.Add(titleTextBox);
            Controls.Add(okButton);
            Controls.Add(cancelButton);
            AcceptButton = okButton;
            CancelButton = cancelButton;
        }
    }
}
