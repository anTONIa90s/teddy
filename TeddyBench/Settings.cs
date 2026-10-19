using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TeddyBench
{
    public class Settings
    {
        public string Username = "";
        public bool NfcEnabled = false;
        public string NfcType = "Proxmark3";
        public bool DebugWindow = false;
        public bool DownloadJson = true;
        public string BackupDirectory = "";

        public static readonly string DefaultFile = Path.Combine("cache", "teddyBench.cfg");

        public static Settings FromFile(string file)
        {
            Settings s = null;

            try
            {
                /* migrate settings from the old location next to the exe */
                if (!File.Exists(file) && File.Exists(Path.GetFileName(file)))
                {
                    file = Path.GetFileName(file);
                }
                s = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(file));
            }
            catch(Exception ex)
            {
            }

            if(s != null)
            {
                return s;
            }

            return new Settings();
        }

        public bool Save(string file)
        {
            try
            {
                string dir = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(file, JsonConvert.SerializeObject(this, Formatting.Indented));
                return true;
            }
            catch (Exception e)
            {
            }
            return false;
        }
    }
}
