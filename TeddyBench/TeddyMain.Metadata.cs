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
public void LoadJson(bool force = false)
        {
            try
            {
                string jsonContent = "";

                if (Settings.DownloadJson || force)
                {
                    try
                    {
                        TonieInfoString = "| Downloading...";
                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create("https://api.revvox.de/tonies.json?source=TeddyBench&version=" + ThisAssembly.Git.BaseTag);
                        HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                        TextReader reader = new StreamReader(response.GetResponseStream());
                        jsonContent = reader.ReadToEnd();
                        TonieInfoString = "| Downloaded";
                    }
                    catch (Exception e)
                    {
                        TonieInfoString = "| Download Failed - attempting local fallback";
                        ReportException("Downloader", e);

                        try
                        {
                            if (File.Exists("tonies.json"))
                            {
                                jsonContent = File.ReadAllText("tonies.json");
                                TonieInfoString = "| Loaded local tonies.json";
                            }
                            else
                            {
                                TonieInfoString = "| Download Failed - no local tonies.json available";
                                return;
                            }
                        }
                        catch (Exception fe)
                        {
                            TonieInfoString = "| Local fallback failed";
                            ReportException("Reading local tonies.json", fe);
                            return;
                        }
                    }

                    if (string.IsNullOrEmpty(jsonContent))
                    {
                        return;
                    }
                    bool written = false;
                    for (int loop = 0; loop < 5; loop++)
                    {
                        try
                        {
                            File.WriteAllText("tonies.json", jsonContent);
                            written = true;
                            break;
                        }
                        catch (IOException e)
                        {
                            ReportException("Writing tonies.json", e);
                            Thread.Sleep(100);
                        }
                    }

                    if (!written)
                    {
                        TonieInfoString = "| Writing Failed - is the tonies.json accessible?";
                    }
                }
                else
                {
                    try
                    {
                        jsonContent = File.ReadAllText("customTonies.json");
                    }
                    catch (Exception e)
                    {
                        TonieInfoString = "| Reading tonies.json Failed";
                        TonieInfo = new TonieTools.TonieData[0];
                    }
                }

                if(string.IsNullOrEmpty(jsonContent))
                {
                    /* failed to read or download. TonieInfoString is already set */
                    return;
                }

                lock (TonieInfoLock)
                {
                    try
                    {
                        TonieInfoString = "| Parsing...";
                        TonieInfo = JsonConvert.DeserializeObject<TonieTools.TonieData[]>(jsonContent);
                        TonieInfoString = "";
                    }
                    catch (Exception e)
                    {
                        TonieInfoString = "| Parsing Failed";
                        TonieInfo = new TonieTools.TonieData[0];
                        ReportException("Parse tonies.json", e);
                    }

                    try
                    {
                        TonieInfoCustom = LoadCustomTonieInfo(File.ReadAllText("customTonies.json"));
                    }
                    catch (FileNotFoundException e)
                    {
                        TonieInfoCustom.Clear();
                    }
                    catch (Exception e)
                    {
                        TonieInfoCustom.Clear();
                    }
                    TonieInfoGeneration++;
                }
            }
            catch (Exception e)
            {
                return;
            }
        }

        void SaveJson()
        {
            lock (TonieInfoLock)
            {
                try
                {
                    File.WriteAllText("customTonies.json", JsonConvert.SerializeObject(TonieInfoCustom, Formatting.Indented));
                }
                catch (Exception e)
                {
                    return;
                }
            }
        }

private Image GetImage(string pic, string hash)
        {
            string cacheFileName = Path.Combine("cache", hash + ".png");

            try
            {
                if (!Directory.Exists("cache"))
                {
                    Directory.CreateDirectory("cache");
                }

                if (File.Exists(cacheFileName))
                {
                    return Image.FromFile(cacheFileName);
                }
            }
            catch (Exception ex)
            {
            }

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(pic);
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Image img = ResizeImage(Image.FromStream(response.GetResponseStream()), 128, 128);

                try
                {
                    img.Save(cacheFileName);
                }
                catch (Exception ex)
                {
                }
                return img;
            }
            catch (Exception ex)
            {
            }

            return null;
        }

        private static Dictionary<string, CustomTonieInfo> LoadCustomTonieInfo(string jsonContent)
        {
            var result = new Dictionary<string, CustomTonieInfo>();
            JObject entries = JObject.Parse(jsonContent);

            foreach (JProperty entry in entries.Properties())
            {
                CustomTonieInfo info;
                if (entry.Value.Type == JTokenType.String)
                {
                    // Migrate the legacy hash-to-title format in memory.
                    info = new CustomTonieInfo { Title = entry.Value.Value<string>() ?? "" };
                }
                else
                {
                    info = entry.Value.ToObject<CustomTonieInfo>() ?? new CustomTonieInfo();
                }

                result[entry.Name] = info;
            }

            return result;
        }

        private static CustomTonieInfo GetOrCreateCustomTonieInfo(string hash)
        {
            CustomTonieInfo info;
            if (!TonieInfoCustom.TryGetValue(hash, out info))
            {
                info = new CustomTonieInfo();
                TonieInfoCustom.Add(hash, info);
            }
            return info;
        }

        private static string[] GetCustomChapterTitles(string hash)
        {
            lock (TonieInfoLock)
            {
                CustomTonieInfo info;
                if (TonieInfoCustom.TryGetValue(hash, out info) && info.Chapters != null)
                {
                    return info.Chapters;
                }
            }
            return null;
        }

        private static string GetDesktopIniValue(string content, string sectionName, string key)
        {
            string currentSection = null;
            foreach (string line in content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                string trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    continue;
                }

                if (!string.Equals(currentSection, sectionName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int separator = line.IndexOf('=');
                if (separator >= 0 && string.Equals(line.Substring(0, separator).Trim(), key, StringComparison.OrdinalIgnoreCase))
                {
                    return line.Substring(separator + 1);
                }
            }

            return null;
        }

        private static string SetDesktopIniValue(string content, string sectionName, string key, string value)
        {
            List<string> lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
            if (lines.Count == 1 && lines[0].Length == 0)
            {
                lines.Clear();
            }

            int sectionStart = -1;
            int sectionEnd = lines.Count;
            for (int index = 0; index < lines.Count; index++)
            {
                if (string.Equals(lines[index].Trim(), "[" + sectionName + "]", StringComparison.OrdinalIgnoreCase))
                {
                    sectionStart = index;
                    for (int next = index + 1; next < lines.Count; next++)
                    {
                        string line = lines[next].Trim();
                        if (line.StartsWith("[") && line.EndsWith("]"))
                        {
                            sectionEnd = next;
                            break;
                        }
                    }
                    break;
                }
            }

            if (sectionStart < 0)
            {
                if (value == null)
                {
                    return string.Join(Environment.NewLine, lines);
                }

                if (lines.Count > 0 && lines[lines.Count - 1].Length != 0)
                {
                    lines.Add(string.Empty);
                }
                lines.Add("[" + sectionName + "]");
                lines.Add(key + "=" + value);
                return string.Join(Environment.NewLine, lines);
            }

            for (int index = sectionStart + 1; index < sectionEnd; index++)
            {
                int separator = lines[index].IndexOf('=');
                if (separator >= 0 && string.Equals(lines[index].Substring(0, separator).Trim(), key, StringComparison.OrdinalIgnoreCase))
                {
                    if (value == null)
                    {
                        lines.RemoveAt(index);
                    }
                    else
                    {
                        lines[index] = key + "=" + value;
                    }
                    return string.Join(Environment.NewLine, lines);
                }
            }

            if (value != null)
            {
                lines.Insert(sectionEnd, key + "=" + value);
            }
            return string.Join(Environment.NewLine, lines);
        }

        private static string NormalizeExplorerTitle(string title)
        {
            return title == null ? "" : title.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }

        private static string ReadDesktopIni(string path)
        {
            if (!File.Exists(path))
            {
                return string.Empty;
            }

            using (StreamReader reader = new StreamReader(path, Encoding.Unicode, true))
            {
                return reader.ReadToEnd().TrimStart('\uFEFF');
            }
        }

        private static bool HasTeddyBenchExplorerTitle(string directoryPath)
        {
            try
            {
                string desktopIniPath = Path.Combine(directoryPath, DesktopIniFileName);
                return File.Exists(desktopIniPath) &&
                    GetDesktopIniValue(ReadDesktopIni(desktopIniPath), TeddyBenchSection, "ExplorerTitle") != null;
            }
            catch
            {
                return false;
            }
        }

        private static void WriteDesktopIni(string directoryPath, string content)
        {
            string path = Path.Combine(directoryPath, DesktopIniFileName);
            bool exists = File.Exists(path);
            FileAttributes originalAttributes = exists ? File.GetAttributes(path) : FileAttributes.Normal;
            DirectoryInfo directory = new DirectoryInfo(directoryPath);
            FileAttributes originalDirectoryAttributes = directory.Attributes;

            if (exists)
            {
                File.SetAttributes(path, originalAttributes & ~(FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System));
            }
            directory.Attributes = originalDirectoryAttributes & ~FileAttributes.ReadOnly;

            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                using (StreamWriter writer = new StreamWriter(stream, Encoding.Unicode))
                {
                    writer.Write(content);
                }
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.SetAttributes(path, originalAttributes | FileAttributes.Hidden | FileAttributes.System);
                }
                directory.Attributes = originalDirectoryAttributes | FileAttributes.ReadOnly;
            }
        }

        private static bool TrySetExplorerTitle(string directoryPath, string title, out string error)
        {
            try
            {
                string normalizedTitle = NormalizeExplorerTitle(title);
                if (string.IsNullOrWhiteSpace(normalizedTitle))
                {
                    throw new ArgumentException("The title cannot be empty.");
                }

                string desktopIniPath = Path.Combine(directoryPath, DesktopIniFileName);
                string content = ReadDesktopIni(desktopIniPath);
                string existingInfoTip = GetDesktopIniValue(content, ShellClassInfoSection, "InfoTip");
                bool replaceInfoTip = string.IsNullOrWhiteSpace(existingInfoTip) ||
                    string.Equals(GetDesktopIniValue(content, TeddyBenchSection, "ExplorerInfoTip"), "1", StringComparison.Ordinal);

                content = SetDesktopIniValue(content, DocumentPropertiesSection, "Prop2", "31," + normalizedTitle);
                content = SetDesktopIniValue(content, TeddyBenchSection, "ExplorerTitle", normalizedTitle);
                content = SetDesktopIniValue(content, TeddyBenchSection, "ExplorerInfoTip", replaceInfoTip ? "1" : "0");
                if (replaceInfoTip)
                {
                    content = SetDesktopIniValue(content, ShellClassInfoSection, "InfoTip", normalizedTitle);
                }

                WriteDesktopIni(directoryPath, content);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryRemoveExplorerTitle(string directoryPath, out string error)
        {
            try
            {
                string desktopIniPath = Path.Combine(directoryPath, DesktopIniFileName);
                if (!File.Exists(desktopIniPath))
                {
                    error = null;
                    return true;
                }

                string content = ReadDesktopIni(desktopIniPath);
                string explorerTitle = GetDesktopIniValue(content, TeddyBenchSection, "ExplorerTitle");
                if (explorerTitle == null)
                {
                    error = null;
                    return true;
                }

                bool removeInfoTip =
                    string.Equals(GetDesktopIniValue(content, TeddyBenchSection, "ExplorerInfoTip"), "1", StringComparison.Ordinal) &&
                    string.Equals(GetDesktopIniValue(content, ShellClassInfoSection, "InfoTip"), explorerTitle, StringComparison.Ordinal);

                content = SetDesktopIniValue(content, DocumentPropertiesSection, "Prop2", null);
                content = SetDesktopIniValue(content, TeddyBenchSection, "ExplorerTitle", null);
                content = SetDesktopIniValue(content, TeddyBenchSection, "ExplorerInfoTip", null);
                if (removeInfoTip)
                {
                    content = SetDesktopIniValue(content, ShellClassInfoSection, "InfoTip", null);
                }

                WriteDesktopIni(directoryPath, content);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private string GetExplorerTitle(ListViewTag tag)
        {
            if (tag == null)
            {
                return null;
            }

            string hash = tag.Hash;
            if (string.IsNullOrWhiteSpace(hash))
            {
                try
                {
                    TonieAudio audio = GetTonieAudio(tag.FileName);
                    hash = BitConverter.ToString(audio.Header.Hash).Replace("-", "");
                    tag.Hash = hash;
                }
                catch
                {
                    return tag.Info?.Title;
                }
            }

            string title = tag.Info?.Title;
            lock (TonieInfoLock)
            {
                if (string.IsNullOrWhiteSpace(title))
                {
                    TonieTools.TonieData info = TonieInfo.FirstOrDefault(t => t.Hash.Contains(hash));
                    title = info?.Title;
                }

                CustomTonieInfo customInfo;
                if (TonieInfoCustom.TryGetValue(hash, out customInfo) && !string.IsNullOrWhiteSpace(customInfo.Title))
                {
                    title = customInfo.Title;
                }
            }

            return NormalizeExplorerTitle(title);
        }

        public class CustomTonieInfo
        {
            [JsonProperty("title")]
            public string Title = "";

            [JsonProperty("chapters")]
            public string[] Chapters;
        }

        private static string GetCustomImageKey(string hash)
        {
            return CustomImageKeyPrefix + hash;
        }

        private static string GetCustomImagePath(string hash)
        {
            return Path.Combine(CustomImageFolder, hash + ".png");
        }

        private static Image LoadResizedImage(string fileName)
        {
            byte[] imageBytes = File.ReadAllBytes(fileName);
            using (MemoryStream stream = new MemoryStream(imageBytes))
            using (Image img = Image.FromStream(stream))
            {
                return ResizeImage(img, 128, 128);
            }
        }

        private void ReplaceImageListImage(string key, Image image)
        {
            if (lstTonies.LargeImageList.Images.ContainsKey(key))
            {
                lstTonies.LargeImageList.Images.RemoveByKey(key);
            }

            lstTonies.LargeImageList.Images.Add(key, image);
        }

        private bool IsCustomTonieItem(ListViewItem item)
        {
            if (item == null)
            {
                return false;
            }

            return item.ImageKey == "custom" || item.ImageKey.StartsWith(CustomImageKeyPrefix);
        }

        private TonieAudio GetTonieAudio(string fileName)
        {
            FileInfo info = new FileInfo(fileName);
            if (CachedAudios.ContainsKey(fileName))
            {
                Tuple<TonieAudio, DateTime> cachedItem = CachedAudios[fileName];
                if (cachedItem.Item2 == info.LastWriteTime)
                {
                    return cachedItem.Item1;
                }
                CachedAudios.Remove(fileName);
            }

            TonieAudio file = TonieAudio.FromFile(fileName, false);
            CachedAudios.Add(fileName, new Tuple<TonieAudio, DateTime>(file, info.LastWriteTime));

            return file;
        }

        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            double srcRatio = (double)image.Width / image.Height;
            int dstWidth = width;
            int dstHeight = (int)(dstWidth / srcRatio);

            if (dstHeight > height)
            {
                dstHeight = height;
                dstWidth = (int)(dstHeight * srcRatio);
            }

            int dstX = (width - dstWidth) / 2;
            int dstY = (height - dstHeight) / 2;
            var destRect = new Rectangle(dstX, dstY, dstWidth, dstHeight);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }
    }
}
