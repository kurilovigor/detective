using Detective.Database;

namespace Detective.Yolo.Export
{
    public class YoloExportOptions
    { 
        /// <summary>
        /// TODO: Это все надо переделать. Нужен путь до yaml датасета и все остальное 
        /// должно строиться как относительные пути
        /// </summary>
        public bool CopyImages {  get; set; }
        public bool CreateLabels { get; set; }
        public bool CreateDataset { get; set; }
        public string? ImagesPath { get; set; }
        public string? LabelsPath { get; set; }
        public string? DatasetPath { get; set; }
    }
}
