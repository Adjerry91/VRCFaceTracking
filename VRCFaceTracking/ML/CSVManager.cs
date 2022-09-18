using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ViveSR.anipal.Lip;
using VRCFaceTracking.Params;

namespace VRCFaceTracking.ML
{
    public static class CSVManager
    {
        public static string OutputArchive;

        private static readonly Version CSV_VERSION = new Version(1, 0, 0);
        private const string LOG_DIRECTORY_NAME = "VRCFaceTracking Logs";
        private const string delimiter = ",";
        private static string context;
        private static string logDirectoryPath;
        private static string eyeImageDirectory;
        private static string lipImageDirectory;

        private static StringBuilder sb;
        private static StreamWriter writer;
        private static int counter = 0;

        public static void Initialize(string logDirectory = null)
        {
            if (!(UnifiedTrackingData.LatestEyeData.SupportsImage || UnifiedTrackingData.LatestLipData.SupportsImage))
            {
                Logger.Warning("Neither eye or lip images were detected! CSVManager will be disabled for this session.");
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

            context = SanitizedLogName(DateTime.UtcNow.ToString());

            eyeImageDirectory = Path.Combine(logDirectoryPath, context, "eyeImages");
            Directory.CreateDirectory(eyeImageDirectory);
            lipImageDirectory = Path.Combine(logDirectoryPath, context, "lipImages");
            Directory.CreateDirectory(lipImageDirectory);

            writer = new StreamWriter(new FileStream(
                Path.Combine(logDirectoryPath, context, $"{Environment.MachineName} - v{CSV_VERSION} - {context}.csv"),
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
            if (counter == 100) // Is called every 10ms, so 10*100 = 1000ms or once a second
            {
                var dateTime = SanitizedLogName(DateTime.UtcNow.Ticks.ToString());
                sb.Append(dateTime);
                sb.Append(delimiter);

                CollectEyes(eyeData, dateTime);
                CollectLips(lipData, dateTime);
            
                if (writer != null)
                    writer.WriteLine(sb.ToString());

                sb.Clear();
                counter = 0;
            }
            else
            {
                counter++;
            }
        }

        private static void CollectEyes(EyeTrackingData eyeData, string dateTime)
        {
            CollectEye(eyeData.Left, dateTime);
            CollectEye(eyeData.Right, dateTime);
            CollectEye(eyeData.Combined, dateTime);

            sb.Append(eyeData.EyesDilation);
            sb.Append(delimiter);

            sb.Append(eyeData.EyesPupilDiameter);
            sb.Append(delimiter);

            sb.Append(eyeData.SupportsImage);
            sb.Append(delimiter);

            sb.Append($"eye.Image.{dateTime}");
            sb.Append(delimiter);
        }

        private static void CollectEye(Eye eye, string dateTime)
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

            if (Utils.HasAdmin && UnifiedTrackingData.LatestEyeData.ImageData != null)
            {
                SaveImage8(UnifiedTrackingData.LatestEyeData.ImageData,
                    new Vector2(UnifiedTrackingData.LatestEyeData.ImageSize.x, UnifiedTrackingData.LatestEyeData.ImageSize.y),
                    Path.Combine(eyeImageDirectory, $"{dateTime}.bmp"));
            }
        }

        private static void CollectLips(LipTrackingData lipData, string dateTime)
        {
            sb.Append(string.Join(delimiter, lipData.LatestShapes));
            sb.Append(delimiter);

            sb.Append(lipData.SupportsImage);
            sb.Append(delimiter);

            sb.Append($"lip.Image.{dateTime}");
            sb.Append(delimiter);

            if (UnifiedTrackingData.LatestLipData.ImageData != null)
            {
                SaveImage8(UnifiedTrackingData.LatestLipData.ImageData,
                    new Vector2(UnifiedTrackingData.LatestLipData.ImageSize.x, UnifiedTrackingData.LatestLipData.ImageSize.y),
                    Path.Combine(lipImageDirectory, $"{dateTime}.bmp"));
            }
        }

        private static void SaveImage32(byte[] image, Vector2 size, string path)
        {
            var imageBytes = new byte[(int)size.x * (int)size.y * 4];
            for (var i = 0; i < size.x * size.y; i++)
            {
                imageBytes[i * 4] = image[i];
                imageBytes[i * 4 + 1] = image[i];
                imageBytes[i * 4 + 2] = image[i];
                imageBytes[i * 4 + 3] = 255;
            }

            var imageBitmap = new Bitmap((int)size.x, (int)size.y, PixelFormat.Format32bppArgb);
            var bitmapData = imageBitmap.LockBits(new Rectangle(0, 0, (int)size.x, (int)size.y), ImageLockMode.WriteOnly, imageBitmap.PixelFormat);
            Marshal.Copy(imageBytes, 0, bitmapData.Scan0, imageBytes.Length);
            imageBitmap.UnlockBits(bitmapData);
            imageBitmap.Save(path);
        }

        private static void SaveImage8(byte[] image, Vector2 size, string path)
        {
            var bitmap = new Bitmap((int)size.x, (int)size.y, PixelFormat.Format8bppIndexed);
            var palette = bitmap.Palette;
            for (var i = 0; i < 256; i++)
                palette.Entries[i] = Color.FromArgb(i, i, i);
            bitmap.Palette = palette;
            var data = bitmap.LockBits(new Rectangle(0, 0, (int)size.x, (int)size.y), ImageLockMode.WriteOnly, bitmap.PixelFormat);
            Marshal.Copy(image, 0, data.Scan0, image.Length);
            bitmap.UnlockBits(data);
            bitmap.Save(path);
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

            var InputDirectory = Path.Combine(logDirectoryPath, context);
            var OutputFilename = Path.Combine(logDirectoryPath, $"{context}.zip");
            OutputArchive = OutputFilename;

            using (Stream zipStream = new FileStream(Path.GetFullPath(OutputFilename), FileMode.Create, FileAccess.Write))
            using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                foreach (var filePath in Directory.GetFiles(InputDirectory, "*.*", SearchOption.AllDirectories))
                {
                    var relativePath = filePath.Replace(InputDirectory, string.Empty);
                    using (Stream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    using (Stream fileStreamInZip = archive.CreateEntry(relativePath).Open())
                        fileStream.CopyTo(fileStreamInZip);
                }
            }

        }
    }
}
