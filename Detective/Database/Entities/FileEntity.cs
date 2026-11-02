namespace Detective.Database.Entities
{
    public class FileEntity
    {
        public int Id { get; set; }
        public string? Path { get; set; }
        public List<DetectionEntity>? Detections { get; set; }
    }
}
