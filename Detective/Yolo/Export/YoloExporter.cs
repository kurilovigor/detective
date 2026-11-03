using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media.Imaging;
using Detective.Database;
using Detective.Database.Entities;

namespace Detective.Yolo.Export
{
    public class YoloExporter
    {
        public void Export(DetectionDatabase database, string destinationFilePath)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (string.IsNullOrWhiteSpace(destinationFilePath)) throw new ArgumentException("Destination path cannot be null or empty.", nameof(destinationFilePath));

            database.Commit();
            var labels = database.Labels.FindAll().OrderBy(l => l.Id).ToList();
            var files = database.Files.FindAll().ToList();

            Export(labels, files, destinationFilePath);
        }

        public void Export(IEnumerable<LabelEntity> labelEntities, IEnumerable<FileEntity> fileEntities, string destinationFilePath)
        {
            if (labelEntities == null) throw new ArgumentNullException(nameof(labelEntities));
            if (fileEntities == null) throw new ArgumentNullException(nameof(fileEntities));
            if (string.IsNullOrWhiteSpace(destinationFilePath)) throw new ArgumentException("Destination path cannot be null or empty.", nameof(destinationFilePath));

            string fullDestinationPath = Path.GetFullPath(destinationFilePath);
            var exportDirectory = Path.GetDirectoryName(fullDestinationPath);
            if (!string.IsNullOrEmpty(exportDirectory) && !Directory.Exists(exportDirectory))
            {
                Directory.CreateDirectory(exportDirectory);
            }

            string projectFolderName = Path.GetFileNameWithoutExtension(fullDestinationPath);
            string projectDir = !string.IsNullOrEmpty(exportDirectory)
                ? Path.Combine(exportDirectory, projectFolderName)
                : projectFolderName;

            // Create directory structure:
            // <projectFolderName>/images/train
            // <projectFolderName>/images/val
            // <projectFolderName>/labels/train
            // <projectFolderName>/labels/val
            string imagesTrainDir = Path.Combine(projectDir, "images", "train");
            string imagesValDir = Path.Combine(projectDir, "images", "val");
            string labelsTrainDir = Path.Combine(projectDir, "labels", "train");
            string labelsValDir = Path.Combine(projectDir, "labels", "val");

            Directory.CreateDirectory(imagesTrainDir);
            Directory.CreateDirectory(imagesValDir);
            Directory.CreateDirectory(labelsTrainDir);
            Directory.CreateDirectory(labelsValDir);

            // 1. Collect ordered label names
            var labels = labelEntities
                .OrderBy(l => l.Id)
                .Select(l => l.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .Distinct()
                .ToList();

            var files = fileEntities.ToList();

            // Append any labels found in detections that might not be in database.Labels
            foreach (var file in files)
            {
                if (file.Detections != null)
                {
                    foreach (var detection in file.Detections)
                    {
                        if (!string.IsNullOrWhiteSpace(detection.Label) && !labels.Contains(detection.Label))
                        {
                            labels.Add(detection.Label);
                        }
                    }
                }
            }

            // 2. Export labels to YAML file
            var ymlContent = BuildDataYaml(labels, projectFolderName);
            File.WriteAllText(fullDestinationPath, ymlContent, new UTF8Encoding(false));

            // 3. Export annotations for each image into <projectDir>/labels/train
            foreach (var file in files)
            {
                if (file.Detections == null || file.Detections.Count == 0 || string.IsNullOrWhiteSpace(file.Path))
                {
                    continue;
                }

                string imageFullPath = Path.IsPathRooted(file.Path)
                    ? file.Path
                    : Path.GetFullPath(file.Path);

                if (!File.Exists(imageFullPath))
                {
                    if (!string.IsNullOrEmpty(exportDirectory))
                    {
                        var fallbackPath = Path.Combine(exportDirectory, Path.GetFileName(file.Path));
                        if (File.Exists(fallbackPath))
                        {
                            imageFullPath = fallbackPath;
                        }
                    }

                    if (!File.Exists(imageFullPath))
                    {
                        var trainFallback = Path.Combine(imagesTrainDir, Path.GetFileName(file.Path));
                        if (File.Exists(trainFallback))
                        {
                            imageFullPath = trainFallback;
                        }
                    }
                }

                var (imageWidth, imageHeight) = GetImageDimensions(imageFullPath);
                if (imageWidth <= 0 || imageHeight <= 0)
                {
                    continue;
                }

                var annotationLines = new List<string>();
                foreach (var detection in file.Detections)
                {
                    if (string.IsNullOrWhiteSpace(detection.Label))
                        continue;

                    int classId = labels.IndexOf(detection.Label);
                    if (classId < 0)
                        continue;

                    if (detection.Width <= 0 || detection.Height <= 0)
                        continue;

                    double xCenter = (detection.X + detection.Width / 2.0) / imageWidth;
                    double yCenter = (detection.Y + detection.Height / 2.0) / imageHeight;
                    double width = (double)detection.Width / imageWidth;
                    double height = (double)detection.Height / imageHeight;

                    xCenter = Math.Clamp(xCenter, 0.0, 1.0);
                    yCenter = Math.Clamp(yCenter, 0.0, 1.0);
                    width = Math.Clamp(width, 0.0, 1.0);
                    height = Math.Clamp(height, 0.0, 1.0);

                    string line = FormattableString.Invariant($"{classId} {xCenter} {yCenter} {width} {height}");
                    annotationLines.Add(line);
                }

                if (annotationLines.Count == 0)
                {
                    continue;
                }

                string txtFileName = Path.GetFileNameWithoutExtension(file.Path) + ".txt";
                string targetTxtPath = Path.Combine(labelsTrainDir, txtFileName);
                File.WriteAllLines(targetTxtPath, annotationLines, new UTF8Encoding(false));
            }
        }

        public static string BuildDataYaml(IReadOnlyList<string> labels, string projectFolderName = "data")
        {
            var sb = new StringBuilder();
            sb.AppendLine(FormattableString.Invariant($"path: {projectFolderName}"));
            sb.AppendLine("train: images/train");
            sb.AppendLine("val: images/val");
            sb.AppendLine();
            sb.AppendLine(FormattableString.Invariant($"nc: {labels.Count}"));
            var formattedNames = labels.Select(l => $"'{l.Replace("'", "''")}'");
            sb.AppendLine($"names: [{string.Join(", ", formattedNames)}]");
            return sb.ToString();
        }

        public static (int width, int height) GetImageDimensions(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return (0, 0);
            }

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                if (decoder.Frames.Count > 0)
                {
                    var frame = decoder.Frames[0];
                    return (frame.PixelWidth, frame.PixelHeight);
                }
            }
            catch
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    return (bitmap.PixelWidth, bitmap.PixelHeight);
                }
                catch
                {
                    // ignored
                }
            }

            return (0, 0);
        }
    }
}

