using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using ViveSR.anipal.Lip;

namespace VRCFaceTracking.ML
{
    public static class CSVManager
    {
        private static readonly Version CSV_VERSION = new Version(1, 0, 0);
        private const string LOG_DIRECTORY_NAME = "VRCFaceTracking Logs";
        private const string delimiter = ",";
        private static string logDirectoryPath;
        private static string eyeImageDirectory;
        private static string lipImageDirectory;

        private static StringBuilder sb;
        private static StreamWriter writer;

        private static MemoryStream eyeMS;
        private static FileStream eyeFS;
        private static MemoryStream lipMS;
        private static FileStream lipFS;

        public static void Initialize(string logDirectory = null)
        {
            if (!(UnifiedTrackingData.LatestEyeData.SupportsImage && UnifiedTrackingData.LatestLipData.SupportsImage))
            {
                Logger.Warning("Neither eye or lip images were detected! CSVManager will be disabled for this session");
                return;
            }

            if (writer != null || sb != null)
            {
                Logger.Error("Can not have multiple instances of the CSVManager manager running!");
                return;
            }
            else if (string.IsNullOrEmpty(logDirectory)) // Place the logs in a subfolder beneath VRCFT
            {
                logDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), LOG_DIRECTORY_NAME);
                Directory.CreateDirectory(LOG_DIRECTORY_NAME);
            }                
            else if (Directory.Exists(logDirectory)) // Place the logs in a given folder, creating it if it does not exist
            {
                logDirectoryPath = Path.Combine(logDirectory, LOG_DIRECTORY_NAME);
                Directory.CreateDirectory(logDirectoryPath);
            }
            else // If an invalid directory was given to us
            {
                Logger.Error("Invalid path was given. Data logging will not be active for this session.");
                return;
            }

            var context = SanitizedLogName(DateTime.UtcNow.ToString());

            eyeImageDirectory = Path.Combine(logDirectoryPath, context, "eyeImages");
            Directory.CreateDirectory(eyeImageDirectory);
            lipImageDirectory = Path.Combine(logDirectoryPath, context, "lipImages");
            Directory.CreateDirectory(lipImageDirectory);

            // Will FS be overriden? Do we need to make a new stream per update?
            // CreateNew will throw an exception if this is the case. TODO Test!
            eyeMS = new MemoryStream(UnifiedTrackingData.LatestEyeData.ImageData);
            eyeFS = new FileStream(eyeImageDirectory, FileMode.CreateNew);
            lipMS = new MemoryStream(UnifiedTrackingData.LatestLipData.ImageData);
            lipFS = new FileStream(lipImageDirectory, FileMode.CreateNew);

            writer = new StreamWriter(new FileStream(
                Path.Combine(logDirectoryPath, $"{Environment.MachineName} - v{CSV_VERSION} - {context}.csv"),
                FileMode.Create, 
                FileAccess.Write));
            writer.AutoFlush = true;

            sb = new StringBuilder();
            LabelColumns();
            
            UnifiedTrackingData.OnUnifiedDataUpdated += Collect;
        }

        private static void LabelColumns()
        {
            sb.Append("dateTime");
            sb.Append(delimiter);

            LabelEyes();
            LabelLips();

            writer.WriteLine(sb.ToString());
            sb.Clear();
        }

        private static void LabelEyes()
        {
            string[] names = new string[] { "Left", "Right", "Combined" };
            for (int i = 0; i < names.Length; i++)
            {
                sb.Append($"eye.{names[i]}.Openness");
                sb.Append(delimiter);

                sb.Append($"eye.{names[i]}.Widen");
                sb.Append(delimiter);

                sb.Append($"eye.{names[i]}.Squeeze");
                sb.Append(delimiter);

                sb.Append($"eye.{names[i]}.Look.x");
                sb.Append(delimiter);

                sb.Append($"eye.{names[i]}.Look.y");
                sb.Append(delimiter);
            }

            sb.Append("eye.eyeData.EyesDilation");
            sb.Append(delimiter);

            sb.Append("eye.eyeData.EyesPupilDiameter");
            sb.Append(delimiter);

            sb.Append("eye.SupportsImage");
            sb.Append(delimiter);

            sb.Append("eye.ImagePath");
            sb.Append(delimiter);
        }

        private static void LabelLips()
        {
            var lipShapes = (LipShape_v2[])Enum.GetValues(typeof(LipShape_v2));
            for (int i = 0; i < lipShapes.Length; i++)
            {
                sb.Append(string.Join(delimiter, $"lip.{lipShapes[i]}"));
                sb.Append(delimiter);
            }

            sb.Append("lip.SupportsImage");
            sb.Append(delimiter);

            sb.Append("lip.ImagePath");
            sb.Append(delimiter);
        }

        private static void Collect(EyeTrackingData eyeData, LipTrackingData lipData)
        {
            var dateTime = SanitizedLogName(DateTime.Now.ToString());
            sb.Append(dateTime);
            sb.Append(delimiter);

            CollectEyes(eyeData, dateTime);
            CollectLips(lipData, dateTime);
            
            writer.WriteLine(sb.ToString());
            sb.Clear();
        }

        private static void CollectEyes(EyeTrackingData eyeData, string dateTime)
        {
            CollectEye(eyeData.Left);
            CollectEye(eyeData.Right);
            CollectEye(eyeData.Combined);

            sb.Append(eyeData.EyesDilation);
            sb.Append(delimiter);

            sb.Append(eyeData.EyesPupilDiameter);
            sb.Append(delimiter);

            sb.Append(eyeData.SupportsImage);
            sb.Append(delimiter);

            sb.Append($"eye.Image.{dateTime}");
            sb.Append(delimiter);
        }

        private static void CollectEye(Eye eye)
        {
            sb.Append(eye.Openness);
            sb.Append(delimiter);

            sb.Append(eye.Widen);
            sb.Append(delimiter);

            sb.Append(eye.Squeeze);
            sb.Append(delimiter);

            sb.Append(eye.Look.x);
            sb.Append(delimiter);
            
            sb.Append(eye.Look.y);
            sb.Append(delimiter);

            eyeMS.WriteTo(eyeFS);
        }

        private static void CollectLips(LipTrackingData lipData, string dateTime)
        {
            sb.Append(string.Join(delimiter, lipData.LatestShapes));
            sb.Append(delimiter);

            sb.Append(lipData.SupportsImage);
            sb.Append(delimiter);

            sb.Append($"lip.Image.{dateTime}");
            sb.Append(delimiter);

            lipMS.WriteTo(lipFS);
        }

        private static string SanitizedLogName(string oldName)
        {
            var charsToRemove = new char[] { '/', ':' };
            foreach (var c in charsToRemove)
            {
                oldName = oldName.Replace(c, '.');
            }
            return oldName;
        }

        public static List<TEnum> GetEnumList<TEnum>() where TEnum : Enum
            => ((TEnum[])Enum.GetValues(typeof(TEnum))).ToList();

        public static void Teardown()
        {
            if (writer == null || sb == null)
            {
                Logger.Error("Streamwriter does not exist in the current context");
                return;
            }

            writer.Close();
            writer.Dispose();

            eyeFS.Close();
            eyeFS.Dispose();
            eyeMS.Close();
            eyeMS.Dispose();

            lipFS.Close();
            lipFS.Dispose();
            lipMS.Close();
            lipMS.Dispose();
        }
    }
}
