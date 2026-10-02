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
private void Proxmark3_FlashResult(object sender, bool e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => Proxmark3_FlashResult(sender, e)));
                return;
            }

            if (e)
            {
                MessageBox.Show("Flashing the device succeeded, will reconnect now", "Flashing Proxmark3 done");
            }
            else
            {
                MessageBox.Show(
                    "Flashing the device FAILED, it will reconnect now." + Environment.NewLine +
                    "" + Environment.NewLine +
                    "If Device starts with LEDs A and C lit and it doesn't show" + Environment.NewLine +
                    "on USB, replug the device with the button pressed and *keep*" + Environment.NewLine +
                    "it pressed until it's flashed again." + Environment.NewLine +
                    "" + Environment.NewLine +
                    "If this still fails, please" + Environment.NewLine +
                    " a) check the firmware file or" + Environment.NewLine +
                    " b) use the official flasher tool"
                    , "Flashing Proxmark3 failed");
            }
        }

        private void Proxmark3_FlashRequest(object sender, Proxmark3.FlashRequestContext e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => Proxmark3_FlashRequest(sender, e)));
                return;
            }

            e.Proceed = false;

            if (e.Bootloader)
            {
                if (MessageBox.Show(
                    "=== FLASHING BOOTLOADER ===" + Environment.NewLine +
                    "" + Environment.NewLine +
                    "Please be aware that this procedure could fail and your device" + Environment.NewLine +
                    "gets bricked. To recover you need a special programmer." + Environment.NewLine +
                    "" + Environment.NewLine +
                    "Do you still want to flash the file " + e.FlashFile + "?",
                    "Flash Proxmark3?", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    e.Proceed = true;
                }
            }
            else
            {

                if (MessageBox.Show(
                    "Please be aware that this procedure could fail and your device" + Environment.NewLine +
                    "main firmware is lost. (Device starts with LEDs A and C lit and" + Environment.NewLine +
                    "it doesn't enumerate on USB anymore)." + Environment.NewLine +
                    "No panic, the bootloader wasn't touched and you can reflash it." + Environment.NewLine +
                    "" + Environment.NewLine +
                    "If so, plug the device with the button pressed and *keep* it" + Environment.NewLine +
                    "pressed until it's flashed again." + Environment.NewLine +
                    "I did this procedure several times and it always worked." + Environment.NewLine +
                    "" + Environment.NewLine +
                    "If this still fails, please" + Environment.NewLine +
                    " a) check the firmware file or" + Environment.NewLine +
                    " b) use the official flasher tool" + Environment.NewLine +
                    "" + Environment.NewLine +
                    "Do you want to flash the file " + e.FlashFile + "?",
                    "Flash Proxmark3?", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    e.Proceed = true;
                }
            }
        }

        private void Proxmark3_DeviceFound(object sender, string e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => Proxmark3_DeviceFound(sender, e)));
                return;
            }

            if (e == null)
            {
                UpdateStatusBar();
                advancedActionsToolStripMenuItem.Enabled = false;
                reportProxmarkAnToolStripMenuItem.Enabled = false;
                reportNFCTagToolStripMenuItem.Enabled = false;
            }
            else
            {
                UpdateStatusBar();
                advancedActionsToolStripMenuItem.Enabled = RfidReader.UnlockSupported;
                reportProxmarkAnToolStripMenuItem.Enabled = true;
                reportNFCTagToolStripMenuItem.Enabled = true;

                flashBootloaderToolStripMenuItem.Enabled = (RfidReader.DeviceInfo & RfidReaderBase.eDeviceInfo.BootromPresent) != 0;
                flashFirmwareToolStripMenuItem.Enabled = (RfidReader.DeviceInfo & RfidReaderBase.eDeviceInfo.BootromPresent) != 0;
            }
        }

        private void UpdateStatusBar()
        {
            string text = "";

            if (RfidReader != null && RfidReader.Connected)
            {
                if (!statusStrip1.Visible)
                {
                    statusStrip1.Show();
                }
                string voltageString = Settings.DebugWindow ? ", " + RfidReader.AntennaVoltage.ToString("0.00", CultureInfo.InvariantCulture) + " V" : "";

                text += " | " + RfidReader.HardwareType + " (FW: " + (RfidReader.UnlockSupported ? "SLIX-L enabled" : "stock") + voltageString + ") found at " + RfidReader.CurrentPort + ". The UID of the tag will be automatically used where applicable.";
            }

            lock (TonieInfoLock)
            {
                if (TonieInfo.Length == 0)
                {
                    text += " | No tonies.json loaded, consider downloading it";
                }
                else
                {
                    text += " | Have " + TonieInfo.Length + " entries in tonies.json";
                }
                if (!string.IsNullOrEmpty(TonieInfoString))
                {
                    text += " " + TonieInfoString;
                }
            }

            if (text == "")
            {
                if (statusStrip1.Visible)
                {
                    statusStrip1.Hide();
                }
            }
            else
            {
                if (!statusStrip1.Visible)
                {
                    statusStrip1.Show();
                }
                statusLabel.Text = text.Trim().Trim('|').Trim();
            }
        }

        private void Proxmark3_UidFound(object sender, string e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => Proxmark3_UidFound(sender, e)));
                return;
            }

            bool found = false;

            if (e != null)
            {
                /* already handled this UID? then return */
                if (e == LastFoundUid)
                {
                    return;
                }

                foreach (ListViewItem item in lstTonies.Items)
                {
                    ListViewTag tag = item.Tag as ListViewTag;

                    if (tag.Uid == e)
                    {
                        if (lstTonies.SelectedItems.Count == 1 && lstTonies.SelectedItems[0] == item)
                        {
                            return;
                        }
                        foreach (ListViewItem sel in lstTonies.SelectedItems)
                        {
                            sel.Selected = false;
                        }
                        item.Selected = true;
                        item.EnsureVisible();
                        ActiveControl = lstTonies;
                        found = true;
                        AutoSelected = true;
                    }
                }
            }

            LastFoundUid = e;

            /* tag was removed and the selected item was auto selected because tag was found, deselect again */
            if (!found && AutoSelected)
            {
                foreach (ListViewItem sel in lstTonies.SelectedItems)
                {
                    sel.Selected = false;
                }
            }
        }

private async void reportNFCTagToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (RfidReader == null || AsyncTagActionThread != null)
            {
                return;
            }

            AsyncTagActionThread = new SafeThread(() =>
            {
                try
                {
                    byte[] data = RfidReader.ReadMemory();

                    Invoke(new Action(async () =>
                    {

                        if (data != null)
                        {
                            StringBuilder str = new StringBuilder();

                            str.AppendLine(" Reporting NFC tag content");
                            str.AppendLine("-----------------------------------");
                            str.AppendLine("UUID:        " + BitConverter.ToString(data.Take(8).ToArray()).Replace("-", ""));
                            str.AppendLine("Memory:      " + BitConverter.ToString(data.Skip(8).ToArray()).Replace("-", ""));

                            ReportForm form = new ReportForm(str.ToString());

                            if (form.ShowDialog() == DialogResult.OK)
                            {
                                Settings.Username = form.Username;
                                SaveSettings();

                                DiagStatus ret= await DiagnosticsSendInfo(str.ToString(), form.Username, form.Message, "Tag Dump.txt");
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
                        else
                        {
                            MessageBox.Show("No tag found. Please make sure you position it correctly.", "Failed");
                        }
                    }));
                }
                catch (Exception ex)
                {
                }
                AsyncTagActionThread = null;
            }, "AsyncTagActionThread");
            AsyncTagActionThread.Start();
            TagOperationDialog opDlg = new TagOperationDialog(true);

            opDlg.Show();

            await Task.Run(() =>
            {
                while (AsyncTagActionThread != null)
                {
                    Thread.Sleep(100);
                    if (opDlg.DialogResult == DialogResult.Cancel)
                    {
                        AsyncTagActionThread.Abort();
                        AsyncTagActionThread = null;
                    }
                }
                Invoke(new Action(() => opDlg.Close()));
            });
        }

        private async void reportProxmarkAnToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TagOperationDialog opDlg = new TagOperationDialog();

            opDlg.Show();
            Proxmark3.MeasurementResult result = RfidReader.MeasureAntenna();

            opDlg.Close();

            if (result == null)
            {
                MessageBox.Show("Measurement failed.", "Failed");
                return;
            }

            string content = " Reporting Proxmark3 antenna performance" + Environment.NewLine;
            content +=       "------------------------------------------" + Environment.NewLine;

            content += "HF:     " + result.vHF.ToString("0.00", CultureInfo.InvariantCulture) + " V" + Environment.NewLine;
            content += "LF125:  " + result.vLF125.ToString("0.00", CultureInfo.InvariantCulture) + " V" + Environment.NewLine;
            content += "LF134:  " + result.vLF134.ToString("0.00", CultureInfo.InvariantCulture) + " V" + Environment.NewLine;
            content += "LFfopt: " + (result.GetPeakFrequency() / 1000.0f).ToString("0.00", CultureInfo.InvariantCulture) + " kHz" + Environment.NewLine;
            content += "LFVopt: " + result.peakV.ToString("0.00", CultureInfo.InvariantCulture) + " V" + Environment.NewLine;

            ReportForm form = new ReportForm(content);

            if (form.ShowDialog() == DialogResult.OK)
            {
                Settings.Username = form.Username;
                SaveSettings();

                DiagStatus ret = await DiagnosticsSendInfo(content, form.Username, form.Message, "Antenna Report.txt");
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

        private async void readContentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (RfidReader == null || AsyncTagActionThread != null)
            {
                return;
            }

            AsyncTagActionThread = new SafeThread(() =>
            {
                try
                {
                    byte[] data = RfidReader.ReadMemory();

                    Invoke(new Action(() =>
                    {
                        if (data != null)
                        {
                            TagDumpDialog dlg = new TagDumpDialog(true, BitConverter.ToString(data).Replace("-", ""));
                            dlg.ShowDialog();
                        }
                        else
                        {
                            MessageBox.Show("No tag found. Please make sure you position it correctly.", "Failed");
                        }
                    }));
                }
                catch (Exception ex)
                {
                }
                AsyncTagActionThread = null;
            }, "AsyncTagActionThread");
            AsyncTagActionThread.Start();
            TagOperationDialog opDlg = new TagOperationDialog(true);

            opDlg.Show();

            await Task.Run(() =>
            {
                while (AsyncTagActionThread != null)
                {
                    Thread.Sleep(100);
                    if (opDlg.DialogResult == DialogResult.Cancel)
                    {
                        AsyncTagActionThread.Abort();
                        AsyncTagActionThread = null;
                    }
                }
                Invoke(new Action(() => opDlg.Close()));
            });
        }

        private async void emulateTagToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (RfidReader == null || AsyncTagActionThread != null)
            {
                return;
            }

            string previous = "";

            while (true)
            {
                TagDumpDialog dlg = new TagDumpDialog(false, previous);

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    byte[] data = null;

                    previous = dlg.String;

                    try
                    {
                        data = Helpers.ConvertHexStringToByteArray(previous);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to parse '" + previous + "'."+Environment.NewLine+ "The hex code must represent 40 byte in hexadecimal numbers:" + Environment.NewLine + "8 byte UUID and 32 byte data.");
                        continue;
                    }

                    AsyncTagActionThread = new SafeThread(() =>
                    {
                        try
                        {
                            RfidReader.EmulateTag(data);
                        }
                        catch (Exception ex)
                        {
                        }
                        AsyncTagActionThread = null;
                    }, "AsyncTagActionThread");
                    AsyncTagActionThread.Start();
                }
                break;
            }

            TagOperationDialog opDlg = new TagOperationDialog();

            opDlg.Show();

            await Task.Run(() =>
            {
                while (AsyncTagActionThread != null)
                {
                    Thread.Sleep(100);
                    if (opDlg.DialogResult == DialogResult.Cancel)
                    {
                        AsyncTagActionThread.Abort();
                        AsyncTagActionThread = null;
                    }
                }
                Invoke(new Action(() => opDlg.Close()));
            });
        }

        private void autodetectionEnabledToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            Settings.NfcEnabled = autodetectionEnabledToolStripMenuItem.Checked;
            SaveSettings();
            UpdateNfcReader();
        }

        private void UpdateNfcReader()
        {
            if (Settings.NfcEnabled)
            {
                if (RfidReader == null)
                {
                    if (Settings.NfcType == "Proxmark3")
                    {
                        RfidReader = new Proxmark3();
                        RfidReader.UidFound += Proxmark3_UidFound;
                        RfidReader.DeviceFound += Proxmark3_DeviceFound;
                        RfidReader.FlashRequest += Proxmark3_FlashRequest;
                        RfidReader.FlashResult += Proxmark3_FlashResult;
                        RfidReader.Start();
                    }
                    else if (Settings.NfcType == "Pn5180Esp")
                    {
                        RfidReader = new Pn5180Esp();
                        RfidReader.UidFound += Proxmark3_UidFound;
                        RfidReader.DeviceFound += Proxmark3_DeviceFound;
                        RfidReader.Start();
                    }
                }
            }
            else
            {
                if (RfidReader != null)
                {
                    RfidReader.Stop();
                    RfidReader = null;
                }
            }
        }

        private void autodetectionEnabledToolStripMenuItem_Click(object sender, EventArgs e)
        {
            autodetectionEnabledToolStripMenuItem.Checked ^= true;
        }

        private void enableDebugModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            enableDebugModeToolStripMenuItem.Checked ^= true;

            Settings.DebugWindow = enableDebugModeToolStripMenuItem.Checked;
            SaveSettings();

            if (Settings.DebugWindow)
            {
                LogWindow.LogLevel = LogWindow.eLogLevel.DebugVerbose;
                Log.Show();
            }
            else
            {
                LogWindow.LogLevel = LogWindow.eLogLevel.Warning;
                Log.Hide();
            }
        }

        private void measureAnteannaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TagOperationDialog opDlg = new TagOperationDialog();

            opDlg.Show();
            Proxmark3.MeasurementResult result = RfidReader.MeasureAntenna();

            opDlg.Close();

            if (result == null)
            {
                MessageBox.Show("Measurement failed.", "Failed");
                return;
            }

            PlotAntennaForm form = new PlotAntennaForm(result);

            form.ShowDialog();
        }

        private void flashFirmwareToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();

            dlg.Title = "Please select the fullimage.elf you want to flash";
            dlg.Filter = "Firmware ELF files (*.elf)|*.elf|All files (*.*)|*.*";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                RfidReader.EnterBootloader(dlg.FileName);
            }
        }

        private void flashBootloaderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();

            dlg.Title = "Please select the bootrom.elf you want to flash";
            dlg.Filter = "Bootloader ELF files (*.elf)|*.elf|All files (*.*)|*.*";
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                RfidReader.EnterBootloader(dlg.FileName);
            }
        }

        private void consoleModeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!consoleModeToolStripMenuItem.Checked)
            {
                consoleModeToolStripMenuItem.Checked = true;
                RfidReader.EnterConsole();
            }
            else
            {
                consoleModeToolStripMenuItem.Checked = false;
                RfidReader.ExitConsole();
            }
        }
    }
}
