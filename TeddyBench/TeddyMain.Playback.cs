using NAudio.Wave;
using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using TonieFile;

namespace TeddyBench
{
    public partial class TeddyMain
    {
        private void btnPlay_Click(object sender, EventArgs e)
        {
            if (PlayThread == null)
            {
                if (lstTonies.SelectedItems.Count != 1)
                {
                    return;
                }

                var selected = lstTonies.SelectedItems[0];
                ListViewTag tag = selected.Tag as ListViewTag;
                string file = tag.FileName;

                PlayThread = new SafeThread(() =>
                {
                    try
                    {
                        TonieAudio dump = TonieAudio.FromFile(file);

                        using (var waveOut = new WaveOutEvent())
                        {
                            using (var reader = new OpusWaveStream(new MemoryStream(dump.Audio), 48000, 2))
                            {

                                waveOut.Init(reader);
                                waveOut.PlaybackStopped += WaveOut_PlaybackStopped;
                                waveOut.Play();
                                LogWindow.Log(LogWindow.eLogLevel.Debug, "Playback started");

                                while (!PlayThreadStop)
                                {
                                    Thread.Sleep(100);
                                    int pos = (int)reader.OpusDecoder.CurrentTime.TotalSeconds;
                                    int len = (int)reader.OpusDecoder.TotalTime.TotalSeconds;

                                    string time = reader.OpusDecoder.CurrentTime.ToString(@"hh\:mm\:ss") + "/" + reader.OpusDecoder.TotalTime.ToString(@"hh\:mm\:ss");

                                    if (TrackNewPosition >= 0)
                                    {
                                        TimeSpan newPos = new TimeSpan(0, 0, 0, TrackNewPosition);
                                        reader.SeekTo(newPos);
                                        TrackNewPosition = -1;
                                    }

                                    BeginInvoke(new Action(() =>
                                    {
                                        if (!TrackMouseDown)
                                        {
                                            if (trackPlayPosition.Maximum != len)
                                            {
                                                trackPlayPosition.Maximum = len;
                                                trackPlayPosition.TickFrequency = 60;
                                            }
                                            trackPlayPosition.Value = pos;
                                        }
                                        lblPlayTime.Text = time;
                                    }));
                                }
                                waveOut.Stop();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Failed to play file '" + tag.FileName + "': " + ex.Message);
                    }
                }, "PlayThread");

                btnPlay.Text = "Stop";
                PlayThreadStop = false;
                PlayThread.Start();
            }
            else
            {
                PlayThreadStop = true;
            }
        }

        private void btnShowChapters_Click(object sender, EventArgs e)
        {
            if (lstTonies.SelectedItems.Count != 1)
            {
                MessageBox.Show("Please select one Tonie first.", "Show chapters");
                return;
            }

            ListViewTag tag = lstTonies.SelectedItems[0].Tag as ListViewTag;
            if (tag == null)
            {
                return;
            }

            using (var dialog = new ChapterListDialog(tag.Info?.Title, GetCustomChapterTitles(tag.Hash) ?? tag.Info?.Tracks))
            {
                dialog.ShowDialog(this);
            }
        }

        private void WaveOut_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            LogWindow.Log(LogWindow.eLogLevel.Debug, "Playback stopped");

            BeginInvoke(new Action(() =>
            {
                PlayThreadStop = true;
                btnPlay.Text = "Play file";
                PlayThread = null;
            }));
        }

        private void trackPlayPosition_Scroll(object sender, EventArgs e)
        {
            TrackNewPosition = trackPlayPosition.Value;
        }

        private void trackPlayPosition_MouseDown(object sender, MouseEventArgs e)
        {
            TrackMouseDown = true;
        }

        private void trackPlayPosition_MouseUp(object sender, MouseEventArgs e)
        {
            TrackMouseDown = false;
        }
    }
}
