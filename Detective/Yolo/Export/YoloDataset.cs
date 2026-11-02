namespace Detective.Yolo.Export
{
    public class YoloDataset
    {
        public string Path { get; set; }

        public string Train { get; set; }

        public string Val { get; set; }

        public object Test { get; set; }

        public Dictionary<int, string> Names { get; set; }
    }
}
