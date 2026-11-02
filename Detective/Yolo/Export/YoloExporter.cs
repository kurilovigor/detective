using Detective.Database;
using Detective.Database.Entities;
using SharpYaml;
using System.IO;
using System.Text.Json;

namespace Detective.Yolo.Export
{
    public class YoloExporter
    {
        private YamlSerializerOptions yamlOptions;

        public YoloExporter() 
        {
            this.yamlOptions = new YamlSerializerOptions()
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                IndentSize = 4,
                DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull,
            };

        }

        public async Task ExportAsync(
            DetectionDatabase database,
            YoloExportOptions options, 
            CancellationToken cancellationToken = default)
        {
            var files = options.CreateLabels || options.CopyImages ? 
                    database.Files.Query().ToList()
                    : null;
            var operationsTodoCount = 
                (options.CreateLabels ? files!.Count : 0) +
                (options.CopyImages ? files!.Count : 0) +
                (options.CreateDataset ? 1 : 0);
            
            var tasks = new List<Task>();
            if (options.CreateLabels)
            {
                tasks.Add(CreateLabelsAsync(options.LabelsPath!, files!, cancellationToken));
            }

            if (options.CopyImages)
            {
                tasks.Add(CopyImagesAsync(options.ImagesPath!, files!, cancellationToken));
            }

            if (options.CreateDataset)
            {
                var dataset = BuildDataset(files!, options.DatasetPath!, options.ImagesPath);
                tasks.Add(CreateDatasetAsync(options.DatasetPath!, dataset, cancellationToken));
            }

            Task.WaitAll(tasks);
        }

        public event EventHandler<YoloExportProgressEventArgs>? ProgressChanged;

        private YoloDataset BuildDataset(
            List<FileEntity> files,
            string datasetPath,
            string? imagesPath)
        {
            var dataset = new YoloDataset();
            // TODO.
            return dataset;
        }

        private async Task CreateLabelsAsync(
            string labelsPath,
            List<FileEntity> files,
            CancellationToken cancellationToken)
        {
            foreach (var file in files)
            {
                if (file.Detections?.Count > 0)
                {
                    var sourcePath = Path.GetFileNameWithoutExtension(file.Path!) + ".txt";
                    var destinationPath = Path.Combine(labelsPath, sourcePath);
                    // TODO.
                }
            }
        }

        private async Task CopyImagesAsync(
            string imagesPath,
            List<FileEntity> files,
            CancellationToken cancellationToken)
        {
            var sourceOptions = new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Options = FileOptions.Asynchronous,
            };

            var destinationOptions = new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Options = FileOptions.Asynchronous,
            };

            foreach (var file in files)
            {
                var sourcePath = file.Path!;
                var destinationPath = Path.Combine(imagesPath, Path.GetFileName(sourcePath));
                using var sourceStream = new FileStream(sourcePath, sourceOptions);
                using var destinationStream = new FileStream(destinationPath, destinationOptions);
                await sourceStream.CopyToAsync(destinationStream);
            }
        }

        private async Task CreateDatasetAsync(
            string datasetPath,
            YoloDataset dataset,
            CancellationToken cancellationToken)
        {
            var yaml = YamlSerializer.Serialize(dataset);
            await File.WriteAllTextAsync(datasetPath, yaml, cancellationToken);
        }
    }
}
