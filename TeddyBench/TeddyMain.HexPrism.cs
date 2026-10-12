using System;
using System.Windows.Forms;

namespace TeddyBench
{
    public partial class TeddyMain
    {
        private void hexPrismNetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (HexPrismNetForm form = new HexPrismNetForm())
            {
                form.ShowDialog(this);
            }
        }
    }
}
