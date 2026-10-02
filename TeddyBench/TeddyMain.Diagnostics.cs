using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TonieFile;

namespace TeddyBench
{
    public partial class TeddyMain
    {
        private async Task<bool> ReportSelected()
        {
            if (lstTonies.SelectedItems.Count == 0)
            {
                return false;
            }

            StringBuilder str = new StringBuilder();

            str.AppendLine(" Reporting " + lstTonies.SelectedItems.Count + " files");
            str.AppendLine("-----------------------------------");

            foreach (ListViewItem t in lstTonies.SelectedItems)
            {
                ListViewTag tag = t.Tag as ListViewTag;

                AddInfo(str, tag);
            }

            ReportForm form = new ReportForm(str.ToString());

            if (form.ShowDialog() == DialogResult.OK)
            {
                Settings.Username = form.Username;
                SaveSettings();

                DiagStatus ret = await DiagnosticsSendInfo(str.ToString(), form.Username, form.Message, "Audio Report.txt");
                if (ret.Success)
                {
                    MessageBox.Show("Report Successfully sent." + Environment.NewLine + "Server response:" + Environment.NewLine + ret.Message + "", "Report sent", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return true;
                }
                else
                {
                    MessageBox.Show("Error sending the report." + Environment.NewLine + "Server response:" + Environment.NewLine + ret.Message + "", "Report sent", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            return false;
        }

        public class DiagStatus
        {
            public bool Success;
            public string Message;
        }

        private async Task<DiagStatus> DiagnosticsSendInfo(string payload, string sender, string message, string filename)
        {
            DiagStatus ret = new DiagStatus();

            ret.Success = false;
            ret.Message = "";

            try
            {
                HttpClient httpClient = new HttpClient();
                MultipartFormDataContent form = new MultipartFormDataContent();

                form.Add(new StringContent(sender), "sender");
                form.Add(new StringContent(payload), "payload");
                form.Add(new StringContent(message), "message");
                form.Add(new StringContent(filename), "filename");
                form.Add(new StringContent(GetVersion()), "version");

                HttpResponseMessage response = await httpClient.PostAsync("https://api.revvox.de/diag", form);
                response.EnsureSuccessStatusCode();
                httpClient.Dispose();
                string sd = response.Content.ReadAsStringAsync().Result;

                LogWindow.Log(LogWindow.eLogLevel.Debug, sd);

                ret.Success = (response.StatusCode == HttpStatusCode.OK);
                ret.Message = sd;
                return ret;
            }
            catch (Exception ee)
            {
            }
            return ret;
        }

        private void AddInfo(StringBuilder str, ListViewTag tag)
        {
            lock (TonieInfoLock)
            {
                string customName = null;
                CustomTonieInfo customInfo;
                if (TonieInfoCustom.TryGetValue(tag.Hash, out customInfo))
                {
                    customName = customInfo.Title;
                }
                TonieTools.DumpInfo(str, TonieTools.eDumpFormat.FormatText, tag.FileName, TonieInfo, customName);
            }
        }
    }
}
